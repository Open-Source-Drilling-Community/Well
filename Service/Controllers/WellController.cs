using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Logging;
using OSDC.DotnetLibraries.General.DataManagement;
using OSDC.Drilling.Well.Service.Managers;
using OSDC.Drilling.Well.Model;

namespace OSDC.Drilling.Well.Service.Controllers
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

        /// <summary>Returns a deterministic, paginated page of Wells matching optional server-side filters.</summary>
        [HttpGet("Search", Name = "SearchWells")]
        [ProducesResponseType<WellSearchResult>(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(WellMutationErrorEnvelope), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(WellMutationErrorEnvelope), StatusCodes.Status500InternalServerError)]
        public ActionResult<WellSearchResult> SearchWells(
            [FromQuery, Range(0, int.MaxValue)] int offset = 0,
            [FromQuery, Range(1, 200)] int limit = 50,
            [FromQuery, StringLength(200)] string? name = null,
            [FromQuery] Guid? clusterId = null,
            [FromQuery] Guid? slotId = null,
            [FromQuery] Guid? identityId = null,
            [FromQuery, StringLength(500)] string? identityValue = null,
            [FromQuery] Guid? featureCategoryId = null,
            [FromQuery] Guid? featureOptionId = null,
            [FromQuery] DateTimeOffset? modifiedFromUtc = null,
            [FromQuery] DateTimeOffset? modifiedToUtc = null)
        {
            if (offset < 0 || limit is < 1 or > 200)
                return BadRequest(WellMutationResult.Invalid("pagination", "invalid_range", "Offset must be non-negative and limit must be between 1 and 200.").Error);
            if (name?.Length > 200 || identityValue?.Length > 500)
                return BadRequest(WellMutationResult.Invalid("filters", "value_too_long", "Name is limited to 200 characters and identityValue to 500 characters.").Error);
            if (new[] { clusterId, slotId, identityId, featureCategoryId, featureOptionId }.Any(value => value == Guid.Empty))
                return BadRequest(WellMutationResult.Invalid("filters", "empty_uuid", "Optional UUID filters must be omitted or non-empty.").Error);
            if (modifiedFromUtc > modifiedToUtc)
                return BadRequest(WellMutationResult.Invalid("modifiedFromUtc", "invalid_date_range", "modifiedFromUtc must be earlier than or equal to modifiedToUtc.").Error);

            WellSearchResult? result = _wellManager.SearchWells(offset, limit, name, clusterId, slotId,
                identityId, identityValue, featureCategoryId, featureOptionId, modifiedFromUtc, modifiedToUtc);
            return result != null
                ? Ok(result)
                : StatusCode(StatusCodes.Status500InternalServerError, WellMutationResult.StorageFailure().Error);
        }

        /// <summary>Exports all Wells or an ordered selection with referenced local catalog definitions.</summary>
        [HttpPost("BatchExport", Name = "BatchExportWells")]
        [ProducesResponseType<WellBatchExportDocument>(StatusCodes.Status200OK)]
        [ProducesResponseType<WellBatchErrorEnvelope>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<WellBatchErrorEnvelope>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<WellBatchErrorEnvelope>(StatusCodes.Status500InternalServerError)]
        public ActionResult<WellBatchExportDocument> BatchExportWells([FromBody] WellBatchExportRequest? request)
        {
            WellBatchExportOutcome outcome = _wellManager.ExportBatch(request);
            if (outcome.IsSuccess) return Ok(outcome.Document);
            return outcome.FailureKind switch
            {
                WellBatchExportFailureKind.InvalidRequest => BadRequest(outcome.Error),
                WellBatchExportFailureKind.WellNotFound => NotFound(outcome.Error),
                _ => StatusCode(StatusCodes.Status500InternalServerError, outcome.Error)
            };
        }

        /// <summary>Validates and atomically restores Wells and their local catalog dependencies.</summary>
        [HttpPost("BatchRestore", Name = "BatchRestoreWells")]
        [ProducesResponseType<WellBatchRestoreResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<WellBatchErrorEnvelope>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<WellBatchErrorEnvelope>(StatusCodes.Status409Conflict)]
        [ProducesResponseType<WellBatchErrorEnvelope>(StatusCodes.Status500InternalServerError)]
        public ActionResult<WellBatchRestoreResponse> BatchRestoreWells([FromBody] WellBatchRestoreRequest? request)
        {
            WellBatchRestoreOutcome outcome = _wellManager.RestoreBatch(request);
            if (outcome.IsSuccess) return Ok(outcome.Response);
            return outcome.FailureKind switch
            {
                WellBatchRestoreFailureKind.InvalidRequest => BadRequest(outcome.Error),
                WellBatchRestoreFailureKind.Conflict => Conflict(outcome.Error),
                _ => StatusCode(StatusCodes.Status500InternalServerError, outcome.Error)
            };
        }


        /// <summary>
        /// Returns the list of all Well present in the microservice database with given SlotId, at endpoint Well/api/Well/HeavyData
        /// </summary>
        /// <returns>the list of all Well present in the microservice database with given SlotId, at endpoint Well/api/Well/HeavyData</returns>
        [HttpGet("SlotId", Name = "GetAllWellBySlotId")]
        public ActionResult<IEnumerable<Model.Well?>> GetAllWellBySlotId(Guid slotId)
        {
            UsageStatisticsWell.Instance.IncrementGetAllWellPerDay();
            var vals = _wellManager.GetAllWellBySlotId(slotId);
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
        /// Returns the list of all Well present in the microservice database with given ClusterId, at endpoint Well/api/Well/HeavyData
        /// </summary>
        /// <returns>the list of all Well present in the microservice database with given ClusterId, at endpoint Well/api/Well/HeavyData</returns>
        [HttpGet("ClusterId", Name = "GetAllWellByClusterId")]
        public ActionResult<IEnumerable<Model.Well?>> GetAllWellByClusterId(Guid clusterId)
        {
            UsageStatisticsWell.Instance.IncrementGetAllWellPerDay();
            var vals = _wellManager.GetAllWellByClusterId(clusterId);
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
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(WellMutationErrorEnvelope), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(WellMutationErrorEnvelope), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(WellMutationErrorEnvelope), StatusCodes.Status500InternalServerError)]
        public ActionResult PostWell([FromBody] Model.Well? data)
        {
            UsageStatisticsWell.Instance.IncrementPostWellPerDay();
            return this.ToActionResult(_wellManager.CreateWell(data));
        }

        /// <summary>
        /// Performs calculation on the given Well and updates it in the microservice database, at the endpoint Well/api/Well/id
        /// </summary>
        /// <param name="well"></param>
        /// <returns>true if the given Well has been updated successfully to the microservice database, at the endpoint Well/api/Well/id</returns>
        [HttpPut("{id}", Name = "PutWellById")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(WellMutationErrorEnvelope), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(WellMutationErrorEnvelope), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(WellMutationErrorEnvelope), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(WellMutationErrorEnvelope), StatusCodes.Status500InternalServerError)]
        public ActionResult PutWellById(Guid id,
            [FromQuery, BindRequired] DateTimeOffset expectedModifiedUtc,
            [FromBody] Model.Well? data)
        {
            UsageStatisticsWell.Instance.IncrementPutWellByIdPerDay();
            return this.ToActionResult(_wellManager.UpdateWell(id, expectedModifiedUtc, data));
        }

        [HttpPost("{wellId}/IdentityAssignments", Name = "PostWellIdentityAssignment")]
        [ProducesResponseType<Model.Well>(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(WellMutationErrorEnvelope), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(WellMutationErrorEnvelope), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(WellMutationErrorEnvelope), StatusCodes.Status409Conflict)]
        public ActionResult PostWellIdentityAssignment(Guid wellId,
            [FromQuery, BindRequired] DateTimeOffset expectedModifiedUtc, [FromBody] WellIdentityAssignment? assignment)
        {
            WellMutationResult outcome = _wellManager.AddIdentityAssignment(wellId, expectedModifiedUtc, assignment);
            return this.ToActionResult(outcome, outcome.Resource);
        }

        [HttpPut("{wellId}/IdentityAssignments/{assignmentId}", Name = "PutWellIdentityAssignment")]
        [ProducesResponseType<Model.Well>(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(WellMutationErrorEnvelope), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(WellMutationErrorEnvelope), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(WellMutationErrorEnvelope), StatusCodes.Status409Conflict)]
        public ActionResult PutWellIdentityAssignment(Guid wellId, Guid assignmentId,
            [FromQuery, BindRequired] DateTimeOffset expectedModifiedUtc, [FromBody] WellIdentityAssignment? assignment)
        {
            WellMutationResult outcome = _wellManager.UpdateIdentityAssignment(wellId, assignmentId, expectedModifiedUtc, assignment);
            return this.ToActionResult(outcome, outcome.Resource);
        }

        [HttpDelete("{wellId}/IdentityAssignments/{assignmentId}", Name = "DeleteWellIdentityAssignment")]
        [ProducesResponseType<Model.Well>(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(WellMutationErrorEnvelope), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(WellMutationErrorEnvelope), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(WellMutationErrorEnvelope), StatusCodes.Status409Conflict)]
        public ActionResult DeleteWellIdentityAssignment(Guid wellId, Guid assignmentId,
            [FromQuery, BindRequired] DateTimeOffset expectedModifiedUtc)
        {
            WellMutationResult outcome = _wellManager.DeleteIdentityAssignment(wellId, assignmentId, expectedModifiedUtc);
            return this.ToActionResult(outcome, outcome.Resource);
        }

        [HttpPost("{wellId}/FeatureAssignments", Name = "PostWellFeatureAssignment")]
        [ProducesResponseType<Model.Well>(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(WellMutationErrorEnvelope), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(WellMutationErrorEnvelope), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(WellMutationErrorEnvelope), StatusCodes.Status409Conflict)]
        public ActionResult PostWellFeatureAssignment(Guid wellId,
            [FromQuery, BindRequired] DateTimeOffset expectedModifiedUtc, [FromBody] WellFeatureAssignment? assignment)
        {
            WellMutationResult outcome = _wellManager.AddFeatureAssignment(wellId, expectedModifiedUtc, assignment);
            return this.ToActionResult(outcome, outcome.Resource);
        }

        [HttpPut("{wellId}/FeatureAssignments/{assignmentId}", Name = "PutWellFeatureAssignment")]
        [ProducesResponseType<Model.Well>(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(WellMutationErrorEnvelope), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(WellMutationErrorEnvelope), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(WellMutationErrorEnvelope), StatusCodes.Status409Conflict)]
        public ActionResult PutWellFeatureAssignment(Guid wellId, Guid assignmentId,
            [FromQuery, BindRequired] DateTimeOffset expectedModifiedUtc, [FromBody] WellFeatureAssignment? assignment)
        {
            WellMutationResult outcome = _wellManager.UpdateFeatureAssignment(wellId, assignmentId, expectedModifiedUtc, assignment);
            return this.ToActionResult(outcome, outcome.Resource);
        }

        [HttpDelete("{wellId}/FeatureAssignments/{assignmentId}", Name = "DeleteWellFeatureAssignment")]
        [ProducesResponseType<Model.Well>(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(WellMutationErrorEnvelope), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(WellMutationErrorEnvelope), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(WellMutationErrorEnvelope), StatusCodes.Status409Conflict)]
        public ActionResult DeleteWellFeatureAssignment(Guid wellId, Guid assignmentId,
            [FromQuery, BindRequired] DateTimeOffset expectedModifiedUtc)
        {
            WellMutationResult outcome = _wellManager.DeleteFeatureAssignment(wellId, assignmentId, expectedModifiedUtc);
            return this.ToActionResult(outcome, outcome.Resource);
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
