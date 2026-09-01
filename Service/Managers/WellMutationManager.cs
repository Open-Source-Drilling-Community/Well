using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using OSDC.Drilling.Well.Model;
using System;
using System.Collections.Generic;
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
            return WellMutationResult.Success();
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
            well.LastModificationDate = DateTimeOffset.UtcNow;
            using SqliteCommand command = CreateWriteCommand(connection, transaction, well, insert: false);
            if (command.ExecuteNonQuery() != 1)
            {
                transaction.Rollback();
                return WellMutationResult.StorageFailure();
            }
            transaction.Commit();
            return WellMutationResult.Success();
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
