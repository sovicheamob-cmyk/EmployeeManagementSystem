using Microsoft.EntityFrameworkCore;
using EmployeeManagementSystem.Data;
using EmployeeManagementSystem.Models;
using EmployeeManagementSystem.Repositories.Interfaces;

namespace EmployeeManagementSystem.Repositories.Implementations;

/// <summary>
/// Employee repository implementation
/// 
/// PATTERN:
/// EmployeeRepository : GenericRepository<Employee> : IEmployeeRepository
/// 
/// Inherits all base CRUD operations from GenericRepository
/// Adds Employee-specific business logic
/// </summary>
public class EmployeeRepository : GenericRepository<Employee>, IEmployeeRepository
{
    public EmployeeRepository(ApplicationDbContext context) : base(context)
    {
    }

    /// <summary>
    /// Find employee by employee code
    /// </summary>
    public async Task<Employee?> GetByEmployeeCodeAsync(string employeeCode)
    {
        return await _dbSet
            .FirstOrDefaultAsync(e => e.EmployeeCode.ToLower() == employeeCode.ToLower());
    }

    /// <summary>
    /// Check if employee code already exists
    /// </summary>
    public async Task<bool> EmployeeCodeExistsAsync(string employeeCode)
    {
        return await _dbSet
            .AnyAsync(e => e.EmployeeCode.ToLower() == employeeCode.ToLower());
    }

    /// <summary>
    /// Get employees by department
    /// 
    /// USAGE:
    /// var itStaff = await empRepo.GetByDepartmentAsync("IT");
    /// 
    /// LINQ QUERY:
    /// SELECT * FROM Employees WHERE Department = @department
    /// </summary>
    public async Task<IEnumerable<Employee>> GetByDepartmentAsync(string department)
    {
        return await _dbSet
            .Where(e => e.Department.ToLower() == department.ToLower())
            .ToListAsync();
    }

    /// <summary>
    /// Get only active employees
    /// 
    /// PATTERN: Soft Delete
    /// Employees marked as IsActive=false are not deleted
    /// They're kept in database for history/audit
    /// But excluded from active queries
    /// </summary>
    public async Task<IEnumerable<Employee>> GetActiveAsync()
    {
        return await _dbSet
            .Where(e => e.IsActive)
            .ToListAsync();
    }

    /// <summary>
    /// Get employees by salary range
    /// 
    /// USAGE:
    /// High earners (100k-200k):
    /// var highEarners = await empRepo.GetBySalaryRangeAsync(100000, 200000);
    /// 
    /// LINQ QUERY:
    /// SELECT * FROM Employees WHERE Salary BETWEEN @min AND @max
    /// 
    /// WHY DECIMAL?
    /// Salary has decimal places (e.g., 50000.50)
    /// Using decimal prevents rounding errors
    /// </summary>
    public async Task<IEnumerable<Employee>> GetBySalaryRangeAsync(decimal minSalary, decimal maxSalary)
    {
        return await _dbSet
            .Where(e => e.Salary >= minSalary && e.Salary <= maxSalary)
            .ToListAsync();
    }

    /// <summary>
    /// Get employees who joined after a certain date
    /// 
    /// USAGE:
    /// Recent hires (joined after Jan 1, 2024):
    /// var newHires = await empRepo.GetByJoinDateAsync(new DateTime(2024, 1, 1));
    /// 
    /// LINQ QUERY:
    /// SELECT * FROM Employees WHERE JoinDate >= @fromDate
    /// 
    /// SORTING:
    /// OrderByDescending sorts newest first
    /// So most recent hires appear first
    /// </summary>
    public async Task<IEnumerable<Employee>> GetByJoinDateAsync(DateTime fromDate)
    {
        return await _dbSet
            .Where(e => e.JoinDate >= fromDate)
            .OrderByDescending(e => e.JoinDate)  // Newest first
            .ToListAsync();
    }

    /// <summary>
    /// Get total active employee count (headcount)
    /// 
    /// USAGE:
    /// int totalStaff = await empRepo.GetHeadcountAsync();
    /// 
    /// LINQ QUERY:
    /// SELECT COUNT(*) FROM Employees WHERE IsActive = true
    /// 
    /// WHY SEPARATE METHOD?
    /// - More readable than CountAsync(e => e.IsActive)
    /// - Common business requirement
    /// - Easy to understand intent
    /// </summary>
    public async Task<int> GetHeadcountAsync()
    {
        return await _dbSet
            .Where(e => e.IsActive)
            .CountAsync();
    }
}
