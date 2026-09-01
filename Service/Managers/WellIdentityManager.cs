using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using OSDC.DotnetLibraries.General.DataManagement;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace OSDC.Drilling.Well.Service.Managers
{
    public class WellIdentityManager
    {
        private static WellIdentityManager? _instance;
        private readonly ILogger<WellIdentityManager> _logger;
        private readonly SqlConnectionManager _connectionManager;
        private static readonly string[] DefaultIdentities =
        [
            "OfficialAuthorityName", "OperatorName", "CompanyInternalName", "PlanningName",
            "DataManagementName", "HistoricalName", "ShortName", "DisplayName",
            "ReportingName", "LegacyName", "ImportedName"
        ];
        private WellIdentityManager(ILogger<WellIdentityManager> logger, SqlConnectionManager connectionManager)
        {
            _logger = logger;
            _connectionManager = connectionManager;
        }

        public static WellIdentityManager GetInstance(ILogger<WellIdentityManager> logger, SqlConnectionManager connectionManager)
        {
            _instance ??= new WellIdentityManager(logger, connectionManager);
            return _instance;
        }

        public List<Guid>? GetAllWellIdentityId()
        {
            EnsureDefaultIdentities();
            List<Guid> ids = [];
            var connection = _connectionManager.GetConnection();
            if (connection == null)
            {
                return null;
            }

            var command = connection.CreateCommand();
            command.CommandText = "SELECT ID FROM WellIdentityTable";
            try
            {
                using var reader = command.ExecuteReader();
                while (reader.Read() && !reader.IsDBNull(0))
                {
                    ids.Add(reader.GetGuid(0));
                }
                return ids;
            }
            catch (SqliteException ex)
            {
                _logger.LogError(ex, "Impossible to get IDs from WellIdentityTable");
                return null;
            }
        }

        public List<MetaInfo?>? GetAllWellIdentityMetaInfo()
        {
            EnsureDefaultIdentities();
            List<MetaInfo?> metaInfos = [];
            var connection = _connectionManager.GetConnection();
            if (connection == null)
            {
                return null;
            }

            var command = connection.CreateCommand();
            command.CommandText = "SELECT MetaInfo FROM WellIdentityTable";
            try
            {
                using var reader = command.ExecuteReader();
                while (reader.Read() && !reader.IsDBNull(0))
                {
                    metaInfos.Add(JsonSerializer.Deserialize<MetaInfo>(reader.GetString(0), JsonSettings.Options));
                }
                return metaInfos;
            }
            catch (SqliteException ex)
            {
                _logger.LogError(ex, "Impossible to get MetaInfo from WellIdentityTable");
                return null;
            }
        }

        public Model.WellIdentity? GetWellIdentityById(Guid guid)
        {
            if (guid == Guid.Empty)
            {
                return null;
            }

            var connection = _connectionManager.GetConnection();
            if (connection == null)
            {
                return null;
            }

            var command = connection.CreateCommand();
            command.CommandText = $"SELECT WellIdentity FROM WellIdentityTable WHERE ID = '{guid}'";
            try
            {
                using var reader = command.ExecuteReader();
                if (reader.Read() && !reader.IsDBNull(0))
                {
                    Model.WellIdentity? data = JsonSerializer.Deserialize<Model.WellIdentity>(reader.GetString(0), JsonSettings.Options);
                    if (data != null && data.MetaInfo != null && data.MetaInfo.ID != guid)
                    {
                        throw new SqliteException("SQLite database corrupted: returned WellIdentity has the wrong ID.", 1);
                    }
                    return data;
                }
            }
            catch (SqliteException ex)
            {
                _logger.LogError(ex, "Impossible to get WellIdentity from WellIdentityTable");
            }

            return null;
        }

        public List<Model.WellIdentity?>? GetAllWellIdentity()
        {
            EnsureDefaultIdentities();
            List<Model.WellIdentity?> values = [];
            var connection = _connectionManager.GetConnection();
            if (connection == null)
            {
                return null;
            }

            var command = connection.CreateCommand();
            command.CommandText = "SELECT WellIdentity FROM WellIdentityTable";
            try
            {
                using var reader = command.ExecuteReader();
                while (reader.Read() && !reader.IsDBNull(0))
                {
                    values.Add(JsonSerializer.Deserialize<Model.WellIdentity>(reader.GetString(0), JsonSettings.Options));
                }
                return values;
            }
            catch (SqliteException ex)
            {
                _logger.LogError(ex, "Impossible to get WellIdentity from WellIdentityTable");
                return null;
            }
        }

        public bool AddWellIdentity(Model.WellIdentity? data)
        {
            if (data?.MetaInfo == null || data.MetaInfo.ID == Guid.Empty)
            {
                return false;
            }
            if (GetWellIdentityById(data.MetaInfo.ID) != null)
            {
                return false;
            }

            var connection = _connectionManager.GetConnection();
            if (connection == null)
            {
                return false;
            }

            using SqliteTransaction transaction = connection.BeginTransaction();
            try
            {
                DateTimeOffset now = DateTimeOffset.UtcNow;
                data.CreationDate = now;
                data.LastModificationDate = now;
                string metaInfo = JsonSerializer.Serialize(data.MetaInfo, JsonSettings.Options);
                string serialized = JsonSerializer.Serialize(data, JsonSettings.Options);
                string? creationDate = data.CreationDate?.ToString(SqlConnectionManager.DATE_TIME_FORMAT);
                string? lastModificationDate = data.LastModificationDate?.ToString(SqlConnectionManager.DATE_TIME_FORMAT);
                var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = "INSERT INTO WellIdentityTable " +
                    "(ID, MetaInfo, Name, CreationDate, LastModificationDate, WellIdentity) " +
                    "VALUES ($id, $meta, $name, $created, $modified, $document)";
                command.Parameters.AddWithValue("$id", data.MetaInfo.ID.ToString());
                command.Parameters.AddWithValue("$meta", metaInfo);
                command.Parameters.AddWithValue("$name", data.Name ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("$created", creationDate ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("$modified", lastModificationDate ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("$document", serialized);
                int count = command.ExecuteNonQuery();
                if (count != 1)
                {
                    transaction.Rollback();
                    return false;
                }
                transaction.Commit();
                return true;
            }
            catch (SqliteException ex)
            {
                transaction.Rollback();
                _logger.LogError(ex, "Impossible to add WellIdentity");
                return false;
            }
        }

        public bool UpdateWellIdentityById(Guid guid, Model.WellIdentity? data)
        {
            if (guid == Guid.Empty || data?.MetaInfo == null || data.MetaInfo.ID != guid)
            {
                return false;
            }

            var connection = _connectionManager.GetConnection();
            if (connection == null)
            {
                return false;
            }

            using SqliteTransaction transaction = connection.BeginTransaction();
            try
            {
                data.LastModificationDate = DateTimeOffset.UtcNow;
                string metaInfo = JsonSerializer.Serialize(data.MetaInfo, JsonSettings.Options);
                string serialized = JsonSerializer.Serialize(data, JsonSettings.Options);
                string? creationDate = data.CreationDate?.ToString(SqlConnectionManager.DATE_TIME_FORMAT);
                string? lastModificationDate = data.LastModificationDate?.ToString(SqlConnectionManager.DATE_TIME_FORMAT);
                var command = connection.CreateCommand();
                command.CommandText = $"UPDATE WellIdentityTable SET " +
                    $"MetaInfo = '{metaInfo}', " +
                    $"Name = '{data.Name}', " +
                    $"CreationDate = '{creationDate}', " +
                    $"LastModificationDate = '{lastModificationDate}', " +
                    $"WellIdentity = '{serialized}' " +
                    $"WHERE ID = '{guid}'";
                int count = command.ExecuteNonQuery();
                if (count != 1)
                {
                    transaction.Rollback();
                    return false;
                }
                transaction.Commit();
                return true;
            }
            catch (SqliteException ex)
            {
                transaction.Rollback();
                _logger.LogError(ex, "Impossible to update WellIdentity");
                return false;
            }
        }

        public bool DeleteWellIdentityById(Guid guid)
        {
            if (guid == Guid.Empty)
            {
                return false;
            }

            var connection = _connectionManager.GetConnection();
            if (connection == null)
            {
                return false;
            }

            using SqliteTransaction transaction = connection.BeginTransaction();
            try
            {
                var command = connection.CreateCommand();
                command.CommandText = $"DELETE FROM WellIdentityTable WHERE ID = '{guid}'";
                command.ExecuteNonQuery();
                transaction.Commit();
                return true;
            }
            catch (SqliteException ex)
            {
                transaction.Rollback();
                _logger.LogError(ex, "Impossible to delete WellIdentity");
                return false;
            }
        }

        private void EnsureDefaultIdentities()
        {
            var connection = _connectionManager.GetConnection();
            if (connection == null)
            {
                return;
            }

            var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM WellIdentityTable";
            try
            {
                using SqliteDataReader reader = command.ExecuteReader();
                if (reader.Read() && reader.GetInt64(0) > 0)
                {
                    return;
                }
            }
            catch (SqliteException ex)
            {
                _logger.LogError(ex, "Impossible to count WellIdentityTable");
                return;
            }

            foreach (string name in DefaultIdentities)
            {
                AddWellIdentity(new Model.WellIdentity
                {
                    MetaInfo = new MetaInfo { ID = Guid.NewGuid() },
                    Name = name
                });
            }
        }
    }
}
