using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OSDC.Drilling.Well.Service.Managers;

namespace OSDC.Drilling.Well.Service.Controllers;

internal static class WellMutationActionResults
{
    public static ActionResult ToActionResult(this ControllerBase controller, WellMutationResult outcome) => outcome.FailureKind switch
    {
        WellMutationFailureKind.None => controller.Ok(),
        WellMutationFailureKind.InvalidRequest => controller.BadRequest(outcome.Error),
        WellMutationFailureKind.NotFound => controller.NotFound(outcome.Error),
        WellMutationFailureKind.Conflict => controller.Conflict(outcome.Error),
        _ => controller.StatusCode(StatusCodes.Status500InternalServerError, outcome.Error)
    };

    public static ActionResult ToActionResult<T>(this ControllerBase controller, WellMutationResult outcome, T? successValue) =>
        outcome.FailureKind == WellMutationFailureKind.None
            ? controller.Ok(successValue)
            : controller.ToActionResult(outcome);
}
