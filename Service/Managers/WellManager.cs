using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using OSDC.DotnetLibraries.General.DataManagement;
using Microsoft.Data.Sqlite;
using System.Text.Json;

namespace NORCE.Drilling.Well.Service.Managers
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
                command.CommandText = $"SELECT COUNT(*) FROM WellTable WHERE ID = '{guid}'";
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
                    command.CommandText = $"SELECT Well FROM WellTable WHERE ID = '{guid}'";
                    try
                    {
                        using var reader = command.ExecuteReader();
                        if (reader.Read() && !reader.IsDBNull(0))
                        {
                            string data = reader.GetString(0);
                            well = JsonSerializer.Deserialize<Model.Well>(data, JsonSettings.Options);
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
                    command.CommandText = $"SELECT Well FROM WellTable WHERE ClusterID = '{clusterId}'";
                    try
                    {
                        using var reader = command.ExecuteReader();
                        while (reader.Read() && !reader.IsDBNull(0))
                        {
                            string data = reader.GetString(0);
                            Model.Well? well = JsonSerializer.Deserialize<Model.Well>(data, JsonSettings.Options);
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
            if (well != null && well.MetaInfo != null && well.MetaInfo.ID != Guid.Empty)
            {
                //update WellTable
                var connection = _connectionManager.GetConnection();
                if (connection != null)
                {
                    using SqliteTransaction transaction = connection.BeginTransaction();
                    bool success = true;
                    try
                    {
                        //add the Well to the WellTable
                        string metaInfo = JsonSerializer.Serialize(well.MetaInfo, JsonSettings.Options);
                        string data = JsonSerializer.Serialize(well, JsonSettings.Options);
                        var command = connection.CreateCommand();
                        command.CommandText = "INSERT INTO WellTable (" +
                            "ID, " +
                            "MetaInfo, " +
                            "ClusterID, " +
                            "SlotID, " +
                            "Well" +
                            ") VALUES (" +
                            $"'{well.MetaInfo.ID}', " +
                            $"'{metaInfo}', " +
                            $"'{(well.ClusterID != null ? well.ClusterID : "")}', " +
                            $"'{(well.SlotID != null ? well.SlotID : "")}', " +
                            $"'{data}'" +
                            ")";
                        int count = command.ExecuteNonQuery();
                        if (count != 1)
                        {
                            _logger.LogWarning("Impossible to insert the given Well into the WellTable");
                            success = false;
                        }
                    }
                    catch (SqliteException ex)
                    {
                        _logger.LogError(ex, "Impossible to add the given Well into WellTable");
                        success = false;
                    }
                    //finalizing SQL transaction
                    if (success)
                    {
                        transaction.Commit();
                        _logger.LogInformation("Added the given Well of given ID into the WellTable successfully");
                    }
                    else
                    {
                        transaction.Rollback();
                    }
                    return success;
                }
                else
                {
                    _logger.LogWarning("Impossible to access the SQLite database");
                }
            }
            else
            {
                _logger.LogWarning("The Well ID or the ID of its input are null or empty");
            }
            return false;
        }

        /// <summary>
        /// Performs calculation on the given Well and updates it in the microservice database
        /// </summary>
        /// <param name="well"></param>
        /// <returns>true if the given Well has been updated successfully</returns>
        public bool UpdateWellById(Guid guid, Model.Well? well)
        {
            bool success = true;
            if (guid != Guid.Empty && well != null && well.MetaInfo != null && well.MetaInfo.ID == guid)
            {
                //update WellTable
                var connection = _connectionManager.GetConnection();
                if (connection != null)
                {
                    using SqliteTransaction transaction = connection.BeginTransaction();
                    //update fields in WellTable
                    try
                    {
                        string metaInfo = JsonSerializer.Serialize(well.MetaInfo, JsonSettings.Options);
                        string data = JsonSerializer.Serialize(well, JsonSettings.Options);
                        var command = connection.CreateCommand();
                        command.CommandText = $"UPDATE WellTable SET " +
                            $"MetaInfo = '{metaInfo}', " +
                            $"ClusterID = '{(well.ClusterID != null ? well.ClusterID : "")}', " +
                            $"SlotID = '{(well.SlotID != null ? well.SlotID : "")}', " +
                            $"Well = '{data}' " +
                            $"WHERE ID = '{guid}'";
                        int count = command.ExecuteNonQuery();
                        if (count != 1)
                        {
                            _logger.LogWarning("Impossible to update the Well");
                            success = false;
                        }
                    }
                    catch (SqliteException ex)
                    {
                        _logger.LogError(ex, "Impossible to update the Well");
                        success = false;
                    }

                    // Finalizing
                    if (success)
                    {
                        transaction.Commit();
                        _logger.LogInformation("Updated the given Well successfully");
                        return true;
                    }
                    else
                    {
                        transaction.Rollback();
                    }
                }
                else
                {
                    _logger.LogWarning("Impossible to access the SQLite database");
                }
            }
            else
            {
                _logger.LogWarning("The Well ID or the ID of some of its attributes are null or empty");
            }
            return false;
        }

        /// <summary>
        /// Deletes the Well of given ID from the microservice database
        /// </summary>
        /// <param name="guid"></param>
        /// <returns>true if the Well was deleted from the microservice database</returns>
        public bool DeleteWellById(Guid guid)
        {
            if (!guid.Equals(Guid.Empty))
            {
                var connection = _connectionManager.GetConnection();
                if (connection != null)
                {
                    using var transaction = connection.BeginTransaction();
                    bool success = true;
                    //delete Well from WellTable
                    try
                    {
                        var command = connection.CreateCommand();
                        command.CommandText = $"DELETE FROM WellTable WHERE ID = '{guid}'";
                        int count = command.ExecuteNonQuery();
                        if (count < 0)
                        {
                            _logger.LogWarning("Impossible to delete the Well of given ID from the WellTable");
                            success = false;
                        }
                    }
                    catch (SqliteException ex)
                    {
                        _logger.LogError(ex, "Impossible to delete the Well of given ID from WellTable");
                        success = false;
                    }
                    if (success)
                    {
                        transaction.Commit();
                        _logger.LogInformation("Removed the Well of given ID from the WellTable successfully");
                    }
                    else
                    {
                        transaction.Rollback();
                    }
                    return success;
                }
                else
                {
                    _logger.LogWarning("Impossible to access the SQLite database");
                }
            }
            else
            {
                _logger.LogWarning("The Well ID is null or empty");
            }
            return false;
        }
    }
}