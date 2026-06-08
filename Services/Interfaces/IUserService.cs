using EmployeeManagementSystem.DTOs;
using EmployeeManagementSystem.Common;

namespace EmployeeManagementSystem.Services.Interfaces;

/// <summary>
/// User service interface
/// 
/// USER MANAGEMENT:
/// - Only SuperAdmin can manage users
/// - Can create, read, update, delete users
/// - Can assign roles
/// 
/// DIFFERENT FROM AUTH SERVICE:
/// - Auth: Register, Login
/// - User: Admin CRUD operations
/// </summary>
public interface IUserService
{
    /// <summary>
    /// Get all users (paginated)
    /// SuperAdmin only
    /// </summary>
    Task<ApiResponse<List<UserDto>>> GetAllUsersAsync();

    /// <summary>
    /// Get user by ID
    /// SuperAdmin only
    /// </summary>
    Task<ApiResponse<UserDto>> GetUserByIdAsync(int id);

    /// <summary>
    /// Create new user (SuperAdmin only)
    /// </summary>
    Task<ApiResponse<UserDto>> CreateUserAsync(CreateUserRequestDto request);

    /// <summary>
    /// Update user (SuperAdmin only)
    /// </summary>
    Task<ApiResponse<UserDto>> UpdateUserAsync(int id, CreateUserRequestDto request);

    /// <summary>
    /// Delete user (SuperAdmin only)
    /// </summary>
    Task<ApiResponse<string>> DeleteUserAsync(int id);

    /// <summary>
    /// Get users by role
    /// SuperAdmin only
    /// </summary>
    Task<ApiResponse<List<UserDto>>> GetUsersByRoleAsync(string role);

    /// <summary>
    /// Get current user profile
    /// Any authenticated user
    /// </summary>
    Task<ApiResponse<UserProfileDto>> GetCurrentUserProfileAsync(int userId);
}
