using System.Diagnostics;
using System.Text;
using Serilog;
using Serilog.Context;

namespace EmployeeManagementSystem.Middleware;

/// <summary>
/// Middleware to log all HTTP requests and responses
/// 
/// LOGS:
/// - Each request gets its own log file
/// - File naming: logs/http-{datetime}-{counter}.log
/// - Example: http-20260608-143748-001.log
/// 
/// LOG CONTENT:
/// - Request: Method, Path, Headers, Body
/// - Response: Status Code, Headers, Body
/// - Duration: How long request took to process
/// 
/// USAGE:
/// Automatically added to middleware pipeline in Program.cs
/// app.UseMiddleware<HttpLoggingMiddleware>();
/// </summary>
public class HttpLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<HttpLoggingMiddleware> _logger;
    private static int _requestCounter = 0;

    public HttpLoggingMiddleware(RequestDelegate next, ILogger<HttpLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip logging for Swagger endpoints
        if (context.Request.Path.StartsWithSegments("/swagger") ||
            context.Request.Path.StartsWithSegments("/openapi"))
        {
            await _next(context);
            return;
        }

        // Generate unique request ID and log file
        var requestId = Guid.NewGuid().ToString();
        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var counter = Interlocked.Increment(ref _requestCounter).ToString("D3");
        var logFileName = $"logs/http-{timestamp}-{counter}.log";
        
        using (LogContext.PushProperty("RequestId", requestId))
        {
            var stopwatch = Stopwatch.StartNew();
            var logEntries = new List<string>();

            try
            {
                // Log incoming request
                await LogRequest(context, requestId, logEntries);

                // Buffer original response stream
                var originalBodyStream = context.Response.Body;
                using (var bufferedStream = new MemoryStream())
                {
                    context.Response.Body = bufferedStream;

                    // Call next middleware
                    await _next(context);

                    // Log outgoing response
                    await LogResponse(context, bufferedStream, requestId, logEntries);

                    // Copy buffered response to original stream
                    await bufferedStream.CopyToAsync(originalBodyStream);
                    context.Response.Body = originalBodyStream;
                }

                stopwatch.Stop();
                var completionLog = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] Request completed: {requestId} | {context.Request.Method} {context.Request.Path} | Status: {context.Response.StatusCode} | Duration: {stopwatch.ElapsedMilliseconds}ms";
                logEntries.Add(completionLog);

                // Write all logs to file
                await WriteLogFile(logFileName, logEntries);

                _logger.LogInformation(
                    "Request logged: {RequestId} | {Method} {Path} | Status: {StatusCode} | File: {LogFile}",
                    requestId, context.Request.Method, context.Request.Path, context.Response.StatusCode, logFileName
                );
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                var errorLog = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] Request error: {requestId} | {context.Request.Method} {context.Request.Path} | Duration: {stopwatch.ElapsedMilliseconds}ms | Exception: {ex.Message}";
                logEntries.Add(errorLog);

                // Write error logs to file
                await WriteLogFile(logFileName, logEntries);

                _logger.LogError(
                    ex,
                    "Request error: {RequestId} | {Method} {Path} | Duration: {DurationMs}ms | File: {LogFile}",
                    requestId, context.Request.Method, context.Request.Path, stopwatch.ElapsedMilliseconds, logFileName
                );
                throw;
            }
        }
    }

    private async Task LogRequest(HttpContext context, string requestId, List<string> logEntries)
    {
        var request = context.Request;
        
        // Read body if present
        request.EnableBuffering();
        var body = "";
        
        if (request.ContentLength > 0)
        {
            using (var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true))
            {
                body = await reader.ReadToEndAsync();
                request.Body.Position = 0;
            }
        }

        // Format request log
        var headers = FormatHeaders(request.Headers);
        var requestLog = $@"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] HTTP Request: {requestId}
        Method: {request.Method}
        Path: {request.Path}
        Headers: {headers}
        Body: {(string.IsNullOrEmpty(body) ? "(empty)" : body)}
        ================================================================================";
        
        logEntries.Add(requestLog);
    }

    private async Task LogResponse(HttpContext context, MemoryStream bufferedStream, string requestId, List<string> logEntries)
    {
        var response = context.Response;
        
        // Read response body
        bufferedStream.Seek(0, SeekOrigin.Begin);
        var body = "";
        
        if (bufferedStream.Length > 0)
        {
            using (var reader = new StreamReader(bufferedStream, Encoding.UTF8, leaveOpen: true))
            {
                body = await reader.ReadToEndAsync();
                bufferedStream.Seek(0, SeekOrigin.Begin);
            }
        }

        // Format response log
        var headers = FormatHeaders(response.Headers);
        var responseLog = $@"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] HTTP Response: {requestId}
        Status Code: {response.StatusCode}
        Headers: {headers}
        Body: {(string.IsNullOrEmpty(body) ? "(empty)" : body)}
        ================================================================================";
        
        logEntries.Add(responseLog);
    }

    private string FormatHeaders(IHeaderDictionary headers)
    {
        var headerList = new StringBuilder();
        foreach (var header in headers)
        {
            // Skip sensitive headers
            if (!header.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase) &&
                !header.Key.Equals("Cookie", StringComparison.OrdinalIgnoreCase))
            {
                headerList.Append($"\n  {header.Key}: {header.Value}");
            }
            else if (header.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase))
            {
                headerList.Append($"\n  {header.Key}: [REDACTED]");
            }
        }
        return headerList.ToString();
    }

    private async Task WriteLogFile(string filePath, List<string> logEntries)
    {
        try
        {
            // Create logs directory if it doesn't exist
            var logsDirectory = Path.GetDirectoryName(filePath) ?? "logs";
            if (!Directory.Exists(logsDirectory))
            {
                Directory.CreateDirectory(logsDirectory);
            }

            // Write all entries to file
            var content = string.Join("\n", logEntries);
            await File.WriteAllTextAsync(filePath, content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write log file: {FilePath}", filePath);
        }
    }
}
