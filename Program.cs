using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Serilog;
using Serilog.Core;
using EmployeeManagementSystem.Data;
using EmployeeManagementSystem.Models;
using EmployeeManagementSystem.Repositories.Interfaces;
using EmployeeManagementSystem.Repositories.Implementations;
using EmployeeManagementSystem.Services.Interfaces;
using EmployeeManagementSystem.Services.Implementations;
using EmployeeManagementSystem.Middleware;
using EmployeeManagementSystem.Common;
using EmployeeManagementSystem.Common.Constants;
using EmployeeManagementSystem.Filters;

// ========== CONFIGURE SERILOG LOGGING ==========
// Configure Serilog to log application startup/shutdown and errors
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File(
        "logs/application-.log",
        rollingInterval: RollingInterval.Day,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u3}] {Message:lj}{NewLine}{Exception}"
    )
    .CreateLogger();

try
{
    Log.Information("========== Application Starting ==========");
    
    // ========== BUILD PHASE: Configure Services ==========
    var builder = WebApplication.CreateBuilder(args);
    
    // Add Serilog to logging for application events
    builder.Host.UseSerilog(Log.Logger);


// ========== 1. ADD CONTROLLERS & API ENDPOINTS ==========
// Enables MVC routing, API controllers, etc.
builder.Services.AddControllers(options =>
{
    options.Filters.Add<ApiResponseValidationFilter>();
})
.ConfigureApiBehaviorOptions(options =>
{
    // Disable the built-in [ApiController] automatic 400 response.
    // Without this, ASP.NET Core short-circuits the pipeline before
    // our global filter runs and returns its own ValidationProblemDetails.
    options.SuppressModelStateInvalidFilter = true;

    // Factory that builds the response when ModelState is invalid.
    // This is the fallback for model-binding errors that occur before
    // action filters execute (e.g., malformed JSON, type coercion).
    // It ensures even these edge-cases return the project's ApiResponse format.
    options.InvalidModelStateResponseFactory = context =>
    {
        // Extract only the first validation error message.
        // Using FirstOrDefault keeps the response flat — one string only —
        // instead of the default array/dictionary of all errors.
        var firstError = context.ModelState
            .SelectMany(kvp => kvp.Value!.Errors)
            .Select(err => err.ErrorMessage)
            .FirstOrDefault();

        // Wrap the error in the project's standard ApiResponse<object>.
        // Status = "Failed", Code = 400, Data = null.
        // This guarantees every 400 response looks identical across the API.
        var response = ApiResponse<object>.Fail(
            400,
            firstError ?? MessageConstants.INVALID_REQUEST);

        // Return a BadRequestObjectResult so the HTTP status code is 400
        // and the body is our ApiResponse JSON.
        return new BadRequestObjectResult(response);
    };
});

// ========== 2. ADD SWAGGER / OPENAPI DOCUMENTATION ==========
// Generates interactive API documentation for Controller-based APIs
// Accessible at /swagger
// NOTE: Using Swashbuckle for Controllers (NOT Minimal API OpenAPI)
builder.Services.AddSwaggerGen();

// ========== 3. CONFIGURE DATABASE ==========
// Register DbContext with PostgreSQL provider
//
// WHY?
// - Connects application to PostgreSQL database
// - EF Core uses this to generate SQL
// - Connection string from appsettings.json
//
// CONNECTION STRING FORMAT:
// Host=localhost;Port=5432;Database=EmployeeManagementDb;Username=postgres;Password=your_password
// - Host: PostgreSQL server address
// - Port: PostgreSQL port (5432 is default)
// - Database: Database name to use
// - Username: PostgreSQL user (postgres is default)
// - Password: User's password
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string not configured");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

// ========== 4. REGISTER REPOSITORIES ==========
// Dependency Injection setup
//
// WHY?
// When a service requests IUserRepository,
// DI container creates UserRepository instance
// and injects its dependencies
//
// LIFETIME SCOPES:
// - Transient: New instance every time (stateless objects)
// - Scoped: One instance per HTTP request (DbContext)
// - Singleton: One instance for entire application (expensive to create)
//
// For repositories and services: Scoped (tied to request)

builder.Services.AddScoped<IGenericRepository<User>, GenericRepository<User>>();
builder.Services.AddScoped<IGenericRepository<Employee>, GenericRepository<Employee>>();

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IEmployeeRepository, EmployeeRepository>();

// ========== 5. REGISTER SERVICES ==========
// Services contain business logic
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IEmployeeService, EmployeeService>();
builder.Services.AddScoped<IUserService, UserService>();

// ========== 6. CONFIGURE JWT AUTHENTICATION ==========
// Sets up JWT Bearer token validation
//
// WHY?
// - Validates tokens in Authorization header
// - Extracts claims from token
// - Populates User.Claims for controllers
// - Enables [Authorize] attribute
//
// FLOW:
// Client sends: Authorization: Bearer {token}
// ↓
// JwtBearerHandler validates signature
// ↓
// If valid: Extracts claims
// ↓
// Controller can access User.FindFirst(ClaimTypes.NameIdentifier)
//
// TOKEN VALIDATION:
// - Checks signature (not tampered with)
// - Checks expiration time
// - Checks issuer and audience match

var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"]
    ?? throw new InvalidOperationException("JWT secret key not configured");
var issuer = jwtSettings["Issuer"] ?? "EmployeeManagementApi";
var audience = jwtSettings["Audience"] ?? "EmployeeManagementClient";

var keyBytes = Encoding.UTF8.GetBytes(secretKey);
var securityKey = new SymmetricSecurityKey(keyBytes);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,  // Verify token wasn't modified
            IssuerSigningKey = securityKey,   // Use this key to validate signature
            ValidateIssuer = true,            // Check issuer matches
            ValidIssuer = issuer,
            ValidateAudience = true,          // Check audience matches
            ValidAudience = audience,
            ValidateLifetime = true,          // Check if token expired
            ClockSkew = TimeSpan.Zero         // Don't allow extra time after expiration
        };

        // Custom authentication failure handler
        // Returns proper ApiResponse format for 401 errors
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                context.Response.ContentType = "application/json";
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;

                var response = new ApiResponse<object>();

                if (context.Exception is SecurityTokenExpiredException)
                {
                    response = ApiResponse<object>.Fail(
                        401,
                        MessageConstants.TOKEN_EXPIRED
                    );
                }
                else
                {
                    response = ApiResponse<object>.Fail(
                        401,
                        MessageConstants.UNAUTHORIZED
                    );
                }

                var json = System.Text.Json.JsonSerializer.Serialize(response, new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
                });

                return context.Response.WriteAsync(json);
            },

            OnChallenge = context =>
            {
                context.HandleResponse();
                context.Response.ContentType = "application/json";
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;

                var response = ApiResponse<object>.Fail(
                    401,
                    MessageConstants.UNAUTHORIZED
                );

                var json = System.Text.Json.JsonSerializer.Serialize(response, new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
                });

                return context.Response.WriteAsync(json);
            }
        };
    });

// ========== 7. CONFIGURE AUTHORIZATION ==========
// Enables [Authorize] and [Authorize(Roles = "...")] attributes
builder.Services.AddAuthorization(options =>
{
    // Custom policy for forbidden responses
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// ========== 8. CONFIGURE CORS (if needed) ==========
// Allows frontend from different domain to call API
// Replace with your actual frontend URL in production
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// ========== 9. ADD LOGGING ==========
// For debugging and monitoring
builder.Services.AddLogging();

// ========== BUILD PHASE: Create Application ==========
var app = builder.Build();

// ========== MIDDLEWARE PIPELINE CONFIGURATION ==========
// Order matters! Middleware executes in registration order
//
// PIPELINE:
// Request →
// CORS Middleware →
// Exception Middleware →
// Authentication Middleware →
// Authorization Middleware →
// Routing Middleware →
// Endpoint (Controller) →
// Response
//

// ========== 1. ENABLE SWAGGER IN DEVELOPMENT ==========
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Employee Management API v1");
        options.RoutePrefix = string.Empty;  // Swagger at root
    });
}

// ========== 2. REDIRECT HTTP TO HTTPS ==========
app.UseHttpsRedirection();

// ========== 3. ENABLE CORS ==========
app.UseCors("AllowAll");

// ========== 3B. HTTP REQUEST/RESPONSE LOGGING ==========
// Logs all HTTP requests and responses to file
app.UseMiddleware<HttpLoggingMiddleware>();

// ========== 4. GLOBAL EXCEPTION HANDLER MIDDLEWARE ==========
// Catches all exceptions and returns ApiResponse format
// Must be before other middleware (so it wraps everything)
app.UseMiddleware<GlobalExceptionHandlerMiddleware>();

// ========== 4B. AUTHORIZATION RESPONSE MIDDLEWARE ==========
// Handles 401/403 responses from auth/authz middleware
// Wraps them in ApiResponse format
// DISABLED FOR NOW - needs debugging to not intercept /api/auth endpoints
// app.UseMiddleware<AuthorizationResponseMiddleware>();

// ========== 5. AUTHENTICATION & AUTHORIZATION ==========
// Must be in this order:
// 1. Authentication - validates token
// 2. Authorization - checks permissions

app.UseAuthentication();  // Validate JWT token
app.UseAuthorization();   // Check user permissions

// ========== 6. MAP CONTROLLERS ==========
// Enables MVC routing to controllers
app.MapControllers();

// ========== 7. DATABASE INITIALIZATION ==========
// Create database and apply migrations if needed
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    
    try
    {
        // Apply pending migrations
        // If database doesn't exist, creates it
        // If migrations pending, applies them
        dbContext.Database.Migrate();
        
        // Seed default data (users, etc.)
        await SeedData(dbContext);
        
        Console.WriteLine("✓ Database initialized successfully");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"✗ Database initialization failed: {ex.Message}");
        throw;
    }
}

// ========== START APPLICATION ==========
app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "========== Application terminated unexpectedly ==========");
}
finally
{
    Log.Information("========== Application Stopping ==========");
    Log.CloseAndFlush();
}

// ========== SEED DATA FUNCTION ==========
// Creates default users if they don't exist
static async Task SeedData(ApplicationDbContext context)
{
    // Check if data already seeded
    if (await context.Users.AnyAsync())
        return;

    // Hash passwords using BCrypt
    var authService = new AuthService(null!, null!);  // We only use HashPassword, which is static-like
    
    // Create default users
    var users = new List<EmployeeManagementSystem.Models.User>
    {
        new EmployeeManagementSystem.Models.User
        {
            FirstName = "System",
            LastName = "Admin",
            Email = "admin@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
            Role = EmployeeManagementSystem.Models.UserRole.SuperAdmin,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        },
        new EmployeeManagementSystem.Models.User
        {
            FirstName = "Manager",
            LastName = "User",
            Email = "manager@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Manager@123"),
            Role = EmployeeManagementSystem.Models.UserRole.Admin,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        },
        new EmployeeManagementSystem.Models.User
        {
            FirstName = "Regular",
            LastName = "User",
            Email = "user@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("User@123"),
            Role = EmployeeManagementSystem.Models.UserRole.User,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        }
    };

    context.Users.AddRange(users);
    await context.SaveChangesAsync();

    Console.WriteLine("✓ Seed data created");
}

