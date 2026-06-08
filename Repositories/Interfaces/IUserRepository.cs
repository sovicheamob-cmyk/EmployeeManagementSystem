using EmployeeManagementSystem.Models;

namespace EmployeeManagementSystem.Repositories.Interfaces;

/// <summary>
/// User-specific repository interface
/// 
/// WHY SPECIFIC REPOSITORY?
/// - IGenericRepository provides basic CRUD
/// - User repository adds User-specific methods
/// - Example: FindByEmailAsync (specific to User)
/// 
/// INHERITANCE:
/// UserRepository inherits from GenericRepository<User>
/// So it has all basic CRUD + custom methods
/// 
/// DEPENDENCY INJECTION:
/// Service receives IUserRepository
/// DI creates UserRepository instance
/// Can mock IUserRepository in unit tests
/// </summary>
public interface IUserRepository : IGenericRepository<User>
{
    /// <summary>
    /// Find user by email
    /// 
    /// USAGE:
    /// var user = await userRepo.GetByEmailAsync("john@example.com");
    /// 
    /// WHEN USED:
    /// - Login: Find user by email, verify password
    /// - Email validation: Check if email already exists
    /// 
    /// RETURNS: User if found, null if not found
    /// </summary>
    Task<User?> GetByEmailAsync(string email);

    /// <summary>
    /// Check if email already exists
    /// 
    /// USAGE:
    /// bool exists = await userRepo.EmailExistsAsync("john@example.com");
    /// 
    /// VALIDATION USE CASE:
    /// During registration/update, check if email is taken
    /// </summary>
    Task<bool> EmailExistsAsync(string email);

    /// <summary>
    /// Get users by role
    /// 
    /// USAGE:
    /// var admins = await userRepo.GetByRoleAsync(UserRole.Admin);
    /// 
    /// WHEN USED:
    /// - Admin dashboard: Show all admins
    /// - Reports: Count users by role
    /// </summary>
    Task<IEnumerable<User>> GetByRoleAsync(UserRole role);
}
