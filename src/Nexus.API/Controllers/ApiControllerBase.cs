using Microsoft.AspNetCore.Mvc;
using Nexus.Domain.Common;

namespace Nexus.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public abstract class ApiControllerBase : ControllerBase
{
    protected IActionResult HandleResult(Result result)
    {
        if (result.IsSuccess)
        {
            return Ok();
        }

        return HandleError(result.Error);
    }

    protected IActionResult HandleResult<T>(Result<T> result)
    {
        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        return HandleError(result.Error);
    }

    protected IActionResult HandleCreatedResult<T>(Result<T> result, string actionName, Func<T, object> routeValuesFactory)
    {
        if (result.IsSuccess)
        {
            return CreatedAtAction(actionName, routeValuesFactory(result.Value), result.Value);
        }

        return HandleError(result.Error);
    }

    protected IActionResult HandleCreatedResult<T>(Result<T> result, string actionName, object routeValues)
    {
        if (result.IsSuccess)
        {
            return CreatedAtAction(actionName, routeValues, result.Value);
        }

        return HandleError(result.Error);
    }

    private IActionResult HandleError(Error error)
    {
        if (error == Error.NotFound || error.Code.EndsWith(".NotFound") || error.Code.Contains("NotFound"))
        {
            return NotFound(new { error.Code, error.Description });
        }

        // 403 Forbidden: specifically for access denial or lack of permission
        if (error == Error.Forbidden ||
            error.Code.EndsWith(".Forbidden") ||
            error.Code.Contains("Forbidden") ||
            error.Code.EndsWith(".AccessDenied") ||
            error.Code.Contains("AccessDenied"))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error.Code, error.Description });
        }

        // 401 Unauthorized: for unauthenticated identity or missing credentials
        if (error == Error.Unauthorized || error.Code.EndsWith(".Unauthorized") || error.Code.Contains("Unauthorized"))
        {
            return Unauthorized(new { error.Code, error.Description });
        }

        if (error == Error.Conflict ||
            error.Code.EndsWith(".DuplicateEmail") ||
            error.Code.Contains("Conflict") ||
            error.Code.Contains("Cyclic") ||
            error.Code.Contains("SelfParenting"))
        {
            return Conflict(new { error.Code, error.Description });
        }

        return BadRequest(new { error.Code, error.Description });
    }
}
