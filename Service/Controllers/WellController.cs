using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OSDC.DotnetLibraries.General.DataManagement;
using NORCE.Drilling.Well.Service.Managers;
using NORCE.Drilling.Well.Model;

namespace NORCE.Drilling.Well.Service.Controllers
{
    [Produces("application/json")]
    [Route("[controller]")]
    [ApiController]
    public class WellController : ControllerBase
    {
        private readonly ILogger<WellManager> _logger;
        private readonly WellManager _wellManager;

        public WellController(ILogger<WellManager> logger, SqlConnectionManager connectionManager)
        {
            _logger = logger;
            _wellManager = WellManager.GetInstance(_logger, connectionManager);
        }

        /// <summary>
        /// Returns the list of Guid of all Well present in the microservice database at endpoint Well/api/Well
        /// </summary>
        /// <returns>the list of Guid of all Well present in the microservice database at endpoint Well/api/Well</returns>
        [HttpGet(Name = "GetAllWellId")]
        public ActionResult<IEnumerable<Guid>> GetAllWellId()
        {
            UsageStatisticsWell.Instance.IncrementGetAllWellIdPerDay();
            var ids = _wellManager.GetAllWellId();
            if (ids != null)
            {
                return Ok(ids);
            }
            else
            {
                return StatusCode(StatusCodes.Status500InternalServerError);
            }
        }

        /// <summary>
        /// Returns the list of MetaInfo of all Well present in the microservice database, at endpoint Well/api/Well/MetaInfo
        /// </summary>
        /// <returns>the list of MetaInfo of all Well present in the microservice database, at endpoint Well/api/Well/MetaInfo</returns>
        [HttpGet("MetaInfo", Name = "GetAllWellMetaInfo")]
        public ActionResult<IEnumerable<MetaInfo>> GetAllWellMetaInfo()
        {
            UsageStatisticsWell.Instance.IncrementGetAllWellMetaInfoPerDay();
            var vals = _wellManager.GetAllWellMetaInfo();
            if (vals != null)
            {
                return Ok(vals);
            }
            else
            {
                return StatusCode(StatusCodes.Status500InternalServerError);
            }
        }

        /// <summary>
        /// Returns the Well identified by its Guid from the microservice database, at endpoint Well/api/Well/MetaInfo/id
        /// </summary>
        /// <param name="guid"></param>
        /// <returns>the Well identified by its Guid from the microservice database, at endpoint Well/api/Well/MetaInfo/id</returns>
        [HttpGet("{id}", Name = "GetWellById")]
        public ActionResult<Model.Well?> GetWellById(Guid id)
        {
            UsageStatisticsWell.Instance.IncrementGetWellByIdPerDay();
            if (!id.Equals(Guid.Empty))
            {
                var val = _wellManager.GetWellById(id);
                if (val != null)
                {
                    return Ok(val);
                }
                else
                {
                    return NotFound();
                }
            }
            else
            {
                return BadRequest();
            }
        }

        /// <summary>
        /// Returns the list of all Well present in the microservice database, at endpoint Well/api/Well/HeavyData
        /// </summary>
        /// <returns>the list of all Well present in the microservice database, at endpoint Well/api/Well/HeavyData</returns>
        [HttpGet("HeavyData", Name = "GetAllWell")]
        public ActionResult<IEnumerable<Model.Well?>> GetAllWell()
        {
            UsageStatisticsWell.Instance.IncrementGetAllWellPerDay();
            var vals = _wellManager.GetAllWell();
            if (vals != null)
            {
                return Ok(vals);
            }
            else
            {
                return StatusCode(StatusCodes.Status500InternalServerError);
            }
        }

        /// <summary>
        /// Returns the MetaInfo of all the slots used in the cluster of given ID, at endpoint Well/api/Well/UsedSlot/clusterId
        /// </summary>
        /// <param name="guid"></param>
        /// <returns>the MetaInfo of all the slots used in the cluster of given ID, at endpoint Well/api/Well/UsedSlot/clusterId</returns>
        [HttpGet("UsedSlot/{clusterId}", Name = "GetAllUsedSlotMetaInfoByClusterId")]
        public ActionResult<IEnumerable<MetaInfo>> GetAllUsedSlotMetaInfoByClusterId(Guid clusterId)
        {
            if (!clusterId.Equals(Guid.Empty))
            {
                var val = _wellManager.GetAllUsedSlotIDByClusterId(clusterId);
                if (val != null)
                {
                    return Ok(val);
                }
                else
                {
                    return NotFound();
                }
            }
            else
            {
                return BadRequest();
            }
        }

        /// <summary>
        /// Performs calculation on the given Well and adds it to the microservice database, at the endpoint Well/api/Well
        /// </summary>
        /// <param name="well"></param>
        /// <returns>true if the given Well has been added successfully to the microservice database, at the endpoint Well/api/Well</returns>
        [HttpPost(Name = "PostWell")]
        public ActionResult PostWell([FromBody] Model.Well? data)
        {
            UsageStatisticsWell.Instance.IncrementPostWellPerDay();
            // Check if well exists in the database through ID
            if (data != null && data.MetaInfo != null && data.MetaInfo.ID != Guid.Empty)
            {
                var existingData = _wellManager.GetWellById(data.MetaInfo.ID);
                if (existingData == null)
                {   
                    //  If well was not found, call AddWell, where the well.Calculate()
                    // method is called. 
                    if (_wellManager.AddWell(data))
                    {
                        return Ok(); // status=OK is used rather than status=Created because NSwag auto-generated controllers use 200 (OK) rather than 201 (Created) as return codes
                    }
                    else
                    {
                        return StatusCode(StatusCodes.Status500InternalServerError);
                    }
                }
                else
                {
                    _logger.LogWarning("The given Well already exists and will not be added");
                    return StatusCode(StatusCodes.Status409Conflict);
                }
            }
            else
            {
                _logger.LogWarning("The given Well is null, badly formed, or its ID is empty");
                return BadRequest();
            }
        }

        /// <summary>
        /// Performs calculation on the given Well and updates it in the microservice database, at the endpoint Well/api/Well/id
        /// </summary>
        /// <param name="well"></param>
        /// <returns>true if the given Well has been updated successfully to the microservice database, at the endpoint Well/api/Well/id</returns>
        [HttpPut("{id}", Name = "PutWellById")]
        public ActionResult PutWellById(Guid id, [FromBody] Model.Well? data)
        {
            UsageStatisticsWell.Instance.IncrementPutWellByIdPerDay();
            // Check if Well is in the data base
            if (data != null && data.MetaInfo != null && data.MetaInfo.ID.Equals(id))
            {
                var existingData = _wellManager.GetWellById(id);
                if (existingData != null)
                {
                    if (_wellManager.UpdateWellById(id, data))
                    {
                        return Ok();
                    }
                    else
                    {
                        return StatusCode(StatusCodes.Status500InternalServerError);
                    }
                }
                else
                {
                    _logger.LogWarning("The given Well has not been found in the database");
                    return NotFound();
                }
            }
            else
            {
                _logger.LogWarning("The given Well is null, badly formed, or its does not match the ID to update");
                return BadRequest();
            }
        }

        /// <summary>
        /// Deletes the Well of given ID from the microservice database, at the endpoint Well/api/Well/id
        /// </summary>
        /// <param name="guid"></param>
        /// <returns>true if the Well was deleted from the microservice database, at the endpoint Well/api/Well/id</returns>
        [HttpDelete("{id}", Name = "DeleteWellById")]
        public ActionResult DeleteWellById(Guid id)
        {
            UsageStatisticsWell.Instance.IncrementDeleteWellByIdPerDay();
            if (_wellManager.GetWellById(id) != null)
            {
                if (_wellManager.DeleteWellById(id))
                {
                    return Ok();
                }
                else
                {
                    return StatusCode(StatusCodes.Status500InternalServerError);
                }
            }
            else
            {
                _logger.LogWarning("The Well of given ID does not exist");
                return NotFound();
            }
        }
    }
}
