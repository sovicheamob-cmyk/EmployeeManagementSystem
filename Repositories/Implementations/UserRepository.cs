using Microsoft.EntityFrameworkCore;
using EmployeeManagementSystem.Data;
using EmployeeManagementSystem.Models;
using EmployeeManagementSystem.Repositories.Interfaces;

namespace EmployeeManagementSystem.Repositories.Implementations;

/// <summary>
/// User repository implementation
/// 
/// INHERITANCE:
/// UserRepository : GenericRepository<User> : IUserRepository
/// 
/// CHAIN OF RESPONSIBILITY:
/// UserRepository has all base methods (CRUD) + User-specific methods
/// Derived class can call base methods via base.GetAllAsync(), etc.
/// 
/// WHY INHERIT INSTEAD OF COMPOSE?
/// - DRY principle: Don't repeat code
/// - Polymorphism: Services see IUserRepository
/// - Cleaner code: No wrapper methods needed
/// </summary>
public class UserRepository : GenericRepository<User>, IUserRepository
{
    /// <summary>
    /// Constructor
    /// 
    /// CALL BASE CONSTRUCTOR:
    /// base(context) passes DbContext to GenericRepository constructor
    /// This initializes _context and _dbSet
    /// 
    /// DEPENDENCY INJECTION:
    /// DI container passes ApplicationDbContext here
    /// Example flow:
    /// 1. Service requests IUserRepository
    /// 2. DI creates UserRepository(dbContext)
    /// 3. UserRepository calls base(dbContext)
    /// 4. GenericRepository stores dbContext
    /// </summary>
    public UserRepository(ApplicationDbContext context) : base(context)
    {
    }

    /// <summary>
    /// Find user by email
    /// 
    /// USAGE IN SERVICE:
    /// var user = await userRepo.GetByEmailAsync("john@example.com");
    /// 
    /// LINQ QUERY GENERATED:
    /// SELECT * FROM Users WHERE Email = @email
    /// 
    /// WHY CASE-INSENSITIVE?
    /// Users might enter "John@Example.COM" or "john@example.com"
    /// Both should find the same user
    /// ToLower() normalizes to lowercase before comparison
    /// 
    /// IN DATABASE:
    /// - PostgreSQL: Use LOWER() function in query
    /// - SQL Server: Use UPPER()
    /// - This is case-insensitive search
    /// </summary>
    public async Task<User?> GetByEmailAsync(string email)
    {
        return await _dbSet
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());
    }

    /// <summary>
    /// Check if email already exists
    /// 
    /// USAGE IN SERVICE:
    /// if (await userRepo.EmailExistsAsync(email))
    ///     throw new Exception("Email already exists");
    /// 
    /// WHY THIS METHOD?
    /// - More efficient than GetByEmailAsync()
    /// - Returns bool instead of full User object
    /// - Database only returns 1 or 0 (not loading user data)
    /// - Better for validation scenarios
    /// 
    /// LINQ QUERY:
    /// SELECT COUNT(*) FROM Users WHERE Email = @email (converted to bool)
    /// 
    /// RETURNS:
    /// true if email exists, false otherwise
    /// </summary>
    public async Task<bool> EmailExistsAsync(string email)
    {
        return await _dbSet
            .AnyAsync(u => u.Email.ToLower() == email.ToLower());
    }

    /// <summary>
    /// Get users by role
    /// 
    /// USAGE IN SERVICE:
    /// var admins = await userRepo.GetByRoleAsync(UserRole.Admin);
    /// var allAdmins = admins.ToList();
    /// 
    /// LINQ QUERY:
    /// SELECT * FROM Users WHERE Role = @role
    /// 
    /// WHEN USED:
    /// - Admin dashboard: Show list of admins
    /// - Reports: Count users by role
    /// - Authorization: Get all users with specific role
    /// 
    /// RETURNS:
    /// List of users with specified role
    /// Empty list if no users with that role
    /// </summary>
    public async Task<IEnumerable<User>> GetByRoleAsync(UserRole role)
    {
        return await _dbSet
            .Where(u => u.Role == role)
            .ToListAsync();
    }
}
