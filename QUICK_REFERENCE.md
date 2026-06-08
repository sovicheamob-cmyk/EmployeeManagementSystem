# 🚀 Quick Reference Guide

## 📦 NuGet Packages at a Glance

| Package | Purpose |
|---------|---------|
| `Swashbuckle.AspNetCore` | API documentation (Swagger UI) |
| `Microsoft.EntityFrameworkCore` | Database ORM |
| `Npgsql.EntityFrameworkCore.PostgreSQL` | PostgreSQL support |
| `System.IdentityModel.Tokens.Jwt` | JWT token handling |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | JWT authentication |
| `BCrypt.Net-Core` | Password hashing |

---

## ⚡ Quick Start (5 minutes)

### 1️⃣ Setup PostgreSQL
```bash
# Create database
createdb EmployeeManagementDb

# Verify (should return 1)
psql -U postgres -d EmployeeManagementDb -c "SELECT 1;"
```

### 2️⃣ Update Connection String
Edit `appsettings.json`:
```json
"DefaultConnection": "Host=localhost;Port=5432;Database=EmployeeManagementDb;Username=postgres;Password=YOUR_PASSWORD"
```

### 3️⃣ Run Application
```bash
cd d:\DotNet\EmployeeManagementSystem
dotnet run
```

### 4️⃣ Test in Browser
```
https://localhost:5001/swagger
```

---

## 📋 API Endpoints Quick Reference

### 🔐 Authentication (No Auth Needed)
```
POST   /api/auth/register    Register new user
POST   /api/auth/login       Login user
```

### 👤 Profile (Auth Required)
```
GET    /api/profile          Get current user profile
```

### 👥 Employees (Auth Required)
```
GET    /api/employees                        List all (paginated)
GET    /api/employees/{id}                   Get by ID
GET    /api/employees?pageNumber=1&pageSize=10  Pagination
GET    /api/employees/department/{dept}     Filter by department
GET    /api/employees/stats/headcount       Total count
```

**Admin+ Only:**
```
POST   /api/employees                Create employee
PUT    /api/employees/{id}          Update employee
DELETE /api/employees/{id}          Delete employee
```

### 👨‍💼 Users (SuperAdmin Only)
```
GET    /api/users                   List all users
GET    /api/users/{id}              Get user by ID
GET    /api/users/role/{role}       Filter by role
POST   /api/users                   Create user
PUT    /api/users/{id}              Update user
DELETE /api/users/{id}              Delete user
```

---

## 🔐 Test Credentials

| Role | Email | Password |
|------|-------|----------|
| SuperAdmin | admin@example.com | Admin@123 |
| Admin | manager@example.com | Manager@123 |
| User | user@example.com | User@123 |

---

## 📊 Authorization Levels

```
READ (Any authenticated user)
├─ GET /api/profile
├─ GET /api/employees
├─ GET /api/employees/{id}
└─ GET /api/employees/department/{dept}

WRITE (Admin + SuperAdmin)
├─ POST /api/employees
├─ PUT /api/employees/{id}
└─ DELETE /api/employees/{id}

ADMIN (SuperAdmin Only)
├─ POST /api/users
├─ PUT /api/users/{id}
├─ DELETE /api/users/{id}
└─ GET /api/users/...
```

---

## 🔄 Request/Response Example

### Request
```bash
curl -X POST https://localhost:5001/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "admin@example.com",
    "password": "Admin@123"
  }'
```

### Response
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

### Using Token
```bash
curl -X GET https://localhost:5001/api/profile \
  -H "Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
```

---

## 📁 Key Files

| File | Purpose |
|------|---------|
| `Program.cs` | Application startup, DI setup |
| `appsettings.json` | Configuration (DB, JWT) |
| `Data/ApplicationDbContext.cs` | EF Core DbContext |
| `Models/` | Database entities |
| `DTOs/` | Request/Response objects |
| `Controllers/` | HTTP endpoints |
| `Services/` | Business logic |
| `Repositories/` | Data access layer |

---

## 🔧 Common Commands

```bash
# Run application
dotnet run

# Create migration
dotnet ef migrations add MigrationName

# Update database
dotnet ef database update

# Drop database
dotnet ef database drop

# Check status
dotnet build

# Run tests (if added)
dotnet test
```

---

## 🧠 Code Patterns Used

### ✅ Dependency Injection
```csharp
public EmployeesController(IEmployeeService service)
{
    _service = service;  // Injected by ASP.NET
}
```

### ✅ Repository Pattern
```csharp
var employee = await _employeeRepository.GetByIdAsync(1);
```

### ✅ Service Layer
```csharp
var result = await _employeeService.CreateEmployeeAsync(dto);
```

### ✅ Standard Response
```csharp
return Ok(ApiResponse<EmployeeDto>.Success(data, "Employee created"));
return BadRequest(ApiResponse<EmployeeDto>.Fail(400, "Invalid data"));
```

### ✅ Authorization
```csharp
[Authorize]                                  // Any authenticated user
[Authorize(Roles = "Admin,SuperAdmin")]      // Admin or SuperAdmin
[Authorize(Roles = "SuperAdmin")]            // SuperAdmin only
```

---

## 🐛 Quick Troubleshooting

| Problem | Solution |
|---------|----------|
| "Connection refused" | Check PostgreSQL running, verify connection string |
| "401 Unauthorized" | Include `Authorization: Bearer {token}` header |
| "403 Forbidden" | Login with higher role (SuperAdmin required) |
| "404 Not Found" | Check endpoint URL spelling, verify ID exists |
| "500 Server Error" | Check logs, verify database migration ran |

---

## 📚 Learn More

Each file has detailed comments explaining:
- WHY the code is structured this way
- HOW it works line by line
- WHEN to use each pattern
- BEST practices

Start with:
1. `Program.cs` - Understand application startup
2. `Controllers/AuthController.cs` - See request/response flow
3. `Services/AuthService.cs` - See business logic
4. `Repositories/GenericRepository.cs` - See data access

---

## 🎯 Next Steps

1. ✅ Run the application
2. ✅ Test endpoints in Swagger
3. ✅ Understand the architecture
4. ✅ Add new features
5. ✅ Deploy to production

---

## 💡 Tips

- **Always return ApiResponse**: Consistency across API
- **Use DTOs**: Never expose Models directly
- **Validate twice**: DataAnnotations + Business logic
- **Hash passwords**: Always use BCrypt
- **Log errors**: Track issues for debugging
- **Use proper HTTP codes**: 200, 201, 400, 401, 403, 404, 500
- **Keep tokens short-lived**: 60 minutes is standard
- **Use soft deletes**: Preserve data for audit

---

**Happy Coding! 🚀**
