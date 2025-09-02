using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NORCE.Drilling.Well.Model;

namespace NORCE.Drilling.Well.Service.Controllers
{
    [Produces("application/json")]
    [Route("[controller]")]
    [ApiController]
    public class WellUsageStatisticsController : ControllerBase
    {
        private readonly ILogger _logger;

        public WellUsageStatisticsController(ILogger<WellUsageStatisticsController> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Returns the usage statistics present in the microservice database at endpoint Well/api/WellUsageStatistics
        /// </summary>
        /// <returns></returns>
        [HttpGet(Name = "GetWellUsageStatistics")]
        public ActionResult<UsageStatisticsWell> GetWellUsageStatistics()
        {
            if (UsageStatisticsWell.Instance != null)
            {
                return Ok(UsageStatisticsWell.Instance);
            }
            else
            {
                return NotFound();
            }
        }
    }
}
