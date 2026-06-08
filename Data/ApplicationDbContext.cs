using Microsoft.EntityFrameworkCore;
using EmployeeManagementSystem.Models;

namespace EmployeeManagementSystem.Data;

/// <summary>
/// Application's Entity Framework Core DbContext
/// 
/// WHAT IS THIS?
/// - Bridge between C# objects and database
/// - Manages all entities and database operations
/// - Tracks changes and generates SQL automatically
/// 
/// HOW IT WORKS:
/// 1. You define DbSet<Entity> properties (one per table)
/// 2. EF Core creates tables matching these DbSets
/// 3. When you call SaveChanges(), EF generates SQL
/// 4. Database executes the SQL
/// 
/// EXAMPLE FLOW:
/// var user = new User { Email = "john@example.com" };
/// context.Users.Add(user);           // Add to context
/// context.SaveChanges();              // EF generates INSERT SQL
/// 
/// WHY PostgreSQL?
/// - Open source and free
/// - Enterprise-grade reliability
/// - Great scalability
/// - JSONB support for complex data
/// - Window functions for analytics
/// </summary>
public class ApplicationDbContext : DbContext
{
    /// <summary>
    /// Constructor receives DbContextOptions from dependency injection
    /// 
    /// DEPENDENCY INJECTION:
    /// Program.cs will do:
    /// services.AddDbContext<ApplicationDbContext>(options =>
    ///     options.UseNpgsql(connectionString)
    /// );
    /// 
    /// This DbContextOptions is passed here automatically
    /// </summary>
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// DbSet for Users entity
    /// Represents the "Users" table in database
    /// 
    /// WHAT CAN YOU DO WITH THIS?
    /// - context.Users.Add(user)               // Insert
    /// - context.Users.ToList()                // Select *
    /// - context.Users.FirstOrDefault()        // Get one
    /// - context.Users.Where(...)              // Filter
    /// - context.Users.Update(user)            // Update
    /// - context.Users.Remove(user)            // Delete
    /// </summary>
    public DbSet<User> Users { get; set; } = null!;

    /// <summary>
    /// DbSet for Employees entity
    /// Represents the "Employees" table in database
    /// </summary>
    public DbSet<Employee> Employees { get; set; } = null!;

    /// <summary>
    /// Override OnModelCreating to configure entity mappings
    /// 
    /// THIS METHOD:
    /// - Configures table names, primary keys
    /// - Sets up unique constraints (like Email)
    /// - Creates indexes for performance
    /// - Sets up relationships (when we have foreign keys)
    /// 
    /// WHY EXPLICIT CONFIGURATION?
    /// - Gives us fine control
    /// - Makes indexes explicit (improves query performance)
    /// - Prevents EF Core from guessing wrong
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ========== USER ENTITY CONFIGURATION ==========
        modelBuilder.Entity<User>(entity =>
        {
            // Set table name (by default it would be "Users" anyway)
            entity.ToTable("Users");

            // Primary key (Id is default, but being explicit)
            entity.HasKey(e => e.Id);

            // Configure properties
            entity.Property(e => e.FirstName)
                .IsRequired()
                .HasMaxLength(50)
                .HasColumnName("FirstName")
                .HasColumnType("varchar(50)");

            entity.Property(e => e.LastName)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(e => e.Email)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.PasswordHash)
                .IsRequired()
                .HasMaxLength(255);  // BCrypt hash is ~60 chars, 255 is safe

            entity.Property(e => e.Role)
                .HasConversion<int>()  // Store enum as integer in DB
                .HasDefaultValue(UserRole.User);

            entity.Property(e => e.CreatedAt)
                .HasColumnType("timestamp with time zone")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.Property(e => e.UpdatedAt)
                .HasColumnType("timestamp with time zone")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Create unique constraint on Email
            // WHY? Email must be unique for login
            // Helps prevent duplicate accounts
            // Also creates index for faster lookups
            entity.HasIndex(e => e.Email)
                .IsUnique()
                .HasDatabaseName("IX_Users_Email_Unique");

            // Create index on Role for faster authorization checks
            entity.HasIndex(e => e.Role)
                .HasDatabaseName("IX_Users_Role");
        });

        // ========== EMPLOYEE ENTITY CONFIGURATION ==========
        modelBuilder.Entity<Employee>(entity =>
        {
            entity.ToTable("Employees");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.EmployeeCode)
                .IsRequired()
                .HasMaxLength(20);

            entity.Property(e => e.FirstName)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(e => e.LastName)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(e => e.Email)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.Department)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(e => e.Position)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(e => e.Salary)
                .HasColumnType("numeric(10,2)")  // 10 digits total, 2 decimal places
                .IsRequired();

            entity.Property(e => e.JoinDate)
                .HasColumnType("date");  // DATE type (not timestamp)

            entity.Property(e => e.IsActive)
                .HasDefaultValue(true);

            entity.Property(e => e.CreatedAt)
                .HasColumnType("timestamp with time zone")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.Property(e => e.UpdatedAt)
                .HasColumnType("timestamp with time zone")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Unique constraint on EmployeeCode
            entity.HasIndex(e => e.EmployeeCode)
                .IsUnique()
                .HasDatabaseName("IX_Employees_Code_Unique");

            // Index on Department for faster department-based queries
            entity.HasIndex(e => e.Department)
                .HasDatabaseName("IX_Employees_Department");

            // Index on IsActive for faster filtering of active employees
            entity.HasIndex(e => e.IsActive)
                .HasDatabaseName("IX_Employees_IsActive");
        });
    }
}
