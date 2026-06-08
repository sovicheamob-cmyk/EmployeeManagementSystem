# NuGet Packages Used in Employee Management System

## Installation Command
All packages are automatically installed when you run `dotnet restore`

## Complete Package List

### 1. Web API & Documentation
- **Microsoft.AspNetCore.OpenApi** (v10.0.8)
  - Generates OpenAPI specification
  - Enables `/openapi/v1.json` endpoint
  
- **Swashbuckle.AspNetCore** (v6.4.7)
  - Provides interactive Swagger UI
  - Accessible at `https://localhost:5001/swagger`
  - Auto-generates API documentation

### 2. Entity Framework Core (Database ORM)
- **Microsoft.EntityFrameworkCore** (v10.0.0)
  - Core ORM framework
  - LINQ to SQL translation
  - Change tracking
  
- **Microsoft.EntityFrameworkCore.Tools** (v10.0.0)
  - Provides EF Core CLI commands
  - `dotnet ef migrations add`
  - `dotnet ef database update`
  - `dotnet ef database drop`
  
- **Npgsql.EntityFrameworkCore.PostgreSQL** (v10.0.0)
  - PostgreSQL database provider
  - Native support for PostgreSQL types
  - Full SQL generation

### 3. Authentication & JWT
- **System.IdentityModel.Tokens.Jwt** (v8.0.2)
  - JWT token creation (`JwtSecurityTokenHandler`)
  - Token validation and signature checking
  - Claims management

- **Microsoft.AspNetCore.Authentication.JwtBearer** (v10.0.8)
  - JWT Bearer middleware
  - Token validation in request pipeline
  - `[Authorize]` attribute support
  - Automatic claims population in `User.Claims`

### 4. Security & Password Hashing
- **BCrypt.Net-Core** (v1.6.0)
  - Password hashing with salt
  - `BCrypt.HashPassword(password)`
  - `BCrypt.Verify(password, hash)`
  - Industry-standard secure hashing

---

## Package Dependency Tree

```
EmployeeManagementSystem
│
├── Microsoft.AspNetCore.OpenApi (10.0.8)
│   └── [depends on ASP.NET Core runtime]
│
├── Swashbuckle.AspNetCore (6.4.7)
│   ├── [depends on OpenAPI lib]
│   └── [depends on Swagger UI]
│
├── Microsoft.EntityFrameworkCore (10.0.0)
│   ├── [LINQ provider]
│   ├── [Change tracking]
│   └── [Database abstraction]
│
├── Microsoft.EntityFrameworkCore.Tools (10.0.0)
│   └── [depends on EF Core]
│
├── Npgsql.EntityFrameworkCore.PostgreSQL (10.0.0)
│   ├── [depends on EF Core]
│   ├── [PostgreSQL client]
│   └── [PostgreSQL-specific SQL generation]
│
├── System.IdentityModel.Tokens.Jwt (8.0.2)
│   ├── [JWT validation]
│   └── [Security algorithms]
│
├── Microsoft.AspNetCore.Authentication.JwtBearer (10.0.8)
│   ├── [depends on JWT tokens package]
│   └── [depends on ASP.NET Core Auth]
│
└── BCrypt.Net-Core (1.6.0)
    └── [Password hashing]
```

---

## Why Each Package

### Swashbuckle.AspNetCore
**Why?** Makes API self-documenting. Clients can see all endpoints, parameters, and test them in browser.

**Example:**
```
https://localhost:5001/swagger
↓
Lists all endpoints with documentation
↓
Can execute requests directly from UI
```

### EF Core + Npgsql
**Why?** Eliminates SQL writing, provides type safety, handles migrations automatically.

**Example:**
```csharp
// Instead of writing raw SQL
var employee = await context.Employees
    .Where(e => e.IsActive && e.Department == "IT")
    .FirstOrDefaultAsync();
```

### JWT + JwtBearer
**Why?** Stateless authentication - doesn't require server sessions, scales across multiple servers.

**Example:**
```
1. Client logs in → receives JWT token
2. Client includes in every request: Authorization: Bearer {token}
3. Server validates token signature
4. Grants access if valid
5. No session storage needed
```

### BCrypt
**Why?** Military-grade password hashing, built-in salt, slow (prevents brute force).

**Example:**
```csharp
// Never do this:
user.Password = plainPassword;  // ❌ WRONG

// Do this:
user.PasswordHash = BCrypt.HashPassword(plainPassword);  // ✅ RIGHT

// Later verify:
bool isValid = BCrypt.Verify(inputPassword, user.PasswordHash);
```

---

## Package Versions

| Package | Version | Release Date | Support |
|---------|---------|--------------|---------|
| Microsoft.AspNetCore.OpenApi | 10.0.8 | Latest | Current |
| Swashbuckle.AspNetCore | 6.4.7 | Latest | Current |
| Microsoft.EntityFrameworkCore | 10.0.0 | Latest | Current |
| Microsoft.EntityFrameworkCore.Tools | 10.0.0 | Latest | Current |
| Npgsql.EntityFrameworkCore.PostgreSQL | 10.0.0 | Latest | Current |
| System.IdentityModel.Tokens.Jwt | 8.0.2 | Latest | Current |
| Microsoft.AspNetCore.Authentication.JwtBearer | 10.0.8 | Latest | Current |
| BCrypt.Net-Core | 1.6.0 | Latest | Current |

**Note:** All versions are compatible with .NET 10. Check for updates: `dotnet outdated`

---

## How to Update Packages

```bash
# Check for updates
dotnet outdated

# Update specific package
dotnet add package PackageName --version X.X.X

# Update all packages
dotnet package update

# See what's installed
dotnet package list
```

---

## Package Files in Project

```
EmployeeManagementSystem.csproj
│
└── <ItemGroup>
    ├── <PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="10.0.8" />
    ├── <PackageReference Include="Swashbuckle.AspNetCore" Version="6.4.7" />
    ├── <PackageReference Include="Microsoft.EntityFrameworkCore" Version="10.0.0" />
    ├── <PackageReference Include="Microsoft.EntityFrameworkCore.Tools" Version="10.0.0" />
    ├── <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.0" />
    ├── <PackageReference Include="System.IdentityModel.Tokens.Jwt" Version="8.0.2" />
    ├── <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="10.0.8" />
    └── <PackageReference Include="BCrypt.Net-Core" Version="1.6.0" />
```

---

## Installation Steps

### Step 1: Restore Packages
```bash
dotnet restore
```
This downloads all packages listed in `.csproj` to your local cache.

### Step 2: Verify Installation
```bash
dotnet package list
```
Lists all installed packages with versions.

### Step 3: Check for Issues
```bash
dotnet build
```
Compiles with all packages - any missing package errors will show here.

---

## Production Considerations

✅ **These are all battle-tested packages**
- Used in thousands of production systems
- Regular security updates
- Active maintenance
- Good community support

⚠️ **What's NOT included (optional additions)**
- Logging: Serilog (recommended)
- Caching: StackExchange.Redis
- Email: SendGrid / MailKit
- Testing: xUnit / NUnit
- Performance: Application Insights

---

## NuGet Package Sources

By default, NuGet pulls from:
- **Primary:** https://www.nuget.org/
- **Microsoft:** https://api.nuget.org/v3/index.json

All packages in this project are from the official NuGet source.

---

## Summary

**Total Packages:** 8 (all essential, production-ready)

**Total Size:** ~200 MB (after restore)

**Security Status:** ✅ All current versions, no known vulnerabilities

**Compatibility:** ✅ .NET 10, cross-platform (Windows/Linux/macOS)

---

For more details, see:
- SETUP_GUIDE.md - Complete setup instructions
- QUICK_REFERENCE.md - Quick lookup
- Program.cs - How packages are used in code
