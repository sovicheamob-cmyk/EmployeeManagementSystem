namespace EmployeeManagementSystem.Models;

/// <summary>
/// User roles for the application
/// 
/// WHY ENUM?
/// - Type-safe role definitions
/// - Prevents invalid role values
/// - Easy to use in authorization logic
/// - Database stores enum values (1, 2, 3)
/// 
/// AUTHORIZATION RULES:
/// - User (1): Can only read data and view own profile
/// - Admin (2): Can manage employees
/// - SuperAdmin (3): Can manage users, roles, and entire system
/// </summary>
public enum UserRole
{
    /// <summary>
    /// Regular user - read-only access
    /// </summary>
    User = 1,

    /// <summary>
    /// Administrator - can manage employees
    /// </summary>
    Admin = 2,

    /// <summary>
    /// Super administrator - can manage users and all resources
    /// </summary>
    SuperAdmin = 3
}
