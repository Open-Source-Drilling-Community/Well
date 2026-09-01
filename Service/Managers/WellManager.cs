using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using OSDC.DotnetLibraries.General.DataManagement;
using Microsoft.Data.Sqlite;
using System.Text.Json;
using OSDC.Drilling.Well.Model;

namespace OSDC.Drilling.Well.Service.Managers
{
    /// <summary>
    /// A manager for Well. The manager implements the singleton pattern as defined by 
    /// Gamma, Erich, et al. "Design patterns: Abstraction and reuse of object-oriented design." 
    /// European Conference on Object-Oriented Programming. Springer, Berlin, Heidelberg, 1993.
    /// </summary>
    public class WellManager
    {
        private static WellManager? _instance = null;
        private readonly ILogger<WellManager> _logger;
        private readonly SqlConnectionManager _connectionManager;

        private WellManager(ILogger<WellManager> logger, SqlConnectionManager connectionManager)
        {
            _logger = logger;
            _connectionManager = connectionManager;
        }

        public static WellManager GetInstance(ILogger<WellManager> logger, SqlConnectionManager connectionManager)
        {
            _instance ??= new WellManager(logger, connectionManager);
            return _instance;
        }

        public int Count
        {
            get
            {
                int count = 0;
                var connection = _connectionManager.GetConnection();
                if (connection != null)
                {
                    var command = connection.CreateCommand();
                    command.CommandText = "SELECT COUNT(*) FROM WellTable";
                    try
                    {
                        using SqliteDataReader reader = command.ExecuteReader();
                        if (reader.Read())
                        {
                            count = (int)reader.GetInt64(0);
                        }
                    }
                    catch (SqliteException ex)
                    {
                        _logger.LogError(ex, "Impossible to count records in the WellTable");
                    }
                }
                else
                {
                    _logger.LogWarning("Impossible to access the SQLite database");
                }
                return count;
            }
        }

        public bool Clear()
        {
            var connection = _connectionManager.GetConnection();
            if (connection != null)
            {
                bool success = false;
                using var transaction = connection.BeginTransaction();
                try
                {
                    //empty WellTable
                    var command = connection.CreateCommand();
                    command.CommandText = "DELETE FROM WellTable";
                    command.ExecuteNonQuery();

                    transaction.Commit();
                    success = true;
                }
                catch (SqliteException ex)
                {
                    transaction.Rollback();
                    _logger.LogError(ex, "Impossible to clear the WellTable");
                }
                return success;
            }
            else
            {
                _logger.LogWarning("Impossible to access the SQLite database");
                return false;
            }
        }

        public bool Contains(Guid guid)
        {
            int count = 0;
            var connection = _connectionManager.GetConnection();
            if (connection != null)
            {
                var command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM WellTable WHERE ID = $id";
                command.Parameters.AddWithValue("$id", guid.ToString());
                try
                {
                    using SqliteDataReader reader = command.ExecuteReader();
                    if (reader.Read())
                    {
                        count = (int)reader.GetInt64(0);
                    }
                }
                catch (SqliteException ex)
                {
                    _logger.LogError(ex, "Impossible to count rows from WellTable");
                }
            }
            else
            {
                _logger.LogWarning("Impossible to access the SQLite database");
            }
            return count >= 1;
        }

        /// <summary>
        /// Returns the list of Guid of all Well present in the microservice database 
        /// </summary>
        /// <returns>the list of Guid of all Well present in the microservice database</returns>
        public List<Guid>? GetAllWellId()
        {
            List<Guid> ids = [];
            var connection = _connectionManager.GetConnection();
            if (connection != null)
            {
                var command = connection.CreateCommand();
                command.CommandText = "SELECT ID FROM WellTable";
                try
                {
                    using var reader = command.ExecuteReader();
                    while (reader.Read() && !reader.IsDBNull(0))
                    {
                        Guid id = reader.GetGuid(0);
                        ids.Add(id);
                    }
                    _logger.LogInformation("Returning the list of ID of existing records from WellTable");
                    return ids;
                }
                catch (SqliteException ex)
                {
                    _logger.LogError(ex, "Impossible to get IDs from WellTable");
                }
            }
            else
            {
                _logger.LogWarning("Impossible to access the SQLite database");
            }
            return null;
        }
        /// <summary>
        /// Returns the list of MetaInfo of all Well present in the microservice database 
        /// </summary>
        /// <returns>the list of MetaInfo of all Well present in the microservice database</returns>
        public List<MetaInfo?>? GetAllWellMetaInfo()
        {
            List<MetaInfo?> metaInfos = [];
            var connection = _connectionManager.GetConnection();
            if (connection != null)
            {
                var command = connection.CreateCommand();
                command.CommandText = "SELECT MetaInfo FROM WellTable";
                try
                {
                    using var reader = command.ExecuteReader();
                    while (reader.Read() && !reader.IsDBNull(0))
                    {
                        string mInfo = reader.GetString(0);
                        MetaInfo? metaInfo = JsonSerializer.Deserialize<MetaInfo>(mInfo, JsonSettings.Options);
                        metaInfos.Add(metaInfo);
                    }
                    _logger.LogInformation("Returning the list of MetaInfo of existing records from WellTable");
                    return metaInfos;
                }
                catch (SqliteException ex)
                {
                    _logger.LogError(ex, "Impossible to get IDs from WellTable");
                }
            }
            else
            {
                _logger.LogWarning("Impossible to access the SQLite database");
            }
            return null;
        }

        /// <summary>
        /// Returns the Well identified by its Guid from the microservice database 
        /// </summary>
        /// <param name="guid"></param>
        /// <returns>the Well identified by its Guid from the microservice database</returns>
        public Model.Well? GetWellById(Guid guid)
        {
            if (!guid.Equals(Guid.Empty))
            {
                var connection = _connectionManager.GetConnection();
                if (connection != null)
                {
                    Model.Well? well;
                    var command = connection.CreateCommand();
                    command.CommandText = "SELECT Well FROM WellTable WHERE ID = $id";
                    command.Parameters.AddWithValue("$id", guid.ToString());
                    try
                    {
                        using var reader = command.ExecuteReader();
                        if (reader.Read() && !reader.IsDBNull(0))
                        {
                            string data = reader.GetString(0);
                            well = JsonSerializer.Deserialize<Model.Well>(data, JsonSettings.Options);
                            WellMutationManager.EnsureRevision(well);
                            if (well != null && well.MetaInfo != null && !well.MetaInfo.ID.Equals(guid))
                                throw new SqliteException("SQLite database corrupted: returned Well is null or has been jsonified with the wrong ID.", 1);
                        }
                        else
                        {
                            _logger.LogInformation("No Well of given ID in the database");
                            return null;
                        }
                    }
                    catch (SqliteException ex)
                    {
                        _logger.LogError(ex, "Impossible to get the Well with the given ID from WellTable");
                        return null;
                    }
                    _logger.LogInformation("Returning the Well of given ID from WellTable");
                    return well;
                }
                else
                {
                    _logger.LogWarning("Impossible to access the SQLite database");
                }
            }
            else
            {
                _logger.LogWarning("The given Well ID is null or empty");
            }
            return null;
        }

        /// <summary>
        /// Returns the list of all Well present in the microservice database 
        /// </summary>
        /// <returns>the list of all Well present in the microservice database</returns>
        public List<Model.Well?>? GetAllWell()
        {
            List<Model.Well?> vals = [];
            var connection = _connectionManager.GetConnection();
            if (connection != null)
            {
                var command = connection.CreateCommand();
                command.CommandText = "SELECT Well FROM WellTable";
                try
                {
                    using var reader = command.ExecuteReader();
                    while (reader.Read() && !reader.IsDBNull(0))
                    {
                        string data = reader.GetString(0);
                        Model.Well? well = JsonSerializer.Deserialize<Model.Well>(data, JsonSettings.Options);
                        WellMutationManager.EnsureRevision(well);
                        vals.Add(well);
                    }
                    _logger.LogInformation("Returning the list of existing Well from WellTable");
                    return vals;
                }
                catch (SqliteException ex)
                {
                    _logger.LogError(ex, "Impossible to get Well from WellTable");
                }
            }
            else
            {
                _logger.LogWarning("Impossible to access the SQLite database");
            }
            return null;
        }

        /// <summary>Returns one deterministically ordered page of Wells matching the supplied filters.</summary>
        public WellSearchResult? SearchWells(int offset, int limit, string? name, Guid? clusterId, Guid? slotId,
            Guid? identityId, string? identityValue, Guid? featureCategoryId, Guid? featureOptionId,
            DateTimeOffset? modifiedFromUtc, DateTimeOffset? modifiedToUtc)
        {
            List<Model.Well?>? documents = GetAllWell();
            if (documents == null) return null;

            IEnumerable<Model.Well> query = documents.Where(value => value != null).Cast<Model.Well>();
            if (!string.IsNullOrWhiteSpace(name))
                query = query.Where(value => value.Name?.Contains(name.Trim(), StringComparison.OrdinalIgnoreCase) == true);
            if (clusterId is Guid cluster)
                query = query.Where(value => value.ClusterID == cluster);
            if (slotId is Guid slot)
                query = query.Where(value => value.SlotID == slot);
            if (identityId is Guid identity || !string.IsNullOrWhiteSpace(identityValue))
            {
                string? soughtValue = string.IsNullOrWhiteSpace(identityValue) ? null : identityValue.Trim();
                query = query.Where(value => (value.WellIdentityAssignments ?? []).Any(assignment =>
                    (identityId is not Guid requiredIdentity || assignment.IdentityID == requiredIdentity) &&
                    (soughtValue == null || assignment.Value?.Contains(soughtValue, StringComparison.OrdinalIgnoreCase) == true)));
            }
            if (featureCategoryId is Guid category || featureOptionId is Guid option)
            {
                query = query.Where(value => (value.WellFeatureAssignments ?? []).Any(assignment =>
                    (featureCategoryId is not Guid requiredCategory || assignment.FeatureCategoryID == requiredCategory) &&
                    (featureOptionId is not Guid requiredOption || assignment.FeatureOptionID == requiredOption)));
            }
            if (modifiedFromUtc is DateTimeOffset modifiedFrom)
                query = query.Where(value => WellMutationManager.RevisionOf(value) >= modifiedFrom);
            if (modifiedToUtc is DateTimeOffset modifiedTo)
                query = query.Where(value => WellMutationManager.RevisionOf(value) <= modifiedTo);

            List<Model.Well> matches = query
                .OrderBy(value => value.MetaInfo?.ID ?? Guid.Empty)
                .ToList();
            return new WellSearchResult
            {
                Total = matches.Count,
                Offset = offset,
                Limit = limit,
                Items = matches.Skip(offset).Take(limit).ToList()
            };
        }

        /// <summary>Creates a dependency-closed Well backup from one SQLite snapshot.</summary>
        public WellBatchExportOutcome ExportBatch(WellBatchExportRequest? request)
        {
            using SqliteConnection? connection = _connectionManager.GetConnection();
            if (connection == null) return WellBatchExporter.StorageFailure("The Well database is unavailable.");
            using SqliteTransaction transaction = connection.BeginTransaction();
            try
            {
                List<Model.Well?> wells = ReadDocuments<Model.Well>(connection, transaction, "WellTable", "Well");
                List<WellIdentity> identities = ReadDocuments<WellIdentity>(connection, transaction,
                    "WellIdentityTable", "WellIdentity").Where(value => value != null).Cast<WellIdentity>().ToList();
                List<WellFeatureCategory> categories = ReadDocuments<WellFeatureCategory>(connection, transaction,
                    "WellFeatureCategoryTable", "WellFeatureCategory").Where(value => value != null).Cast<WellFeatureCategory>().ToList();
                WellBatchExportOutcome outcome = WellBatchExporter.Create(request, wells, DateTimeOffset.UtcNow, identities, categories);
                transaction.Commit();
                return outcome;
            }
            catch (Exception exception) when (exception is SqliteException or JsonException or InvalidOperationException)
            {
                try { transaction.Rollback(); } catch (InvalidOperationException) { }
                _logger.LogError(exception, "Unable to create a dependency-closed Well backup");
                return WellBatchExporter.StorageFailure("The stored Wells or catalog dependencies could not be read.");
            }
        }

        /// <summary>Validates and restores a Well backup in one transaction.</summary>
        public WellBatchRestoreOutcome RestoreBatch(WellBatchRestoreRequest? request)
        {
            try
            {
                using SqliteConnection? connection = _connectionManager.GetConnection();
                if (connection == null) return WellBatchRestorer.StorageFailure("The Well database is unavailable.");
                return WellBatchRestorer.Restore(connection, request, DateTimeOffset.UtcNow);
            }
            catch (SqliteException exception)
            {
                _logger.LogError(exception, "Unable to open the Well database for batch restore");
                return WellBatchRestorer.StorageFailure("The Well database is unavailable.");
            }
        }

        private static List<T?> ReadDocuments<T>(SqliteConnection connection, SqliteTransaction transaction,
            string table, string documentColumn)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = $"SELECT {documentColumn} FROM {table} ORDER BY ID";
            using SqliteDataReader reader = command.ExecuteReader();
            List<T?> result = [];
            while (reader.Read())
            {
                if (reader.IsDBNull(0)) throw new JsonException($"{table} contains a null document.");
                T? value = JsonSerializer.Deserialize<T>(reader.GetString(0), JsonSettings.Options);
                if (value == null) throw new JsonException($"{table} contains an invalid document.");
                result.Add(value);
            }
            return result;
        }

        /// <summary>
        /// Returns the Well identified by its Guid from the microservice database 
        /// </summary>
        /// <param name="clusterId"></param>
        /// <returns>the Well identified by its Guid from the microservice database</returns>
        public List<Guid>? GetAllUsedSlotIDByClusterId(Guid clusterId)
        {
            if (!clusterId.Equals(Guid.Empty))
            {
                List<Guid> slotIDs = [];
                var connection = _connectionManager.GetConnection();
                if (connection != null)
                {
                    var command = connection.CreateCommand();
                    command.CommandText = "SELECT Well FROM WellTable WHERE ClusterID = $clusterId";
                    command.Parameters.AddWithValue("$clusterId", clusterId.ToString());
                    try
                    {
                        using var reader = command.ExecuteReader();
                        while (reader.Read() && !reader.IsDBNull(0))
                        {
                            string data = reader.GetString(0);
                            Model.Well? well = JsonSerializer.Deserialize<Model.Well>(data, JsonSettings.Options);
                            WellMutationManager.EnsureRevision(well);
                            if (well != null)
                            {
                                if (well.ClusterID != null && !well.ClusterID.Equals(clusterId))
                                    throw new SqliteException("SQLite database corrupted: returned Well is null or has been jsonified with the wrong cluster ID.", 1);
                                if (well.SlotID != null && !well.SlotID.Equals(Guid.Empty))
                                    slotIDs.Add(well.SlotID.Value);
                            }
                        }
                        _logger.LogInformation("Returning the list of slot MetaInfo of existing records from WellTable");
                        return slotIDs;
                    }
                    catch (SqliteException ex)
                    {
                        _logger.LogError(ex, "Impossible to get the Well with the given ID from WellTable");
                        return null;
                    }
                }
                else
                {
                    _logger.LogWarning("Impossible to access the SQLite database");
                }
            }
            else
            {
                _logger.LogWarning("The given Well ID is null or empty");
            }
            return null;
        }

        /// <summary>
        /// Performs calculation on the given Well and adds it to the microservice database
        /// </summary>
        /// <param name="well"></param>
        /// <returns>true if the given Well has been added successfully to the microservice database</returns>
        public bool AddWell(Model.Well? well)
        {
            return CreateWell(well).Succeeded;
        }

        internal WellMutationResult CreateWell(Model.Well? well) =>
            WellMutationManager.Create(_connectionManager, _logger, well);

        /// <summary>
        /// Performs calculation on the given Well and updates it in the microservice database
        /// </summary>
        /// <param name="well"></param>
        /// <returns>true if the given Well has been updated successfully</returns>
        public bool UpdateWellById(Guid guid, Model.Well? well)
        {
            Model.Well? stored = GetWellById(guid);
            return stored != null && UpdateWell(guid, WellMutationManager.RevisionOf(stored), well).Succeeded;
        }

        internal WellMutationResult UpdateWell(Guid guid, DateTimeOffset expectedModifiedUtc, Model.Well? well) =>
            WellMutationManager.Update(_connectionManager, _logger, guid, expectedModifiedUtc, well);

        internal WellMutationResult UpdateWellDetails(Guid wellId, DateTimeOffset expectedModifiedUtc, WellDetailsUpdate? details) =>
            WellMutationManager.UpdateDetails(_connectionManager, _logger, wellId, expectedModifiedUtc, details);

        internal WellMutationResult UpdateWellLocation(Guid wellId, DateTimeOffset expectedModifiedUtc, WellLocationUpdate? location) =>
            WellMutationManager.UpdateLocation(_connectionManager, _logger, wellId, expectedModifiedUtc, location);

        internal WellMutationResult DeleteWell(Guid wellId, DateTimeOffset expectedModifiedUtc) =>
            WellMutationManager.Delete(_connectionManager, _logger, wellId, expectedModifiedUtc);

        internal WellMutationResult AddIdentityAssignment(Guid wellId, DateTimeOffset expectedModifiedUtc, WellIdentityAssignment? assignment) =>
            WellMutationManager.AddIdentityAssignment(_connectionManager, _logger, wellId, expectedModifiedUtc, assignment);

        internal WellMutationResult UpdateIdentityAssignment(Guid wellId, Guid assignmentId, DateTimeOffset expectedModifiedUtc, WellIdentityAssignment? assignment) =>
            WellMutationManager.UpdateIdentityAssignment(_connectionManager, _logger, wellId, assignmentId, expectedModifiedUtc, assignment);

        internal WellMutationResult DeleteIdentityAssignment(Guid wellId, Guid assignmentId, DateTimeOffset expectedModifiedUtc) =>
            WellMutationManager.DeleteIdentityAssignment(_connectionManager, _logger, wellId, assignmentId, expectedModifiedUtc);

        internal WellMutationResult AddFeatureAssignment(Guid wellId, DateTimeOffset expectedModifiedUtc, WellFeatureAssignment? assignment) =>
            WellMutationManager.AddFeatureAssignment(_connectionManager, _logger, wellId, expectedModifiedUtc, assignment);

        internal WellMutationResult UpdateFeatureAssignment(Guid wellId, Guid assignmentId, DateTimeOffset expectedModifiedUtc, WellFeatureAssignment? assignment) =>
            WellMutationManager.UpdateFeatureAssignment(_connectionManager, _logger, wellId, assignmentId, expectedModifiedUtc, assignment);

        internal WellMutationResult DeleteFeatureAssignment(Guid wellId, Guid assignmentId, DateTimeOffset expectedModifiedUtc) =>
            WellMutationManager.DeleteFeatureAssignment(_connectionManager, _logger, wellId, assignmentId, expectedModifiedUtc);

    }
}
