using EmployeeManagementSystem.DTOs;
using EmployeeManagementSystem.Common;

namespace EmployeeManagementSystem.Services.Interfaces;

/// <summary>
/// Employee service interface
/// 
/// SERVICE LAYER RESPONSIBILITY:
/// - Business logic (not in repository or controller)
/// - Convert DTOs ↔ Models
/// - Validate business rules
/// - Orchestrate repository calls
/// - Handle transactions
/// 
/// WHY SERVICE LAYER?
/// Controllers shouldn't have business logic
/// Keep controllers thin (just HTTP handling)
/// Services handle domain logic
/// 
/// DEPENDENCY INJECTION:
/// Controllers receive IEmployeeService
/// IEmployeeService depends on IEmployeeRepository
/// Repository depends on DbContext
/// 
/// CHAIN:
/// Controller → Service → Repository → DbContext → Database
/// </summary>
public interface IEmployeeService
{
    /// <summary>
    /// Get all employees (paginated)
    /// 
    /// PAGINATION:
    /// - pageNumber: Which page (1, 2, 3...)
    /// - pageSize: How many per page (10, 20...)
    /// 
    /// USAGE:
    /// GET /api/employees?pageNumber=1&pageSize=10
    /// Returns first 10 employees
    /// </summary>
    Task<ApiResponse<PaginatedEmployeeResponseDto>> GetAllEmployeesAsync(int pageNumber, int pageSize);

    /// <summary>
    /// Get employee by ID
    /// </summary>
    Task<ApiResponse<EmployeeDto>> GetEmployeeByIdAsync(int id);

    /// <summary>
    /// Create new employee
    /// 
    /// VALIDATION:
    /// - Employee code unique
    /// - All required fields present
    /// - Salary > 0
    /// - Join date not in future
    /// </summary>
    Task<ApiResponse<EmployeeDto>> CreateEmployeeAsync(CreateEmployeeRequestDto request);

    /// <summary>
    /// Update existing employee
    /// </summary>
    Task<ApiResponse<EmployeeDto>> UpdateEmployeeAsync(int id, UpdateEmployeeRequestDto request);

    /// <summary>
    /// Delete employee (soft delete - mark as inactive)
    /// </summary>
    Task<ApiResponse<string>> DeleteEmployeeAsync(int id);

    /// <summary>
    /// Get employees by department
    /// </summary>
    Task<ApiResponse<List<EmployeeDto>>> GetEmployeesByDepartmentAsync(string department);

    /// <summary>
    /// Get active employee count (headcount)
    /// </summary>
    Task<ApiResponse<int>> GetHeadcountAsync();
}
