using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Logging;
using OSDC.Drilling.Well.Model;
using OSDC.Drilling.Well.Service.Managers;
using OSDC.DotnetLibraries.General.DataManagement;
using System;
using System.Collections.Generic;

namespace OSDC.Drilling.Well.Service.Controllers
{
    [Produces("application/json")]
    [Route("[controller]")]
    [ApiController]
    public class WellFeatureCategoryController : ControllerBase
    {
        private readonly ILogger<WellFeatureCategoryManager> _logger;
        private readonly WellFeatureCategoryManager _manager;
        private readonly SqlConnectionManager _connectionManager;

        public WellFeatureCategoryController(ILogger<WellFeatureCategoryManager> logger, SqlConnectionManager connectionManager)
        {
            _logger = logger;
            _connectionManager = connectionManager;
            _manager = WellFeatureCategoryManager.GetInstance(_logger, connectionManager);
        }

        [HttpGet(Name = "GetAllWellFeatureCategoryId")]
        public ActionResult<IEnumerable<Guid>> GetAllWellFeatureCategoryId()
        {
            var ids = _manager.GetAllWellFeatureCategoryId();
            return ids != null ? Ok(ids) : StatusCode(StatusCodes.Status500InternalServerError);
        }

        [HttpGet("MetaInfo", Name = "GetAllWellFeatureCategoryMetaInfo")]
        public ActionResult<IEnumerable<MetaInfo?>> GetAllWellFeatureCategoryMetaInfo()
        {
            var metaInfos = _manager.GetAllWellFeatureCategoryMetaInfo();
            return metaInfos != null ? Ok(metaInfos) : StatusCode(StatusCodes.Status500InternalServerError);
        }

        [HttpGet("{id}", Name = "GetWellFeatureCategoryById")]
        public ActionResult<Model.WellFeatureCategory?> GetWellFeatureCategoryById(Guid id)
        {
            if (id == Guid.Empty)
            {
                return BadRequest();
            }

            var data = _manager.GetWellFeatureCategoryById(id);
            return data != null ? Ok(data) : NotFound();
        }

        [HttpGet("HeavyData", Name = "GetAllWellFeatureCategory")]
        public ActionResult<IEnumerable<Model.WellFeatureCategory?>> GetAllWellFeatureCategory()
        {
            var data = _manager.GetAllWellFeatureCategory();
            return data != null ? Ok(data) : StatusCode(StatusCodes.Status500InternalServerError);
        }

        [HttpPost(Name = "PostWellFeatureCategory")]
        [ProducesResponseType<Model.WellFeatureCategory>(StatusCodes.Status200OK)]
        public ActionResult PostWellFeatureCategory([FromBody] Model.WellFeatureCategory? data)
        {
            if (data?.MetaInfo == null || data.MetaInfo.ID == Guid.Empty)
            {
                return BadRequest();
            }

            if (_manager.GetWellFeatureCategoryById(data.MetaInfo.ID) != null)
            {
                return StatusCode(StatusCodes.Status409Conflict);
            }

            return _manager.AddWellFeatureCategory(data)
                ? Ok(data)
                : StatusCode(StatusCodes.Status500InternalServerError);
        }

        [HttpPut("{id}", Name = "PutWellFeatureCategoryById")]
        [ProducesResponseType<Model.WellFeatureCategory>(StatusCodes.Status200OK)]
        [ProducesResponseType<WellMutationErrorEnvelope>(StatusCodes.Status409Conflict)]
        public ActionResult PutWellFeatureCategoryById(Guid id, [FromQuery, BindRequired] DateTimeOffset expectedModifiedUtc, [FromBody] Model.WellFeatureCategory? data)
        {
            if (expectedModifiedUtc == default)
            {
                return BadRequest(new WellMutationErrorEnvelope { Error = "invalid_request", Message = "expectedModifiedUtc is required." });
            }
            return this.ToActionResult(WellCatalogMutationManager.UpdateFeatureCategory(_connectionManager, _logger, id, expectedModifiedUtc, data), data);
        }

        [HttpDelete("{id}", Name = "DeleteWellFeatureCategoryById")]
        public ActionResult DeleteWellFeatureCategoryById(Guid id)
        {
            return this.ToActionResult(WellCatalogMutationManager.DeleteFeatureCategory(_connectionManager, _logger, id));
        }
    }
}
