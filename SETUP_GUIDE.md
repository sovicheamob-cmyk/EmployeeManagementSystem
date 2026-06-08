# Employee Management System - Complete Setup Guide

## 🚀 Project Overview

This is a **production-grade Employee Management System** built with:
- **.NET 10** with ASP.NET Core Web API
- **PostgreSQL** database
- **JWT Authentication** (stateless)
- **Role-Based Authorization**
- **Clean Architecture** with Service/Repository pattern
- **Global Exception Handling**
- **API Documentation** (Swagger)

---

## 📦 System Architecture

```
ARCHITECTURE LAYERS:
┌─────────────────────────────────────────────┐
│         HTTP Clients (Frontend)             │
├─────────────────────────────────────────────┤
│     Controllers (HTTP Routing)              │
├─────────────────────────────────────────────┤
│  Services (Business Logic)                  │
├─────────────────────────────────────────────┤
│  Repositories (Data Access)                 │
├─────────────────────────────────────────────┤
│  DbContext (EF Core)                        │
├─────────────────────────────────────────────┤
│        PostgreSQL Database                  │
└─────────────────────────────────────────────┘

REQUEST FLOW:
Client → [Middleware] → Controller → Service → Repository → Database
Database → Repository → Service → Controller → [Middleware] → Client
```

---

## 🛠️ Prerequisites

1. **.NET 10 SDK**
   - Download from: https://dotnet.microsoft.com/download
   - Verify: `dotnet --version`

2. **PostgreSQL 12+**
   - Download from: https://www.postgresql.org/download/
   - Default port: 5432
   - Default user: postgres

3. **Git** (optional, for version control)

4. **VS Code or Visual Studio** (already have it)

---

## 📦 NuGet Packages Used

| Package | Version | Purpose |
|---------|---------|---------|
| **Microsoft.AspNetCore.OpenApi** | 10.0.8 | OpenAPI/Swagger documentation |
| **Swashbuckle.AspNetCore** | 6.4.7 | Swagger UI for API docs |
| **Microsoft.EntityFrameworkCore** | 10.0.0 | ORM (Object-Relational Mapping) |
| **Microsoft.EntityFrameworkCore.Tools** | 10.0.0 | EF Core CLI tools (migrations) |
| **Npgsql.EntityFrameworkCore.PostgreSQL** | 10.0.0 | PostgreSQL provider for EF Core |
| **System.IdentityModel.Tokens.Jwt** | 8.0.2 | JWT token creation & validation |
| **Microsoft.AspNetCore.Authentication.JwtBearer** | 10.0.8 | JWT Bearer authentication middleware |
| **BCrypt.Net-Core** | 1.6.0 | Password hashing library |

### Package Purposes Explained

🔐 **Authentication & Security**
- `System.IdentityModel.Tokens.Jwt` - Creates and validates JWT tokens
- `Microsoft.AspNetCore.Authentication.JwtBearer` - Middleware for JWT validation
- `BCrypt.Net-Core` - Secure password hashing with salt

📊 **Database & ORM**
- `Microsoft.EntityFrameworkCore` - Core ORM framework
- `Npgsql.EntityFrameworkCore.PostgreSQL` - PostgreSQL-specific EF Core provider
- `Microsoft.EntityFrameworkCore.Tools` - Migration commands (add-migration, update-database)

📚 **Documentation & API**
- `Microsoft.AspNetCore.OpenApi` - OpenAPI spec generation
- `Swashbuckle.AspNetCore` - Interactive Swagger UI

### How Packages Are Used

**Example: Creating JWT Token**
```csharp
using System.IdentityModel.Tokens.Jwt;  // From System.IdentityModel.Tokens.Jwt

var tokenHandler = new JwtSecurityTokenHandler();
var token = tokenHandler.CreateToken(tokenDescriptor);
```

**Example: Hashing Password**
```csharp
using BC = BCrypt.Net.BCrypt;  // From BCrypt.Net-Core

string hash = BC.HashPassword(plainPassword, workFactor: 11);
bool isValid = BC.Verify(plainPassword, hash);
```

**Example: Database Access**
```csharp
using Microsoft.EntityFrameworkCore;  // From Microsoft.EntityFrameworkCore

var employee = await context.Employees.FirstOrDefaultAsync(e => e.Id == 1);
```

---

## ⚙️ Setup Instructions

### Step 1: PostgreSQL Database Setup

**Windows (using pgAdmin or psql):**

```sql
-- Open PostgreSQL Command Line (psql)
-- Login: psql -U postgres

-- Create database
CREATE DATABASE EmployeeManagementDb;

-- Create user (optional, if you want to use different user)
CREATE USER emp_user WITH PASSWORD 'emp_password';

-- Grant privileges
GRANT ALL PRIVILEGES ON DATABASE EmployeeManagementDb TO emp_user;

-- Connect to database
\c EmployeeManagementDb

-- Verify connection
SELECT 1;
```

**Connection String Examples:**
```
# Using default postgres user
Host=localhost;Port=5432;Database=EmployeeManagementDb;Username=postgres;Password=your_password

# Using custom user
Host=localhost;Port=5432;Database=EmployeeManagementDb;Username=emp_user;Password=emp_password
```

### Step 2: Update Connection String

Edit `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=EmployeeManagementDb;Username=postgres;Password=your_password"
  },
  "JwtSettings": {
    "SecretKey": "your-super-secret-key-min-32-chars-long-123456",
    "Issuer": "EmployeeManagementApi",
    "Audience": "EmployeeManagementClient",
    "ExpirationMinutes": 60
  }
}
```

**IMPORTANT:** Change the password to your PostgreSQL password!

### Step 3: Restore NuGet Packages

```bash
dotnet restore
```

### Step 4: Run Migrations & Start Application

```bash
# Option 1: Run and automatically migrate
dotnet run

# Option 2: Manually create migrations
dotnet ef migrations add InitialCreate
dotnet ef database update
```

The application will:
1. Create all database tables
2. Create default users (seed data)
3. Start on: https://localhost:5001

---

## 📚 Default Users (for testing)

Created automatically when database initializes:

| Role | Email | Password |
|------|-------|----------|
| SuperAdmin | admin@example.com | Admin@123 |
| Admin | manager@example.com | Manager@123 |
| User | user@example.com | User@123 |

---

## 🧪 Testing the API

### Using Swagger UI (Easiest)

1. Start application: `dotnet run`
2. Open browser: https://localhost:5001
3. Swagger documentation opens automatically

### Using cURL or Postman

#### 1. **Login** (Get JWT Token)

```bash
POST /api/auth/login
Content-Type: application/json

{
  "email": "admin@example.com",
  "password": "Admin@123"
}
```

**Response:**
```json
{
  "status": "Success",
  "code": 200,
  "message": "Login successful",
  "data": {
    "userId": 1,
    "firstName": "System",
    "lastName": "Admin",
    "email": "admin@example.com",
    "role": "SuperAdmin",
    "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "expiresIn": 3600
  }
}
```

#### 2. **Get Current User Profile**

```bash
GET /api/profile
Authorization: Bearer {token_from_login}
```

#### 3. **Create Employee** (Admin Only)

```bash
POST /api/employees
Authorization: Bearer {admin_token}
Content-Type: application/json

{
  "employeeCode": "EMP001",
  "firstName": "John",
  "lastName": "Doe",
  "email": "john@company.com",
  "department": "IT",
  "position": "Senior Developer",
  "salary": 100000,
  "joinDate": "2024-01-15"
}
```

#### 4. **Get All Employees** (Paginated)

```bash
GET /api/employees?pageNumber=1&pageSize=10
Authorization: Bearer {token}
```

#### 5. **Get Employees by Department**

```bash
GET /api/employees/department/IT
Authorization: Bearer {token}
```

#### 6. **Get System Headcount**

```bash
GET /api/employees/stats/headcount
Authorization: Bearer {token}
```

#### 7. **Create User** (SuperAdmin Only)

```bash
POST /api/users
Authorization: Bearer {superadmin_token}
Content-Type: application/json

{
  "firstName": "New",
  "lastName": "Admin",
  "email": "newadmin@example.com",
  "password": "SecurePass123",
  "role": "Admin"
}
```

---

## 🔐 Authorization Rules

```
[Authorize]
├─ Login, Register: NO authorization needed
├─ GET /api/profile: Any authenticated user
├─ GET /api/employees: Any authenticated user
├─ GET /api/employees/{id}: Any authenticated user
├─ GET /api/employees/department/{dept}: Any authenticated user
│
├─ [Authorize(Roles = "Admin,SuperAdmin")]
│  ├─ POST /api/employees: Create
│  ├─ PUT /api/employees/{id}: Update
│  ├─ DELETE /api/employees/{id}: Delete
│
└─ [Authorize(Roles = "SuperAdmin")]
   ├─ GET /api/users: List all users
   ├─ GET /api/users/{id}: Get user
   ├─ POST /api/users: Create user
   ├─ PUT /api/users/{id}: Update user
   ├─ DELETE /api/users/{id}: Delete user
   └─ GET /api/users/role/{role}: Get by role
```

---

## 📁 Project Structure

```
EmployeeManagementSystem/
├── Controllers/
│   ├── AuthController.cs          # Login, Register
│   ├── EmployeesController.cs     # Employee CRUD
│   ├── UsersController.cs         # User management (SuperAdmin)
│   └── ProfileController.cs       # Current user profile
│
├── Services/
│   ├── Interfaces/
│   │   ├── IAuthService.cs
│   │   ├── IEmployeeService.cs
│   │   └── IUserService.cs
│   └── Implementations/
│       ├── AuthService.cs         # JWT, BCrypt, Registration
│       ├── EmployeeService.cs
│       └── UserService.cs
│
├── Repositories/
│   ├── Interfaces/
│   │   ├── IGenericRepository.cs  # Base CRUD
│   │   ├── IUserRepository.cs
│   │   └── IEmployeeRepository.cs
│   └── Implementations/
│       ├── GenericRepository.cs
│       ├── UserRepository.cs
│       └── EmployeeRepository.cs
│
├── Models/
│   ├── User.cs                    # Database entity
│   ├── Employee.cs                # Database entity
│   └── UserRole.cs                # Enum
│
├── DTOs/
│   ├── AuthDtos.cs                # LoginRequestDto, RegisterRequestDto, etc.
│   └── EmployeeDtos.cs            # EmployeeDto, CreateEmployeeRequestDto, etc.
│
├── Data/
│   └── ApplicationDbContext.cs     # EF Core DbContext
│
├── Middleware/
│   └── GlobalExceptionHandlerMiddleware.cs
│
├── Common/
│   ├── ApiResponse.cs             # Standard response wrapper
│   └── Constants/
│       └── MessageConstants.cs     # All messages
│
├── Program.cs                      # Startup configuration
├── appsettings.json               # Configuration
└── EmployeeManagementSystem.csproj # Project file with NuGet packages
```

---

## 🔄 Request/Response Flow Example

### Create Employee Request

```
1. CLIENT
   POST /api/employees
   Authorization: Bearer {token}
   {
     "employeeCode": "EMP001",
     "firstName": "John",
     "lastName": "Doe",
     ...
   }

2. ASP.NET CORE
   ├─ Global Exception Middleware (wraps request)
   ├─ Authentication Middleware (validates token)
   └─ Authorization Middleware (checks role)

3. CONTROLLER (EmployeesController.Create)
   ├─ Receives CreateEmployeeRequestDto
   └─ Calls employeeService.CreateEmployeeAsync(request)

4. SERVICE (EmployeeService.CreateEmployeeAsync)
   ├─ Validates employee code is unique
   ├─ Creates Employee model from DTO
   └─ Calls employeeRepository.AddAsync(employee)

5. REPOSITORY (EmployeeRepository)
   ├─ Extends GenericRepository<Employee>
   └─ Calls dbSet.AddAsync(entity)

6. EF CORE DbContext
   ├─ Adds entity to context
   ├─ Marks as "Added" state
   └─ Returns to repository

7. SERVICE (returns ApiResponse)
   └─ Returns: ApiResponse<EmployeeDto>.Success(dto, message)

8. CONTROLLER
   ├─ Gets ApiResponse from service
   └─ Returns: CreatedAtAction(nameof(GetById), ..., result)

9. MIDDLEWARE
   ├─ Exception handler (no exception, so passes through)
   └─ Returns response

10. CLIENT RECEIVES
    {
      "status": "Success",
      "code": 200,
      "message": "Employee created successfully",
      "data": { created employee }
    }
```

---

## 🛡️ Security Best Practices Implemented

✅ **Password Security**
- Passwords hashed with BCrypt (slow, salt-based)
- Never stored as plain text
- Minimum 8 characters enforced

✅ **JWT Authentication**
- Stateless tokens (no session storage needed)
- Digitally signed (can't be modified)
- Expires after 60 minutes
- Contains user role for authorization

✅ **Authorization**
- Role-based access control (RBAC)
- Different endpoints have different role requirements
- Employee operations: Admin/SuperAdmin
- User management: SuperAdmin only

✅ **Data Validation**
- Client-side: DataAnnotations ([Required], [EmailAddress], etc.)
- Server-side: Business logic validation
- Consistent error responses

✅ **Error Handling**
- Global exception middleware
- No stack traces exposed to clients
- Consistent ApiResponse format
- Proper HTTP status codes

✅ **Soft Deletes**
- Employees marked inactive instead of deleted
- Preserves data for audit trails
- Can be reactivated if needed

---

## 🚨 Troubleshooting

### "Connection refused" Error
```
Problem: Can't connect to PostgreSQL
Solution:
1. Check PostgreSQL is running
2. Verify connection string in appsettings.json
3. Check port (default 5432)
4. Check username/password
```

### "Migration pending" Error
```
Problem: Database schema out of sync
Solution:
dotnet ef database update
```

### "Unauthorized (401)" Error
```
Problem: JWT token invalid or missing
Solution:
1. Include Authorization header: Authorization: Bearer {token}
2. Check token hasn't expired
3. Login again to get new token
```

### "Forbidden (403)" Error
```
Problem: User doesn't have required role
Solution:
1. Login with appropriate role
2. Admin can't access SuperAdmin endpoints
3. User can't create employees (Admin only)
```

---

## 📖 Key Concepts Explained

### **1. Dependency Injection (DI)**
```csharp
// Program.cs registers services
builder.Services.AddScoped<IEmployeeService, EmployeeService>();

// Controller receives dependency
public EmployeesController(IEmployeeService employeeService)
{
    _employeeService = employeeService;
}

// Benefits:
// - Loose coupling (depends on interface)
// - Easy to test (can mock IEmployeeService)
// - Automatic instance creation
```

### **2. Repository Pattern**
```csharp
// Abstracts database access
var employee = await repository.GetByIdAsync(5);
var employees = await repository.GetPagedAsync(1, 10);

// Benefits:
// - Database logic separate from business logic
// - Easy to test (mock repository)
// - Can swap database provider
```

### **3. Service Pattern**
```csharp
// Contains business logic
var response = await service.CreateEmployeeAsync(request);

// Benefits:
// - Controllers stay thin
// - Reusable business logic
// - Easy to test
```

### **4. DTO Pattern**
```csharp
// CreateEmployeeRequestDto: For input
// EmployeeDto: For output
// Separation: Model ≠ DTO

// Benefits:
// - Security (don't expose all model fields)
// - Flexibility (API format ≠ DB format)
// - Validation (DataAnnotations on DTOs)
```

### **5. JWT Authentication**
```csharp
// Token contains claims
var claims = new List<Claim>
{
    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
    new Claim(ClaimTypes.Role, user.Role.ToString())
};

// Server validates token signature
// Client can't modify without invalidating

// Benefits:
// - Stateless (no session server)
// - Scalable (works across servers)
// - Standard (JWT is industry standard)
```

---

## 🎓 Learning Outcomes

After completing this project, you should understand:

✅ **Backend Architecture**
- Layered architecture (Controllers → Services → Repositories)
- Separation of concerns
- Dependency injection

✅ **Database Design**
- Entity relationships
- Indexes for performance
- Soft deletes pattern

✅ **Authentication & Authorization**
- JWT tokens (format, validation, claims)
- BCrypt password hashing
- Role-based access control

✅ **API Best Practices**
- RESTful design
- Standard response format
- HTTP status codes
- Error handling

✅ **ASP.NET Core**
- Middleware pipeline
- Dependency injection
- EF Core ORM
- Configuration

---

## 📚 Additional Resources

- **JWT.io**: https://jwt.io/ (decode/analyze tokens)
- **API Status Codes**: https://httpwg.org/specs/rfc7231.html#status.codes
- **Entity Framework Core**: https://docs.microsoft.com/ef/core/
- **ASP.NET Core Security**: https://docs.microsoft.com/aspnet/core/security/
- **PostgreSQL Docs**: https://www.postgresql.org/docs/

---

## 🎉 You're Ready!

1. ✅ Setup PostgreSQL
2. ✅ Update connection string
3. ✅ Run `dotnet run`
4. ✅ Visit https://localhost:5001/swagger
5. ✅ Login with admin@example.com / Admin@123
6. ✅ Test API endpoints!

**Happy coding! 🚀**
