using System.ComponentModel.DataAnnotations;

namespace EmployeeManagementSystem.DTOs;

/// <summary>
/// DTO for user registration request
/// 
/// WHY DTO PATTERN?
/// - Client sends this object (not the User model)
/// - Validation annotations ensure data quality
/// - Only required fields are included
/// - ASP.NET Core automatically validates using these attributes
/// 
/// VALIDATION FLOW:
/// 1. Client sends JSON → ASP.NET deserializes to RegisterRequestDto
/// 2. Validation attributes check data
/// 3. If invalid, returns 400 Bad Request with validation errors
/// 4. If valid, Service receives this DTO
/// 
/// DATA ANNOTATIONS:
/// - [Required]: Field cannot be null/empty
/// - [EmailAddress]: Validates email format
/// - [StringLength]: Sets min/max length
/// - [MinLength]: Minimum string length
/// </summary>
public class RegisterRequestDto
{
    [Required(ErrorMessage = "First name is required")]
    [StringLength(50, ErrorMessage = "First name cannot exceed 50 characters")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Last name is required")]
    [StringLength(50, ErrorMessage = "Last name cannot exceed 50 characters")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required")]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be between 8 and 100 characters")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirm password is required")]
    [Compare("Password", ErrorMessage = "Passwords do not match")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

/// <summary>
/// DTO for login request
/// 
/// MINIMAL DESIGN:
/// Only includes email and password (what's needed for login)
/// Does NOT include FirstName, LastName (not needed for login)
/// </summary>
public class LoginRequestDto
{
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required")]
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// DTO for authentication response (after successful login)
/// 
/// SECURITY CONSIDERATION:
/// - Returns JWT token (NOT password)
/// - Returns user info (for client to display)
/// - Client stores token in localStorage/cookies
/// - Token sent with every request in Authorization header
/// 
/// TOKEN USAGE:
/// Client sends: Authorization: Bearer {token}
/// Server validates token and extracts user info from JWT claims
/// </summary>
public class LoginResponseDto
{
    public int UserId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// JWT Token to be used for subsequent requests
    /// Format: Authorization: Bearer {Token}
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Token expiration time (in seconds from now)
    /// Client can use this to refresh before expiry
    /// </summary>
    public int ExpiresIn { get; set; }
}

/// <summary>
/// DTO for user information response
/// Used in GET /api/profile and other user endpoints
/// 
/// DIFFERENCE FROM LOGIN RESPONSE:
/// - Login: Returns token (one-time)
/// - Profile: Returns user info (reusable)
/// </summary>
public class UserProfileDto
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// DTO for creating/updating user (Admin only)
/// </summary>
public class UserDto
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// DTO for creating a new user (SuperAdmin only)
/// </summary>
public class CreateUserRequestDto
{
    [Required(ErrorMessage = "First name is required")]
    [StringLength(50)]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Last name is required")]
    [StringLength(50)]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required")]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required")]
    [StringLength(100, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Role is required")]
    public string Role { get; set; } = string.Empty;
}
