using EmployeeManagementSystem.DTOs;
using EmployeeManagementSystem.Common;

namespace EmployeeManagementSystem.Services.Interfaces;

/// <summary>
/// Authentication service interface
/// 
/// RESPONSIBILITIES:
/// 1. Register new users (hash password)
/// 2. Login (validate password, generate JWT)
/// 3. Generate JWT tokens
/// 4. Validate passwords using BCrypt
/// 
/// DEPENDENCY INJECTION:
/// Services receive IAuthService
/// Implementation: AuthService
/// 
/// WHY INTERFACE?
/// - Can mock in unit tests
/// - Can swap implementations
/// - Services don't depend on concrete class
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Register a new user
    /// 
    /// USAGE:
    /// var response = await authService.RegisterAsync(registerDto);
    /// if (response.Code == 200)
    ///     user created successfully
    /// else
    ///     registration failed
    /// 
    /// RESPONSIBILITIES:
    /// - Validate email doesn't exist
    /// - Validate password meets requirements
    /// - Hash password using BCrypt
    /// - Create user in database
    /// - Return user data (NOT password)
    /// 
    /// RETURNS:
    /// - ApiResponse with UserProfileDto on success
    /// - ApiResponse with error message on failure
    /// </summary>
    Task<ApiResponse<UserProfileDto>> RegisterAsync(RegisterRequestDto request);

    /// <summary>
    /// Login user and return JWT token
    /// 
    /// USAGE:
    /// var response = await authService.LoginAsync(loginDto);
    /// if (response.Code == 200)
    ///     string token = response.Data.Token;
    ///     client stores token
    /// else
    ///     invalid credentials
    /// 
    /// RESPONSIBILITIES:
    /// - Find user by email
    /// - Verify password matches hash
    /// - Generate JWT token
    /// - Return user info + token
    /// 
    /// RETURNS:
    /// - LoginResponseDto with token on success
    /// - Null on failure (email not found or password wrong)
    /// </summary>
    Task<ApiResponse<LoginResponseDto>> LoginAsync(LoginRequestDto request);

    /// <summary>
    /// Validate password using BCrypt
    /// 
    /// USAGE:
    /// bool isValid = authService.VerifyPassword(plainPassword, hashFromDb);
    /// 
    /// HOW BCRYPT WORKS:
    /// 1. User enters password: "SecurePassword123"
    /// 2. Server retrieves hash from DB: "$2a$11$abcd1234..."
    /// 3. BCrypt hashes input with same salt as stored hash
    /// 4. Compares result with stored hash
    /// 5. Returns true if match, false otherwise
    /// 
    /// WHY BCRYPT INSTEAD OF SHA256?
    /// - SHA256: Fast (bad for passwords - enables brute force)
    /// - BCrypt: Slow (good for passwords - prevents brute force)
    /// - BCrypt: Has built-in salt
    /// - BCrypt: Allows increasing cost as computers get faster
    /// </summary>
    bool VerifyPassword(string plainPassword, string passwordHash);

    /// <summary>
    /// Hash password for storage
    /// 
    /// USAGE:
    /// string hash = authService.HashPassword(plainPassword);
    /// user.PasswordHash = hash;
    /// 
    /// SECURITY:
    /// - Never store plain passwords
    /// - Use strong hashing (BCrypt, Argon2, etc.)
    /// - Make hashes different even for same password (salt)
    /// 
    /// BCRYPT EXAMPLE:
    /// Input:  "SecurePassword123"
    /// Output: "$2a$11$N9qo8ucoExampleHashedPassword123456..."
    /// 
    /// Notes:
    /// - Hash includes salt (first 29 characters)
    /// - Hash includes cost factor (11 = very strong)
    /// - Each run produces different hash (but VerifyPassword still works)
    /// </summary>
    string HashPassword(string plainPassword);

    /// <summary>
    /// Generate JWT token for user
    /// 
    /// USAGE:
    /// var token = authService.GenerateJwtToken(user);
    /// 
    /// WHAT'S IN JWT?
    /// Header: { "alg": "HS256", "typ": "JWT" }
    /// Payload (Claims): { "sub": "5", "email": "john@example.com", "role": "Admin" }
    /// Signature: HMACSHA256(header.payload, secret)
    /// 
    /// FORMAT:
    /// eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiI1IiwiZW1haWwiOiJqb2huQGV4YW1wbGUuY29tIn0.signature
    /// 
    /// THREE PARTS (separated by dots):
    /// - eyJ... = Header (Base64)
    /// - eyJz... = Payload/Claims (Base64)
    /// - sign... = Signature (HMAC)
    /// 
    /// SECURITY:
    /// - Client can't modify payload (signature would be invalid)
    /// - Server validates signature using secret
    /// - Secret known only to server
    /// 
    /// EXPIRATION:
    /// - Token expires after specified time (e.g., 60 minutes)
    /// - Client must get new token after expiration
    /// - Better than storing session on server (scalable)
    /// </summary>
    string GenerateJwtToken(UserProfileDto user);
}
