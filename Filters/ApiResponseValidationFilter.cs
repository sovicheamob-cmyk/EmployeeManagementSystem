using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using EmployeeManagementSystem.Common;

namespace EmployeeManagementSystem.Filters;

/// <summary>
/// Global action filter that intercepts failed model validation
/// and returns the project's standardized ApiResponse format.
/// </summary>
public class ApiResponseValidationFilter : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        // ModelState is populated by model binding and validation attributes
        // (e.g., [Required], [EmailAddress], [StringLength]) BEFORE the action runs.
        // If any validation failed, short-circuit here instead of letting the action execute.
        if (!context.ModelState.IsValid)
        {
            // Flatten all validation errors from every model property into a single list.
            var errors = context.ModelState
                .SelectMany(kvp => kvp.Value!.Errors)
                .Select(err => err.ErrorMessage)
                .ToList();

            // Build the message: only the first error is returned as a single string.
            // This keeps the response flat and avoids arrays/dictionaries of errors.
            var message = errors.FirstOrDefault() ?? "Invalid request";

            // Wrap in the project's standard ApiResponse<object>.
            // Status = "Failed", Code = 400, Data = null (or the full error list for client-side debugging).
            var response = ApiResponse<object>.Fail(400, message);

            // Set the action result directly. ASP.NET Core will skip the action method
            // and send this 400 response to the client.
            context.Result = new BadRequestObjectResult(response);
        }
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
        // No post-execution logic needed.
    }
}
