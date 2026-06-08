using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using EmployeeManagementSystem.Common;
using EmployeeManagementSystem.DTOs;
using EmployeeManagementSystem.Services.Interfaces;

namespace EmployeeManagementSystem.Controllers;

/// <summary>
/// Authentication controller
/// 
/// ENDPOINTS:
/// POST /api/auth/register  - Register new user
/// POST /api/auth/login     - Login user
/// 
/// NO [Authorize] - These endpoints don't require authentication
/// (because user isn't logged in yet)
/// 
/// DEPENDENCY INJECTION:
/// IAuthService - handles auth logic
/// 
/// REQUEST FLOW:
/// 1. Client sends HTTP request
/// 2. ASP.NET deserializes JSON to DTO
/// 3. Validation attributes check data
/// 4. If invalid: 400 Bad Request
/// 5. If valid: Call service method
/// 6. Service returns ApiResponse
/// 7. Controller returns HTTP response
/// 8. Client receives JSON response
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    /// <summary>
    /// Constructor - receives dependencies
    /// 
    /// IAuthService: Handles authentication logic
    /// ILogger: For logging errors/info
    /// 
    /// DEPENDENCY INJECTION:
    /// ASP.NET Core automatically provides these
    /// Configured in Program.cs:
    /// services.AddScoped<IAuthService, AuthService>();
    /// services.AddLogging();
    /// </summary>
    public AuthController(IAuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    /// <summary>
    /// Register new user
    /// 
    /// ENDPOINT:
    /// POST /api/auth/register
    /// 
    /// REQUEST:
    /// {
    ///   "firstName": "John",
    ///   "lastName": "Doe",
    ///   "email": "john@example.com",
    ///   "password": "SecurePass123",
    ///   "confirmPassword": "SecurePass123"
    /// }
    /// 
    /// RESPONSE (Success):
    /// {
    ///   "status": "Success",
    ///   "code": 200,
    ///   "message": "User registered successfully",
    ///   "data": {
    ///     "id": 1,
    ///     "firstName": "John",
    ///     "lastName": "Doe",
    ///     "email": "john@example.com",
    ///     "role": "User",
    ///     "createdAt": "2024-01-15T10:30:00Z"
    ///   }
    /// }
    /// 
    /// RESPONSE (Failure - email exists):
    /// {
    ///   "status": "Failed",
    ///   "code": 400,
    ///   "message": "Email already exists",
    ///   "data": null
    /// }
    /// 
    /// VALIDATION:
    /// - [Required]: All fields required
    /// - [EmailAddress]: Valid email format
    /// - [StringLength(100, MinimumLength = 8)]: Password 8-100 chars
    /// - [Compare("Password")]: Confirm password matches
    /// 
    /// If validation fails:
    /// - ASP.NET returns 400 automatically
    /// - Includes validation error details
    /// - Service method not called
    /// 
    /// IF VALIDATION PASSES:
    /// 1. Call _authService.RegisterAsync(registerDto)
    /// 2. Service validates business rules
    /// 3. Service hashes password
    /// 4. Service creates user in database
    /// 5. Returns ApiResponse
    /// 6. Controller returns HTTP response
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<UserProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<UserProfileDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterRequestDto request)
    {
        try
        {
            // Call service to handle registration
            var result = await _authService.RegisterAsync(request);

            // Return response based on result
            // If success (code 200): return Ok
            // If failure: return BadRequest
            if (result.Code == 200)
                return Ok(result);
            else
                return BadRequest(result);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Register error: {ex.Message}");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                ApiResponse<UserProfileDto>.Fail(500, "Registration failed")
            );
        }
    }

    /// <summary>
    /// Login user
    /// 
    /// ENDPOINT:
    /// POST /api/auth/login
    /// 
    /// REQUEST:
    /// {
    ///   "email": "john@example.com",
    ///   "password": "SecurePass123"
    /// }
    /// 
    /// RESPONSE (Success):
    /// {
    ///   "status": "Success",
    ///   "code": 200,
    ///   "message": "Login successful",
    ///   "data": {
    ///     "userId": 1,
    ///     "firstName": "John",
    ///     "lastName": "Doe",
    ///     "email": "john@example.com",
    ///     "role": "User",
    ///     "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    ///     "expiresIn": 3600
    ///   }
    /// }
    /// 
    /// RESPONSE (Failure - invalid credentials):
    /// {
    ///   "status": "Failed",
    ///   "code": 401,
    ///   "message": "Invalid email or password",
    ///   "data": null
    /// }
    /// 
    /// CLIENT USAGE:
    /// 1. Store token from response
    /// 2. Send token with future requests: Authorization: Bearer {token}
    /// 3. Server validates token for protected endpoints
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<LoginResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<LoginResponseDto>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
    {
        try
        {
            // Call service to handle login
            var result = await _authService.LoginAsync(request);

            // Return response
            if (result.Code == 200)
                return Ok(result);
            else
                return Unauthorized(result);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Login error: {ex.Message}");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                ApiResponse<LoginResponseDto>.Fail(500, "Login failed")
            );
        }
    }
}
