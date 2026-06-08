using System.Net;
using System.Text.Json;
using EmployeeManagementSystem.Common;
using EmployeeManagementSystem.Common.Constants;

namespace EmployeeManagementSystem.Middleware;

/// <summary>
/// Global exception handler middleware
/// 
/// WHAT IT DOES:
/// - Catches all unhandled exceptions
/// - Converts to ApiResponse format
/// - Sends consistent error response
/// 
/// WHY MIDDLEWARE?
/// - Runs for EVERY request
/// - Can catch exceptions from any layer
/// - Centralized error handling
/// - Prevents exceptions from reaching client as 500 HTML
/// 
/// USAGE IN PROGRAM.CS:
/// app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
/// 
/// EXECUTION ORDER:
/// Must be one of the FIRST middlewares added
/// So it wraps all other middlewares/endpoints
/// 
/// EXAMPLE FLOW:
/// 1. Request comes in
/// 2. Middleware wraps request execution
/// 3. If exception occurs anywhere: caught by middleware
/// 4. Middleware creates ApiResponse with error
/// 5. Sends response to client
/// 6. Client always gets structured response
/// </summary>
public class GlobalExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;

    /// <summary>
    /// Constructor
    /// 
    /// RequestDelegate _next: The next middleware in the pipeline
    /// Every middleware must accept RequestDelegate in constructor
    /// </summary>
    public GlobalExceptionHandlerMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionHandlerMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>
    /// Middleware invoke method
    /// 
    /// ASP.NET Core calls this for every request
    /// Must be named "InvokeAsync" or "Invoke"
    /// 
    /// FLOW:
    /// 1. Try to execute next middleware
    /// 2. If exception: catch it
    /// 3. Create error response
    /// 4. Send to client
    /// 5. Never throw exception to client
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            // Step 1: Call next middleware
            // If no exception, response goes normally
            await _next(context);
        }
        catch (Exception ex)
        {
            // Step 2: Exception caught!
            // Log it for debugging
            _logger.LogError($"Global Exception: {ex.Message}\n{ex.StackTrace}");

            // Step 3: Handle the exception
            await HandleExceptionAsync(context, ex);
        }
    }

    /// <summary>
    /// Handle exception and create response
    /// 
    /// STEPS:
    /// 1. Get exception type
    /// 2. Create appropriate error response
    /// 3. Set HTTP status code
    /// 4. Write response as JSON
    /// </summary>
    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        // Step 1: Set response properties
        context.Response.ContentType = "application/json";

        // Step 2: Create response based on exception type
        // Different exceptions get different status codes
        var response = new ApiResponse<object>();

        switch (exception)
        {
            // Authorization exception
            case UnauthorizedAccessException:
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                response = ApiResponse<object>.Fail(
                    401,
                    MessageConstants.UNAUTHORIZED
                );
                break;

            // Argument/validation exception
            case ArgumentNullException or ArgumentException:
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                response = ApiResponse<object>.Fail(
                    400,
                    MessageConstants.INVALID_REQUEST
                );
                break;

            // General error
            case Exception:
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                response = ApiResponse<object>.Fail(
                    500,
                    MessageConstants.FAILED
                );
                break;
        }

        // Step 3: Serialize response to JSON
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var json = JsonSerializer.Serialize(response, options);

        // Step 4: Write response
        return context.Response.WriteAsync(json);
    }
}

/// <summary>
/// Extension method to register middleware in Program.cs
/// 
/// USAGE IN PROGRAM.CS:
/// var app = builder.Build();
/// app.UseGlobalExceptionHandler();
/// 
/// This is cleaner than:
/// app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
/// </summary>
public static class GlobalExceptionHandlerExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandler(
        this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<GlobalExceptionHandlerMiddleware>();
    }
}
