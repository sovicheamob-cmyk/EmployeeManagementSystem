using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EmployeeManagementSystem.Common;
using EmployeeManagementSystem.DTOs;
using EmployeeManagementSystem.Services.Interfaces;

namespace EmployeeManagementSystem.Controllers;

/// <summary>
/// User management controller
/// 
/// ENDPOINTS:
/// GET    /api/users              - Get all users
/// GET    /api/users/{id}         - Get user by ID
/// POST   /api/users              - Create user
/// PUT    /api/users/{id}         - Update user
/// DELETE /api/users/{id}         - Delete user
/// GET    /api/users/role/{role}  - Get users by role
/// 
/// AUTHORIZATION:
/// [Authorize(Roles = "SuperAdmin")] - Only SuperAdmin
/// 
/// WHY ONLY SUPERADMIN?
/// - User management is critical operation
/// - Should be restricted to highest role
/// - Admin can manage employees but not users
/// - Only SuperAdmin can manage system users
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "SuperAdmin")]  // All endpoints require SuperAdmin
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ILogger<UsersController> _logger;

    public UsersController(IUserService userService, ILogger<UsersController> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    /// <summary>
    /// Get all users
    /// 
    /// ENDPOINT:
    /// GET /api/users
    /// 
    /// AUTHORIZATION:
    /// SuperAdmin only
    /// 
    /// RESPONSE:
    /// {
    ///   "status": "Success",
    ///   "code": 200,
    ///   "message": "Operation completed successfully",
    ///   "data": [
    ///     {
    ///       "id": 1,
    ///       "firstName": "Admin",
    ///       "lastName": "User",
    ///       "email": "admin@example.com",
    ///       "role": "SuperAdmin",
    ///       "createdAt": "2024-01-15T10:30:00Z",
    ///       "updatedAt": "2024-01-15T10:30:00Z"
    ///     }
    ///   ]
    /// }
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<UserDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<List<UserDto>>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAll()
    {
        try
        {
            var result = await _userService.GetAllUsersAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError($"GetAll error: {ex.Message}");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                ApiResponse<List<UserDto>>.Fail(500, "Failed to get users")
            );
        }
    }

    /// <summary>
    /// Get user by ID
    /// 
    /// ENDPOINT:
    /// GET /api/users/5
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetById(int id)
    {
        try
        {
            var result = await _userService.GetUserByIdAsync(id);

            if (result.Code == 200)
                return Ok(result);
            else
                return NotFound(result);
        }
        catch (Exception ex)
        {
            _logger.LogError($"GetById error: {ex.Message}");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                ApiResponse<UserDto>.Fail(500, "Failed to get user")
            );
        }
    }

    /// <summary>
    /// Create new user
    /// 
    /// ENDPOINT:
    /// POST /api/users
    /// 
    /// REQUEST:
    /// {
    ///   "firstName": "John",
    ///   "lastName": "Admin",
    ///   "email": "john@example.com",
    ///   "password": "SecurePass123",
    ///   "role": "Admin"
    /// }
    /// 
    /// AUTHORIZATION:
    /// SuperAdmin only
    /// 
    /// RESPONSE (Success):
    /// {
    ///   "status": "Success",
    ///   "code": 200,
    ///   "message": "User created successfully",
    ///   "data": { created user }
    /// }
    /// 
    /// HTTP STATUS:
    /// - 201 Created: User created
    /// - 400 Bad Request: Validation failed (email exists, etc.)
    /// - 403 Forbidden: Not SuperAdmin
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create([FromBody] CreateUserRequestDto request)
    {
        try
        {
            var result = await _userService.CreateUserAsync(request);

            if (result.Code == 200)
                return CreatedAtAction(nameof(GetById), new { id = result.Data?.Id }, result);
            else
                return BadRequest(result);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Create error: {ex.Message}");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                ApiResponse<UserDto>.Fail(500, "Failed to create user")
            );
        }
    }

    /// <summary>
    /// Update user
    /// 
    /// ENDPOINT:
    /// PUT /api/users/5
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Update(int id, [FromBody] CreateUserRequestDto request)
    {
        try
        {
            var result = await _userService.UpdateUserAsync(id, request);

            if (result.Code == 200)
                return Ok(result);
            else
                return NotFound(result);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Update error: {ex.Message}");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                ApiResponse<UserDto>.Fail(500, "Failed to update user")
            );
        }
    }

    /// <summary>
    /// Delete user
    /// 
    /// ENDPOINT:
    /// DELETE /api/users/5
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var result = await _userService.DeleteUserAsync(id);

            if (result.Code == 200)
                return Ok(result);
            else
                return NotFound(result);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Delete error: {ex.Message}");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                ApiResponse<string>.Fail(500, "Failed to delete user")
            );
        }
    }

    /// <summary>
    /// Get users by role
    /// 
    /// ENDPOINT:
    /// GET /api/users/role/Admin
    /// 
    /// ROLES:
    /// - User
    /// - Admin
    /// - SuperAdmin
    /// </summary>
    [HttpGet("role/{role}")]
    [ProducesResponseType(typeof(ApiResponse<List<UserDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<List<UserDto>>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetByRole(string role)
    {
        try
        {
            var result = await _userService.GetUsersByRoleAsync(role);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError($"GetByRole error: {ex.Message}");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                ApiResponse<List<UserDto>>.Fail(500, "Failed to get users")
            );
        }
    }
}
