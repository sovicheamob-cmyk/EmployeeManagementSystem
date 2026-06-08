namespace EmployeeManagementSystem.Models;

/// <summary>
/// Employee model represents company employees
/// 
/// WHY THIS STRUCTURE?
/// - Stores employee information and employment details
/// - Separate from User (User = system access, Employee = payroll/HR)
/// - Tracks employment history with timestamps
/// - Can be queried by department, status, salary range, etc.
/// 
/// DATABASE MAPPING:
/// This class maps to the "Employees" table in PostgreSQL
/// 
/// BUSINESS LOGIC:
/// - One employee per employee code (unique constraint)
/// - Can be marked inactive instead of deleted (soft delete)
/// - JoinDate tracks when employee started
/// - Salary is decimal for financial accuracy
/// </summary>
public class Employee
{
    /// <summary>
    /// Primary key - unique identifier
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Unique employee code (e.g., "EMP001", "EMP002")
    /// Used for quick employee lookup
    /// </summary>
    public string EmployeeCode { get; set; } = string.Empty;

    /// <summary>
    /// Employee's first name
    /// </summary>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>
    /// Employee's last name
    /// </summary>
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// Employee's email address
    /// Should be unique in most organizations
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Department employee works in
    /// Examples: "IT", "HR", "Finance", "Sales"
    /// </summary>
    public string Department { get; set; } = string.Empty;

    /// <summary>
    /// Employee's position/job title
    /// Examples: "Senior Developer", "Manager", "Analyst"
    /// </summary>
    public string Position { get; set; } = string.Empty;

    /// <summary>
    /// Employee's salary
    /// Uses decimal for precision with money calculations
    /// Examples: 50000.00, 75000.50
    /// </summary>
    public decimal Salary { get; set; }

    /// <summary>
    /// Date employee joined the company
    /// Used for tenure calculations
    /// </summary>
    public DateTime JoinDate { get; set; }

    /// <summary>
    /// Indicates if employee is still active
    /// 
    /// SOFT DELETE PATTERN:
    /// Instead of deleting records (which loses data):
    /// - Set IsActive = false
    /// - Keeps historical data intact
    /// - Can reactivate employees if needed
    /// - Better for audit trails
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Timestamp when employee record was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp when employee record was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
