# .NET MVC Project Structure — Deep Dive Guide

> A practical reference for understanding, navigating, and contributing to any existing .NET MVC / Web API project fast.

---

## Table of Contents

1. [Folder Structure Overview](#1-folder-structure-overview)
2. [Program.cs — The App Entry Point](#2-programcs--the-app-entry-point)
3. [Models / Entities](#3-models--entities)
4. [DTOs (Data Transfer Objects)](#4-dtos-data-transfer-objects)
5. [DbContext — Database Bridge](#5-dbcontext--database-bridge)
6. [Repositories — Data Access Layer](#6-repositories--data-access-layer)
7. [Services — Business Logic Layer](#7-services--business-logic-layer)
8. [Controllers — Request Handler](#8-controllers--request-handler)
9. [Middleware — Cross-Cutting Concerns](#9-middleware--cross-cutting-concerns)
10. [Filters — Action-Level Hooks](#10-filters--action-level-hooks)
11. [Dependency Injection — Wiring It All Together](#11-dependency-injection--wiring-it-all-together)
12. [Configuration — appsettings.json](#12-configuration--appsettingsjson)
13. [Full Request Flow — End to End](#13-full-request-flow--end-to-end)
14. [Common Patterns You'll See in Existing Projects](#14-common-patterns-youll-see-in-existing-projects)
15. [Quick Navigation Cheat Sheet](#15-quick-navigation-cheat-sheet)

---

## 1. Folder Structure Overview

This is the typical layout of an MVC-structured .NET Web API project:

```
MyApp/
│
├── Controllers/                  # Receives HTTP requests, returns responses
│   ├── UsersController.cs
│   └── ProductsController.cs
│
├── Models/                       # Database entities (maps to DB tables)
│   ├── User.cs
│   └── Product.cs
│
├── DTOs/                         # What you send/receive via API (not raw entities)
│   ├── Request/
│   │   ├── CreateUserDto.cs
│   │   └── UpdateUserDto.cs
│   └── Response/
│       └── UserResponseDto.cs
│
├── Services/                     # Business logic lives here
│   ├── Interfaces/
│   │   └── IUserService.cs
│   └── UserService.cs
│
├── Repositories/                 # Database queries only — no logic
│   ├── Interfaces/
│   │   └── IUserRepository.cs
│   └── UserRepository.cs
│
├── Middleware/                   # Custom pipeline components (error handling, logging)
│   └── ExceptionHandlingMiddleware.cs
│
├── Data/                         # EF Core DbContext + Migrations
│   ├── AppDbContext.cs
│   └── Migrations/
│
├── Common/                       # Shared utilities, wrappers
│   ├── ApiResponse.cs
│   └── Exceptions/
│       └── NotFoundException.cs
│
├── appsettings.json              # App configuration (connection strings, JWT, etc.)
├── appsettings.Development.json  # Overrides for local dev only
└── Program.cs                    # App boot — registers everything
```

> **Rule of thumb:** When you open an existing project, always start with `Program.cs` and work outward. It's the map of the entire application.

---

## 2. Program.cs — The App Entry Point

`Program.cs` does two things in order:

1. **Register services** into the DI container (`builder.Services`)
2. **Register middleware** into the request pipeline (`app.Use...`)

```csharp
var builder = WebApplication.CreateBuilder(args);

// ════════════════════════════════════════
//  PHASE 1: REGISTER SERVICES
//  Everything here goes into the DI container.
//  Order does NOT matter here.
// ════════════════════════════════════════

// Adds MVC controllers
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        // Override default 400 validation error format
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState
                .Where(e => e.Value.Errors.Count > 0)
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToArray()
                );

            return new BadRequestObjectResult(new ApiResponse<object>
            {
                Success = false,
                Message = "Validation failed",
                Data = errors
            });
        };
    });

// EF Core with PostgreSQL
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Your own services — Scoped = one instance per HTTP request
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IUserService, UserService>();

// AutoMapper (if project uses it for DTO mapping)
builder.Services.AddAutoMapper(typeof(Program));

// Swagger for API docs
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build(); // <-- Everything above is locked in after this line

// ════════════════════════════════════════
//  PHASE 2: REGISTER MIDDLEWARE
//  Order HERE matters — top to bottom = request flow
// ════════════════════════════════════════

app.UseMiddleware<ExceptionHandlingMiddleware>(); // 1. Catch ALL unhandled errors first

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();          // 2. Swagger only in dev
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();     // 3. Force HTTPS
app.UseCors("AllowAll");       // 4. CORS policy (if configured)
app.UseAuthentication();       // 5. Who are you? (reads JWT/cookie)
app.UseAuthorization();        // 6. Are you allowed? (checks [Authorize])
app.MapControllers();          // 7. Route request to a controller

app.Run();
```

**What to look for in an existing `Program.cs`:**
- What services are registered? → tells you the architecture
- What middleware is used? → tells you cross-cutting behavior (auth, error handling, CORS)
- What is the DB provider? → `UseNpgsql` = PostgreSQL, `UseSqlServer` = SQL Server

---

## 3. Models / Entities

Models represent **database tables**. They are plain C# classes (POCOs) that EF Core maps to rows.

```csharp
// Models/User.cs
public class User
{
    public int Id { get; set; }            // Primary key — EF auto-detects "Id" naming
    public string Name { get; set; }
    public string Email { get; set; }
    public string PasswordHash { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;

    // Navigation property — EF uses this for JOINs
    // One User has many Orders
    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
```

```csharp
// Models/Order.cs
public class Order
{
    public int Id { get; set; }
    public decimal Amount { get; set; }
    public DateTime OrderDate { get; set; }

    // Foreign key — links back to User
    public int UserId { get; set; }
    public User User { get; set; }         // Navigation property back to User
}
```

**Key rules:**
- Entity = 1 class : 1 table
- Never add API-specific logic in entities
- Navigation properties (`ICollection<Order>`) represent relationships
- EF detects `Id` or `{ClassName}Id` as the primary key automatically

---

## 4. DTOs (Data Transfer Objects)

DTOs are what your API **accepts** (request) and **returns** (response). They are separate from entities on purpose.

```
Why not just use the Entity directly?
  ✗ Exposes DB fields you don't want public (PasswordHash, etc.)
  ✗ Causes circular reference in serialization (User → Orders → User → ...)
  ✗ Tight coupling — DB schema change breaks your API contract
```

```csharp
// DTOs/Request/CreateUserDto.cs
public class CreateUserDto
{
    [Required(ErrorMessage = "Name is required")]
    [MaxLength(100)]
    public string Name { get; set; }

    [Required]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    public string Email { get; set; }

    [Required]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters")]
    public string Password { get; set; }
}
```

```csharp
// DTOs/Response/UserResponseDto.cs
// Only expose what the client needs — password is excluded
public class UserResponseDto
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }
}
```

**Mapping entity → DTO (two approaches):**

```csharp
// Approach 1: Manual mapping (simple, explicit)
var dto = new UserResponseDto
{
    Id = user.Id,
    Name = user.Name,
    Email = user.Email,
    CreatedAt = user.CreatedAt,
    IsActive = user.IsActive
};

// Approach 2: AutoMapper (if project uses it)
// Configured once in a MappingProfile, then used like:
var dto = _mapper.Map<UserResponseDto>(user);
```

---

## 5. DbContext — Database Bridge

`AppDbContext` is EF Core's bridge between your C# classes and the database.

```csharp
// Data/AppDbContext.cs
public class AppDbContext : DbContext
{
    // Constructor — receives options (connection string etc.) via DI
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // Each DbSet = one table in the DB
    public DbSet<User> Users { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<Product> Products { get; set; }

    // Fluent API — configure relationships, constraints, indexes
    // Preferred over DataAnnotations on the entity
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Configure User table
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");                          // explicit table name
            entity.HasKey(u => u.Id);
            entity.Property(u => u.Email).IsRequired().HasMaxLength(255);
            entity.HasIndex(u => u.Email).IsUnique();        // unique constraint

            // One User → Many Orders
            entity.HasMany(u => u.Orders)
                  .WithOne(o => o.User)
                  .HasForeignKey(o => o.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToTable("orders");
            entity.Property(o => o.Amount).HasColumnType("decimal(18,2)");
        });
    }
}
```

**EF Core workflow when you change the model:**

```bash
# 1. Create migration (generates SQL change script)
dotnet ef migrations add AddUserPhoneNumber

# 2. Apply migration to DB
dotnet ef database update

# 3. If you messed up, roll back
dotnet ef database update PreviousMigrationName

# 4. Remove last migration (if not yet applied to DB)
dotnet ef migrations remove
```

---

## 6. Repositories — Data Access Layer

Repositories **only do DB queries** — no business logic. They hide EF Core details from the rest of the app.

```csharp
// Repositories/Interfaces/IUserRepository.cs
public interface IUserRepository
{
    Task<IEnumerable<User>> GetAllAsync();
    Task<User?> GetByIdAsync(int id);
    Task<User?> GetByEmailAsync(string email);
    Task<User> CreateAsync(User user);
    Task<User> UpdateAsync(User user);
    Task DeleteAsync(int id);
    Task<bool> ExistsAsync(int id);
}
```

```csharp
// Repositories/UserRepository.cs
public class UserRepository : IUserRepository
{
    private readonly AppDbContext _context;

    // DI injects AppDbContext here
    public UserRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<User>> GetAllAsync()
    {
        return await _context.Users
            .Where(u => u.IsActive)           // filter
            .OrderBy(u => u.Name)             // sort
            .ToListAsync();                   // async execute
    }

    public async Task<User?> GetByIdAsync(int id)
    {
        return await _context.Users
            .Include(u => u.Orders)           // JOIN with orders (eager loading)
            .FirstOrDefaultAsync(u => u.Id == id);
        //   ↑ returns null if not found, unlike First() which throws
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        return await _context.Users
            .FirstOrDefaultAsync(u => u.Email == email.ToLower());
    }

    public async Task<User> CreateAsync(User user)
    {
        _context.Users.Add(user);
        await _context.SaveChangesAsync(); // commits to DB
        return user;                       // user.Id is now populated by DB
    }

    public async Task<User> UpdateAsync(User user)
    {
        _context.Users.Update(user);       // marks entity as modified
        await _context.SaveChangesAsync();
        return user;
    }

    public async Task DeleteAsync(int id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user != null)
        {
            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> ExistsAsync(int id)
    {
        return await _context.Users.AnyAsync(u => u.Id == id);
    }
}
```

**Repository vs Direct DbContext:**

```
Repository       → abstracts DB queries, easier to test (mock the interface)
Direct DbContext → simpler for small projects, but harder to test and maintain
```

---

## 7. Services — Business Logic Layer

Services contain **business rules** and **orchestration**. They depend on repository interfaces.

```csharp
// Services/Interfaces/IUserService.cs
public interface IUserService
{
    Task<IEnumerable<UserResponseDto>> GetAllUsersAsync();
    Task<UserResponseDto> GetUserByIdAsync(int id);
    Task<UserResponseDto> CreateUserAsync(CreateUserDto dto);
    Task<UserResponseDto> UpdateUserAsync(int id, UpdateUserDto dto);
    Task DeleteUserAsync(int id);
}
```

```csharp
// Services/UserService.cs
public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;

    public UserService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<IEnumerable<UserResponseDto>> GetAllUsersAsync()
    {
        var users = await _userRepository.GetAllAsync();

        // Map entities → DTOs (manual mapping)
        return users.Select(u => new UserResponseDto
        {
            Id = u.Id,
            Name = u.Name,
            Email = u.Email,
            CreatedAt = u.CreatedAt,
            IsActive = u.IsActive
        });
    }

    public async Task<UserResponseDto> GetUserByIdAsync(int id)
    {
        var user = await _userRepository.GetByIdAsync(id);

        // Business rule: throw if not found
        // This exception is caught by global middleware and returns 404
        if (user == null)
            throw new NotFoundException($"User with ID {id} not found");

        return new UserResponseDto
        {
            Id = user.Id,
            Name = user.Name,
            Email = user.Email,
            CreatedAt = user.CreatedAt,
            IsActive = user.IsActive
        };
    }

    public async Task<UserResponseDto> CreateUserAsync(CreateUserDto dto)
    {
        // Business rule: email must be unique
        var existing = await _userRepository.GetByEmailAsync(dto.Email);
        if (existing != null)
            throw new ConflictException("Email already in use");

        // Map DTO → Entity
        var user = new User
        {
            Name = dto.Name,
            Email = dto.Email.ToLower(),                 // normalize
            PasswordHash = HashPassword(dto.Password),  // hash password
            CreatedAt = DateTime.UtcNow
        };

        var created = await _userRepository.CreateAsync(user);

        return new UserResponseDto
        {
            Id = created.Id,
            Name = created.Name,
            Email = created.Email,
            CreatedAt = created.CreatedAt,
            IsActive = created.IsActive
        };
    }

    public async Task DeleteUserAsync(int id)
    {
        // Business rule: check existence before delete
        var exists = await _userRepository.ExistsAsync(id);
        if (!exists)
            throw new NotFoundException($"User with ID {id} not found");

        await _userRepository.DeleteAsync(id);
    }

    private string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password);
    }
}
```

**Service vs Repository responsibilities:**

```
Service:
  ✓ Business rules (email unique, user must be active, etc.)
  ✓ DTO ↔ Entity mapping
  ✓ Orchestrating multiple repos (e.g. create user + send email)
  ✓ Throwing meaningful business exceptions

Repository:
  ✓ SQL / EF Core queries
  ✓ SaveChangesAsync
  ✗ Never contains if/else business logic
```

---

## 8. Controllers — Request Handler

Controllers are **thin**. They receive the request, call a service, and return a response.

```csharp
// Controllers/UsersController.cs
[ApiController]                    // enables auto model validation, auto binding
[Route("api/[controller]")]        // route = api/users
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    // DI injects the service
    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    // GET api/users
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var users = await _userService.GetAllUsersAsync();
        return Ok(new ApiResponse<IEnumerable<UserResponseDto>>
        {
            Success = true,
            Data = users
        });
    }

    // GET api/users/5
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        // NotFoundException thrown in service → caught by middleware → returns 404
        var user = await _userService.GetUserByIdAsync(id);
        return Ok(new ApiResponse<UserResponseDto> { Success = true, Data = user });
    }

    // POST api/users
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserDto dto)
    {
        // [ApiController] auto-validates dto and returns 400 if invalid
        // So by the time we get here, dto is guaranteed valid
        var created = await _userService.CreateUserAsync(dto);
        return CreatedAtAction(                          // returns 201 Created
            nameof(GetById),
            new { id = created.Id },
            new ApiResponse<UserResponseDto> { Success = true, Data = created }
        );
    }

    // PUT api/users/5
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateUserDto dto)
    {
        var updated = await _userService.UpdateUserAsync(id, dto);
        return Ok(new ApiResponse<UserResponseDto> { Success = true, Data = updated });
    }

    // DELETE api/users/5
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _userService.DeleteUserAsync(id);
        return Ok(new ApiResponse<object> { Success = true, Message = "User deleted" });
    }
}
```

**Common return methods:**

| Method | HTTP Status | Use when |
|--------|-------------|----------|
| `Ok(data)` | 200 | Successful GET, PUT, DELETE |
| `CreatedAtAction(...)` | 201 | Successful POST (resource created) |
| `NoContent()` | 204 | Success with no response body |
| `BadRequest(error)` | 400 | Validation error |
| `NotFound(error)` | 404 | Resource not found |
| `Unauthorized()` | 401 | Not authenticated |
| `Forbid()` | 403 | Authenticated but not authorized |

---

## 9. Middleware — Cross-Cutting Concerns

Middleware wraps every request. The most important one you'll find in existing projects is **global exception handling**.

```csharp
// Middleware/ExceptionHandlingMiddleware.cs
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;  // points to the NEXT middleware
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);  // ← run everything downstream (controllers, services)
        }
        catch (NotFoundException ex)
        {
            // Business exception → 404
            _logger.LogWarning(ex.Message);
            await WriteErrorResponse(context, 404, ex.Message);
        }
        catch (ConflictException ex)
        {
            // Business exception → 409
            _logger.LogWarning(ex.Message);
            await WriteErrorResponse(context, 409, ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            await WriteErrorResponse(context, 401, ex.Message);
        }
        catch (Exception ex)
        {
            // Unexpected error → 500
            _logger.LogError(ex, "Unhandled exception");
            await WriteErrorResponse(context, 500, "An unexpected error occurred");
        }
    }

    private static async Task WriteErrorResponse(HttpContext context, int statusCode, string message)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var response = new ApiResponse<object>
        {
            Success = false,
            Message = message,
            Data = null
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
}
```

**How the middleware wraps the pipeline:**

```
Request comes in
     │
     ▼
ExceptionHandlingMiddleware.InvokeAsync()
  │  try {
  │       await _next(context)  ────────────────────────────────────────────────►
  │                                                                    Controller runs
  │                                                                    Service throws NotFoundException
  │  ◄──────────────────────────────────────────────────── exception bubbles back up
  │  } catch (NotFoundException) {
  │       WriteErrorResponse(404)
  │  }
  ▼
HTTP Response: 404 with JSON body
```

---

## 10. Filters — Action-Level Hooks

Filters are like mini-middleware but only run around controller actions.

```csharp
// Example: Custom ActionFilter for logging execution time
public class LogExecutionTimeFilter : IActionFilter
{
    private Stopwatch _stopwatch;
    private readonly ILogger<LogExecutionTimeFilter> _logger;

    public LogExecutionTimeFilter(ILogger<LogExecutionTimeFilter> logger)
    {
        _logger = logger;
    }

    // Runs BEFORE the action method
    public void OnActionExecuting(ActionExecutingContext context)
    {
        _stopwatch = Stopwatch.StartNew();
    }

    // Runs AFTER the action method
    public void OnActionExecuted(ActionExecutedContext context)
    {
        _stopwatch.Stop();
        _logger.LogInformation(
            "Action {Action} took {ElapsedMs}ms",
            context.ActionDescriptor.DisplayName,
            _stopwatch.ElapsedMilliseconds
        );
    }
}
```

```csharp
// Apply globally to all controllers in Program.cs:
builder.Services.AddControllers(options =>
{
    options.Filters.Add<LogExecutionTimeFilter>();
});

// Or apply to one controller or action:
[ServiceFilter(typeof(LogExecutionTimeFilter))]
public class UsersController : ControllerBase { }
```

**Filter execution order:**

```
→ Authorization Filter  (e.g. [Authorize])
→ Resource Filter
→ Model Binding
→ Action Filter — OnActionExecuting  ← your custom logic runs here
→ ACTION METHOD RUNS
→ Action Filter — OnActionExecuted   ← and here after
→ Result Filter
→ Response sent
```

---

## 11. Dependency Injection — Wiring It All Together

DI is how every class gets its dependencies. You **never use `new`** for services in .NET.

```csharp
// Program.cs — registration
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IUserService, UserService>();

// At runtime, .NET resolves this chain automatically:
//
//  UsersController needs IUserService
//      → creates UserService
//          → UserService needs IUserRepository
//              → creates UserRepository
//                  → UserRepository needs AppDbContext
//                      → creates AppDbContext (already registered)
```

**Lifetime rules — the most common source of bugs:**

```
Scoped    → one instance per HTTP request
            Use for: DbContext, Repositories, Services

Singleton → one instance for entire app lifetime
            Use for: caches, config helpers, HttpClient factory
            ⚠️ Never inject a Scoped service into a Singleton!

Transient → new instance every time it's requested
            Use for: lightweight, stateless utilities
```

**What happens when you forget to register a service:**

```
System.InvalidOperationException:
  Unable to resolve service for type 'IUserService'
  while attempting to activate 'UsersController'
```

→ Go to `Program.cs` and add `builder.Services.AddScoped<IUserService, UserService>();`

---

## 12. Configuration — appsettings.json

Configuration values are stored in `appsettings.json` and accessed via `IConfiguration` or `IOptions<T>`.

```json
// appsettings.json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=myapp;Username=postgres;Password=secret"
  },
  "JwtSettings": {
    "Secret": "your-super-secret-key-min-32-chars",
    "ExpiryInMinutes": 60,
    "Issuer": "MyApp"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

```csharp
// Strongly typed config — preferred way
// Common/Settings/JwtSettings.cs
public class JwtSettings
{
    public string Secret { get; set; }
    public int ExpiryInMinutes { get; set; }
    public string Issuer { get; set; }
}

// Register in Program.cs
builder.Services.Configure<JwtSettings>(
    builder.Configuration.GetSection("JwtSettings"));

// Inject and use in any service
public class AuthService
{
    private readonly JwtSettings _jwtSettings;

    public AuthService(IOptions<JwtSettings> options)
    {
        _jwtSettings = options.Value;
    }
}
```

**Configuration hierarchy (who wins):**

```
appsettings.json
  ← appsettings.Development.json   (local dev overrides)
      ← Environment Variables       (server/production overrides)
          ← User Secrets             (sensitive local values, never committed to git)
```

```bash
# Set user secret locally (never goes in git)
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;..."
```

---

## 13. Full Request Flow — End to End

**Example:** `POST /api/users` with body `{ "name": "John", "email": "john@example.com", "password": "pass1234" }`

```
1. HTTP Request arrives at Kestrel (web server)

2. Program.cs middleware pipeline starts (top → down):
   → ExceptionHandlingMiddleware.InvokeAsync() starts — wraps everything in try/catch
   → UseHttpsRedirection — redirects if HTTP
   → UseAuthentication — no JWT on this endpoint, skipped
   → UseAuthorization — no [Authorize] attribute, skipped
   → MapControllers — routing begins

3. Routing:
   POST /api/users → UsersController.Create()

4. Model Binding:
   JSON body → CreateUserDto { Name="John", Email="john@example.com", Password="pass1234" }

5. Validation ([ApiController] kicks in):
   All attributes on CreateUserDto pass → continue
   (If [Required] fails → auto 400 before reaching action)

6. Action Filters (if any):
   OnActionExecuting() runs

7. Controller.Create() runs:
   → calls _userService.CreateUserAsync(dto)

8. UserService.CreateUserAsync(dto):
   → calls _userRepository.GetByEmailAsync("john@example.com")
   → email not found → continue
   → creates User entity, hashes password
   → calls _userRepository.CreateAsync(user)

9. UserRepository.CreateAsync(user):
   → _context.Users.Add(user)
   → _context.SaveChangesAsync() → SQL INSERT runs → PostgreSQL stores the row
   → returns user with populated Id

10. Response travels back:
    UserService → returns UserResponseDto
    Controller → return CreatedAtAction(201, dto)
    Action Filter → OnActionExecuted() runs
    ExceptionHandlingMiddleware → no exception, passes through
    Kestrel → sends HTTP 201 response with JSON body
```

---

## 14. Common Patterns You'll See in Existing Projects

### ApiResponse Wrapper

```csharp
// Common/ApiResponse.cs
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public T? Data { get; set; }
    public List<string>? Errors { get; set; }
}

// Usage
return Ok(new ApiResponse<UserResponseDto>
{
    Success = true,
    Message = "User retrieved",
    Data = userDto
});
```

### Custom Exceptions

```csharp
// Common/Exceptions/NotFoundException.cs
public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}

// Common/Exceptions/ConflictException.cs
public class ConflictException : Exception
{
    public ConflictException(string message) : base(message) { }
}

// Pattern: throw in service → caught by middleware → returns correct HTTP status
throw new NotFoundException("User not found");     // → 404
throw new ConflictException("Email already used"); // → 409
```

### Repository Pattern with Generic Base

```csharp
// Some projects use a generic repository for shared CRUD:
public interface IGenericRepository<T> where T : class
{
    Task<IEnumerable<T>> GetAllAsync();
    Task<T?> GetByIdAsync(int id);
    Task<T> CreateAsync(T entity);
    Task<T> UpdateAsync(T entity);
    Task DeleteAsync(int id);
}

// Then specific repositories extend it:
public interface IUserRepository : IGenericRepository<User>
{
    Task<User?> GetByEmailAsync(string email); // extra method
}
```

---

## 15. Quick Navigation Cheat Sheet

| You want to... | Go to... |
|---|---|
| Understand how app boots | `Program.cs` |
| Find all registered services | `Program.cs` → `builder.Services.Add...` |
| Find middleware order | `Program.cs` → `app.Use...` |
| Find routes / endpoints | Controller files → `[Route]`, `[HttpGet/Post/...]` |
| Find business logic | `Services/` folder |
| Find DB queries | `Repositories/` folder |
| Find DB schema / relationships | `Data/AppDbContext.cs` → `OnModelCreating` |
| Find DB history/changes | `Data/Migrations/` folder |
| Find validation rules | DTO files in `DTOs/Request/` |
| Find error handling | `Middleware/ExceptionHandlingMiddleware.cs` |
| Find app config / secrets | `appsettings.json` + `appsettings.Development.json` |
| Find custom exceptions | `Common/Exceptions/` |
| Understand response format | `Common/ApiResponse.cs` |
| Debug a 500 error | Start at middleware, check `ILogger` output |
| Debug a 404 error | Check route attributes + service NotFoundException |
| Debug a 400 error | Check DTO validation attributes |
| Debug injection error | Check `Program.cs` registrations |

---

> **Golden rule:** Every HTTP request enters at `Program.cs` middleware → routes to a Controller → delegates to a Service → queries via a Repository → hits the DB → bubbles back up the same path as a response.
> 
> Follow that chain and you can debug anything.
