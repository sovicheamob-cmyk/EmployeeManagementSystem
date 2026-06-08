using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using BC = BCrypt.Net.BCrypt;
using EmployeeManagementSystem.Common;
using EmployeeManagementSystem.Common.Constants;
using EmployeeManagementSystem.DTOs;
using EmployeeManagementSystem.Models;
using EmployeeManagementSystem.Repositories.Interfaces;
using EmployeeManagementSystem.Services.Interfaces;

namespace EmployeeManagementSystem.Services.Implementations;

/// <summary>
/// Authentication service implementation
/// 
/// RESPONSIBILITIES:
/// - User registration with password hashing
/// - User login with credential validation
/// - JWT token generation
/// - Password verification using BCrypt
/// 
/// DEPENDENCIES:
/// - IUserRepository: Access database for user operations
/// - IConfiguration: Read JWT settings from appsettings.json
/// 
/// SECURITY NOTES:
/// - Passwords are hashed, never stored as plain text
/// - BCrypt automatically adds salt
/// - JWT tokens are signed with secret key
/// - Tokens expire after configured duration
/// </summary>
public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IConfiguration _configuration;

    /// <summary>
    /// Constructor - receives dependencies
    /// 
    /// DEPENDENCY INJECTION:
    /// DI container provides:
    /// - IUserRepository: User data access
    /// - IConfiguration: Settings from appsettings.json
    /// 
    /// WHY INJECT?
    /// - Easy to test (can mock IUserRepository)
    /// - Loose coupling (service doesn't know about concrete repo)
    /// - Configuration centralized in appsettings.json
    /// </summary>
    public AuthService(IUserRepository userRepository, IConfiguration configuration)
    {
        _userRepository = userRepository;
        _configuration = configuration;
    }

    /// <summary>
    /// Register a new user
    /// 
    /// PROCESS:
    /// 1. Validate input data (done by ASP.NET via DataAnnotations)
    /// 2. Check if email already exists
    /// 3. Check password strength
    /// 4. Hash password
    /// 5. Create user in database
    /// 6. Return user data (not password)
    /// 
    /// SECURITY FLOW:
    /// User sends: { email, password, confirmPassword }
    /// ↓
    /// Service hashes password
    /// ↓
    /// Database stores: { email, hashedPassword }
    /// ↓
    /// Response to client: { user data, NO password }
    /// </summary>
    public async Task<ApiResponse<UserProfileDto>> RegisterAsync(RegisterRequestDto request)
    {
        try
        {
            // Step 1: Check if email already exists
            // WHY? Can't have duplicate emails (login would be ambiguous)
            var emailExists = await _userRepository.EmailExistsAsync(request.Email);
            if (emailExists)
            {
                return ApiResponse<UserProfileDto>.Fail(
                    400,
                    MessageConstants.EMAIL_ALREADY_EXISTS
                );
            }

            // Step 2: Validate password meets minimum requirements
            // WHY? Weak passwords are security risk
            if (request.Password.Length < 8)
            {
                return ApiResponse<UserProfileDto>.Fail(
                    400,
                    MessageConstants.INVALID_PASSWORD
                );
            }

            // Step 3: Hash the password
            // WHY?
            // - Never store plain passwords
            // - If database breached, passwords still protected
            // - BCrypt is slow (prevents brute force attacks)
            // 
            // EXAMPLE:
            // Input:  "SecurePassword123"
            // Output: "$2a$11$N9qo8ucoExampleHashedPassword123456..."
            // Each run produces different hash but VerifyPassword still works
            string passwordHash = HashPassword(request.Password);

            // Step 4: Create user object
            var user = new User
            {
                FirstName = request.FirstName,
                LastName = request.LastName,
                Email = request.Email,
                PasswordHash = passwordHash,  // HASH, not plain password
                Role = UserRole.User,          // Default role for new users
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Step 5: Save to database
            // This adds user to DbContext but doesn't save yet
            await _userRepository.AddAsync(user);
            
            // SaveChanges() would be called by Unit of Work or explicitly here
            // For now, we'll handle this in Program.cs by saving after service completes
            // OR we can add a SaveChangesAsync call

            // Step 6: Create response DTO (user info WITHOUT password)
            var userProfile = new UserProfileDto
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                Role = user.Role.ToString(),
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt
            };

            return ApiResponse<UserProfileDto>.Success(
                userProfile,
                MessageConstants.REGISTER_SUCCESS
            );
        }
        catch (Exception)
        {
            // Log exception here (not shown for brevity)
            return ApiResponse<UserProfileDto>.Fail(
                500,
                MessageConstants.FAILED
            );
        }
    }

    /// <summary>
    /// Login user with email and password
    /// 
    /// PROCESS:
    /// 1. Find user by email
    /// 2. Verify password matches hash
    /// 3. Generate JWT token
    /// 4. Return user info + token
    /// 
    /// CLIENT USAGE:
    /// 1. User enters email and password
    /// 2. Client sends to POST /api/auth/login
    /// 3. Server returns token
    /// 4. Client stores token (localStorage/cookies)
    /// 5. Client sends token with every request in Authorization header
    /// 
    /// ERROR SCENARIOS:
    /// - Email not found: "Invalid email or password"
    /// - Password wrong: "Invalid email or password"
    /// Note: Same message for both (security - don't reveal which is wrong)
    /// </summary>
    public async Task<ApiResponse<LoginResponseDto>> LoginAsync(LoginRequestDto request)
    {
        try
        {
            // Step 1: Find user by email
            var user = await _userRepository.GetByEmailAsync(request.Email);
            
            // Step 2: If not found, return error
            if (user == null)
            {
                return ApiResponse<LoginResponseDto>.Fail(
                    401,
                    MessageConstants.LOGIN_FAILED
                );
            }

            // Step 3: Verify password
            // WHY BCrypt.Verify?
            // - Takes user input password
            // - Hashes it with same salt as stored hash
            // - Compares with stored hash
            // - Returns true if match, false otherwise
            // 
            // EXAMPLE:
            // Stored:    "$2a$11$N9qo8ucoExampleHash123456..."
            // Input:     "SecurePassword123"
            // Hashed:    "$2a$11$N9qo8ucoExampleHash123456..." (after applying stored salt)
            // Match:     ✓ true
            bool passwordIsValid = VerifyPassword(request.Password, user.PasswordHash);

            if (!passwordIsValid)
            {
                return ApiResponse<LoginResponseDto>.Fail(
                    401,
                    MessageConstants.LOGIN_FAILED
                );
            }

            // Step 4: Password verified! Generate JWT token
            // Create user profile for token generation
            var userProfile = new UserProfileDto
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                Role = user.Role.ToString(),
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt
            };

            // Generate token
            string token = GenerateJwtToken(userProfile);

            // Get token expiration time
            int expirationMinutes = int.Parse(
                _configuration["JwtSettings:ExpirationMinutes"] ?? "60"
            );

            // Step 5: Create response with token
            var loginResponse = new LoginResponseDto
            {
                UserId = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                Role = user.Role.ToString(),
                Token = token,
                ExpiresIn = expirationMinutes * 60  // Convert to seconds
            };

            return ApiResponse<LoginResponseDto>.Success(
                loginResponse,
                MessageConstants.LOGIN_SUCCESS
            );
        }
        catch (Exception)
        {
            return ApiResponse<LoginResponseDto>.Fail(
                500,
                MessageConstants.FAILED
            );
        }
    }

    /// <summary>
    /// Hash password using BCrypt
    /// 
    /// WHAT IS BCRYPT?
    /// - Industry standard for password hashing
    /// - Automatically adds salt (random data)
    /// - Designed to be slow (prevents brute force)
    /// 
    /// HOW IT WORKS:
    /// 1. Input password: "SecurePassword123"
    /// 2. Generate random salt
    /// 3. Hash password with salt (2^cost times)
    /// 4. Output: "$2a$11$[salt][hash]"
    /// 
    /// COST FACTOR:
    /// - Cost 10 = 2^10 iterations (default)
    /// - Cost 11 = 2^11 iterations (stronger, slower)
    /// - Cost 12 = 2^12 iterations (very strong, even slower)
    /// - Recommended: 11-12
    /// - We use 11 for good security/performance balance
    /// 
    /// SALT:
    /// - Random data prepended to password before hashing
    /// - Even same password produces different hash
    /// - BCrypt automatically handles salt
    /// - If database breached, can't use rainbow tables
    /// 
    /// EXAMPLE:
    /// Password: "SecurePassword123"
    /// Run 1: "$2a$11$N9qo8ucoExampleHash123456..."
    /// Run 2: "$2a$11$DifferentSaltExampleHash123456..."
    /// Both verify correctly with VerifyPassword()
    /// </summary>
    public string HashPassword(string plainPassword)
    {
        // BCrypt.HashPassword(password, cost)
        // Cost 11 = 2^11 iterations (good security/performance)
        // BCrypt internally generates salt, so we don't need to handle it
        return BC.HashPassword(plainPassword, workFactor: 11);
    }

    /// <summary>
    /// Verify password against hash
    /// 
    /// HOW IT WORKS:
    /// 1. Takes plain password from user input
    /// 2. Extracts salt from stored hash (first 29 chars: "$2a$11$" + 22-char salt)
    /// 3. Hashes plain password with extracted salt
    /// 4. Compares with stored hash
    /// 5. Returns true if they match
    /// 
    /// SECURITY:
    /// - Timing attack resistant
    /// - BCrypt handles all salt logic
    /// - We just provide password and hash
    /// 
    /// USAGE:
    /// bool isValid = VerifyPassword("password123", userHashFromDb);
    /// </summary>
    public bool VerifyPassword(string plainPassword, string passwordHash)
    {
        // BCrypt.Verify(password, hash)
        // Automatically extracts salt from hash and compares
        return BC.Verify(plainPassword, passwordHash);
    }

    /// <summary>
    /// Generate JWT token for user
    /// 
    /// WHAT IS JWT?
    /// - JSON Web Token (industry standard)
    /// - Contains claims (key-value pairs)
    /// - Signed with secret key
    /// - Self-contained (no server-side session needed)
    /// 
    /// THREE PARTS:
    /// Header: { "alg": "HS256", "typ": "JWT" }
    /// Payload: { "sub": "5", "email": "john@example.com", "role": "Admin" }
    /// Signature: HMACSHA256(header.payload, secretKey)
    /// 
    /// FORMAT:
    /// eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiI1IiwiZW1haWwiOiJqb2huQGV4YW1wbGUuY29tIn0.signature
    ///           ↑ Header (Base64)                  ↑ Payload (Base64)           ↑ Signature
    /// 
    /// TOKEN VALIDATION:
    /// 1. Client sends: Authorization: Bearer {token}
    /// 2. Server extracts token
    /// 3. Server validates signature using secret key
    /// 4. If valid, server trusts claims in token
    /// 5. Server extracts user info from claims
    /// 
    /// WHY SECURE?
    /// - Client can't modify payload without invalidating signature
    /// - Server can verify token wasn't tampered with
    /// - Secret key only known to server
    /// 
    /// CLAIMS (what's in the token):
    /// - sub (subject): User ID
    /// - email: User's email
    /// - role: User's role (for authorization)
    /// - iat (issued at): When token was created
    /// - exp (expiration): When token expires
    /// - iss (issuer): Who created the token
    /// - aud (audience): Who the token is for
    /// 
    /// USAGE IN CONTROLLER:
    /// [Authorize(Roles = "Admin")]
    /// public async Task<IActionResult> DeleteUser(int id)
    /// {
    ///     // Framework automatically:
    ///     // 1. Validates token signature
    ///     // 2. Checks expiration
    ///     // 3. Verifies role claim
    ///     // 4. Gives access if all valid
    /// }
    /// 
    /// FLOW:
    /// 1. Login successful → Generate token
    /// 2. Token sent to client
    /// 3. Client stores token
    /// 4. Client sends token with every request
    /// 5. Server validates token
    /// 6. Server grants access based on claims
    /// </summary>
    public string GenerateJwtToken(UserProfileDto user)
    {
        // Get JWT settings from configuration (appsettings.json)
        var jwtSettings = _configuration.GetSection("JwtSettings");
        var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT secret key not configured");
        var issuer = jwtSettings["Issuer"] ?? "EmployeeManagementApi";
        var audience = jwtSettings["Audience"] ?? "EmployeeManagementClient";
        var expirationMinutes = int.Parse(jwtSettings["ExpirationMinutes"] ?? "60");

        // Step 1: Create security key
        // Secret key must be at least 32 characters for HS256
        var keyBytes = System.Text.Encoding.UTF8.GetBytes(secretKey);
        var securityKey = new SymmetricSecurityKey(keyBytes);

        // Step 2: Create signing credentials
        // HS256 = HMAC with SHA-256
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        // Step 3: Create claims (data in token)
        var claims = new List<Claim>
        {
            // sub (subject) - typically user ID
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            
            // email
            new Claim(ClaimTypes.Email, user.Email),
            
            // name (combination of first and last)
            new Claim(ClaimTypes.Name, $"{user.FirstName} {user.LastName}"),
            
            // role (used for [Authorize(Roles = "Admin")] attributes)
            new Claim(ClaimTypes.Role, user.Role),
            
            // Custom claims if needed
            new Claim("FirstName", user.FirstName),
            new Claim("LastName", user.LastName)
        };

        // Step 4: Create JWT descriptor
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(expirationMinutes),  // Token valid for 60 minutes
            Issuer = issuer,                                           // Who created it
            Audience = audience,                                       // Who it's for
            SigningCredentials = credentials                           // How to sign it
        };

        // Step 5: Create token
        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);

        // Step 6: Write token as string
        // Returns format: "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOi..."
        return tokenHandler.WriteToken(token);
    }
}
