namespace EmployeeManagementSystem.Repositories.Interfaces;

/// <summary>
/// Generic repository interface for all entities
/// 
/// WHY GENERIC?
/// - Avoid code duplication
/// - UserRepository, EmployeeRepository can inherit from this
/// - All CRUD operations defined once
/// 
/// DEPENDENCY INJECTION:
/// - Services receive IGenericRepository<T>
/// - Can be tested without real database
/// - Can swap implementations easily
/// 
/// TEntity: Type parameter - the entity this repository works with
/// Example: IGenericRepository<User>, IGenericRepository<Employee>
/// </summary>
/// <typeparam name="TEntity">The entity type (User, Employee, etc.)</typeparam>
public interface IGenericRepository<TEntity> where TEntity : class
{
    // ========== READ OPERATIONS ==========

    /// <summary>
    /// Get all entities
    /// 
    /// WARNING: Use with caution on large tables!
    /// If 10,000 employees, this loads all 10,000 into memory
    /// Better to use GetPagedAsync with pagination
    /// 
    /// USAGE:
    /// var allUsers = await userRepo.GetAllAsync();
    /// </summary>
    Task<IEnumerable<TEntity>> GetAllAsync();

    /// <summary>
    /// Get entity by ID
    /// 
    /// USAGE:
    /// var user = await userRepo.GetByIdAsync(5);
    /// </summary>
    Task<TEntity?> GetByIdAsync(int id);

    /// <summary>
    /// Get entities matching a condition
    /// 
    /// USAGE:
    /// var activeEmployees = await empRepo.FindAsync(e => e.IsActive == true);
    /// 
    /// Func<TEntity, bool>: A function that returns bool
    /// Example: e => e.IsActive == true
    /// </summary>
    Task<IEnumerable<TEntity>> FindAsync(Func<TEntity, bool> predicate);

    /// <summary>
    /// Get first entity matching a condition (or null)
    /// 
    /// USAGE:
    /// var user = await userRepo.FirstOrDefaultAsync(u => u.Email == email);
    /// 
    /// This is commonly used for looking up by email or code
    /// </summary>
    Task<TEntity?> FirstOrDefaultAsync(Func<TEntity, bool> predicate);

    /// <summary>
    /// Check if any entity matches a condition
    /// 
    /// USAGE:
    /// bool emailExists = await userRepo.AnyAsync(u => u.Email == email);
    /// 
    /// Useful for validation before insert/update
    /// </summary>
    Task<bool> AnyAsync(Func<TEntity, bool> predicate);

    // ========== CREATE OPERATIONS ==========

    /// <summary>
    /// Add a new entity
    /// 
    /// IMPORTANT: This doesn't save to database immediately!
    /// You must call SaveChangesAsync() after this
    /// 
    /// USAGE:
    /// var user = new User { Email = "john@example.com" };
    /// await userRepo.AddAsync(user);
    /// await unitOfWork.SaveChangesAsync();  // Now it's saved
    /// </summary>
    Task<TEntity> AddAsync(TEntity entity);

    // ========== UPDATE OPERATIONS ==========

    /// <summary>
    /// Update an entity
    /// 
    /// IMPORTANT: Entity must already be tracked by DbContext
    /// EF Core tracks entities fetched from database
    /// 
    /// USAGE:
    /// user.FirstName = "Jane";
    /// await userRepo.UpdateAsync(user);
    /// await unitOfWork.SaveChangesAsync();
    /// </summary>
    Task<TEntity> UpdateAsync(TEntity entity);

    // ========== DELETE OPERATIONS ==========

    /// <summary>
    /// Delete an entity
    /// 
    /// USAGE:
    /// await userRepo.DeleteAsync(user);
    /// await unitOfWork.SaveChangesAsync();
    /// 
    /// Returns bool indicating if deletion was successful
    /// </summary>
    Task<bool> DeleteAsync(TEntity entity);

    // ========== QUERY HELPERS ==========

    /// <summary>
    /// Get paged results
    /// 
    /// WHY PAGINATION?
    /// - Performance: Returns only requested page
    /// - Memory: Doesn't load all entities
    /// - UX: Users see results immediately
    /// 
    /// USAGE:
    /// var page1 = await empRepo.GetPagedAsync(pageNumber: 1, pageSize: 10);
    /// </summary>
    Task<(IEnumerable<TEntity> items, int totalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        Func<TEntity, bool>? predicate = null
    );

    /// <summary>
    /// Get count of all entities
    /// 
    /// USAGE:
    /// int totalUsers = await userRepo.CountAsync();
    /// </summary>
    Task<int> CountAsync();

    /// <summary>
    /// Get count matching a condition
    /// 
    /// USAGE:
    /// int activeEmployees = await empRepo.CountAsync(e => e.IsActive == true);
    /// </summary>
    Task<int> CountAsync(Func<TEntity, bool> predicate);
}
