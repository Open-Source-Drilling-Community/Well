using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using OSDC.DotnetLibraries.General.DataManagement;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace OSDC.Drilling.Well.Service.Managers
{
    public class WellFeatureCategoryManager
    {
        private static WellFeatureCategoryManager? _instance;
        private readonly ILogger<WellFeatureCategoryManager> _logger;
        private readonly SqlConnectionManager _connectionManager;
        private static readonly DefaultWellFeatureCategory[] DefaultCategories =
        [
            new("WellPurpose", false, true, ["Producer", "Injector", "Observer", "Monitoring", "Disposal", "Relief", "Storage", "GeothermalProducer", "GeothermalInjector"]),
            new("WellBusinessRole", true, true, ["Exploration", "Appraisal", "Development", "Infill", "StepOut", "Pilot", "Redevelopment", "Research", "Test"]),
            new("WellLifecycleStatus", true, true, ["Proposed", "Planned", "Approved", "Drilling", "Suspended", "Completed", "Producing", "Injecting", "ShutIn", "Plugged", "Abandoned", "Decommissioned"]),
            new("FluidIntent", false, true, ["Oil", "Gas", "Condensate", "Water", "Steam", "CO2", "Nitrogen", "Brine", "WasteFluid", "GeothermalFluid", "Hydrogen"]),
            new("TrajectoryClass", true, false, ["Vertical", "Deviated", "Directional", "Horizontal", "ExtendedReach", "Complex3D", "Unknown"]),
            new("WellArchitecture", false, false, ["SingleBore", "Sidetracked", "Multilateral", "ReEntry", "SlotRecovery", "DualCompletion", "SmartWell", "Unknown"]),
            new("WellOrigin", true, false, ["NewDrill", "ReEntry", "SlotRecovery", "ConvertedWell", "RecompletedWell", "Unknown"]),
            new("PressureTemperatureClass", false, true, ["NormalPressure", "HighPressure", "HighTemperature", "HPHT", "UltraHPHT", "Overpressured", "Depleted", "Unknown"]),
            new("WellHazard", false, true, ["H2S", "CO2", "ShallowGas", "ShallowWaterFlow", "LossesExpected", "GainsExpected", "NarrowMudWindow", "UnstableFormation", "DepletedReservoir", "FaultCrossing", "Salt", "HydrateRisk", "BallooningRisk"]),
            new("DrillingConcept", false, true, ["ConventionalDrilling", "ManagedPressureDrilling", "UnderbalancedDrilling", "CasingWhileDrilling", "LinerDrilling", "CoiledTubingDrilling", "BatchDrilling", "Geosteering"]),
            new("CompletionIntent", false, true, ["OpenHole", "CasedHole", "Perforated", "SlottedLiner", "SandControl", "GravelPack", "FracPack", "IntelligentCompletion", "DualCompletion", "SelectiveInjection", "CommingledProduction"]),
            new("RegulatoryWellClass", true, true, ["ExplorationWell", "AppraisalWell", "DevelopmentWell", "ProductionWell", "InjectionWell", "ObservationWell", "MonitoringWell", "DisposalWell", "StorageWell", "AbandonmentWell", "Unknown"])
        ];
        private WellFeatureCategoryManager(ILogger<WellFeatureCategoryManager> logger, SqlConnectionManager connectionManager)
        {
            _logger = logger;
            _connectionManager = connectionManager;
        }

        public static WellFeatureCategoryManager GetInstance(ILogger<WellFeatureCategoryManager> logger, SqlConnectionManager connectionManager)
        {
            _instance ??= new WellFeatureCategoryManager(logger, connectionManager);
            return _instance;
        }

        public List<Guid>? GetAllWellFeatureCategoryId()
        {
            EnsureDefaultCategories();
            List<Guid> ids = [];
            var connection = _connectionManager.GetConnection();
            if (connection == null)
            {
                return null;
            }

            var command = connection.CreateCommand();
            command.CommandText = "SELECT ID FROM WellFeatureCategoryTable";
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
                _logger.LogError(ex, "Impossible to get IDs from WellFeatureCategoryTable");
                return null;
            }
        }

        public List<MetaInfo?>? GetAllWellFeatureCategoryMetaInfo()
        {
            EnsureDefaultCategories();
            List<MetaInfo?> metaInfos = [];
            var connection = _connectionManager.GetConnection();
            if (connection == null)
            {
                return null;
            }

            var command = connection.CreateCommand();
            command.CommandText = "SELECT MetaInfo FROM WellFeatureCategoryTable";
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
                _logger.LogError(ex, "Impossible to get MetaInfo from WellFeatureCategoryTable");
                return null;
            }
        }

        public Model.WellFeatureCategory? GetWellFeatureCategoryById(Guid guid)
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
            command.CommandText = $"SELECT WellFeatureCategory FROM WellFeatureCategoryTable WHERE ID = '{guid}'";
            try
            {
                using var reader = command.ExecuteReader();
                if (reader.Read() && !reader.IsDBNull(0))
                {
                    Model.WellFeatureCategory? data = JsonSerializer.Deserialize<Model.WellFeatureCategory>(reader.GetString(0), JsonSettings.Options);
                    if (data != null && data.MetaInfo != null && data.MetaInfo.ID != guid)
                    {
                        throw new SqliteException("SQLite database corrupted: returned WellFeatureCategory has the wrong ID.", 1);
                    }
                    return data;
                }
            }
            catch (SqliteException ex)
            {
                _logger.LogError(ex, "Impossible to get WellFeatureCategory from WellFeatureCategoryTable");
            }

            return null;
        }

        public List<Model.WellFeatureCategory?>? GetAllWellFeatureCategory()
        {
            EnsureDefaultCategories();
            List<Model.WellFeatureCategory?> values = [];
            var connection = _connectionManager.GetConnection();
            if (connection == null)
            {
                return null;
            }

            var command = connection.CreateCommand();
            command.CommandText = "SELECT WellFeatureCategory FROM WellFeatureCategoryTable";
            try
            {
                using var reader = command.ExecuteReader();
                while (reader.Read() && !reader.IsDBNull(0))
                {
                    values.Add(JsonSerializer.Deserialize<Model.WellFeatureCategory>(reader.GetString(0), JsonSettings.Options));
                }
                return values;
            }
            catch (SqliteException ex)
            {
                _logger.LogError(ex, "Impossible to get WellFeatureCategory from WellFeatureCategoryTable");
                return null;
            }
        }

        public bool AddWellFeatureCategory(Model.WellFeatureCategory? data)
        {
            if (data?.MetaInfo == null || data.MetaInfo.ID == Guid.Empty)
            {
                return false;
            }
            if (GetWellFeatureCategoryById(data.MetaInfo.ID) != null)
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
                PrepareCategory(data);
                DateTimeOffset now = DateTimeOffset.UtcNow;
                data.CreationDate = now;
                data.LastModificationDate = now;
                string metaInfo = JsonSerializer.Serialize(data.MetaInfo, JsonSettings.Options);
                string serialized = JsonSerializer.Serialize(data, JsonSettings.Options);
                string? creationDate = data.CreationDate?.ToString(SqlConnectionManager.DATE_TIME_FORMAT);
                string? lastModificationDate = data.LastModificationDate?.ToString(SqlConnectionManager.DATE_TIME_FORMAT);
                var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = "INSERT INTO WellFeatureCategoryTable " +
                    "(ID, MetaInfo, Name, IsExclusive, HasValidityPeriod, CreationDate, LastModificationDate, WellFeatureCategory) " +
                    "VALUES ($id, $meta, $name, $exclusive, $validity, $created, $modified, $document)";
                command.Parameters.AddWithValue("$id", data.MetaInfo.ID.ToString());
                command.Parameters.AddWithValue("$meta", metaInfo);
                command.Parameters.AddWithValue("$name", data.Name ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("$exclusive", data.IsExclusive ? 1 : 0);
                command.Parameters.AddWithValue("$validity", data.HasValidityPeriod ? 1 : 0);
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
                _logger.LogError(ex, "Impossible to add WellFeatureCategory");
                return false;
            }
        }

        public bool UpdateWellFeatureCategoryById(Guid guid, Model.WellFeatureCategory? data)
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
                PrepareCategory(data);
                data.LastModificationDate = DateTimeOffset.UtcNow;
                string metaInfo = JsonSerializer.Serialize(data.MetaInfo, JsonSettings.Options);
                string serialized = JsonSerializer.Serialize(data, JsonSettings.Options);
                string? creationDate = data.CreationDate?.ToString(SqlConnectionManager.DATE_TIME_FORMAT);
                string? lastModificationDate = data.LastModificationDate?.ToString(SqlConnectionManager.DATE_TIME_FORMAT);
                var command = connection.CreateCommand();
                command.CommandText = $"UPDATE WellFeatureCategoryTable SET " +
                    $"MetaInfo = '{metaInfo}', " +
                    $"Name = '{data.Name}', " +
                    $"IsExclusive = {(data.IsExclusive ? 1 : 0)}, " +
                    $"HasValidityPeriod = {(data.HasValidityPeriod ? 1 : 0)}, " +
                    $"CreationDate = '{creationDate}', " +
                    $"LastModificationDate = '{lastModificationDate}', " +
                    $"WellFeatureCategory = '{serialized}' " +
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
                _logger.LogError(ex, "Impossible to update WellFeatureCategory");
                return false;
            }
        }

        public bool DeleteWellFeatureCategoryById(Guid guid)
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
                command.CommandText = $"DELETE FROM WellFeatureCategoryTable WHERE ID = '{guid}'";
                command.ExecuteNonQuery();
                transaction.Commit();
                return true;
            }
            catch (SqliteException ex)
            {
                transaction.Rollback();
                _logger.LogError(ex, "Impossible to delete WellFeatureCategory");
                return false;
            }
        }

        private void EnsureDefaultCategories()
        {
            var connection = _connectionManager.GetConnection();
            if (connection == null)
            {
                return;
            }

            var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM WellFeatureCategoryTable";
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
                _logger.LogError(ex, "Impossible to count WellFeatureCategoryTable");
                return;
            }

            foreach (DefaultWellFeatureCategory defaultCategory in DefaultCategories)
            {
                AddWellFeatureCategory(CreateDefaultCategory(defaultCategory));
            }
        }

        private static Model.WellFeatureCategory CreateDefaultCategory(DefaultWellFeatureCategory defaultCategory) =>
            new()
            {
                MetaInfo = new MetaInfo { ID = Guid.NewGuid() },
                Name = defaultCategory.Name,
                IsExclusive = defaultCategory.IsExclusive,
                HasValidityPeriod = defaultCategory.HasValidityPeriod,
                Options = defaultCategory.Options
                    .Select(option => new Model.WellFeatureOption { ID = Guid.NewGuid(), Name = option })
                    .ToList()
            };

        private static void PrepareCategory(Model.WellFeatureCategory category)
        {
            category.Options ??= [];
            foreach (Model.WellFeatureOption option in category.Options)
            {
                if (option.ID == Guid.Empty)
                {
                    option.ID = Guid.NewGuid();
                }
            }
        }

        private sealed record DefaultWellFeatureCategory(
            string Name,
            bool IsExclusive,
            bool HasValidityPeriod,
            string[] Options);
    }
}
