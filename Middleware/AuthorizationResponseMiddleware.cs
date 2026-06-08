using System.Text.Json;
using EmployeeManagementSystem.Common;
using EmployeeManagementSystem.Common.Constants;

namespace EmployeeManagementSystem.Middleware;

/// <summary>
/// Middleware to handle 401 Unauthorized and 403 Forbidden responses
/// 
/// WHY THIS MIDDLEWARE?
/// - Authentication/Authorization middleware sets status codes directly
/// - They don't go through our exception handler
/// - Need to intercept and wrap responses with ApiResponse format
/// 
/// WHAT IT DOES:
/// - Catches 401 (Unauthorized) responses
/// - Catches 403 (Forbidden) responses
/// - Wraps them in consistent ApiResponse<object> format
/// - Sends JSON instead of default error page
/// </summary>
public class AuthorizationResponseMiddleware
{
    private readonly RequestDelegate _next;

    public AuthorizationResponseMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip this middleware for auth endpoints (they handle their own responses)
        var path = context.Request.Path.Value?.ToLower() ?? "";
        if (path.Contains("/api/auth"))
        {
            await _next(context);
            return;
        }

        // Only intercept protected endpoints (not auth)
        // These are endpoints that can return 401/403 due to authorization checks
        var originalBodyStream = context.Response.Body;

        using (var responseBody = new MemoryStream())
        {
            context.Response.Body = responseBody;

            try
            {
                await _next(context);

                // Check if response is 401 or 403 - these come from authorization filters
                if (context.Response.StatusCode == StatusCodes.Status401Unauthorized ||
                    context.Response.StatusCode == StatusCodes.Status403Forbidden)
                {
                    // Only write error response if headers haven't been sent yet
                    if (!context.Response.HasStarted)
                    {
                        context.Response.ContentType = "application/json";

                        // Create appropriate error response
                        var response = new ApiResponse<object>();

                        if (context.Response.StatusCode == StatusCodes.Status401Unauthorized)
                        {
                            response = ApiResponse<object>.Fail(
                                401,
                                MessageConstants.UNAUTHORIZED
                            );
                        }
                        else if (context.Response.StatusCode == StatusCodes.Status403Forbidden)
                        {
                            response = ApiResponse<object>.Fail(
                                403,
                                MessageConstants.FORBIDDEN
                            );
                        }

                        // Serialize to JSON
                        var options = new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true,
                            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                        };

                        var json = JsonSerializer.Serialize(response, options);
                        context.Response.ContentLength = json.Length;
                        await originalBodyStream.WriteAsync(System.Text.Encoding.UTF8.GetBytes(json));
                    }
                }
                else
                {
                    // For non-error responses, copy the captured body to original stream
                    responseBody.Seek(0, SeekOrigin.Begin);
                    await responseBody.CopyToAsync(originalBodyStream);
                }
            }
            finally
            {
                context.Response.Body = originalBodyStream;
            }
        }
    }
}
