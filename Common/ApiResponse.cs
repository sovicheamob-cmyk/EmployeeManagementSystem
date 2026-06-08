namespace EmployeeManagementSystem.Common;

/// <summary>
/// Standard API Response wrapper for all endpoints.
/// 
/// WHY THIS PATTERN?
/// - Provides consistent response format across entire API
/// - Makes error handling predictable for clients
/// - Includes metadata (Status, Code, Message) along with data
/// - Allows typed data responses with generics
/// 
/// REQUEST FLOW:
/// Controller → Service → Database → Service wraps result in ApiResponse<T> → Client receives standardized format
/// </summary>
/// <typeparam name="T">The type of data being returned (e.g., UserDto, EmployeeDto, List<Employee>)</typeparam>
public class ApiResponse<T>
{
    /// <summary>
    /// Status of the operation: "Success" or "Failed"
    /// </summary>
    public string Status { get; set; } = "Success";

    /// <summary>
    /// HTTP-like status code:
    /// - 200: Success
    /// - 400: Bad Request
    /// - 401: Unauthorized
    /// - 403: Forbidden
    /// - 404: Not Found
    /// - 500: Internal Server Error
    /// </summary>
    public int Code { get; set; }

    /// <summary>
    /// Human-readable message describing the operation result
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// The actual data returned from the operation
    /// Is null/default when operation fails
    /// </summary>
    public T? Data { get; set; }

    /// <summary>
    /// Creates a successful response
    /// 
    /// USAGE EXAMPLE:
    /// return Ok(ApiResponse<UserDto>.Success(userDto, MessageConstants.USER_CREATED));
    /// 
    /// LINE BY LINE EXPLANATION:
    /// - Sets Status to "Success"
    /// - Sets Code to 200 (HTTP OK)
    /// - Sets the custom message
    /// - Includes the returned data
    /// </summary>
    public static ApiResponse<T> Success(T data, string message)
    {
        return new ApiResponse<T>
        {
            Status = "Success",
            Code = 200,
            Message = message,
            Data = data
        };
    }

    /// <summary>
    /// Creates a failed response
    /// 
    /// USAGE EXAMPLE:
    /// return NotFound(ApiResponse<User>.Fail(404, MessageConstants.USER_NOT_FOUND));
    /// 
    /// LINE BY LINE EXPLANATION:
    /// - Sets Status to "Failed"
    /// - Sets the provided error code (400, 404, 500, etc.)
    /// - Sets the error message
    /// - Data is null (no data on failure)
    /// </summary>
    public static ApiResponse<T> Fail(int code, string message)
    {
        return new ApiResponse<T>
        {
            Status = "Failed",
            Code = code,
            Message = message,
            Data = default  // null for reference types, default for value types
        };
    }
}
