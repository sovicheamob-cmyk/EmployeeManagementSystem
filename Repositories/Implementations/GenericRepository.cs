using Microsoft.EntityFrameworkCore;
using EmployeeManagementSystem.Data;
using EmployeeManagementSystem.Repositories.Interfaces;

namespace EmployeeManagementSystem.Repositories.Implementations;

/// <summary>
/// Generic repository implementation
/// 
/// HOW THIS WORKS:
/// 1. Receives DbContext via constructor (dependency injection)
/// 2. Uses DbContext to perform database operations
/// 3. All LINQ queries translated to SQL by EF Core
/// 4. Returns data to caller
/// 
/// LINE BY LINE EXPLANATION OF PATTERN:
/// - T : class - Ensures TEntity is a class type (not struct)
/// - DbSet<T> - Gets the DbSet for the entity (Users, Employees, etc.)
/// - SaveChangesAsync() - Commits changes to database
/// - AsNoTracking() - For read-only queries (better performance)
/// 
/// DEPENDENCY INJECTION:
/// When service requests IGenericRepository<User>,
/// DI container creates GenericRepository<User> instance
/// with ApplicationDbContext injected into constructor
/// </summary>
/// <typeparam name="TEntity">The entity type this repository manages</typeparam>
public class GenericRepository<TEntity> : IGenericRepository<TEntity> where TEntity : class
{
    /// <summary>
    /// DbContext - provides access to database
    /// Marked as protected so derived classes can access it
    /// </summary>
    protected readonly ApplicationDbContext _context;

    /// <summary>
    /// DbSet for this entity type
    /// Cached in constructor for faster access
    /// </summary>
    protected readonly DbSet<TEntity> _dbSet;

    /// <summary>
    /// Constructor - receives DbContext via dependency injection
    /// </summary>
    public GenericRepository(ApplicationDbContext context)
    {
        _context = context;
        _dbSet = context.Set<TEntity>();  // Get DbSet for TEntity (e.g., DbSet<User>)
    }

    // ========== READ OPERATIONS ==========

    /// <summary>
    /// Get all entities
    /// 
    /// IMPORTANT: Loads ALL entities into memory
    /// If 10,000 employees, loads all 10,000
    /// Use GetPagedAsync for large datasets
    /// 
    /// LINQ QUERY:
    /// _dbSet.ToListAsync() → SELECT * FROM [TableName]
    /// </summary>
    public async Task<IEnumerable<TEntity>> GetAllAsync()
    {
        return await _dbSet.ToListAsync();
    }

    /// <summary>
    /// Get entity by ID
    /// 
    /// LINQ QUERY:
    /// _dbSet.FindAsync(id) → SELECT * FROM [TableName] WHERE Id = @id
    /// 
    /// WHY FindAsync?
    /// - Checks context first (very fast if already loaded)
    /// - Falls back to database if not found
    /// - Perfect for getting by primary key
    /// </summary>
    public async Task<TEntity?> GetByIdAsync(int id)
    {
        return await _dbSet.FindAsync(id);
    }

    /// <summary>
    /// Find entities matching a condition
    /// 
    /// LINQ QUERY:
    /// _dbSet.Where(predicate).ToListAsync()
    /// → SELECT * FROM [TableName] WHERE [condition]
    /// 
    /// USAGE:
    /// var activeUsers = await repo.FindAsync(u => u.IsActive == true);
    /// 
    /// NOTE: Uses .ToList() before Where() - loads all data first
    /// This is SLOW for large datasets
    /// Better approach would be to use IQueryable (shown in advanced step)
    /// </summary>
    public async Task<IEnumerable<TEntity>> FindAsync(Func<TEntity, bool> predicate)
    {
        return await Task.FromResult(
            _dbSet.Where(predicate).ToList()
        );
    }

    /// <summary>
    /// Get first entity matching condition (or null)
    /// 
    /// USAGE:
    /// var user = await repo.FirstOrDefaultAsync(u => u.Email == "john@example.com");
    /// 
    /// LINQ QUERY:
    /// SELECT * FROM [TableName] WHERE [condition] LIMIT 1
    /// </summary>
    public async Task<TEntity?> FirstOrDefaultAsync(Func<TEntity, bool> predicate)
    {
        return await Task.FromResult(
            _dbSet.FirstOrDefault(predicate)
        );
    }

    /// <summary>
    /// Check if any entity matches condition
    /// 
    /// USAGE:
    /// bool exists = await repo.AnyAsync(u => u.Email == "john@example.com");
    /// 
    /// USEFUL FOR:
    /// - Validation before insert (check duplicate email)
    /// - Validation before delete (check if in use)
    /// 
    /// MORE EFFICIENT THAN:
    /// - GetAllAsync() then checking count
    /// - Returns true as soon as first match found
    /// </summary>
    public async Task<bool> AnyAsync(Func<TEntity, bool> predicate)
    {
        return await Task.FromResult(
            _dbSet.Any(predicate)
        );
    }

    // ========== CREATE OPERATIONS ==========

    /// <summary>
    /// Add a new entity
    /// 
    /// IMPORTANT: Does NOT save to database immediately!
    /// Must call SaveChangesAsync() after this
    /// 
    /// WHY SEPARATE OPERATIONS?
    /// - Allows batch operations
    /// - Can add multiple entities before saving
    /// - Better for transaction handling
    /// 
    /// USAGE:
    /// var user = new User { Email = "john@example.com" };
    /// await userRepo.AddAsync(user);
    /// await unitOfWork.SaveChangesAsync();  // Now it's saved
    /// 
    /// WHAT HAPPENS:
    /// 1. EF Core adds entity to context
    /// 2. Marks it as "Added" state
    /// 3. SaveChangesAsync() generates INSERT SQL
    /// 4. Database inserts record and returns Id
    /// 5. Entity.Id is populated with auto-generated Id
    /// </summary>
    public async Task<TEntity> AddAsync(TEntity entity)
    {
        await _dbSet.AddAsync(entity);
        return entity;
    }

    // ========== UPDATE OPERATIONS ==========

    /// <summary>
    /// Update an entity
    /// 
    /// IMPORTANT: Entity must be tracked by DbContext
    /// EF Core tracks entities fetched from database
    /// 
    /// USAGE:
    /// var user = await repo.GetByIdAsync(5);  // Fetched - now tracked
    /// user.FirstName = "Jane";                  // Change property
    /// await repo.UpdateAsync(user);             // Update in context
    /// await unitOfWork.SaveChangesAsync();      // SaveChanges generates UPDATE SQL
    /// 
    /// HOW EF TRACKS CHANGES:
    /// 1. User fetched - EF stores original state
    /// 2. FirstName changed - EF detects change
    /// 3. SaveChanges() generates: UPDATE Users SET FirstName='Jane' WHERE Id=5
    /// 4. Only changed columns are updated (efficient)
    /// </summary>
    public async Task<TEntity> UpdateAsync(TEntity entity)
    {
        _dbSet.Update(entity);
        return await Task.FromResult(entity);
    }

    // ========== DELETE OPERATIONS ==========

    /// <summary>
    /// Delete an entity
    /// 
    /// USAGE:
    /// var user = await repo.GetByIdAsync(5);
    /// bool success = await repo.DeleteAsync(user);
    /// await unitOfWork.SaveChangesAsync();
    /// 
    /// WHAT HAPPENS:
    /// 1. Entity marked as "Deleted" state
    /// 2. SaveChangesAsync() generates DELETE SQL
    /// 3. Database deletes the record
    /// 4. Returns true if successful, false if not found
    /// </summary>
    public async Task<bool> DeleteAsync(TEntity entity)
    {
        if (entity == null)
            return false;

        _dbSet.Remove(entity);
        return await Task.FromResult(true);
    }

    // ========== QUERY HELPERS ==========

    /// <summary>
    /// Get paged results
    /// 
    /// PAGINATION PATTERN:
    /// - PageNumber 1 = first page
    /// - Skip: (pageNumber - 1) * pageSize
    /// - Take: pageSize
    /// 
    /// MATH EXAMPLE:
    /// Total: 25 employees, PageSize: 10
    /// Page 1: Skip 0, Take 10   → items 1-10
    /// Page 2: Skip 10, Take 10  → items 11-20
    /// Page 3: Skip 20, Take 10  → items 21-25
    /// 
    /// USAGE:
    /// var (items, total) = await repo.GetPagedAsync(pageNumber: 2, pageSize: 10);
    /// 
    /// RETURNS:
    /// - items: Page 2 results
    /// - total: 25 (total count for client to calculate pages)
    /// </summary>
    public async Task<(IEnumerable<TEntity> items, int totalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        Func<TEntity, bool>? predicate = null
    )
    {
        // Get query
        IEnumerable<TEntity> query = _dbSet.AsEnumerable();

        // Apply filter if predicate provided
        if (predicate != null)
        {
            query = query.Where(predicate);
        }

        // Get total count before pagination
        int totalCount = query.Count();

        // Apply pagination
        var items = query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return await Task.FromResult((items, totalCount));
    }

    /// <summary>
    /// Get count of all entities
    /// </summary>
    public async Task<int> CountAsync()
    {
        return await _dbSet.CountAsync();
    }

    /// <summary>
    /// Get count matching a condition
    /// </summary>
    public async Task<int> CountAsync(Func<TEntity, bool> predicate)
    {
        return await Task.FromResult(
            _dbSet.Count(predicate)
        );
    }
}
