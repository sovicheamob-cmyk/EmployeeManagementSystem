using EmployeeManagementSystem.Common;
using EmployeeManagementSystem.Common.Constants;
using EmployeeManagementSystem.DTOs;
using EmployeeManagementSystem.Models;
using EmployeeManagementSystem.Repositories.Interfaces;
using EmployeeManagementSystem.Services.Interfaces;

namespace EmployeeManagementSystem.Services.Implementations;

/// <summary>
/// User service implementation
/// 
/// USER MANAGEMENT (SuperAdmin only)
/// - Create users
/// - Update users
/// - Delete users (soft delete)
/// - View users by role
/// 
/// DIFFERS FROM AUTH SERVICE:
/// Auth: Register (self), Login
/// User: Admin CRUD (SuperAdmin)
/// </summary>
public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IAuthService _authService;

    public UserService(IUserRepository userRepository, IAuthService authService)
    {
        _userRepository = userRepository;
        _authService = authService;
    }

    /// <summary>
    /// Get all users
    /// </summary>
    public async Task<ApiResponse<List<UserDto>>> GetAllUsersAsync()
    {
        try
        {
            var users = await _userRepository.GetAllAsync();
            var dtos = users.Select(MapToDto).ToList();

            return ApiResponse<List<UserDto>>.Success(dtos, MessageConstants.SUCCESS);
        }
        catch (Exception)
        {
            return ApiResponse<List<UserDto>>.Fail(500, MessageConstants.FAILED);
        }
    }

    /// <summary>
    /// Get user by ID
    /// </summary>
    public async Task<ApiResponse<UserDto>> GetUserByIdAsync(int id)
    {
        try
        {
            var user = await _userRepository.GetByIdAsync(id);

            if (user == null)
            {
                return ApiResponse<UserDto>.Fail(404, MessageConstants.USER_NOT_FOUND);
            }

            var dto = MapToDto(user);
            return ApiResponse<UserDto>.Success(dto, MessageConstants.SUCCESS);
        }
        catch (Exception)
        {
            return ApiResponse<UserDto>.Fail(500, MessageConstants.FAILED);
        }
    }

    /// <summary>
    /// Create new user (SuperAdmin only)
    /// 
    /// DIFFERENCE FROM REGISTER:
    /// Register: User creates own account
    /// CreateUser: Admin creates account for others
    /// </summary>
    public async Task<ApiResponse<UserDto>> CreateUserAsync(CreateUserRequestDto request)
    {
        try
        {
            // Validate email unique
            bool emailExists = await _userRepository.EmailExistsAsync(request.Email);
            if (emailExists)
            {
                return ApiResponse<UserDto>.Fail(
                    400,
                    MessageConstants.EMAIL_ALREADY_EXISTS
                );
            }

            // Parse role
            if (!Enum.TryParse<UserRole>(request.Role, out var role))
            {
                return ApiResponse<UserDto>.Fail(400, "Invalid role");
            }

            // Hash password
            string passwordHash = _authService.HashPassword(request.Password);

            // Create user
            var user = new User
            {
                FirstName = request.FirstName,
                LastName = request.LastName,
                Email = request.Email,
                PasswordHash = passwordHash,
                Role = role,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _userRepository.AddAsync(user);

            var dto = MapToDto(user);
            return ApiResponse<UserDto>.Success(dto, MessageConstants.USER_CREATED);
        }
        catch (Exception)
        {
            return ApiResponse<UserDto>.Fail(500, MessageConstants.FAILED);
        }
    }

    /// <summary>
    /// Update user (SuperAdmin only)
    /// </summary>
    public async Task<ApiResponse<UserDto>> UpdateUserAsync(int id, CreateUserRequestDto request)
    {
        try
        {
            var user = await _userRepository.GetByIdAsync(id);
            if (user == null)
            {
                return ApiResponse<UserDto>.Fail(404, MessageConstants.USER_NOT_FOUND);
            }

            // Parse role
            if (!Enum.TryParse<UserRole>(request.Role, out var role))
            {
                return ApiResponse<UserDto>.Fail(400, "Invalid role");
            }

            // Update
            user.FirstName = request.FirstName;
            user.LastName = request.LastName;
            user.Email = request.Email;
            user.PasswordHash = _authService.HashPassword(request.Password);
            user.Role = role;
            user.UpdatedAt = DateTime.UtcNow;

            await _userRepository.UpdateAsync(user);

            var dto = MapToDto(user);
            return ApiResponse<UserDto>.Success(dto, MessageConstants.USER_UPDATED);
        }
        catch (Exception)
        {
            return ApiResponse<UserDto>.Fail(500, MessageConstants.FAILED);
        }
    }

    /// <summary>
    /// Delete user
    /// 
    /// NOTE: For security, we might want to keep user records
    /// This could be a soft delete or permanent delete
    /// For now, we'll do permanent delete
    /// </summary>
    public async Task<ApiResponse<string>> DeleteUserAsync(int id)
    {
        try
        {
            var user = await _userRepository.GetByIdAsync(id);
            if (user == null)
            {
                return ApiResponse<string>.Fail(404, MessageConstants.USER_NOT_FOUND);
            }

            await _userRepository.DeleteAsync(user);

            return ApiResponse<string>.Success(id.ToString(), MessageConstants.USER_DELETED);
        }
        catch (Exception)
        {
            return ApiResponse<string>.Fail(500, MessageConstants.FAILED);
        }
    }

    /// <summary>
    /// Get users by role
    /// </summary>
    public async Task<ApiResponse<List<UserDto>>> GetUsersByRoleAsync(string role)
    {
        try
        {
            if (!Enum.TryParse<UserRole>(role, out var userRole))
            {
                return ApiResponse<List<UserDto>>.Fail(400, "Invalid role");
            }

            var users = await _userRepository.GetByRoleAsync(userRole);
            var dtos = users.Select(MapToDto).ToList();

            return ApiResponse<List<UserDto>>.Success(dtos, MessageConstants.SUCCESS);
        }
        catch (Exception)
        {
            return ApiResponse<List<UserDto>>.Fail(500, MessageConstants.FAILED);
        }
    }

    /// <summary>
    /// Get current user profile
    /// 
    /// USAGE:
    /// When user requests their own profile
    /// Can be any authenticated user
    /// 
    /// USER FLOW:
    /// 1. User sends request with JWT token
    /// 2. Server extracts UserId from token claims
    /// 3. Calls this method with UserId
    /// 4. Returns user profile
    /// </summary>
    public async Task<ApiResponse<UserProfileDto>> GetCurrentUserProfileAsync(int userId)
    {
        try
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                return ApiResponse<UserProfileDto>.Fail(404, MessageConstants.USER_NOT_FOUND);
            }

            var profile = new UserProfileDto
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                Role = user.Role.ToString(),
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt
            };

            return ApiResponse<UserProfileDto>.Success(profile, MessageConstants.SUCCESS);
        }
        catch (Exception)
        {
            return ApiResponse<UserProfileDto>.Fail(500, MessageConstants.FAILED);
        }
    }

    // ========== HELPER METHODS ==========

    /// <summary>
    /// Convert User Model to UserDto
    /// </summary>
    private static UserDto MapToDto(User user)
    {
        return new UserDto
        {
            Id = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            Role = user.Role.ToString(),
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };
    }
}
