using EmployeeManagementSystem.Models;

namespace EmployeeManagementSystem.Repositories.Interfaces;

/// <summary>
/// Employee-specific repository interface
/// 
/// Similar to IUserRepository but for employees
/// Includes Employee-specific query methods
/// </summary>
public interface IEmployeeRepository : IGenericRepository<Employee>
{
    /// <summary>
    /// Find employee by employee code
    /// 
    /// USAGE:
    /// var emp = await empRepo.GetByEmployeeCodeAsync("EMP001");
    /// 
    /// WHEN USED:
    /// - Employee lookup by code
    /// - Unique identifier for employee
    /// - Prevent duplicate codes
    /// </summary>
    Task<Employee?> GetByEmployeeCodeAsync(string employeeCode);

    /// <summary>
    /// Check if employee code already exists
    /// 
    /// USAGE:
    /// if (await empRepo.EmployeeCodeExistsAsync("EMP001"))
    ///     throw new Exception("Code already exists");
    /// </summary>
    Task<bool> EmployeeCodeExistsAsync(string employeeCode);

    /// <summary>
    /// Get employees by department
    /// 
    /// USAGE:
    /// var itEmployees = await empRepo.GetByDepartmentAsync("IT");
    /// 
    /// WHEN USED:
    /// - Department reports
    /// - Filter employees by department
    /// </summary>
    Task<IEnumerable<Employee>> GetByDepartmentAsync(string department);

    /// <summary>
    /// Get active employees only
    /// 
    /// USAGE:
    /// var activeEmps = await empRepo.GetActiveAsync();
    /// 
    /// WHEN USED:
    /// - HR dashboard: Show current employees
    /// - Payroll: Process salary for active employees
    /// - Exclude inactive/terminated employees
    /// </summary>
    Task<IEnumerable<Employee>> GetActiveAsync();

    /// <summary>
    /// Get employees by salary range
    /// 
    /// USAGE:
    /// var highEarners = await empRepo.GetBySalaryRangeAsync(100000, 200000);
    /// 
    /// WHEN USED:
    /// - Salary analytics
    /// - Compensation reports
    /// </summary>
    Task<IEnumerable<Employee>> GetBySalaryRangeAsync(decimal minSalary, decimal maxSalary);

    /// <summary>
    /// Get employees who joined after a certain date
    /// 
    /// USAGE:
    /// var newEmployees = await empRepo.GetByJoinDateAsync(new DateTime(2024, 1, 1));
    /// 
    /// WHEN USED:
    /// - New hire reports
    /// - Training programs for recent joiners
    /// </summary>
    Task<IEnumerable<Employee>> GetByJoinDateAsync(DateTime fromDate);

    /// <summary>
    /// Get total headcount (active employees)
    /// 
    /// USAGE:
    /// int headcount = await empRepo.GetHeadcountAsync();
    /// 
    /// WHEN USED:
    /// - Dashboards
    /// - Reports
    /// - Analytics
    /// </summary>
    Task<int> GetHeadcountAsync();
}
