using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EmployeeManagementSystem.Common;
using EmployeeManagementSystem.DTOs;
using EmployeeManagementSystem.Services.Interfaces;
using EmployeeManagementSystem.Common.Constants;

namespace EmployeeManagementSystem.Controllers;

/// <summary>
/// Employee controller
/// 
/// ENDPOINTS:
/// GET    /api/employees              - Get all employees (paginated)
/// GET    /api/employees/{id}         - Get employee by ID
/// POST   /api/employees              - Create employee
/// PUT    /api/employees/{id}         - Update employee
/// DELETE /api/employees/{id}         - Delete employee
/// GET    /api/employees/department/{dept} - Get by department
/// GET    /api/employees/headcount    - Get total headcount
/// 
/// AUTHORIZATION:
/// - [Authorize]: Any authenticated user can READ
/// - [Authorize(Roles = "Admin,SuperAdmin")]: Only Admin/SuperAdmin can WRITE
/// 
/// WHY SEPARATE AUTHORIZATION FOR READ vs WRITE?
/// - Users should see employee list
/// - But only admins can modify
/// - More granular control
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]  // All endpoints require authentication
public class EmployeesController : ControllerBase
{
    private readonly IEmployeeService _employeeService;
    private readonly ILogger<EmployeesController> _logger;

    public EmployeesController(IEmployeeService employeeService, ILogger<EmployeesController> logger)
    {
        _employeeService = employeeService;
        _logger = logger;
    }

    /// <summary>
    /// Get all employees (paginated)
    /// 
    /// ENDPOINT:
    /// GET /api/employees?pageNumber=1&pageSize=10
    /// 
    /// AUTHORIZATION:
    /// [Authorize] - Any authenticated user
    /// 
    /// RESPONSE:
    /// {
    ///   "status": "Success",
    ///   "code": 200,
    ///   "message": "Operation completed successfully",
    ///   "data": [
    ///     {
    ///       "id": 1,
    ///       "employeeCode": "EMP001",
    ///       "firstName": "John",
    ///       "lastName": "Doe",
    ///       "email": "john@company.com",
    ///       "department": "IT",
    ///       "position": "Senior Developer",
    ///       "salary": 100000,
    ///       "joinDate": "2022-01-15",
    ///       "isActive": true,
    ///       "fullName": "John Doe",
    ///       "yearsOfEmployment": 2
    ///     }
    ///   ],
    ///   "pageNumber": 1,
    ///   "pageSize": 10,
    ///   "totalCount": 45,
    ///   "totalPages": 5,
    ///   "hasNextPage": true,
    ///   "hasPreviousPage": false
    /// }
    /// 
    /// PAGINATION PARAMETERS:
    /// - pageNumber: Which page (default 1)
    /// - pageSize: Results per page (default 10)
    /// 
    /// WHY PAGINATION?
    /// - Performance: Returns only requested page
    /// - Memory: Doesn't load 10,000 employees at once
    /// - UX: Users see results immediately
    /// - Bandwidth: Smaller response size
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<EmployeeDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
    {
        try
        {
            // Validate pagination parameters
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 100) pageSize = 100;  // Max 100 per page

            var result = await _employeeService.GetAllEmployeesAsync(pageNumber, pageSize);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError($"GetAll error: {ex.Message}");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                ApiResponse<List<EmployeeDto>>.Fail(500, MessageConstants.SERVER_ERROR)
            );
        }
    }

    /// <summary>
    /// Get employee by ID
    /// 
    /// ENDPOINT:
    /// GET /api/employees/5
    /// 
    /// RESPONSE (Success):
    /// {
    ///   "status": "Success",
    ///   "code": 200,
    ///   "message": "Operation completed successfully",
    ///   "data": { employee object }
    /// }
    /// 
    /// RESPONSE (Not Found):
    /// {
    ///   "status": "Failed",
    ///   "code": 404,
    ///   "message": "Employee not found",
    ///   "data": null
    /// }
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<EmployeeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<EmployeeDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        try
        {
            var result = await _employeeService.GetEmployeeByIdAsync(id);

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
                ApiResponse<EmployeeDto>.Fail(500, MessageConstants.SERVER_ERROR)  
            );
        }
    }

    /// <summary>
    /// Create new employee
    /// 
    /// ENDPOINT:
    /// POST /api/employees
    /// 
    /// AUTHORIZATION:
    /// [Authorize(Roles = "Admin,SuperAdmin")]
    /// Only Admin and SuperAdmin can create employees
    /// 
    /// REQUEST:
    /// {
    ///   "employeeCode": "EMP123",
    ///   "firstName": "Jane",
    ///   "lastName": "Smith",
    ///   "email": "jane@company.com",
    ///   "department": "HR",
    ///   "position": "HR Manager",
    ///   "salary": 75000,
    ///   "joinDate": "2024-01-15"
    /// }
    /// 
    /// VALIDATION:
    /// - All fields required [Required]
    /// - Email must be valid [EmailAddress]
    /// - Salary > 0 [Range]
    /// 
    /// SERVER-SIDE VALIDATION:
    /// - Employee code must be unique
    /// - Join date not in future
    /// 
    /// RESPONSE (Success):
    /// {
    ///   "status": "Success",
    ///   "code": 200,
    ///   "message": "Employee created successfully",
    ///   "data": { created employee }
    /// }
    /// 
    /// HTTP STATUS:
    /// - 201 Created: Employee created
    /// - 400 Bad Request: Validation failed
    /// - 401 Unauthorized: Not authenticated
    /// - 403 Forbidden: Not admin
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin,SuperAdmin")]
    [ProducesResponseType(typeof(ApiResponse<EmployeeDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<EmployeeDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<EmployeeDto>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create([FromBody] CreateEmployeeRequestDto request)
    {
        try
        {
            var result = await _employeeService.CreateEmployeeAsync(request);

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
                ApiResponse<EmployeeDto>.Fail(500, MessageConstants.SERVER_ERROR)
            );
        }
    }

    /// <summary>
    /// Update employee
    /// 
    /// ENDPOINT:
    /// PUT /api/employees/5
    /// 
    /// AUTHORIZATION:
    /// [Authorize(Roles = "Admin,SuperAdmin")]
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    [ProducesResponseType(typeof(ApiResponse<EmployeeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<EmployeeDto>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<EmployeeDto>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateEmployeeRequestDto request)
    {
        try
        {
            var result = await _employeeService.UpdateEmployeeAsync(id, request);

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
                ApiResponse<EmployeeDto>.Fail(500, MessageConstants.SERVER_ERROR)
            );
        }
    }

    /// <summary>
    /// Delete employee (soft delete)
    /// 
    /// ENDPOINT:
    /// DELETE /api/employees/5
    /// 
    /// AUTHORIZATION:
    /// [Authorize(Roles = "Admin,SuperAdmin")]
    /// 
    /// RESPONSE:
    /// {
    ///   "status": "Success",
    ///   "code": 200,
    ///   "message": "Employee deleted successfully",
    ///   "data": "5"
    /// }
    /// 
    /// Note: Returns deleted employee ID as confirmation
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var result = await _employeeService.DeleteEmployeeAsync(id);

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
                ApiResponse<string>.Fail(500, MessageConstants.SERVER_ERROR)
            );
        }
    }

    /// <summary>
    /// Get employees by department
    /// 
    /// ENDPOINT:
    /// GET /api/employees/department/IT
    /// </summary>
    [HttpGet("department/{department}")]
    [ProducesResponseType(typeof(ApiResponse<List<EmployeeDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByDepartment(string department)
    {
        try
        {
            var result = await _employeeService.GetEmployeesByDepartmentAsync(department);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError($"GetByDepartment error: {ex.Message}");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                ApiResponse<List<EmployeeDto>>.Fail(500, MessageConstants.SERVER_ERROR)
            );
        }
    }

    /// <summary>
    /// Get total headcount
    /// 
    /// ENDPOINT:
    /// GET /api/employees/headcount
    /// 
    /// RESPONSE:
    /// {
    ///   "status": "Success",
    ///   "code": 200,
    ///   "message": "Operation completed successfully",
    ///   "data": 45
    /// }
    /// </summary>
    [HttpGet("stats/headcount")]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHeadcount()
    {
        try
        {
            var result = await _employeeService.GetHeadcountAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError($"GetHeadcount error: {ex.Message}");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                ApiResponse<int>.Fail(500, MessageConstants.SERVER_ERROR)
            );
        }
    }
}
