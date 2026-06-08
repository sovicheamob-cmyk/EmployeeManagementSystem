namespace EmployeeManagementSystem.Models;

/// <summary>
/// User model represents a system user
/// 
/// WHY THIS STRUCTURE?
/// - Stores user credentials and identity information
/// - Linked to authentication and authorization
/// - Tracks creation/update timestamps for auditing
/// 
/// DATABASE MAPPING:
/// This class maps to the "Users" table in PostgreSQL
/// Each property becomes a column in the database
/// 
/// EF CORE CONVENTIONS:
/// - Id is the primary key (by convention)
/// - DateTime properties auto-map to TIMESTAMP type
/// - String properties auto-map to VARCHAR type
/// - Enum properties store as INT by default
/// </summary>
public class User
{
    /// <summary>
    /// Primary key - unique identifier
    /// EF Core automatically treats "Id" property as the primary key
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// User's first name
    /// </summary>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>
    /// User's last name
    /// </summary>
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// User's email - MUST be unique
    /// Used for login and communication
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Hashed password using BCrypt
    /// 
    /// SECURITY BEST PRACTICE:
    /// - NEVER store plain-text passwords
    /// - Always use BCrypt (or similar) hashing
    /// - Never transmit passwords in responses
    /// - Example hash: $2a$11$N9qo8ucoExampleHashedPassword...
    /// </summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>
    /// User's role in the system
    /// Determines what operations they can perform
    /// </summary>
    public UserRole Role { get; set; } = UserRole.User;

    /// <summary>
    /// Timestamp when user was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp when user was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
