namespace EmployeeManagementSystem.Common.Constants;

/// <summary>
/// Centralized message constants for the entire application.
/// 
/// WHY THIS IS IMPORTANT?
/// - NO HARD-CODED STRINGS: All messages are in one place
/// - MAINTAINABILITY: Change a message once, applies everywhere
/// - CONSISTENCY: Same message used across the app (no typos)
/// - TRANSLATION-READY: Easy to support multiple languages
/// - SINGLE RESPONSIBILITY: Only responsible for message definitions
/// 
/// BEST PRACTICE:
/// Always use these constants instead of hardcoding strings in Services/Controllers
/// 
/// WRONG:
///     return ApiResponse<User>.Fail(404, "User not found");
/// 
/// RIGHT:
///     return ApiResponse<User>.Fail(404, MessageConstants.USER_NOT_FOUND);
/// </summary>
public static class MessageConstants
{
    
    // ========== GENERAL MESSAGES ==========
    public const string SUCCESS = "Operation completed successfully";
    public const string FAILED = "Operation failed";
    public const string SERVER_ERROR = "An error occurred while processing your request";

    // ========== AUTHENTICATION MESSAGES ==========
    /// <summary>
    /// User successfully logged in
    /// </summary>
    public const string LOGIN_SUCCESS = "Login successful";

    /// <summary>
    /// Login failed due to invalid credentials
    /// </summary>
    public const string LOGIN_FAILED = "Invalid email or password";

    /// <summary>
    /// New user registered successfully
    /// </summary>
    public const string REGISTER_SUCCESS = "User registered successfully";

    /// <summary>
    /// Email already exists in the system
    /// </summary>
    public const string EMAIL_ALREADY_EXISTS = "Email already exists";

    /// <summary>
    /// Password requirements not met
    /// </summary>
    public const string INVALID_PASSWORD = "Password must be at least 8 characters";

    // ========== USER MESSAGES ==========
    public const string USER_NOT_FOUND = "User not found";
    public const string USER_CREATED = "User created successfully";
    public const string USER_UPDATED = "User updated successfully";
    public const string USER_DELETED = "User deleted successfully";
    public const string USER_ALREADY_EXISTS = "User already exists";

    // ========== EMPLOYEE MESSAGES ==========
    public const string EMPLOYEE_NOT_FOUND = "Employee not found";
    public const string EMPLOYEE_CREATED = "Employee created successfully";
    public const string EMPLOYEE_UPDATED = "Employee updated successfully";
    public const string EMPLOYEE_DELETED = "Employee deleted successfully";
    public const string EMPLOYEE_CODE_ALREADY_EXISTS = "Employee code already exists";

    // ========== AUTHORIZATION MESSAGES ==========
    /// <summary>
    /// User is not authenticated (no token or invalid token)
    /// </summary>
    public const string UNAUTHORIZED = "Unauthorized access";

    /// <summary>
    /// User is authenticated but doesn't have permission for this action
    /// </summary>
    public const string FORBIDDEN = "Forbidden access - insufficient permissions";

    public const string TOKEN_EXPIRED = "Token has expired";

    public const string INVALID_TOKEN = "Invalid token";

    // ========== VALIDATION MESSAGES ==========
    public const string INVALID_REQUEST = "Invalid request data";
    public const string REQUIRED_FIELD = "This field is required";
    public const string INVALID_EMAIL = "Invalid email format";
}
