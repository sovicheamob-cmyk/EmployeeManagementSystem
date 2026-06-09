# Complete Authentication & Authorization Guide
### Extending Your .NET User CRUD API — Step by Step

> **Audience:** Junior–Mid backend developer who already has a working User CRUD API.
> Every step explains **why**, shows **complete code**, and explains **every line**.

---

## Table of Contents

1. [Architecture Overview](#1-architecture-overview)
2. [Install NuGet Packages](#2-install-nuget-packages)
3. [Update appsettings.json](#3-update-appsettingsjson)
4. [Update the User Entity](#4-update-the-user-entity)
5. [Common Constants — Roles.cs](#5-common-constants--rolescs)
6. [DTOs](#6-dtos)
7. [Update AutoMapper Profile](#7-update-automapper-profile)
8. [Update Repository Layer](#8-update-repository-layer)
9. [JWT Service](#9-jwt-service)
10. [Auth Service](#10-auth-service)
11. [Update User Service](#11-update-user-service)
12. [Auth Controller](#12-auth-controller)
13. [Profile Controller](#13-profile-controller)
14. [Update Users Controller](#14-update-users-controller)
15. [Update Exception Middleware](#15-update-exception-middleware)
16. [Update AppDbContext](#16-update-appdbcontext)
17. [Update Program.cs](#17-update-programcs)
18. [EF Core Migration](#18-ef-core-migration)
19. [Final Project Structure](#19-final-project-structure)
20. [Testing the API](#20-testing-the-api)

---

## 1. Architecture Overview

### What we are building and why

Before writing a single line of code, understand **what each layer does** and **why it exists**.

```
HTTP Request
     │
     ▼
[ExceptionMiddleware]      ← catches ALL unhandled exceptions → ApiResponse error
     │
     ▼
[JWT Middleware]           ← reads Authorization header → validates token → sets User.Claims
     │
     ▼
[Controller]               ← routes request, checks [Authorize] roles, delegates to service
     │
     ▼
[AuthService / UserService] ← business rules (duplicate email, password verify, role checks)
     │
     ▼
[UserRepository]           ← ALL database queries live here (EF Core)
     │
     ▼
[PostgreSQL]               ← stores users table with role + password_hash columns
```

### The Authentication Flow (Login)

```
Client sends POST /api/auth/login { email, password }
    → AuthService.LoginAsync()
        → UserRepository finds user by email
        → BCrypt verifies password against stored hash
        → JwtService generates a signed token with claims (userId, email, role)
    → Controller wraps in ApiResponse
    → Client stores the token
    → Client sends future requests with: Authorization: Bearer <token>
    → JWT Middleware validates token → sets HttpContext.User.Claims
    → [Authorize(Roles="Admin")] checks the role claim
```

### Why Dependency Injection (DI)?

DI means **you never write `new SomeService()`** in your code. Instead:

1. You register services in `Program.cs` (the "composition root")
2. ASP.NET Core creates them for you and injects them into constructors
3. Benefits:
   - Easy to swap implementations (e.g. swap BCrypt for Argon2)
   - Easy to unit test (inject a fake/mock instead of the real service)
   - Lifetime management (Scoped = one per request, so the same DbContext is shared)

```csharp
// WITHOUT DI (bad — hard to test, tightly coupled)
public class AuthController
{
    private AuthService _service = new AuthService(new UserRepository(new AppDbContext(...)));
}

// WITH DI (good — loose coupling, testable)
public class AuthController
{
    private readonly IAuthService _service;
    public AuthController(IAuthService service) { _service = service; } // injected!
}
```

### New Files We Will Create

```
UserApi/
├── Common/
│   ├── ApiResponse.cs          (already exists)
│   └── Roles.cs                ← NEW: role name constants
├── Models/
│   └── User.cs                 ← UPDATED: add Role, PasswordHash
├── DTOs/
│   ├── Auth/
│   │   ├── RegisterDto.cs      ← NEW
│   │   ├── LoginDto.cs         ← NEW
│   │   └── AuthResponseDto.cs  ← NEW
│   ├── Users/
│   │   ├── CreateUserDto.cs    ← UPDATED: add Role field
│   │   ├── UpdateUserDto.cs    (unchanged)
│   │   ├── UpdateUserRoleDto.cs ← NEW
│   │   └── UserResponseDto.cs  ← UPDATED: add Role field
│   └── Profile/
│       └── ProfileResponseDto.cs ← NEW
├── Services/
│   ├── IJwtService.cs          ← NEW
│   ├── JwtService.cs           ← NEW
│   ├── IAuthService.cs         ← NEW
│   ├── AuthService.cs          ← NEW
│   ├── IUserService.cs         ← UPDATED
│   └── UserService.cs          ← UPDATED
├── Repositories/
│   ├── IUserRepository.cs      ← UPDATED
│   └── UserRepository.cs       ← UPDATED
├── Controllers/
│   ├── AuthController.cs       ← NEW
│   ├── ProfileController.cs    ← NEW
│   └── UsersController.cs      ← UPDATED
├── Middleware/
│   └── ExceptionMiddleware.cs  ← UPDATED
├── Data/
│   └── AppDbContext.cs         ← UPDATED
├── Mappings/
│   └── UserMappingProfile.cs   ← UPDATED
├── appsettings.json            ← UPDATED
└── Program.cs                  ← UPDATED
```

---

## 2. Install NuGet Packages

### Why these packages?

```bash
# JWT Bearer Authentication — ASP.NET Core middleware that reads and validates JWT tokens
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer

# JWT token creation — lets us build and sign JWT tokens
dotnet add package System.IdentityModel.Tokens.Jwt

# BCrypt password hashing — industry standard for securely hashing passwords
# NEVER store plain text passwords. BCrypt adds a random salt automatically.
dotnet add package BCrypt.Net-Next
```

> **Why BCrypt and not SHA256/MD5?**
> MD5 and SHA256 are *fast* — that is bad for passwords because attackers can try
> billions of guesses per second. BCrypt is *deliberately slow* (configurable cost factor)
> and includes a random salt, making rainbow table attacks impossible.

---

## 3. Update appsettings.json

### Why?

We need to store JWT configuration (secret key, issuer, audience, expiry) outside the code.
Hard-coding secrets in code is a security risk and makes deployment harder.

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=UserApiDb;Username=postgres;Password=yourpassword"
  },
  "JwtSettings": {
    "SecretKey": "your-super-secret-key-must-be-at-least-32-characters-long!",
    "Issuer":    "UserApi",
    "Audience":  "UserApiClients",
    "ExpiryMinutes": 60
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

> **SecretKey:** This key signs and verifies every JWT token.
> Anyone with this key can forge tokens. In production, store it in
> environment variables or a secrets manager (Azure Key Vault, AWS Secrets Manager).
> It must be at least 32 characters for HMAC-SHA256.

---

## 4. Update the User Entity

### Why?

Our current `User` model has no `PasswordHash` or `Role`. We need to add:
- `PasswordHash` — the BCrypt-hashed password (never plain text)
- `Role` — an enum that controls what the user can do

### `Models/User.cs`

```csharp
// Models/User.cs
// The User model is the central entity of our entire application.
// EF Core maps this class directly to the 'users' table in PostgreSQL.
// Every field here becomes a column in the database.

namespace UserApi.Models;

// ── UserRole Enum ──────────────────────────────────────────────────────────
// An enum gives us type safety — you cannot accidentally assign role = 99.
// The integer values (1, 2, 3) are stored in PostgreSQL as a string via EF
// Core's HasConversion<string>() (configured in AppDbContext).
// We use explicit integer values so adding roles later doesn't shift existing ones.
public enum UserRole
{
    User       = 1,   // lowest privilege — can only read own profile
    Admin      = 2,   // can manage regular users
    SuperAdmin = 3    // full access including role assignment
}

// ── User Entity ────────────────────────────────────────────────────────────
public class User
{
    // Primary key — EF Core recognises "Id" by convention and auto-increments it
    public int Id { get; set; }

    // User's first name — required, max 100 chars (enforced in AppDbContext)
    public string FirstName { get; set; } = string.Empty;

    // User's last name
    public string LastName { get; set; } = string.Empty;

    // Email is the login identifier — must be unique (unique index in AppDbContext)
    public string Email { get; set; } = string.Empty;

    // BCrypt hash of the user's password.
    // Example value: "$2a$11$rBnqeLnqxyzABC..."
    // We NEVER store or log the plain text password anywhere.
    public string PasswordHash { get; set; } = string.Empty;

    // The user's role — determines what endpoints they can access.
    // Defaults to User (lowest privilege) on every new account.
    public UserRole Role { get; set; } = UserRole.User;

    // Audit timestamp — set when the row is first inserted
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Audit timestamp — updated every time the row is modified
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
```

---

## 5. Common Constants — Roles.cs

### Why?

Without this file, role names are scattered as magic strings across your code:
```csharp
[Authorize(Roles = "Admin")]       // in UsersController
[Authorize(Roles = "Admin")]       // in ProfileController
// ... if you rename "Admin" → "Manager" you must find every string
```

With `Roles.cs`, you write `Roles.Admin` everywhere.
If you rename a role, you change it in one place.

### `Common/Roles.cs`

```csharp
// Common/Roles.cs
// Central source of truth for all role name strings.
//
// Why static class with const strings instead of using the enum directly?
// [Authorize(Roles = ...)] only accepts strings, not enums.
// These constants ensure the strings always match the enum names.

namespace UserApi.Common;

public static class Roles
{
    // Matches UserRole.User.ToString() → "User"
    public const string User       = "User";

    // Matches UserRole.Admin.ToString() → "Admin"
    public const string Admin      = "Admin";

    // Matches UserRole.SuperAdmin.ToString() → "SuperAdmin"
    public const string SuperAdmin = "SuperAdmin";

    // Convenience combination string for [Authorize] — avoids repeating
    // "Admin,SuperAdmin" in every controller action
    public const string AdminOrSuperAdmin = "Admin,SuperAdmin";
}
```

---

## 6. DTOs

### Why DTOs?

DTOs (Data Transfer Objects) are the **contract between your API and the outside world**.
They protect your `User` model from being exposed directly. Benefits:
- Control exactly what fields come IN (no over-posting attacks)
- Control exactly what fields go OUT (never accidentally expose `PasswordHash`)
- The API contract stays stable even if the DB schema changes

---

### `DTOs/Auth/RegisterDto.cs`

```csharp
// DTOs/Auth/RegisterDto.cs
// Defines the request body shape for POST /api/auth/register.
//
// Notice: NO Role field here — callers cannot self-assign a privileged role.
// All self-registrations are locked to UserRole.User in the service layer.

using System.ComponentModel.DataAnnotations;

namespace UserApi.DTOs.Auth;

public class RegisterDto
{
    // [Required] — [ApiController] returns 400 automatically if this field is missing
    // [MaxLength] — prevents excessively long inputs that could waste DB storage
    [Required(ErrorMessage = "First name is required.")]
    [MaxLength(100, ErrorMessage = "First name cannot exceed 100 characters.")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Last name is required.")]
    [MaxLength(100, ErrorMessage = "Last name cannot exceed 100 characters.")]
    public string LastName { get; set; } = string.Empty;

    // [EmailAddress] validates format — checks for @ symbol and valid structure
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Invalid email format.")]
    public string Email { get; set; } = string.Empty;

    // [MinLength] — enforce a minimum password length before it reaches BCrypt
    [Required(ErrorMessage = "Password is required.")]
    [MinLength(6, ErrorMessage = "Password must be at least 6 characters.")]
    public string Password { get; set; } = string.Empty;
}
```

---

### `DTOs/Auth/LoginDto.cs`

```csharp
// DTOs/Auth/LoginDto.cs
// Request body for POST /api/auth/login.
// Intentionally minimal — only what we need to authenticate.

using System.ComponentModel.DataAnnotations;

namespace UserApi.DTOs.Auth;

public class LoginDto
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Invalid email format.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    public string Password { get; set; } = string.Empty;
}
```

---

### `DTOs/Auth/AuthResponseDto.cs`

```csharp
// DTOs/Auth/AuthResponseDto.cs
// The response body returned after a successful register or login.
// The client stores this token and sends it with every subsequent request
// in the Authorization header: "Bearer <token>"

namespace UserApi.DTOs.Auth;

public class AuthResponseDto
{
    // The signed JWT string — e.g. "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
    // The client must include this in: Authorization: Bearer <Token>
    public string Token { get; set; } = string.Empty;

    // Convenience fields so the client doesn't have to decode the JWT
    public int    UserId   { get; set; }
    public string Email    { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role     { get; set; } = string.Empty;

    // When the token stops working — the client can use this to proactively
    // refresh before it expires instead of waiting for a 401 response
    public DateTime ExpiresAt { get; set; }
}
```

---

### `DTOs/Users/CreateUserDto.cs` *(updated)*

```csharp
// DTOs/Users/CreateUserDto.cs
// Used by Admin/SuperAdmin when creating a user via POST /api/users.
// Includes an optional Role field — only SuperAdmin can set Admin/SuperAdmin roles
// (enforced in the service layer, not here).

using System.ComponentModel.DataAnnotations;
using UserApi.Models;

namespace UserApi.DTOs.Users;

public class CreateUserDto
{
    [Required(ErrorMessage = "First name is required.")]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Last name is required.")]
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [MinLength(6)]
    public string Password { get; set; } = string.Empty;

    [Phone]
    public string? PhoneNumber { get; set; }

    // Optional — if not provided, defaults to UserRole.User in the service.
    // Admin callers can only set User here; SuperAdmin can set any role.
    public UserRole? Role { get; set; }
}
```

---

### `DTOs/Users/UpdateUserDto.cs` *(updated)*

```csharp
// DTOs/Users/UpdateUserDto.cs
// All fields are nullable/optional — only provided fields will be updated.
// Password is included here so admins can reset a user's password.
// Role is intentionally excluded — role changes go through a dedicated endpoint.

namespace UserApi.DTOs.Users;

public class UpdateUserDto
{
    public string? FirstName   { get; set; }
    public string? LastName    { get; set; }
    public string? Email       { get; set; }
    public string? PhoneNumber { get; set; }

    // If provided, the service will hash this before saving.
    // If null, the existing password hash is left untouched.
    public string? Password { get; set; }
}
```

---

### `DTOs/Users/UpdateUserRoleDto.cs` *(new)*

```csharp
// DTOs/Users/UpdateUserRoleDto.cs
// Used exclusively by PUT /api/users/{id}/role — SuperAdmin only.
// Keeping role changes in a separate DTO/endpoint makes it impossible for
// a regular update request to accidentally change someone's role.

using System.ComponentModel.DataAnnotations;
using UserApi.Models;

namespace UserApi.DTOs.Users;

public class UpdateUserRoleDto
{
    // [Required] + [EnumDataType] ensures only valid enum values are accepted
    [Required(ErrorMessage = "Role is required.")]
    [EnumDataType(typeof(UserRole), ErrorMessage = "Invalid role value.")]
    public UserRole Role { get; set; }
}
```

---

### `DTOs/Users/UserResponseDto.cs` *(updated)*

```csharp
// DTOs/Users/UserResponseDto.cs
// What the API returns when representing a user.
// Critical: NO PasswordHash field — never expose hashed passwords in responses.

namespace UserApi.DTOs.Users;

public class UserResponseDto
{
    public int      Id          { get; set; }
    public string   FirstName   { get; set; } = string.Empty;
    public string   LastName    { get; set; } = string.Empty;
    public string   Email       { get; set; } = string.Empty;
    public string?  PhoneNumber { get; set; }
    public string   Role        { get; set; } = string.Empty; // "User", "Admin", "SuperAdmin"
    public DateTime CreatedAt   { get; set; }
}
```

---

### `DTOs/Profile/ProfileResponseDto.cs` *(new)*

```csharp
// DTOs/Profile/ProfileResponseDto.cs
// Used by GET /api/profile — returns the currently logged-in user's own data.
// Similar to UserResponseDto but kept separate so the two can evolve independently.
// For example, the profile might later include account settings, avatar URL, etc.

namespace UserApi.DTOs.Profile;

public class ProfileResponseDto
{
    public int      Id          { get; set; }
    public string   FirstName   { get; set; } = string.Empty;
    public string   LastName    { get; set; } = string.Empty;
    public string   Email       { get; set; } = string.Empty;
    public string?  PhoneNumber { get; set; }
    public string   Role        { get; set; } = string.Empty;
    public DateTime CreatedAt   { get; set; }
}
```

---

## 7. Update AutoMapper Profile

### Why AutoMapper?

Writing `user.FirstName = dto.FirstName; user.LastName = dto.LastName; ...`
for every mapping is tedious and error-prone. AutoMapper maps matching property
names automatically. You only configure exceptions.

### `Mappings/UserMappingProfile.cs`

```csharp
// Mappings/UserMappingProfile.cs
// Defines all object-to-object mapping rules.
// AutoMapper scans for Profile subclasses and registers them automatically
// via AddAutoMapper(typeof(UserMappingProfile)) in Program.cs.

using AutoMapper;
using UserApi.DTOs.Auth;
using UserApi.DTOs.Profile;
using UserApi.DTOs.Users;
using UserApi.Models;

namespace UserApi.Mappings;

public class UserMappingProfile : Profile
{
    public UserMappingProfile()
    {
        // ── Inbound mappings (DTO → Entity) ───────────────────────────────

        // RegisterDto → User
        // AutoMapper copies FirstName, LastName, Email (same name, same type).
        // PasswordHash and Role are NOT in RegisterDto so they stay at default.
        // The service layer sets PasswordHash and Role manually after this map.
        CreateMap<RegisterDto, User>();

        // CreateUserDto → User
        // Password field exists in DTO but NOT on User (User has PasswordHash).
        // We explicitly ignore Password here — the service hashes it separately.
        CreateMap<CreateUserDto, User>()
            .ForMember(dest => dest.PasswordHash,
                       opt  => opt.Ignore())  // service handles hashing
            .ForMember(dest => dest.Role,
                       opt  => opt.MapFrom(src => src.Role ?? UserRole.User)); // default to User

        // UpdateUserDto → User (partial update)
        // ForAllMembers with the Condition skips any null source property.
        // This means: only overwrite a field if the DTO provides a non-null value.
        // Example: if dto.Email is null, the existing user.Email is left intact.
        CreateMap<UpdateUserDto, User>()
            .ForMember(dest => dest.PasswordHash,
                       opt  => opt.Ignore())  // service handles password hashing
            .ForAllMembers(opts =>
                opts.Condition((src, dest, srcMember) => srcMember != null));

        // ── Outbound mappings (Entity → DTO) ─────────────────────────────

        // User → UserResponseDto
        // Role is an enum (UserRole.Admin) but the DTO has a string ("Admin").
        // AutoMapper's ToString() conversion handles this automatically via
        // the .ConvertUsing() or simply because string ← enum works out of the box.
        CreateMap<User, UserResponseDto>()
            .ForMember(dest => dest.Role,
                       opt  => opt.MapFrom(src => src.Role.ToString()));

        // User → ProfileResponseDto — same shape as UserResponseDto
        CreateMap<User, ProfileResponseDto>()
            .ForMember(dest => dest.Role,
                       opt  => opt.MapFrom(src => src.Role.ToString()));
    }
}
```

---

## 8. Update Repository Layer

### Why update the repository?

The service layer now needs to:
- Find a user by email (for login + duplicate check)
- Find a user by ID with tracking (for updates)
- Update a user's role separately

### `Repositories/IUserRepository.cs`

```csharp
// Repositories/IUserRepository.cs
// The repository interface is the CONTRACT that the service layer depends on.
// Controllers and services only know this interface — not the concrete class.
// This is the Dependency Inversion Principle: depend on abstractions, not concretions.
//
// Benefit: to unit-test UserService, you inject a fake/mock IUserRepository
// that returns predictable data — no real database needed in tests.

using UserApi.Models;

namespace UserApi.Repositories;

public interface IUserRepository
{
    // Returns all users — used by Admin/SuperAdmin
    Task<IEnumerable<User>> GetAllAsync();

    // Returns one user or null — used by GET /api/users/{id}
    Task<User?> GetByIdAsync(int id);

    // Returns one user or null — used for login and duplicate-email check
    Task<User?> GetByEmailAsync(string email);

    // Inserts a new user row — returns the entity with the DB-generated Id
    Task<User> CreateAsync(User user);

    // Updates an existing user row — returns null if the user doesn't exist
    Task<User?> UpdateAsync(int id, User updatedData);

    // Deletes a user row — returns false if the user doesn't exist
    Task<bool> DeleteAsync(int id);
}
```

---

### `Repositories/UserRepository.cs`

```csharp
// Repositories/UserRepository.cs
// The ONLY class in the entire application that directly touches the database.
// All other layers (services, controllers) go through the interface above.
//
// Why isolate DB access here?
// - If you switch from EF Core to Dapper, you only change this file.
// - All SQL/EF logic is in one place — easy to review and optimize.
// - Services stay clean: no DbContext imports outside this file.

using Microsoft.EntityFrameworkCore;
using UserApi.Data;
using UserApi.Models;

namespace UserApi.Repositories;

public class UserRepository : IUserRepository
{
    // AppDbContext is injected by the DI container.
    // It is Scoped (one per HTTP request), which means all repository
    // operations within the same request share the same DB connection/transaction.
    private readonly AppDbContext _context;

    public UserRepository(AppDbContext context)
    {
        _context = context;
    }

    // ── GET ALL ────────────────────────────────────────────────────────────
    public async Task<IEnumerable<User>> GetAllAsync()
    {
        // AsNoTracking() = read-only query.
        // EF Core normally tracks every entity it loads (for change detection).
        // For queries where we only READ and never UPDATE, tracking wastes memory.
        // AsNoTracking() skips that overhead — faster for list queries.
        return await _context.Users
                             .AsNoTracking()
                             .OrderBy(u => u.Id)   // consistent, predictable order
                             .ToListAsync();        // async = doesn't block the thread
    }

    // ── GET BY ID ──────────────────────────────────────────────────────────
    public async Task<User?> GetByIdAsync(int id)
    {
        // FirstOrDefaultAsync returns null if no user matches — the ? makes that explicit.
        // The caller (service) decides whether null means 404 or something else.
        return await _context.Users
                             .AsNoTracking()
                             .FirstOrDefaultAsync(u => u.Id == id);
    }

    // ── GET BY EMAIL ───────────────────────────────────────────────────────
    public async Task<User?> GetByEmailAsync(string email)
    {
        // Used in two scenarios:
        // 1. Login: find the user to verify their password
        // 2. Registration/creation: check that the email is not already taken
        // OrdinalIgnoreCase comparison — "John@Example.COM" == "john@example.com"
        return await _context.Users
                             .AsNoTracking()
                             .FirstOrDefaultAsync(u =>
                                 u.Email.ToLower() == email.ToLower());
    }

    // ── CREATE ─────────────────────────────────────────────────────────────
    public async Task<User> CreateAsync(User user)
    {
        // Add() stages the entity — EF Core marks it as EntityState.Added.
        // No SQL runs yet.
        _context.Users.Add(user);

        // SaveChangesAsync() flushes all staged changes to the DB in one transaction.
        // After this line, user.Id is populated with the value PostgreSQL assigned.
        await _context.SaveChangesAsync();

        return user; // return the full entity including the new Id
    }

    // ── UPDATE ─────────────────────────────────────────────────────────────
    public async Task<User?> UpdateAsync(int id, User updatedData)
    {
        // FindAsync uses the primary key — it first checks the EF Core identity cache
        // (already-loaded entities) before going to the DB. Also async-safe.
        // We do NOT use AsNoTracking() here because we need EF to track changes.
        var existing = await _context.Users.FindAsync(id);

        if (existing == null)
            return null; // signal to service: user not found → return 404

        // Copy only the fields that are safe to update.
        // We never copy Id (PK should never change) or CreatedAt (immutable audit field).
        existing.FirstName   = updatedData.FirstName;
        existing.LastName    = updatedData.LastName;
        existing.Email       = updatedData.Email;
        existing.PhoneNumber = updatedData.PhoneNumber;
        existing.PasswordHash = updatedData.PasswordHash;
        existing.Role        = updatedData.Role;
        existing.UpdatedAt   = DateTime.UtcNow; // refresh the audit timestamp

        // EF Core detects the changed fields and generates a targeted UPDATE SQL.
        // Only modified columns appear in the UPDATE statement — efficient.
        await _context.SaveChangesAsync();

        return existing;
    }

    // ── DELETE ─────────────────────────────────────────────────────────────
    public async Task<bool> DeleteAsync(int id)
    {
        var user = await _context.Users.FindAsync(id);

        if (user == null)
            return false; // caller returns 404

        // Remove() stages the entity as EntityState.Deleted.
        _context.Users.Remove(user);

        // SaveChangesAsync() executes DELETE FROM users WHERE id = @id
        await _context.SaveChangesAsync();

        return true;
    }
}
```

---

## 9. JWT Service

### What is a JWT?

A JWT (JSON Web Token) is a **self-contained, signed token** made of three parts:

```
eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9   ← Header (algorithm + type, base64)
.
eyJzdWIiOiIxIiwiZW1haWwiOiJqb2huQGV4YW1wbGUuY29tIiwicm9sZSI6IlVzZXIifQ   ← Payload (claims, base64)
.
SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c   ← Signature (HMAC-SHA256)
```

The **signature** is computed using your secret key. The server validates it on every
request — if the payload is tampered with, the signature won't match and the token is rejected.

**Claims** are key-value pairs embedded in the payload:
- `sub` (Subject) = UserId
- `email` = user's email
- `role` = "User" / "Admin" / "SuperAdmin"
- `jti` = unique token ID (prevents token reuse after logout, if you implement a blocklist)
- `exp` = expiry timestamp (token auto-expires)

### `Services/IJwtService.cs`

```csharp
// Services/IJwtService.cs
// Interface for JWT generation. Keeping it behind an interface means you can
// swap the implementation (e.g. use asymmetric RSA keys instead of HMAC)
// without touching any controller or service code.

using UserApi.Models;

namespace UserApi.Services;

public interface IJwtService
{
    // Takes a User entity, embeds their Id/Email/Role as claims,
    // signs the token, and returns the serialized JWT string.
    string GenerateToken(User user);
}
```

---

### `Services/JwtService.cs`

```csharp
// Services/JwtService.cs
// Responsible for one thing only: creating signed JWT tokens.
// It reads configuration from appsettings.json and uses System.IdentityModel.Tokens.Jwt.
//
// Dependency Injection:
//   Registered as Scoped in Program.cs.
//   IConfiguration is a built-in .NET service — automatically available for injection.

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using UserApi.Models;

namespace UserApi.Services;

public class JwtService : IJwtService
{
    // IConfiguration lets us read appsettings.json values at runtime.
    // We inject it rather than reading the file ourselves so the DI container
    // manages its lifetime and we can mock it in tests.
    private readonly IConfiguration _config;

    public JwtService(IConfiguration config)
    {
        _config = config;
    }

    public string GenerateToken(User user)
    {
        // ── Step 1: Read settings from appsettings.json ──────────────────
        // The ! (null-forgiving operator) tells the compiler we are sure
        // these values exist. In production, validate them on startup instead.
        var secretKey     = _config["JwtSettings:SecretKey"]!;
        var issuer        = _config["JwtSettings:Issuer"]!;
        var audience      = _config["JwtSettings:Audience"]!;
        var expiryMinutes = int.Parse(_config["JwtSettings:ExpiryMinutes"]!);

        // ── Step 2: Create the signing key ────────────────────────────────
        // The secret key is converted to bytes and wrapped in SymmetricSecurityKey.
        // "Symmetric" means the same key is used to SIGN (on login) and VERIFY
        // (on every request). This is simpler than asymmetric (RSA) but requires
        // keeping the secret key private on the server.
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));

        // SigningCredentials bundles the key with the algorithm (HMAC-SHA256).
        // HMAC-SHA256 is the standard algorithm for JWT signing.
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        // ── Step 3: Define the claims ─────────────────────────────────────
        // Claims are facts about the user that are embedded in the token payload.
        // The client can read them (base64 decode) but cannot forge them (signed).
        var claims = new[]
        {
            // Sub (Subject) — standard claim for the user identifier
            // We use user.Id.ToString() because claims are strings
            new Claim(JwtRegisteredClaimNames.Sub,   user.Id.ToString()),

            // Email — embedded so we don't need a DB lookup on every request
            new Claim(JwtRegisteredClaimNames.Email, user.Email),

            // Role — this is what [Authorize(Roles = "Admin")] checks.
            // ClaimTypes.Role is the specific claim type ASP.NET Core reads for role authorization.
            // user.Role.ToString() converts the enum to "User", "Admin", or "SuperAdmin"
            new Claim(ClaimTypes.Role, user.Role.ToString()),

            // Jti (JWT ID) — a unique ID for this specific token.
            // Useful if you implement token revocation (store revoked Jti values in a blocklist).
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        // ── Step 4: Build the token descriptor ────────────────────────────
        var token = new JwtSecurityToken(
            issuer:             issuer,              // who created this token ("UserApi")
            audience:           audience,            // who it's intended for ("UserApiClients")
            claims:             claims,              // the payload data
            expires:            DateTime.UtcNow.AddMinutes(expiryMinutes), // expiry time
            signingCredentials: credentials          // how it's signed
        );

        // ── Step 5: Serialize to string ───────────────────────────────────
        // JwtSecurityTokenHandler converts the token object into the
        // "header.payload.signature" string the client will receive.
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
```

---

## 10. Auth Service

### Why a separate AuthService?

Authentication logic (register, login) is distinct from user management (CRUD).
Separating them follows the Single Responsibility Principle — each class does one job.

### `Services/IAuthService.cs`

```csharp
// Services/IAuthService.cs
// Authentication use cases — what the AuthController can do.

using UserApi.DTOs.Auth;

namespace UserApi.Services;

public interface IAuthService
{
    // Creates a new User-role account and returns a JWT
    Task<AuthResponseDto> RegisterAsync(RegisterDto dto);

    // Verifies credentials and returns a JWT
    Task<AuthResponseDto> LoginAsync(LoginDto dto);
}
```

---

### `Services/AuthService.cs`

```csharp
// Services/AuthService.cs
// Handles registration and login.
//
// Dependencies injected:
//   IUserRepository — to find/create users in the DB
//   IJwtService     — to generate JWT tokens after successful auth
//
// Both are interfaces → easily mockable in unit tests.

using UserApi.DTOs.Auth;
using UserApi.Models;
using UserApi.Repositories;

namespace UserApi.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepo;
    private readonly IJwtService     _jwtService;
    private readonly IConfiguration  _config;

    // Constructor injection — the DI container resolves these automatically
    // because we registered them in Program.cs with AddScoped<>().
    public AuthService(
        IUserRepository userRepo,
        IJwtService     jwtService,
        IConfiguration  config)
    {
        _userRepo   = userRepo;
        _jwtService = jwtService;
        _config     = config;
    }

    // ── REGISTER ──────────────────────────────────────────────────────────
    public async Task<AuthResponseDto> RegisterAsync(RegisterDto dto)
    {
        // Business rule: email addresses must be unique across all users.
        // We check this here (service layer) rather than relying solely on the
        // DB unique constraint — so we can return a meaningful error message
        // instead of a raw PostgreSQL exception.
        var existing = await _userRepo.GetByEmailAsync(dto.Email);
        if (existing != null)
            throw new InvalidOperationException($"Email '{dto.Email}' is already registered.");

        // BCrypt.HashPassword() does two things:
        //   1. Generates a cryptographically random salt
        //   2. Hashes the password + salt together using the bcrypt algorithm
        // The resulting string contains the salt embedded in it, so we only
        // need to store one column (PasswordHash) — no separate salt column needed.
        // The work factor (default 11) means ~100ms per hash — slow enough to
        // defeat brute-force attacks, fast enough for users not to notice.
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);

        // Build the User entity — Role is always User on self-registration.
        // No caller can set a higher role through this endpoint.
        var user = new User
        {
            FirstName    = dto.FirstName,
            LastName     = dto.LastName,
            Email        = dto.Email,
            PasswordHash = passwordHash,
            Role         = UserRole.User   // always lowest privilege on self-register
        };

        // Persist to DB — after this, user.Id is populated
        await _userRepo.CreateAsync(user);

        // Generate a JWT for the new user so they are immediately logged in
        return BuildAuthResponse(user);
    }

    // ── LOGIN ─────────────────────────────────────────────────────────────
    public async Task<AuthResponseDto> LoginAsync(LoginDto dto)
    {
        // Find the user by email — returns null if no account exists
        var user = await _userRepo.GetByEmailAsync(dto.Email);

        // BCrypt.Verify() takes the plain-text password the user submitted
        // and the hash from the DB, re-hashes with the embedded salt, and
        // compares. Returns true only if they match.
        //
        // Why do we check user == null AND BCrypt.Verify in the same condition?
        // Security: returning different errors for "user not found" vs "wrong password"
        // would let attackers enumerate valid email addresses. Always return the
        // same error for both cases.
        if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid email or password.");

        // Credentials are valid — generate and return a JWT
        return BuildAuthResponse(user);
    }

    // ── HELPER ────────────────────────────────────────────────────────────
    // Builds the AuthResponseDto from a User entity.
    // Private because it is only used internally by Register and Login.
    private AuthResponseDto BuildAuthResponse(User user)
    {
        var token         = _jwtService.GenerateToken(user);
        var expiryMinutes = int.Parse(_config["JwtSettings:ExpiryMinutes"]!);

        return new AuthResponseDto
        {
            Token     = token,
            UserId    = user.Id,
            Email     = user.Email,
            FullName  = $"{user.FirstName} {user.LastName}",
            Role      = user.Role.ToString(),
            ExpiresAt = DateTime.UtcNow.AddMinutes(expiryMinutes)
        };
    }
}
```

---

## 11. Update User Service

### Why update UserService?

The service now needs to:
- Hash passwords when creating or updating users
- Enforce role-assignment rules (Admin cannot create SuperAdmin)

### `Services/IUserService.cs`

```csharp
// Services/IUserService.cs
// Updated interface — added UpdateRoleAsync and changed CreateUserAsync
// to accept a callerRole so we can enforce role-assignment permissions.

using UserApi.DTOs.Users;
using UserApi.Models;

namespace UserApi.Services;

public interface IUserService
{
    Task<IEnumerable<UserResponseDto>> GetAllUsersAsync();
    Task<UserResponseDto?> GetUserByIdAsync(int id);

    // callerRole — the role of the user making the request.
    // Admin can create User accounts; SuperAdmin can create any role.
    Task<UserResponseDto> CreateUserAsync(CreateUserDto dto, UserRole callerRole);

    Task<UserResponseDto?> UpdateUserAsync(int id, UpdateUserDto dto);
    Task<bool>             DeleteUserAsync(int id);

    // Change a user's role — SuperAdmin only (enforced at controller AND service level)
    Task<UserResponseDto?> UpdateUserRoleAsync(int id, UpdateUserRoleDto dto);
}
```

---

### `Services/UserService.cs`

```csharp
// Services/UserService.cs
// Business logic for user management operations.
// All authorization POLICY is enforced at the controller ([Authorize] attributes).
// Additional business RULES are enforced here (e.g. role-assignment constraints).
//
// The double-layer is intentional:
//   - [Authorize] at the controller is the FIRST gate (fast, framework-level)
//   - Service-level checks are the SECOND gate (business rules, more nuanced)

using AutoMapper;
using UserApi.DTOs.Users;
using UserApi.Models;
using UserApi.Repositories;

namespace UserApi.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepo;
    private readonly IMapper         _mapper;

    public UserService(IUserRepository userRepo, IMapper mapper)
    {
        _userRepo = userRepo;
        _mapper   = mapper;
    }

    // ── GET ALL ────────────────────────────────────────────────────────────
    public async Task<IEnumerable<UserResponseDto>> GetAllUsersAsync()
    {
        var users = await _userRepo.GetAllAsync();
        // Map the entire list at once — AutoMapper handles IEnumerable<T>
        return _mapper.Map<IEnumerable<UserResponseDto>>(users);
    }

    // ── GET BY ID ──────────────────────────────────────────────────────────
    public async Task<UserResponseDto?> GetUserByIdAsync(int id)
    {
        var user = await _userRepo.GetByIdAsync(id);
        // null propagation: if user is null, Map is not called, null is returned
        return user == null ? null : _mapper.Map<UserResponseDto>(user);
    }

    // ── CREATE ─────────────────────────────────────────────────────────────
    // callerRole is passed from the controller so we can enforce the rule:
    // Admin can only create User-role accounts;
    // SuperAdmin can create User, Admin, or SuperAdmin accounts.
    public async Task<UserResponseDto> CreateUserAsync(CreateUserDto dto, UserRole callerRole)
    {
        // Duplicate email check
        if (await _userRepo.GetByEmailAsync(dto.Email) != null)
            throw new InvalidOperationException($"Email '{dto.Email}' is already in use.");

        // Determine the role for the new account
        var assignedRole = dto.Role ?? UserRole.User; // default to User if not specified

        // Role-assignment permission check:
        // An Admin cannot create Admin or SuperAdmin accounts —
        // even if they send Role = Admin in the request body.
        if (callerRole == UserRole.Admin && assignedRole != UserRole.User)
            throw new ForbiddenException("Admins can only create User-role accounts.");

        // Map DTO → User entity (AutoMapper ignores PasswordHash — we set it manually)
        var user = _mapper.Map<User>(dto);
        user.Role         = assignedRole;
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);

        var created = await _userRepo.CreateAsync(user);
        return _mapper.Map<UserResponseDto>(created);
    }

    // ── UPDATE ─────────────────────────────────────────────────────────────
    public async Task<UserResponseDto?> UpdateUserAsync(int id, UpdateUserDto dto)
    {
        // Fetch the current state of the user
        var existing = await _userRepo.GetByIdAsync(id);
        if (existing == null) return null;

        // AutoMapper merges non-null fields from dto onto existing
        // (configured with .ForAllMembers + null condition in the mapping profile)
        _mapper.Map(dto, existing);

        // If a new password was provided, hash it before saving.
        // If dto.Password is null, the existing PasswordHash is unchanged
        // because AutoMapper skips null fields and PasswordHash is Ignored.
        if (!string.IsNullOrWhiteSpace(dto.Password))
            existing.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);

        var updated = await _userRepo.UpdateAsync(id, existing);
        return updated == null ? null : _mapper.Map<UserResponseDto>(updated);
    }

    // ── DELETE ─────────────────────────────────────────────────────────────
    public async Task<bool> DeleteUserAsync(int id)
    {
        return await _userRepo.DeleteAsync(id);
    }

    // ── UPDATE ROLE ────────────────────────────────────────────────────────
    public async Task<UserResponseDto?> UpdateUserRoleAsync(int id, UpdateUserRoleDto dto)
    {
        var user = await _userRepo.GetByIdAsync(id);
        if (user == null) return null;

        user.Role = dto.Role; // assign the new role

        var updated = await _userRepo.UpdateAsync(id, user);
        return updated == null ? null : _mapper.Map<UserResponseDto>(updated);
    }
}
```

---

## 12. Auth Controller

### `Controllers/AuthController.cs`

```csharp
// Controllers/AuthController.cs
// Handles authentication endpoints — publicly accessible (no token required).
//
// [AllowAnonymous] on both actions explicitly marks them as public.
// Even if we add a global [Authorize] policy later, [AllowAnonymous] overrides it.
//
// Request flow:
//   Client → AuthController → AuthService → UserRepository → DB
//                                         ↓
//                                    JwtService → JWT string → Client

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UserApi.Common;
using UserApi.DTOs.Auth;
using UserApi.Services;

namespace UserApi.Controllers;

[ApiController]
[Route("api/auth")]  // all routes in this controller start with /api/auth
public class AuthController : ControllerBase
{
    // IAuthService is injected — not AuthService directly.
    // The controller only knows the interface contract, not the implementation.
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    // ── POST /api/auth/register ────────────────────────────────────────────
    // [AllowAnonymous] — anyone can register, no token required
    // [HttpPost("register")] — maps to /api/auth/register
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<AuthResponseDto>>> Register(
        [FromBody] RegisterDto dto)  // [FromBody] reads JSON from the request body
    {
        // [ApiController] already validated the DTO against Data Annotations.
        // If validation failed, a 400 was returned before this line ran.
        var result = await _authService.RegisterAsync(dto);

        // 201 Created — the resource (user account) was created
        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<AuthResponseDto>.Success(result, "Registration successful.", 201));
    }

    // ── POST /api/auth/login ───────────────────────────────────────────────
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<AuthResponseDto>>> Login(
        [FromBody] LoginDto dto)
    {
        // If credentials are wrong, AuthService throws UnauthorizedAccessException.
        // ExceptionMiddleware catches it and returns a 401 with ApiResponse format.
        var result = await _authService.LoginAsync(dto);

        return Ok(ApiResponse<AuthResponseDto>.Success(result, "Login successful."));
    }
}
```

---

## 13. Profile Controller

### Why a separate Profile Controller?

The `/api/profile` endpoint is conceptually different from `/api/users`:
- It always refers to **the currently logged-in user** (read from their JWT claims)
- It has its own access rules (any authenticated user can access it)
- It will likely grow to include profile picture, settings, etc.

### `Controllers/ProfileController.cs`

```csharp
// Controllers/ProfileController.cs
// Handles the currently logged-in user's own profile.
//
// How does it know WHO is logged in?
// The JWT middleware reads the Authorization header, validates the token,
// and populates HttpContext.User with claims from the token payload.
// We read those claims here using User.FindFirstValue().
//
// Request flow:
//   GET /api/profile
//   → JWT Middleware validates token → sets HttpContext.User.Claims
//   → [Authorize] checks: is there a valid authenticated user? yes → continue
//   → ProfileController reads UserId from claims
//   → Fetches that specific user from DB
//   → Returns only THEIR data

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UserApi.Common;
using UserApi.DTOs.Profile;
using UserApi.Repositories;
using AutoMapper;

namespace UserApi.Controllers;

[ApiController]
[Route("api/profile")]
[Authorize]   // any authenticated user (User, Admin, SuperAdmin) can access this
public class ProfileController : ControllerBase
{
    // We query the repository directly here because this is a simple lookup.
    // Alternatively you could add GetProfileAsync to UserService.
    private readonly IUserRepository _userRepo;
    private readonly IMapper         _mapper;

    public ProfileController(IUserRepository userRepo, IMapper mapper)
    {
        _userRepo = userRepo;
        _mapper   = mapper;
    }

    // ── GET /api/profile ───────────────────────────────────────────────────
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<ProfileResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<ProfileResponseDto>>> GetProfile()
    {
        // HttpContext.User is populated by the JWT middleware.
        // It contains all the claims we embedded when we created the token.

        // Read the "sub" claim (JwtRegisteredClaimNames.Sub) which holds the UserId.
        // FindFirstValue returns null if the claim is missing (token issue).
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier)
                       ?? User.FindFirstValue("sub"); // fallback for different JWT libs

        // Parse to int — if it fails, something is wrong with the token
        if (!int.TryParse(userIdClaim, out var userId))
            return Unauthorized(ApiResponse<object>.Fail("Invalid token claims.", 401));

        // Fetch the user's current data from the DB using the Id from the token.
        // We don't use the email from the token because the user may have changed it.
        var user = await _userRepo.GetByIdAsync(userId);

        if (user == null)
            return NotFound(ApiResponse<object>.Fail("User not found.", 404));

        var profile = _mapper.Map<ProfileResponseDto>(user);
        return Ok(ApiResponse<ProfileResponseDto>.Success(profile, "Profile retrieved successfully."));
    }
}
```

---

## 14. Update Users Controller

### Authorization explained

ASP.NET Core's authorization pipeline works in layers:

```
Request arrives
    │
    ▼
[Authorize] on the class       ← must have a VALID token (any role)
    │
    ▼
[Authorize(Roles="Admin,...")]  ← token must have a specific role claim
    │
    ▼
Controller action runs
```

If either check fails:
- No token / invalid token → **401 Unauthorized** (handled by `OnChallenge` in Program.cs)
- Valid token but wrong role → **403 Forbidden** (handled by `OnForbidden` in Program.cs)

### `Controllers/UsersController.cs`

```csharp
// Controllers/UsersController.cs
// User management endpoints — all require authentication.
// Each action has a specific role requirement on top of that.
//
// Authorization layers:
//   1. [Authorize] on the class  → valid JWT required for ALL actions
//   2. [Authorize(Roles = "...")] on each action → specific role required

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UserApi.Common;
using UserApi.DTOs.Users;
using UserApi.Models;
using UserApi.Services;

namespace UserApi.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]   // GATE 1: every action in this controller requires a valid JWT
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    // ── GET /api/users ─────────────────────────────────────────────────────
    // Admin and SuperAdmin can view all users. Regular User cannot.
    [HttpGet]
    [Authorize(Roles = Roles.AdminOrSuperAdmin)]   // GATE 2: role check
    public async Task<ActionResult<ApiResponse<IEnumerable<UserResponseDto>>>> GetAll()
    {
        var users = await _userService.GetAllUsersAsync();
        return Ok(ApiResponse<IEnumerable<UserResponseDto>>.Success(
            users, "Users retrieved successfully."));
    }

    // ── GET /api/users/{id} ────────────────────────────────────────────────
    // Admin and SuperAdmin can view any user by ID.
    [HttpGet("{id:int}")]
    [Authorize(Roles = Roles.AdminOrSuperAdmin)]
    public async Task<ActionResult<ApiResponse<UserResponseDto>>> GetById(int id)
    {
        var user = await _userService.GetUserByIdAsync(id);

        if (user == null)
            return NotFound(ApiResponse<UserResponseDto>.Fail(
                $"User with ID {id} not found.", 404));

        return Ok(ApiResponse<UserResponseDto>.Success(user, "User retrieved successfully."));
    }

    // ── POST /api/users ────────────────────────────────────────────────────
    // Admin and SuperAdmin can create users.
    // Admin can only create User-role accounts (enforced in UserService.CreateUserAsync).
    [HttpPost]
    [Authorize(Roles = Roles.AdminOrSuperAdmin)]
    public async Task<ActionResult<ApiResponse<UserResponseDto>>> Create(
        [FromBody] CreateUserDto dto)
    {
        // Read the caller's role from JWT claims so we can enforce
        // role-assignment permissions in the service layer.
        // ClaimTypes.Role is the standard claim type for role authorization.
        var callerRoleClaim = User.FindFirstValue(ClaimTypes.Role);

        // Parse the claim string back into our UserRole enum.
        // TryParse returns false if the string doesn't match any enum value.
        if (!Enum.TryParse<UserRole>(callerRoleClaim, out var callerRole))
            return Unauthorized(ApiResponse<object>.Fail("Invalid role claim in token.", 401));

        var created = await _userService.CreateUserAsync(dto, callerRole);

        return CreatedAtAction(
            nameof(GetById),
            new { id = created.Id },
            ApiResponse<UserResponseDto>.Success(created, "User created successfully.", 201));
    }

    // ── PUT /api/users/{id} ────────────────────────────────────────────────
    // Admin and SuperAdmin can update users.
    // The Admin restriction on SuperAdmin accounts is enforced in the service.
    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.AdminOrSuperAdmin)]
    public async Task<ActionResult<ApiResponse<UserResponseDto>>> Update(
        int id, [FromBody] UpdateUserDto dto)
    {
        var updated = await _userService.UpdateUserAsync(id, dto);

        if (updated == null)
            return NotFound(ApiResponse<UserResponseDto>.Fail(
                $"User with ID {id} not found.", 404));

        return Ok(ApiResponse<UserResponseDto>.Success(updated, "User updated successfully."));
    }

    // ── DELETE /api/users/{id} ─────────────────────────────────────────────
    // Admin and SuperAdmin can delete users.
    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.AdminOrSuperAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(int id)
    {
        var deleted = await _userService.DeleteUserAsync(id);

        if (!deleted)
            return NotFound(ApiResponse<object>.Fail($"User with ID {id} not found.", 404));

        return Ok(ApiResponse<object>.SuccessNoData("User deleted successfully.", 204));
    }

    // ── PUT /api/users/{id}/role ───────────────────────────────────────────
    // ONLY SuperAdmin can change roles — this is the most privileged operation.
    [HttpPut("{id:int}/role")]
    [Authorize(Roles = Roles.SuperAdmin)]   // GATE 2: SuperAdmin ONLY
    public async Task<ActionResult<ApiResponse<UserResponseDto>>> UpdateRole(
        int id, [FromBody] UpdateUserRoleDto dto)
    {
        var updated = await _userService.UpdateUserRoleAsync(id, dto);

        if (updated == null)
            return NotFound(ApiResponse<UserResponseDto>.Fail(
                $"User with ID {id} not found.", 404));

        return Ok(ApiResponse<UserResponseDto>.Success(updated, "User role updated successfully."));
    }
}
```

---

## 15. Update Exception Middleware

### Why update it?

We have two new exception types to handle:
- `UnauthorizedAccessException` → 401 (wrong password, bad credentials)
- `ForbiddenException` → 403 (authenticated but not allowed, e.g. Admin trying to create Admin)

We also need to create `ForbiddenException` as a custom exception class.

### `Common/ForbiddenException.cs` *(new)*

```csharp
// Common/ForbiddenException.cs
// A custom exception thrown when a user is authenticated but lacks the
// PERMISSION to perform the requested action.
//
// The difference between 401 and 403:
//   401 Unauthorized = "I don't know who you are" (no/invalid token)
//   403 Forbidden    = "I know who you are, but you can't do this" (wrong role)
//
// Throwing this from the service layer rather than returning 403 directly
// keeps the service clean (no HTTP dependencies) while still producing the
// correct HTTP response via the exception middleware.

namespace UserApi.Common;

public class ForbiddenException : Exception
{
    // Inherits from Exception — the standard base class for all exceptions.
    // We just need a message; no extra properties needed for now.
    public ForbiddenException(string message) : base(message) { }
}
```

---

### `Middleware/ExceptionMiddleware.cs`

```csharp
// Middleware/ExceptionMiddleware.cs
// Global exception handler — wraps the ENTIRE request pipeline.
// Any exception thrown anywhere in the app (controller, service, repository)
// bubbles up here and gets converted to a clean ApiResponse JSON error.
//
// Why middleware instead of try/catch in every controller?
// - DRY principle: handle all errors in one place
// - Consistency: every error response has the same ApiResponse shape
// - Separation of concerns: business code throws exceptions, middleware handles HTTP

using System.Net;
using System.Text.Json;
using UserApi.Common;

namespace UserApi.Middleware;

public class ExceptionMiddleware
{
    // _next is the next middleware in the pipeline.
    // Calling await _next(context) passes the request forward.
    private readonly RequestDelegate                _next;
    private readonly ILogger<ExceptionMiddleware>   _logger;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        _next   = next;
        _logger = logger;
    }

    // InvokeAsync is called for every HTTP request.
    // This is the middleware convention in ASP.NET Core.
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            // Pass request down the pipeline.
            // If no exception occurs: response flows back up normally.
            // If any exception occurs: it's caught below.
            await _next(context);
        }
        catch (UnauthorizedAccessException ex)
        {
            // Thrown by AuthService.LoginAsync() for wrong credentials.
            // 401 Unauthorized — "your credentials are invalid"
            _logger.LogWarning("Unauthorized access: {Message}", ex.Message);
            await WriteErrorResponse(context, HttpStatusCode.Unauthorized, ex.Message);
        }
        catch (ForbiddenException ex)
        {
            // Thrown by UserService when a caller lacks permission for an action.
            // 403 Forbidden — "you are authenticated but not allowed to do this"
            _logger.LogWarning("Forbidden: {Message}", ex.Message);
            await WriteErrorResponse(context, HttpStatusCode.Forbidden, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            // Thrown for business rule violations: duplicate email, invalid state, etc.
            // 409 Conflict — "the request conflicts with the current state of the server"
            _logger.LogWarning("Business rule violation: {Message}", ex.Message);
            await WriteErrorResponse(context, HttpStatusCode.Conflict, ex.Message);
        }
        catch (Exception ex)
        {
            // Catch-all for unexpected errors (null reference, DB connection failure, etc.)
            // 500 Internal Server Error — never expose the real error message to clients
            _logger.LogError(ex, "Unhandled exception: {Message}", ex.Message);
            await WriteErrorResponse(context, HttpStatusCode.InternalServerError,
                "An unexpected error occurred. Please try again later.");
        }
    }

    // Writes a standardised ApiResponse<object> JSON error to the HTTP response.
    private static async Task WriteErrorResponse(
        HttpContext  context,
        HttpStatusCode statusCode,
        string       message)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode  = (int)statusCode;

        // Use ApiResponse<object> with Fail() — data is always null for errors
        var response = ApiResponse<object>.Fail(message, (int)statusCode);

        // Serialize with camelCase keys to match our API's JSON convention
        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }
}
```

---

## 16. Update AppDbContext

### Why update it?

We added two new columns to the `users` table:
- `password_hash` — stores the BCrypt hash
- `role` — stores the role name as a string

EF Core must know about these columns to generate correct migrations and SQL.

### `Data/AppDbContext.cs`

```csharp
// Data/AppDbContext.cs
// The EF Core DbContext is the central hub for all database operations.
// It manages:
//   - Database connections (via the connection string)
//   - Entity tracking (detecting what changed)
//   - Schema configuration (via OnModelCreating / Fluent API)
//   - Migrations (tracking schema version history)
//
// We configure schema details here (via Fluent API) rather than Data Annotations
// on the model for two reasons:
//   1. Keeps the model class clean — no infrastructure concerns
//   2. More powerful — Fluent API can do things annotations can't (composite keys, etc.)

using Microsoft.EntityFrameworkCore;
using UserApi.Models;

namespace UserApi.Data;

public class AppDbContext : DbContext
{
    // Constructor receives DbContextOptions injected by the DI container.
    // Options contain the connection string and provider (Npgsql).
    // Never construct AppDbContext with 'new' — always inject it.
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // DbSet<User> represents the 'users' table.
    // You query users via _context.Users.Where(...) etc.
    public DbSet<User> Users { get; set; }

    // OnModelCreating is called once when the app starts.
    // Use it to configure the exact DB schema using Fluent API.
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder); // always call base first

        modelBuilder.Entity<User>(entity =>
        {
            // Map this C# class to the PostgreSQL table named "users"
            entity.ToTable("users");

            // Primary key — maps to "id" column, auto-incrementing integer
            entity.HasKey(u => u.Id);
            entity.Property(u => u.Id).HasColumnName("id");

            // First and last name — required, max 100 chars, snake_case column names
            entity.Property(u => u.FirstName)
                  .IsRequired()
                  .HasMaxLength(100)
                  .HasColumnName("first_name");

            entity.Property(u => u.LastName)
                  .IsRequired()
                  .HasMaxLength(100)
                  .HasColumnName("last_name");

            // Email — required, unique index prevents duplicate accounts at the DB level.
            // The service-level check runs FIRST (better error message).
            // The DB constraint is a safety net (defence in depth).
            entity.Property(u => u.Email)
                  .IsRequired()
                  .HasMaxLength(200)
                  .HasColumnName("email");
            entity.HasIndex(u => u.Email)
                  .IsUnique();  // generates: CREATE UNIQUE INDEX ix_users_email ON users (email)

            // PasswordHash — required, stored as text.
            // BCrypt hashes are always 60 characters but we use 255 for safety.
            entity.Property(u => u.PasswordHash)
                  .IsRequired()
                  .HasMaxLength(255)
                  .HasColumnName("password_hash");

            // Role — stored as a string ("User", "Admin", "SuperAdmin") in the DB.
            // HasConversion<string>() tells EF Core to:
            //   - On WRITE: call UserRole.ToString() → "Admin"
            //   - On READ:  call Enum.Parse<UserRole>("Admin") → UserRole.Admin
            // Why string instead of int?
            //   - DB rows are human-readable without a lookup table
            //   - Adding new enum values doesn't shift integer values
            entity.Property(u => u.Role)
                  .HasConversion<string>()
                  .HasMaxLength(20)
                  .HasColumnName("role")
                  .HasDefaultValue(UserRole.User);  // DB default = "User"

            // Phone number — optional (nullable in C# and PostgreSQL)
            entity.Property(u => u.PhoneNumber)
                  .HasMaxLength(20)
                  .HasColumnName("phone_number");

            // Audit timestamps — stored as UTC timestamps in PostgreSQL
            entity.Property(u => u.CreatedAt).HasColumnName("created_at");
            entity.Property(u => u.UpdatedAt).HasColumnName("updated_at");
        });
    }
}
```

---

## 17. Update Program.cs

### What Program.cs does

`Program.cs` is the **composition root** — the single place where:
1. Services are registered into the DI container (`builder.Services.Add...`)
2. The HTTP request pipeline (middleware order) is configured (`app.Use...`)

### Middleware Order Matters!

```
Request → ExceptionMiddleware → Authentication → Authorization → Controller
```

- `UseAuthentication()` MUST come before `UseAuthorization()`
- `UseMiddleware<ExceptionMiddleware>()` MUST be first to catch all errors

### `Program.cs`

```csharp
// Program.cs
// Application entry point and composition root.
// Every dependency injection registration and pipeline configuration lives here.

using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using UserApi.Common;
using UserApi.Data;
using UserApi.Mappings;
using UserApi.Middleware;
using UserApi.Repositories;
using UserApi.Services;

var builder = WebApplication.CreateBuilder(args);

// ═══════════════════════════════════════════════════════════════════════════
// SECTION 1: Register Services (Dependency Injection Container)
// ═══════════════════════════════════════════════════════════════════════════

// ── Controllers ────────────────────────────────────────────────────────────
// Scans the assembly for all [ApiController] classes and registers them.
// ConfigureApiBehaviorOptions overrides [ApiController]'s default validation
// error format (ProblemDetails) to use our ApiResponse wrapper instead.
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            // Collect all validation error messages into a list
            var errors = context.ModelState
                .Where(e => e.Value?.Errors.Count > 0)
                .SelectMany(e => e.Value!.Errors.Select(x => x.ErrorMessage))
                .ToList();

            // One error → plain string; multiple → array
            var message = errors.Count == 1 ? (object)errors[0] : errors;

            // Return 400 with our ApiResponse format
            return new ObjectResult(ApiResponse<object>.Fail(message, 400))
            {
                StatusCode = StatusCodes.Status400BadRequest
            };
        };
    });

// ── Database ───────────────────────────────────────────────────────────────
// Register EF Core with the PostgreSQL provider (Npgsql).
// Reads the connection string from appsettings.json.
// AddDbContext registers AppDbContext as SCOPED — one DbContext per HTTP request.
// Scoped lifetime is important: all repository operations in one request share
// the same connection and can participate in the same transaction.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")));

// ── AutoMapper ─────────────────────────────────────────────────────────────
// Scans the assembly containing UserMappingProfile and registers all Profile subclasses.
// Registered as SINGLETON — mappings are stateless and safe to share across requests.
builder.Services.AddAutoMapper(typeof(UserMappingProfile));

// ── JWT Authentication ─────────────────────────────────────────────────────
// This block configures the JWT Bearer middleware that:
//   1. Reads the "Authorization: Bearer <token>" header on every request
//   2. Validates the token (signature, issuer, audience, expiry)
//   3. If valid: populates HttpContext.User with the token's claims
//   4. If invalid/missing: returns 401 (customized below via OnChallenge)
var jwtKey = builder.Configuration["JwtSettings:SecretKey"]!;

builder.Services.AddAuthentication(options =>
{
    // Set JWT Bearer as the DEFAULT scheme.
    // Without this, ASP.NET Core might use Cookies as default.
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    // TokenValidationParameters tells the middleware EXACTLY how to validate tokens.
    options.TokenValidationParameters = new TokenValidationParameters
    {
        // Validate that the token was issued by "UserApi" (our server)
        ValidateIssuer           = true,
        ValidIssuer              = builder.Configuration["JwtSettings:Issuer"],

        // Validate that the token is intended for "UserApiClients"
        ValidateAudience         = true,
        ValidAudience            = builder.Configuration["JwtSettings:Audience"],

        // Validate the expiry — reject tokens past their ExpiresAt timestamp
        ValidateLifetime         = true,

        // Validate the signature — ensures the token was signed with OUR secret key
        // and hasn't been tampered with
        ValidateIssuerSigningKey = true,
        IssuerSigningKey         = new SymmetricSecurityKey(
                                       Encoding.UTF8.GetBytes(jwtKey)),

        // By default .NET adds a 5-minute clock skew tolerance.
        // Set to zero for strict expiry enforcement.
        ClockSkew                = TimeSpan.Zero
    };

    // ── Custom 401 and 403 responses ──────────────────────────────────────
    // By default, failed auth returns an empty response body.
    // We override these to return our standard ApiResponse format.
    options.Events = new JwtBearerEvents
    {
        // OnChallenge fires when authentication FAILS (no token, invalid token, expired)
        // → 401 Unauthorized
        OnChallenge = async context =>
        {
            context.HandleResponse(); // suppress the default empty 401 response

            context.Response.StatusCode  = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";

            var response = ApiResponse<object>.Fail(
                "Authentication required. Please login to access this resource.", 401);

            await context.Response.WriteAsJsonAsync(response);
        },

        // OnForbidden fires when authentication SUCCEEDS but the user lacks the role
        // → 403 Forbidden
        OnForbidden = async context =>
        {
            context.Response.StatusCode  = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";

            var response = ApiResponse<object>.Fail(
                "You do not have permission to perform this action.", 403);

            await context.Response.WriteAsJsonAsync(response);
        }
    };
});

// ── Authorization ──────────────────────────────────────────────────────────
// AddAuthorization registers the authorization services that process
// [Authorize] and [Authorize(Roles="...")] attributes on controllers.
// Must be added alongside AddAuthentication.
builder.Services.AddAuthorization();

// ── Application Services (DI Registrations) ────────────────────────────────
// AddScoped = one instance per HTTP request.
// Using the interface as the service type enforces coding to abstractions.

// Repository — talks to the database
builder.Services.AddScoped<IUserRepository, UserRepository>();

// JWT generation service
builder.Services.AddScoped<IJwtService, JwtService>();

// Authentication business logic
builder.Services.AddScoped<IAuthService, AuthService>();

// User management business logic
builder.Services.AddScoped<IUserService, UserService>();

// ── Swagger ────────────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // Configure Swagger to show the "Authorize" button and
    // send the JWT in the Authorization header when testing
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name         = "Authorization",
        Type         = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme       = "Bearer",
        BearerFormat = "JWT",
        In           = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description  = "Enter your JWT token here. Example: eyJhbGci..."
    });
    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id   = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// ═══════════════════════════════════════════════════════════════════════════
// SECTION 2: Build the App and Configure the HTTP Pipeline
// ═══════════════════════════════════════════════════════════════════════════

var app = builder.Build();

// ── Middleware Pipeline (ORDER MATTERS!) ───────────────────────────────────
//
// Every HTTP request passes through middleware IN THE ORDER THEY ARE ADDED.
// Think of it as a series of filters the request must pass through.
//
// The response travels back through the same middleware in REVERSE order.
//
//   Request:  ExceptionMiddleware → Swagger → HTTPS → Auth → Authz → Controller
//   Response: Controller → Authz → Auth → HTTPS → Swagger → ExceptionMiddleware

// 1. Exception middleware FIRST — wraps everything else to catch all errors.
//    If this were registered after Authentication, auth exceptions would not
//    be caught and would return a raw ASP.NET Core error page.
app.UseMiddleware<ExceptionMiddleware>();

// 2. Swagger UI — only in Development (not exposed in production)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// 3. HTTPS redirection — redirect HTTP requests to HTTPS
app.UseHttpsRedirection();

// 4. Authentication — reads the Authorization header, validates the JWT,
//    and populates HttpContext.User.
//    MUST come before UseAuthorization().
app.UseAuthentication();

// 5. Authorization — enforces [Authorize] and [Authorize(Roles="...")] attributes.
//    Reads HttpContext.User which was set by UseAuthentication().
//    MUST come after UseAuthentication().
app.UseAuthorization();

// 6. Map controllers — connects HTTP routes to controller actions.
//    This is where [Route] and [HttpGet/Post/...] attributes are processed.
app.MapControllers();

// ── Auto-run Migrations ────────────────────────────────────────────────────
// Applies any pending EF Core migrations when the app starts.
// Convenient for development; in production use CI/CD pipeline migrations instead.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.Run();
```

---

## 18. EF Core Migration

### What is a Migration?

EF Core tracks your model changes and generates SQL scripts to update the database schema.
Think of migrations as **version control for your database schema**.

```
User model updated (added PasswordHash, Role)
    → dotnet ef migrations add AddAuthFields
        → EF Core compares: current model vs. last migration snapshot
        → Generates: Migrations/20240101_AddAuthFields.cs
            → Up() method:   ALTER TABLE users ADD COLUMN password_hash TEXT, ...
            → Down() method: ALTER TABLE users DROP COLUMN password_hash, ...
    → dotnet ef database update
        → Applies Up() method to your PostgreSQL database
```

### Commands

```bash
# Generate the migration file (compares model to last snapshot)
dotnet ef migrations add AddAuthFields

# Review the generated migration file in Migrations/ folder before applying!
# Look for: Up() adds columns, Down() removes them

# Apply the migration to the database
dotnet ef database update

# If something goes wrong, rollback to the previous migration
dotnet ef database update InitialCreate

# View migration history
dotnet ef migrations list
```

### What the migration generates (conceptually)

```sql
-- Up() — applied by dotnet ef database update
ALTER TABLE users
  ADD COLUMN password_hash VARCHAR(255) NOT NULL DEFAULT '',
  ADD COLUMN role          VARCHAR(20)  NOT NULL DEFAULT 'User';

-- Create index on email if not already present
CREATE UNIQUE INDEX ix_users_email ON users (email);

-- Down() — applied by dotnet ef database update <previous-migration-name>
ALTER TABLE users
  DROP COLUMN password_hash,
  DROP COLUMN role;
```

---

## 19. Final Project Structure

```
UserApi/
├── Common/
│   ├── ApiResponse.cs          ← Generic response wrapper {status,code,message,data}
│   ├── ForbiddenException.cs   ← Custom 403 exception
│   └── Roles.cs                ← Role name constants
├── Controllers/
│   ├── AuthController.cs       ← POST /api/auth/register, /api/auth/login
│   ├── ProfileController.cs    ← GET /api/profile
│   └── UsersController.cs      ← CRUD + role update
├── Data/
│   └── AppDbContext.cs         ← EF Core DbContext + schema config
├── DTOs/
│   ├── Auth/
│   │   ├── AuthResponseDto.cs
│   │   ├── LoginDto.cs
│   │   └── RegisterDto.cs
│   ├── Profile/
│   │   └── ProfileResponseDto.cs
│   └── Users/
│       ├── CreateUserDto.cs
│       ├── UpdateUserDto.cs
│       ├── UpdateUserRoleDto.cs
│       └── UserResponseDto.cs
├── Mappings/
│   └── UserMappingProfile.cs
├── Middleware/
│   └── ExceptionMiddleware.cs
├── Migrations/                 ← Auto-generated by EF Core
│   └── ...
├── Models/
│   └── User.cs                 ← UserRole enum + User entity
├── Repositories/
│   ├── IUserRepository.cs
│   └── UserRepository.cs
├── Services/
│   ├── IAuthService.cs
│   ├── AuthService.cs
│   ├── IJwtService.cs
│   ├── JwtService.cs
│   ├── IUserService.cs
│   └── UserService.cs
├── appsettings.json
└── Program.cs
```

---

## 20. Testing the API

### Step-by-step test sequence

```bash
# ── 1. Register a new user ─────────────────────────────────────────────────
curl -X POST https://localhost:5001/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "firstName": "John",
    "lastName":  "Doe",
    "email":     "john@example.com",
    "password":  "secret123"
  }'

# Expected response: 201 Created
# {
#   "status": true, "code": 201,
#   "message": "Registration successful.",
#   "data": { "token": "eyJ...", "role": "User", ... }
# }

# ── 2. Login ──────────────────────────────────────────────────────────────
curl -X POST https://localhost:5001/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{ "email": "john@example.com", "password": "secret123" }'

# Copy the "token" from the response data — use it below as <token>

# ── 3. View own profile (User role — allowed) ─────────────────────────────
curl https://localhost:5001/api/profile \
  -H "Authorization: Bearer <token>"

# ── 4. Try to view all users (User role — should get 403) ─────────────────
curl https://localhost:5001/api/users \
  -H "Authorization: Bearer <token>"

# Expected: 403 Forbidden
# { "status": false, "code": 403,
#   "message": "You do not have permission to perform this action.", "data": null }

# ── 5. Try without token (should get 401) ─────────────────────────────────
curl https://localhost:5001/api/users

# Expected: 401 Unauthorized
# { "status": false, "code": 401,
#   "message": "Authentication required. Please login...", "data": null }

# ── 6. Manually set a SuperAdmin in the database (first-time setup) ────────
# In a real app you'd seed this. For testing, update directly:
# UPDATE users SET role = 'SuperAdmin' WHERE email = 'john@example.com';
# Then login again to get a new token with the SuperAdmin role.

# ── 7. SuperAdmin: change a user's role ───────────────────────────────────
curl -X PUT https://localhost:5001/api/users/2/role \
  -H "Authorization: Bearer <superadmin-token>" \
  -H "Content-Type: application/json" \
  -d '{ "role": 2 }'   # 2 = Admin

# ── 8. Now login as that admin and view all users ─────────────────────────
curl https://localhost:5001/api/users \
  -H "Authorization: Bearer <admin-token>"
# Expected: 200 OK with user list
```

---

## Quick Reference: Access Matrix

| Endpoint                      | Anonymous | User | Admin | SuperAdmin |
|-------------------------------|:---------:|:----:|:-----:|:----------:|
| POST /api/auth/register       | ✅         | ✅    | ✅     | ✅          |
| POST /api/auth/login          | ✅         | ✅    | ✅     | ✅          |
| GET  /api/profile             | ❌ 401     | ✅    | ✅     | ✅          |
| GET  /api/users               | ❌ 401     | ❌ 403 | ✅    | ✅          |
| GET  /api/users/{id}          | ❌ 401     | ❌ 403 | ✅    | ✅          |
| POST /api/users               | ❌ 401     | ❌ 403 | ✅ (User only) | ✅  |
| PUT  /api/users/{id}          | ❌ 401     | ❌ 403 | ✅    | ✅          |
| DELETE /api/users/{id}        | ❌ 401     | ❌ 403 | ✅    | ✅          |
| PUT  /api/users/{id}/role     | ❌ 401     | ❌ 403 | ❌ 403 | ✅         |

---

## Key Concepts Summary

| Concept | Where it lives | What it does |
|---------|---------------|--------------|
| BCrypt hashing | `AuthService`, `UserService` | Converts plain password → secure hash |
| JWT generation | `JwtService` | Creates signed token with claims |
| JWT validation | `Program.cs` AddJwtBearer | Validates token on every request |
| Claims | `JwtService` + `ProfileController` | Data embedded in token (UserId, Role) |
| [Authorize] | Controllers | Requires any valid token |
| [Authorize(Roles="...")] | Controller actions | Requires specific role in token |
| [AllowAnonymous] | Auth controller | Overrides [Authorize] — public endpoint |
| ForbiddenException | Service layer | Throws when auth rules are violated |
| ExceptionMiddleware | Pipeline | Converts exceptions → ApiResponse JSON |
| DI registration | Program.cs | Wires interfaces to implementations |
| EF Core migration | CLI commands | Syncs model changes to DB schema |
