using EmployeeManagementSystem.Common;
using EmployeeManagementSystem.Common.Constants;
using EmployeeManagementSystem.DTOs;
using EmployeeManagementSystem.Models;
using EmployeeManagementSystem.Repositories.Interfaces;
using EmployeeManagementSystem.Services.Interfaces;

namespace EmployeeManagementSystem.Services.Implementations;

/// <summary>
/// Employee service implementation
/// 
/// RESPONSIBILITIES:
/// 1. Business logic for employees
/// 2. DTO ↔ Model conversions
/// 3. Validation
/// 4. Coordinate repository operations
/// 5. Return ApiResponse wrapper
/// 
/// DEPENDENCY:
/// IEmployeeRepository - access employee data
/// 
/// WHY SERVICE PATTERN?
/// - Separation of Concerns
/// - Controllers handle HTTP
/// - Services handle business
/// - Repositories handle data
/// 
/// DTO → MODEL → DATABASE:
/// 1. Controller receives DTO
/// 2. Service validates DTO
/// 3. Service converts DTO to Model
/// 4. Service passes Model to Repository
/// 5. Repository persists to Database
/// 
/// DATABASE → MODEL → DTO:
/// 1. Repository fetches Model from Database
/// 2. Service converts Model to DTO
/// 3. Service wraps in ApiResponse
/// 4. Controller sends to Client
/// </summary>
public class EmployeeService : IEmployeeService
{
    private readonly IEmployeeRepository _employeeRepository;

    public EmployeeService(IEmployeeRepository employeeRepository)
    {
        _employeeRepository = employeeRepository;
    }

    /// <summary>
    /// Get all employees with pagination
    /// </summary>
    public async Task<ApiResponse<List<EmployeeDto>>> GetAllEmployeesAsync(
        int pageNumber,
        int pageSize)
    {
        try
        {
            // Get paged results from repository
            var (employees, totalCount) = await _employeeRepository.GetPagedAsync(
                pageNumber,
                pageSize,
                e => e.IsActive  // Only active employees
            );

            var employeeDtos = employees.Select(MapToDto).ToList();

            return ApiResponse<List<EmployeeDto>>.SuccessWithPagination(
                employeeDtos,
                MessageConstants.SUCCESS,
                pageNumber,
                pageSize,
                totalCount
            );
        }
        catch (Exception)
        {
            return ApiResponse<List<EmployeeDto>>.Fail(
                500,
                MessageConstants.FAILED
            );
        }
    }

    /// <summary>
    /// Get employee by ID
    /// </summary>
    public async Task<ApiResponse<EmployeeDto>> GetEmployeeByIdAsync(int id)
    {
        try
        {
            var employee = await _employeeRepository.GetByIdAsync(id);

            if (employee == null)
            {
                return ApiResponse<EmployeeDto>.Fail(
                    404,
                    MessageConstants.EMPLOYEE_NOT_FOUND
                );
            }

            var dto = MapToDto(employee);
            return ApiResponse<EmployeeDto>.Success(dto, MessageConstants.SUCCESS);
        }
        catch (Exception)
        {
            return ApiResponse<EmployeeDto>.Fail(500, MessageConstants.FAILED);
        }
    }

    /// <summary>
    /// Create new employee
    /// 
    /// BUSINESS LOGIC:
    /// 1. Validate employee code is unique
    /// 2. Validate required fields
    /// 3. Validate salary > 0
    /// 4. Create model from DTO
    /// 5. Save to database
    /// 6. Return created employee
    /// 
    /// VALIDATION HAPPENS IN:
    /// - DTO attributes (handled by ASP.NET)
    /// - This service (business rules)
    /// </summary>
    public async Task<ApiResponse<EmployeeDto>> CreateEmployeeAsync(
        CreateEmployeeRequestDto request)
    {
        try
        {
            // Business Validation: Employee code must be unique
            bool codeExists = await _employeeRepository.EmployeeCodeExistsAsync(request.EmployeeCode);
            if (codeExists)
            {
                return ApiResponse<EmployeeDto>.Fail(
                    400,
                    MessageConstants.EMPLOYEE_CODE_ALREADY_EXISTS
                );
            }

            // Create model from DTO
            var employee = new Employee
            {
                EmployeeCode = request.EmployeeCode,
                FirstName = request.FirstName,
                LastName = request.LastName,
                Email = request.Email,
                Department = request.Department,
                Position = request.Position,
                Salary = request.Salary,
                JoinDate = request.JoinDate,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Add to repository
            await _employeeRepository.AddAsync(employee);

            // NOTE: SaveChangesAsync should be called by:
            // - Unit of Work pattern, or
            // - DbContext wrapper, or
            // - Middleware after service completes
            // For now, we'll handle in Program.cs

            var dto = MapToDto(employee);
            return ApiResponse<EmployeeDto>.Success(dto, MessageConstants.EMPLOYEE_CREATED);
        }
        catch (Exception)
        {
            return ApiResponse<EmployeeDto>.Fail(500, MessageConstants.FAILED);
        }
    }

    /// <summary>
    /// Update existing employee
    /// </summary>
    public async Task<ApiResponse<EmployeeDto>> UpdateEmployeeAsync(
        int id,
        UpdateEmployeeRequestDto request)
    {
        try
        {
            // Get existing employee
            var employee = await _employeeRepository.GetByIdAsync(id);
            if (employee == null)
            {
                return ApiResponse<EmployeeDto>.Fail(
                    404,
                    MessageConstants.EMPLOYEE_NOT_FOUND
                );
            }

            // Update properties
            employee.FirstName = request.FirstName;
            employee.LastName = request.LastName;
            employee.Email = request.Email;
            employee.Department = request.Department;
            employee.Position = request.Position;
            employee.Salary = request.Salary;
            employee.IsActive = request.IsActive;
            employee.UpdatedAt = DateTime.UtcNow;

            // Update in repository
            await _employeeRepository.UpdateAsync(employee);

            var dto = MapToDto(employee);
            return ApiResponse<EmployeeDto>.Success(dto, MessageConstants.EMPLOYEE_UPDATED);
        }
        catch (Exception)
        {
            return ApiResponse<EmployeeDto>.Fail(500, MessageConstants.FAILED);
        }
    }

    /// <summary>
    /// Delete employee (soft delete)
    /// 
    /// WHY SOFT DELETE?
    /// - Marks IsActive = false
    /// - Doesn't delete from database
    /// - Keeps historical data
    /// - Can reactivate if needed
    /// - Better for audit trails
    /// </summary>
    public async Task<ApiResponse<string>> DeleteEmployeeAsync(int id)
    {
        try
        {
            var employee = await _employeeRepository.GetByIdAsync(id);
            if (employee == null)
            {
                return ApiResponse<string>.Fail(
                    404,
                    MessageConstants.EMPLOYEE_NOT_FOUND
                );
            }

            // Soft delete: mark as inactive
            employee.IsActive = false;
            employee.UpdatedAt = DateTime.UtcNow;

            await _employeeRepository.UpdateAsync(employee);

            return ApiResponse<string>.Success(
                id.ToString(),
                MessageConstants.EMPLOYEE_DELETED
            );
        }
        catch (Exception)
        {
            return ApiResponse<string>.Fail(500, MessageConstants.FAILED);
        }
    }

    /// <summary>
    /// Get employees by department
    /// </summary>
    public async Task<ApiResponse<List<EmployeeDto>>> GetEmployeesByDepartmentAsync(
        string department)
    {
        try
        {
            var employees = await _employeeRepository.GetByDepartmentAsync(department);
            var dtos = employees.Select(MapToDto).ToList();

            return ApiResponse<List<EmployeeDto>>.Success(dtos, MessageConstants.SUCCESS);
        }
        catch (Exception)
        {
            return ApiResponse<List<EmployeeDto>>.Fail(500, MessageConstants.FAILED);
        }
    }

    /// <summary>
    /// Get active employee count
    /// </summary>
    public async Task<ApiResponse<int>> GetHeadcountAsync()
    {
        try
        {
            int count = await _employeeRepository.GetHeadcountAsync();
            return ApiResponse<int>.Success(count, MessageConstants.SUCCESS);
        }
        catch (Exception)
        {
            return ApiResponse<int>.Fail(500, MessageConstants.FAILED);
        }
    }

    // ========== HELPER METHODS ==========

    /// <summary>
    /// Convert Employee Model to EmployeeDto
    /// 
    /// WHY THIS METHOD?
    /// - Centralized conversion logic
    /// - Used by all methods
    /// - Easy to update in one place
    /// - Consistent DTO creation
    /// </summary>
    private static EmployeeDto MapToDto(Employee employee)
    {
        return new EmployeeDto
        {
            Id = employee.Id,
            EmployeeCode = employee.EmployeeCode,
            FirstName = employee.FirstName,
            LastName = employee.LastName,
            Email = employee.Email,
            Department = employee.Department,
            Position = employee.Position,
            Salary = employee.Salary,
            JoinDate = employee.JoinDate,
            IsActive = employee.IsActive,
            CreatedAt = employee.CreatedAt,
            UpdatedAt = employee.UpdatedAt
        };
    }
}
