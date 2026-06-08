using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using EmployeeManagementSystem.Common;
using EmployeeManagementSystem.DTOs;
using EmployeeManagementSystem.Services.Interfaces;

namespace EmployeeManagementSystem.Controllers;

/// <summary>
/// Profile controller
/// 
/// ENDPOINTS:
/// GET /api/profile - Get current user's profile
/// 
/// AUTHORIZATION:
/// [Authorize] - Any authenticated user can access
/// 
/// HOW TO GET CURRENT USER:
/// 1. User sends request with JWT token in Authorization header
/// 2. ASP.NET validates token
/// 3. Extracts claims from token
/// 4. User.FindFirst(ClaimTypes.NameIdentifier) gets UserId
/// 5. Can use userId to fetch user details
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]  // All endpoints require authentication
public class ProfileController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ILogger<ProfileController> _logger;

    public ProfileController(IUserService userService, ILogger<ProfileController> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    /// <summary>
    /// Get current user's profile
    /// 
    /// ENDPOINT:
    /// GET /api/profile
    /// 
    /// AUTHENTICATION:
    /// Authorization: Bearer {JWT Token}
    /// 
    /// HOW IT WORKS:
    /// 1. Client sends request with JWT token
    /// 2. [Authorize] middleware validates token
    /// 3. Extracts claims from token
    /// 4. Populates User.Claims with token claims
    /// 5. Controller can access User.FindFirst(ClaimTypes.NameIdentifier)
    /// 6. Gets UserId from token
    /// 7. Fetches user from database
    /// 8. Returns profile data
    /// 
    /// CLAIMS IN JWT:
    /// The JWT contains these claims:
    /// - NameIdentifier: User ID
    /// - Email: User's email
    /// - Name: User's full name
    /// - Role: User's role
    /// 
    /// RESPONSE:
    /// {
    ///   "status": "Success",
    ///   "code": 200,
    ///   "message": "Operation completed successfully",
    ///   "data": {
    ///     "id": 1,
    ///     "firstName": "John",
    ///     "lastName": "Doe",
    ///     "email": "john@example.com",
    ///     "role": "Admin",
    ///     "createdAt": "2024-01-15T10:30:00Z",
    ///     "updatedAt": "2024-01-15T10:30:00Z"
    ///   }
    /// }
    /// 
    /// ERROR RESPONSES:
    /// 401 Unauthorized: No token or invalid token
    /// 404 Not Found: User not found (shouldn't happen normally)
    /// 500 Internal Server Error: Server error
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<UserProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<UserProfileDto>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<UserProfileDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProfile()
    {
        try
        {
            // Step 1: Extract UserId from JWT token claims
            // User property is populated by [Authorize] middleware
            // It contains claims extracted from the token
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);

            // Step 2: Validate UserId exists in claims
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return Unauthorized(
                    ApiResponse<UserProfileDto>.Fail(401, "Invalid token")
                );
            }

            // Step 3: Fetch user from database
            var result = await _userService.GetCurrentUserProfileAsync(userId);

            // Step 4: Return response
            if (result.Code == 200)
                return Ok(result);
            else if (result.Code == 404)
                return NotFound(result);
            else
                return BadRequest(result);
        }
        catch (Exception ex)
        {
            _logger.LogError($"GetProfile error: {ex.Message}");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                ApiResponse<UserProfileDto>.Fail(500, "Failed to get profile")
            );
        }
    }
}
