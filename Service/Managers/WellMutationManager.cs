using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using OSDC.Drilling.Well.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using WellModel = OSDC.Drilling.Well.Model.Well;

namespace OSDC.Drilling.Well.Service.Managers;

/// <summary>Creates and updates Wells with validation, optimistic concurrency, and parameterized SQL.</summary>
internal static class WellMutationManager
{
    private static readonly DateTimeOffset LegacyRevision = DateTimeOffset.UnixEpoch;

    public static WellMutationResult Create(SqlConnectionManager manager, ILogger logger, WellModel? well)
    {
        if (well?.MetaInfo == null || well.MetaInfo.ID == Guid.Empty)
            return WellMutationResult.Invalid("MetaInfo.ID", "invalid_id", "A caller-generated non-empty Well UUID is required.");

        using SqliteConnection? connection = manager.GetConnection();
        if (connection == null) return WellMutationResult.StorageFailure();
        using SqliteTransaction transaction = connection.BeginTransaction();
        try
        {
            if (Exists(connection, transaction, well.MetaInfo.ID))
            {
                transaction.Rollback();
                return WellMutationResult.AlreadyExists($"A Well with UUID '{well.MetaInfo.ID}' already exists.");
            }

            List<WellMutationError> errors = WellReferenceIntegrityValidator.ValidateWell(connection, transaction, well);
            if (errors.Count != 0)
            {
                transaction.Rollback();
                return WellMutationResult.InvalidWell(errors);
            }

            DateTimeOffset now = DateTimeOffset.UtcNow;
            well.CreationDate = now;
            well.LastModificationDate = now;
            using SqliteCommand command = CreateWriteCommand(connection, transaction, well, insert: true);
            if (command.ExecuteNonQuery() != 1)
            {
                transaction.Rollback();
                return WellMutationResult.StorageFailure();
            }
            transaction.Commit();
            return WellMutationResult.Success(well);
        }
        catch (Exception ex) when (ex is SqliteException or JsonException)
        {
            transaction.Rollback();
            logger.LogError(ex, "Unable to create Well {WellId}", well.MetaInfo.ID);
            return WellMutationResult.StorageFailure();
        }
    }

    public static WellMutationResult Update(SqlConnectionManager manager, ILogger logger, Guid id,
        DateTimeOffset expectedModifiedUtc, WellModel? well)
    {
        if (id == Guid.Empty || well?.MetaInfo == null || well.MetaInfo.ID != id)
            return WellMutationResult.Invalid("MetaInfo.ID", "id_mismatch", "The route UUID must be non-empty and equal MetaInfo.ID.");
        if (expectedModifiedUtc == default)
            return WellMutationResult.Invalid("expectedModifiedUtc", "required", "A non-default optimistic-concurrency timestamp is required.");

        using SqliteConnection? connection = manager.GetConnection();
        if (connection == null) return WellMutationResult.StorageFailure();
        using SqliteTransaction transaction = connection.BeginTransaction();
        try
        {
            WellModel? stored = Read(connection, transaction, id);
            if (stored == null)
            {
                transaction.Rollback();
                return WellMutationResult.NotFound("The Well does not exist.");
            }

            DateTimeOffset storedRevision = RevisionOf(stored);
            if (storedRevision.UtcTicks != expectedModifiedUtc.UtcTicks)
            {
                transaction.Rollback();
                return WellMutationResult.ConcurrencyConflict("expectedModifiedUtc",
                    $"Expected {expectedModifiedUtc:O}, but the stored Well was modified at {storedRevision:O}.");
            }

            List<WellMutationError> errors = WellReferenceIntegrityValidator.ValidateWell(connection, transaction, well);
            if (errors.Count != 0)
            {
                transaction.Rollback();
                return WellMutationResult.InvalidWell(errors);
            }

            well.CreationDate = stored.CreationDate;
            well.LastModificationDate = NextRevision(storedRevision);
            using SqliteCommand command = CreateWriteCommand(connection, transaction, well, insert: false);
            if (command.ExecuteNonQuery() != 1)
            {
                transaction.Rollback();
                return WellMutationResult.StorageFailure();
            }
            transaction.Commit();
            return WellMutationResult.Success(well);
        }
        catch (Exception ex) when (ex is SqliteException or JsonException)
        {
            transaction.Rollback();
            logger.LogError(ex, "Unable to update Well {WellId}", id);
            return WellMutationResult.StorageFailure();
        }
    }

    public static DateTimeOffset RevisionOf(WellModel well) =>
        well.LastModificationDate ?? well.CreationDate ?? LegacyRevision;

    public static void EnsureRevision(WellModel? well)
    {
        if (well != null && well.LastModificationDate == null) well.LastModificationDate = RevisionOf(well);
    }

    public static WellMutationResult AddIdentityAssignment(SqlConnectionManager manager, ILogger logger, Guid wellId,
        DateTimeOffset expectedModifiedUtc, WellIdentityAssignment? assignment) =>
        Mutate(manager, logger, wellId, expectedModifiedUtc, well =>
        {
            if (assignment == null)
                return WellMutationResult.Invalid("assignment", "required", "An identity assignment is required.");
            if (AssignmentIdExists(well, assignment.ID))
                return WellMutationResult.AlreadyExists($"Assignment UUID '{assignment.ID}' already exists on this Well.");
            (well.WellIdentityAssignments ??= []).Add(assignment);
            return null;
        });

    public static WellMutationResult UpdateIdentityAssignment(SqlConnectionManager manager, ILogger logger, Guid wellId,
        Guid assignmentId, DateTimeOffset expectedModifiedUtc, WellIdentityAssignment? assignment) =>
        Mutate(manager, logger, wellId, expectedModifiedUtc, well =>
        {
            if (assignmentId == Guid.Empty || assignment?.ID != assignmentId)
                return WellMutationResult.Invalid("assignment.ID", "id_mismatch", "The route assignment UUID must be non-empty and equal assignment.ID.");
            int index = (well.WellIdentityAssignments ?? []).FindIndex(value => value?.ID == assignmentId);
            if (index < 0) return WellMutationResult.NotFound("The Well identity assignment does not exist.");
            well.WellIdentityAssignments![index] = assignment;
            return null;
        });

    public static WellMutationResult DeleteIdentityAssignment(SqlConnectionManager manager, ILogger logger, Guid wellId,
        Guid assignmentId, DateTimeOffset expectedModifiedUtc) =>
        Mutate(manager, logger, wellId, expectedModifiedUtc, well =>
        {
            if (assignmentId == Guid.Empty)
                return WellMutationResult.Invalid("assignmentId", "invalid_id", "A non-empty assignment UUID is required.");
            int removed = (well.WellIdentityAssignments ?? []).RemoveAll(value => value?.ID == assignmentId);
            return removed == 0 ? WellMutationResult.NotFound("The Well identity assignment does not exist.") : null;
        });

    public static WellMutationResult AddFeatureAssignment(SqlConnectionManager manager, ILogger logger, Guid wellId,
        DateTimeOffset expectedModifiedUtc, WellFeatureAssignment? assignment) =>
        Mutate(manager, logger, wellId, expectedModifiedUtc, well =>
        {
            if (assignment == null)
                return WellMutationResult.Invalid("assignment", "required", "A feature assignment is required.");
            if (AssignmentIdExists(well, assignment.ID))
                return WellMutationResult.AlreadyExists($"Assignment UUID '{assignment.ID}' already exists on this Well.");
            (well.WellFeatureAssignments ??= []).Add(assignment);
            return null;
        });

    public static WellMutationResult UpdateFeatureAssignment(SqlConnectionManager manager, ILogger logger, Guid wellId,
        Guid assignmentId, DateTimeOffset expectedModifiedUtc, WellFeatureAssignment? assignment) =>
        Mutate(manager, logger, wellId, expectedModifiedUtc, well =>
        {
            if (assignmentId == Guid.Empty || assignment?.ID != assignmentId)
                return WellMutationResult.Invalid("assignment.ID", "id_mismatch", "The route assignment UUID must be non-empty and equal assignment.ID.");
            int index = (well.WellFeatureAssignments ?? []).FindIndex(value => value?.ID == assignmentId);
            if (index < 0) return WellMutationResult.NotFound("The Well feature assignment does not exist.");
            well.WellFeatureAssignments![index] = assignment;
            return null;
        });

    public static WellMutationResult DeleteFeatureAssignment(SqlConnectionManager manager, ILogger logger, Guid wellId,
        Guid assignmentId, DateTimeOffset expectedModifiedUtc) =>
        Mutate(manager, logger, wellId, expectedModifiedUtc, well =>
        {
            if (assignmentId == Guid.Empty)
                return WellMutationResult.Invalid("assignmentId", "invalid_id", "A non-empty assignment UUID is required.");
            int removed = (well.WellFeatureAssignments ?? []).RemoveAll(value => value?.ID == assignmentId);
            return removed == 0 ? WellMutationResult.NotFound("The Well feature assignment does not exist.") : null;
        });

    private static WellMutationResult Mutate(SqlConnectionManager manager, ILogger logger, Guid wellId,
        DateTimeOffset expectedModifiedUtc, Func<WellModel, WellMutationResult?> mutation)
    {
        if (wellId == Guid.Empty)
            return WellMutationResult.Invalid("wellId", "invalid_id", "A non-empty Well UUID is required.");
        if (expectedModifiedUtc == default)
            return WellMutationResult.Invalid("expectedModifiedUtc", "required", "A non-default optimistic-concurrency timestamp is required.");

        using SqliteConnection? connection = manager.GetConnection();
        if (connection == null) return WellMutationResult.StorageFailure();
        using SqliteTransaction transaction = connection.BeginTransaction();
        try
        {
            WellModel? stored = Read(connection, transaction, wellId);
            if (stored == null)
            {
                transaction.Rollback();
                return WellMutationResult.NotFound("The Well does not exist.");
            }
            DateTimeOffset storedRevision = RevisionOf(stored);
            if (storedRevision.UtcTicks != expectedModifiedUtc.UtcTicks)
            {
                transaction.Rollback();
                return WellMutationResult.ConcurrencyConflict("expectedModifiedUtc",
                    $"Expected {expectedModifiedUtc:O}, but the stored Well was modified at {storedRevision:O}.");
            }

            WellMutationResult? mutationError = mutation(stored);
            if (mutationError != null)
            {
                transaction.Rollback();
                return mutationError;
            }
            List<WellMutationError> errors = WellReferenceIntegrityValidator.ValidateWell(connection, transaction, stored);
            if (errors.Count != 0)
            {
                transaction.Rollback();
                return WellMutationResult.InvalidWell(errors);
            }

            stored.LastModificationDate = NextRevision(storedRevision);
            using SqliteCommand command = CreateWriteCommand(connection, transaction, stored, insert: false);
            if (command.ExecuteNonQuery() != 1)
            {
                transaction.Rollback();
                return WellMutationResult.StorageFailure();
            }
            transaction.Commit();
            return WellMutationResult.Success(stored);
        }
        catch (Exception ex) when (ex is SqliteException or JsonException)
        {
            transaction.Rollback();
            logger.LogError(ex, "Unable to mutate assignments for Well {WellId}", wellId);
            return WellMutationResult.StorageFailure();
        }
    }

    private static bool AssignmentIdExists(WellModel well, Guid id) =>
        (well.WellIdentityAssignments ?? []).Any(value => value?.ID == id) ||
        (well.WellFeatureAssignments ?? []).Any(value => value?.ID == id);

    private static DateTimeOffset NextRevision(DateTimeOffset storedRevision)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        return now.UtcTicks > storedRevision.UtcTicks ? now : storedRevision.AddTicks(1);
    }

    private static bool Exists(SqliteConnection connection, SqliteTransaction transaction, Guid id)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT COUNT(*) FROM WellTable WHERE ID=$id";
        command.Parameters.AddWithValue("$id", id.ToString());
        return Convert.ToInt64(command.ExecuteScalar()) != 0;
    }

    private static WellModel? Read(SqliteConnection connection, SqliteTransaction transaction, Guid id)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT Well FROM WellTable WHERE ID=$id";
        command.Parameters.AddWithValue("$id", id.ToString());
        return command.ExecuteScalar() is string json
            ? JsonSerializer.Deserialize<WellModel>(json, JsonSettings.Options)
            : null;
    }

    private static SqliteCommand CreateWriteCommand(SqliteConnection connection, SqliteTransaction transaction,
        WellModel well, bool insert)
    {
        SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = insert
            ? "INSERT INTO WellTable (ID,MetaInfo,ClusterID,SlotID,Well) VALUES ($id,$meta,$cluster,$slot,$well)"
            : "UPDATE WellTable SET MetaInfo=$meta,ClusterID=$cluster,SlotID=$slot,Well=$well WHERE ID=$id";
        command.Parameters.AddWithValue("$id", well.MetaInfo!.ID.ToString());
        command.Parameters.AddWithValue("$meta", JsonSerializer.Serialize(well.MetaInfo, JsonSettings.Options));
        command.Parameters.AddWithValue("$cluster", well.ClusterID?.ToString() ?? string.Empty);
        command.Parameters.AddWithValue("$slot", well.SlotID?.ToString() ?? string.Empty);
        command.Parameters.AddWithValue("$well", JsonSerializer.Serialize(well, JsonSettings.Options));
        return command;
    }
}
