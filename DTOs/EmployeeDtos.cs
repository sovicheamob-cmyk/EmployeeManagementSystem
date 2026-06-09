using System.ComponentModel.DataAnnotations;

namespace EmployeeManagementSystem.DTOs;

/// <summary>
/// DTO for employee information response
/// Used when returning employee data to clients
/// </summary>
public class EmployeeDto
{
    public int Id { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public decimal Salary { get; set; }
    public DateTime JoinDate { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Computed property: Full name
    /// Client can use this directly (no need to concatenate)
    /// </summary>
    public string FullName => $"{FirstName} {LastName}";

    /// <summary>
    /// Computed property: Years of employment
    /// Useful for HR analytics
    /// </summary>
    public int YearsOfEmployment => (DateTime.UtcNow - JoinDate).Days / 365;
}

/// <summary>
/// DTO for creating a new employee (Admin only)
/// 
/// VALIDATION RULES:
/// - All fields required
/// - Email must be valid format
/// - Salary must be positive
/// - JoinDate must be in past
/// 
/// NOTE: We don't include Id (database auto-generates)
/// </summary>
public class CreateEmployeeRequestDto
{
    [Required(ErrorMessage = "Employee code is required")]
    [StringLength(20, ErrorMessage = "Employee code cannot exceed 20 characters")]
    public string EmployeeCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "First name is required")]
    [StringLength(50)]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Last name is required")]
    [StringLength(50)]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required")]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Department is required")]
    [StringLength(50)]
    public string Department { get; set; } = string.Empty;

    [Required(ErrorMessage = "Position is required")]
    [StringLength(50)]
    public string Position { get; set; } = string.Empty;

    [Required(ErrorMessage = "Salary is required")]
    [Range(0.01, double.MaxValue, ErrorMessage = "Salary must be greater than 0")]
    public decimal Salary { get; set; }

    [Required(ErrorMessage = "Join date is required")]
    public DateTime JoinDate { get; set; }
}

/// <summary>
/// DTO for updating existing employee
/// 
/// OPTIONAL FIELDS:
/// In real scenarios, you might want to make fields optional
/// for partial updates (PATCH). This example shows full update (PUT)
/// </summary>
public class UpdateEmployeeRequestDto
{
    [Required(ErrorMessage = "First name is required")]
    [StringLength(50)]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Last name is required")]
    [StringLength(50)]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required")]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Department is required")]
    [StringLength(50)]
    public string Department { get; set; } = string.Empty;

    [Required(ErrorMessage = "Position is required")]
    [StringLength(50)]
    public string Position { get; set; } = string.Empty;

    [Required(ErrorMessage = "Salary is required")]
    [Range(0.01, double.MaxValue, ErrorMessage = "Salary must be greater than 0")]
    public decimal Salary { get; set; }

    [Required(ErrorMessage = "Active status is required")]
    public bool IsActive { get; set; }
}
