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
    public class WellIdentityController : ControllerBase
    {
        private readonly ILogger<WellIdentityManager> _logger;
        private readonly WellIdentityManager _manager;
        private readonly SqlConnectionManager _connectionManager;

        public WellIdentityController(ILogger<WellIdentityManager> logger, SqlConnectionManager connectionManager)
        {
            _logger = logger;
            _connectionManager = connectionManager;
            _manager = WellIdentityManager.GetInstance(_logger, connectionManager);
        }

        [HttpGet(Name = "GetAllWellIdentityId")]
        public ActionResult<IEnumerable<Guid>> GetAllWellIdentityId()
        {
            var ids = _manager.GetAllWellIdentityId();
            return ids != null ? Ok(ids) : StatusCode(StatusCodes.Status500InternalServerError);
        }

        [HttpGet("MetaInfo", Name = "GetAllWellIdentityMetaInfo")]
        public ActionResult<IEnumerable<MetaInfo?>> GetAllWellIdentityMetaInfo()
        {
            var metaInfos = _manager.GetAllWellIdentityMetaInfo();
            return metaInfos != null ? Ok(metaInfos) : StatusCode(StatusCodes.Status500InternalServerError);
        }

        [HttpGet("{id}", Name = "GetWellIdentityById")]
        public ActionResult<Model.WellIdentity?> GetWellIdentityById(Guid id)
        {
            if (id == Guid.Empty)
            {
                return BadRequest();
            }

            var data = _manager.GetWellIdentityById(id);
            return data != null ? Ok(data) : NotFound();
        }

        [HttpGet("HeavyData", Name = "GetAllWellIdentity")]
        public ActionResult<IEnumerable<Model.WellIdentity?>> GetAllWellIdentity()
        {
            var data = _manager.GetAllWellIdentity();
            return data != null ? Ok(data) : StatusCode(StatusCodes.Status500InternalServerError);
        }

        [HttpPost(Name = "PostWellIdentity")]
        [ProducesResponseType<Model.WellIdentity>(StatusCodes.Status200OK)]
        public ActionResult PostWellIdentity([FromBody] Model.WellIdentity? data)
        {
            if (data?.MetaInfo == null || data.MetaInfo.ID == Guid.Empty)
            {
                return BadRequest();
            }

            if (_manager.GetWellIdentityById(data.MetaInfo.ID) != null)
            {
                return StatusCode(StatusCodes.Status409Conflict);
            }

            return _manager.AddWellIdentity(data)
                ? Ok(data)
                : StatusCode(StatusCodes.Status500InternalServerError);
        }

        [HttpPut("{id}", Name = "PutWellIdentityById")]
        [ProducesResponseType<Model.WellIdentity>(StatusCodes.Status200OK)]
        [ProducesResponseType<WellMutationErrorEnvelope>(StatusCodes.Status409Conflict)]
        public ActionResult PutWellIdentityById(Guid id, [FromQuery, BindRequired] DateTimeOffset expectedModifiedUtc, [FromBody] Model.WellIdentity? data)
        {
            if (expectedModifiedUtc == default)
            {
                return BadRequest(new WellMutationErrorEnvelope { Error = "invalid_request", Message = "expectedModifiedUtc is required." });
            }
            return this.ToActionResult(WellCatalogMutationManager.UpdateIdentity(_connectionManager, _logger, id, expectedModifiedUtc, data), data);
        }

        [HttpDelete("{id}", Name = "DeleteWellIdentityById")]
        public ActionResult DeleteWellIdentityById(Guid id)
        {
            return this.ToActionResult(WellCatalogMutationManager.DeleteIdentity(_connectionManager, _logger, id));
        }
    }
}
