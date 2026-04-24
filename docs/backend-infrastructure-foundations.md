# MarketNest — Backend Infrastructure Foundations

> Version: 0.1 | Status: Draft | Date: 2026-04  
> Defines all base classes, contracts, and infrastructure services that EVERY module inherits.  
> **Rule: Build these once, never duplicate. If you find yourself copy-pasting a handler — it belongs here.**

---

## 1. Startup Infrastructure: Database Migrations + Seed Data

### 1.1 Auto-Migration on Startup

Every run in non-Production (and explicitly opted-in Production) will:
1. Apply pending EF Core migrations
2. Run all registered `IDataSeeder` implementations in order

```csharp
// Infrastructure/Startup/DatabaseInitializer.cs
public class DatabaseInitializer(
    MarketNestDbContext db,
    IEnumerable<IDataSeeder> seeders,
    ILogger<DatabaseInitializer> logger,
    IHostEnvironment env)
{
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        // Step 1: Apply pending migrations
        var pending = await db.Database.GetPendingMigrationsAsync(ct);
        if (pending.Any())
        {
            logger.LogInformation("Applying {Count} pending migrations: {Migrations}",
                pending.Count(), string.Join(", ", pending));
            await db.Database.MigrateAsync(ct);
            logger.LogInformation("Migrations applied successfully");
        }

        // Step 2: Run seeders in priority order
        foreach (var seeder in seeders.OrderBy(s => s.Order))
        {
            if (!env.IsProduction() || seeder.RunInProduction)
            {
                logger.LogInformation("Running seeder: {Seeder}", seeder.GetType().Name);
                await seeder.SeedAsync(ct);
            }
        }
    }
}

// Register in Program.cs
builder.Services.AddScoped<DatabaseInitializer>();

// Run before app starts
using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider
               .GetRequiredService<DatabaseInitializer>()
               .InitializeAsync();
}
```

### 1.2 IDataSeeder Contract

```csharp
// Core/Common/IDataSeeder.cs
public interface IDataSeeder
{
    /// <summary>Lower number = runs first. Convention: 100=Roles, 200=Admin, 300=Categories, 400=Demo</summary>
    int Order { get; }
    
    /// <summary>true = safe to run in Production (idempotent, reference data only)</summary>
    bool RunInProduction { get; }
    
    Task SeedAsync(CancellationToken ct = default);
}
```

### 1.3 Seeder Implementations

```csharp
// Identity/Infrastructure/Seeders/RoleSeeder.cs
public class RoleSeeder(RoleManager<IdentityRole<Guid>> roleManager) : IDataSeeder
{
    public int Order => 100;
    public bool RunInProduction => true; // Safe — idempotent role creation

    public async Task SeedAsync(CancellationToken ct = default)
    {
        string[] roles = ["Admin", "Seller", "Buyer"];
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));
        }
    }
}

// Identity/Infrastructure/Seeders/AdminUserSeeder.cs
public class AdminUserSeeder(
    UserManager<AppUser> userManager,
    IConfiguration config) : IDataSeeder
{
    public int Order => 200;
    public bool RunInProduction => true;

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var email = config["Seed:AdminEmail"] ?? "admin@marketnest.local";
        if (await userManager.FindByEmailAsync(email) is not null) return; // idempotent

        var user = new AppUser { Email = email, UserName = email, EmailConfirmed = true };
        await userManager.CreateAsync(user, config["Seed:AdminPassword"] ?? "Admin@123!");
        await userManager.AddToRoleAsync(user, "Admin");
    }
}

// Catalog/Infrastructure/Seeders/CategorySeeder.cs
public class CategorySeeder(MarketNestDbContext db) : IDataSeeder
{
    public int Order => 300;
    public bool RunInProduction => true;

    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (await db.Categories.AnyAsync(ct)) return; // idempotent

        db.Categories.AddRange(
            new Category("electronics",   "Electronics"),
            new Category("fashion",        "Fashion"),
            new Category("home-living",    "Home & Living"),
            new Category("sports",         "Sports & Outdoors"),
            new Category("books",          "Books")
        );
        await db.SaveChangesAsync(ct);
    }
}

// DemoDataSeeder — Development only, rich fake data
// Catalog/Infrastructure/Seeders/DemoDataSeeder.cs
public class DemoDataSeeder(MarketNestDbContext db, UserManager<AppUser> userManager) : IDataSeeder
{
    public int Order => 400;
    public bool RunInProduction => false; // NEVER in production

    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (await db.Storefronts.AnyAsync(ct)) return;
        // Create 3 demo sellers, 5 products each, sample orders, sample reviews...
    }
}
```

### 1.4 Seeder Registration (DI)

```csharp
// Each module registers its own seeders
// Identity/Infrastructure/DependencyInjection.cs
services.AddScoped<IDataSeeder, RoleSeeder>();
services.AddScoped<IDataSeeder, AdminUserSeeder>();

// Catalog/Infrastructure/DependencyInjection.cs
services.AddScoped<IDataSeeder, CategorySeeder>();
services.AddScoped<IDataSeeder, DemoDataSeeder>();
```

---

## 2. Base Repository Contract

### 2.1 IBaseRepository<T, TKey>

```csharp
// Core/Common/Persistence/IBaseRepository.cs
public interface IBaseRepository<TEntity, TKey>
    where TEntity : Entity<TKey>
{
    // ── Read ──────────────────────────────────────────────────────────────
    Task<TEntity?> GetByKeyAsync(TKey id, CancellationToken ct = default);
    Task<TEntity>  GetByKeyOrThrowAsync(TKey id, CancellationToken ct = default);
    Task<bool>     ExistsAsync(TKey id, CancellationToken ct = default);

    // ── Write ─────────────────────────────────────────────────────────────
    void Add(TEntity entity);
    void Update(TEntity entity);    // EF tracks; explicit for clarity
    void Remove(TEntity entity);    // Soft delete via interceptor

    // ── Persistence ───────────────────────────────────────────────────────
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
```

### 2.2 BaseRepository<T, TKey> Implementation

```csharp
// Core/Common/Persistence/BaseRepository.cs
public abstract class BaseRepository<TEntity, TKey>(MarketNestDbContext db)
    : IBaseRepository<TEntity, TKey>
    where TEntity : Entity<TKey>
{
    protected readonly MarketNestDbContext Db = db;
    protected readonly DbSet<TEntity> Set = db.Set<TEntity>();

    public virtual async Task<TEntity?> GetByKeyAsync(TKey id, CancellationToken ct = default)
        => await Set.FirstOrDefaultAsync(e => e.Id!.Equals(id), ct);

    public virtual async Task<TEntity> GetByKeyOrThrowAsync(TKey id, CancellationToken ct = default)
        => await GetByKeyAsync(id, ct)
           ?? throw new NotFoundException(typeof(TEntity).Name, id!.ToString()!);

    public virtual async Task<bool> ExistsAsync(TKey id, CancellationToken ct = default)
        => await Set.AnyAsync(e => e.Id!.Equals(id), ct);

    public virtual void Add(TEntity entity)    => Set.Add(entity);
    public virtual void Update(TEntity entity) => Set.Update(entity);
    public virtual void Remove(TEntity entity) => Set.Remove(entity);

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => Db.SaveChangesAsync(ct);

    // ── Protected helpers for derived repositories ─────────────────────
    protected IQueryable<TEntity> Query()           => Set.AsNoTracking();
    protected IQueryable<TEntity> QueryTracked()    => Set;
    protected IQueryable<TEntity> QueryWithDeleted()=> Set.IgnoreQueryFilters();
}
```

### 2.3 Aggregate Repository (extends base with domain event dispatch)

```csharp
// Core/Common/Persistence/AggregateRepository.cs
public abstract class AggregateRepository<TAggregate, TKey>(
    MarketNestDbContext db,
    IPublisher publisher)
    : BaseRepository<TAggregate, TKey>(db)
    where TAggregate : AggregateRoot<TKey>
{
    public new async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        // Dispatch domain events BEFORE save (within same transaction)
        // Or AFTER save via Outbox (Phase 3). Toggle via config.
        var aggregates = Db.ChangeTracker
            .Entries<AggregateRoot<TKey>>()
            .Where(e => e.Entity.DomainEvents.Any())
            .Select(e => e.Entity)
            .ToList();

        var result = await Db.SaveChangesAsync(ct);

        // Publish after successful save
        foreach (var aggregate in aggregates)
        {
            foreach (var domainEvent in aggregate.DomainEvents)
                await publisher.Publish(domainEvent, ct);
            aggregate.ClearDomainEvents();
        }

        return result;
    }
}
```

---

## 3. Base Query Handler — Paged List

### 3.1 Paged List Contracts

```csharp
// Core/Common/Queries/PagedQuery.cs
public abstract record PagedQuery
{
    public int Page        { get; init; } = 1;
    public int PageSize    { get; init; } = 20;
    public string? SortBy  { get; init; }
    public bool SortDesc   { get; init; } = false;
    public string? Search  { get; init; }

    public int Skip => (Page - 1) * PageSize;

    // Validation: override to add per-query constraints
    public virtual IEnumerable<ValidationFailure> Validate()
    {
        if (Page < 1)    yield return new("Page", "Page must be ≥ 1");
        if (PageSize < 1 || PageSize > 100)
                         yield return new("PageSize", "PageSize must be between 1 and 100");
    }
}

// Core/Common/Queries/PagedResult.cs
public record PagedResult<T>
{
    public IReadOnlyList<T> Items     { get; init; } = [];
    public int              Page      { get; init; }
    public int              PageSize  { get; init; }
    public int              TotalCount{ get; init; }
    public int              TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool             HasPrev   => Page > 1;
    public bool             HasNext   => Page < TotalPages;

    public static PagedResult<T> Empty(int page, int pageSize)
        => new() { Items = [], Page = page, PageSize = pageSize, TotalCount = 0 };

    public PagedResult<TOut> Map<TOut>(Func<T, TOut> mapper)
        => new()
        {
            Items      = Items.Select(mapper).ToList(),
            Page       = Page,
            PageSize   = PageSize,
            TotalCount = TotalCount
        };
}
```

### 3.2 BasePagedQueryHandler

```csharp
// Core/Common/Queries/BasePagedQueryHandler.cs
/// <summary>
/// Base handler for all paged list screens.
/// Provides: filter → sort → paginate → project pipeline.
/// 
/// Usage:
///   1. Inherit this class
///   2. Override BuildQuery() to apply module-specific filters
///   3. Override ApplySort() if custom sort columns needed
///   4. Call GetPagedListAsync() in Handle()
/// </summary>
public abstract class BasePagedQueryHandler<TQuery, TDto>(MarketNestDbContext db)
    where TQuery : PagedQuery
{
    protected readonly MarketNestDbContext Db = db;

    /// <summary>Apply module-specific WHERE clauses here. Start from Db.Set&lt;T&gt;().AsNoTracking()</summary>
    protected abstract IQueryable<TDto> BuildQuery(TQuery query);

    /// <summary>Override to support sortBy column mapping. Default: no sort.</summary>
    protected virtual IQueryable<TDto> ApplySort(IQueryable<TDto> query, TQuery request)
        => query; // Override with switch on request.SortBy

    protected async Task<PagedResult<TDto>> GetPagedListAsync(TQuery request, CancellationToken ct)
    {
        var baseQuery = BuildQuery(request);
        var sorted    = ApplySort(baseQuery, request);

        var total = await sorted.CountAsync(ct);
        if (total == 0) return PagedResult<TDto>.Empty(request.Page, request.PageSize);

        var items = await sorted
            .Skip(request.Skip)
            .Take(request.PageSize)
            .ToListAsync(ct);

        return new PagedResult<TDto>
        {
            Items      = items,
            Page       = request.Page,
            PageSize   = request.PageSize,
            TotalCount = total
        };
    }
}
```

### 3.3 Real Usage Example

```csharp
// Catalog/Application/Queries/GetProductListQuery.cs
public record GetProductListQuery : PagedQuery
{
    public Guid?   StoreId    { get; init; }
    public string? Category   { get; init; }
    public decimal? MinPrice  { get; init; }
    public decimal? MaxPrice  { get; init; }
}

public record ProductListItemDto(
    Guid   ProductId,
    string Title,
    string StoreSlug,
    decimal Price,
    decimal? CompareAtPrice,
    double  Rating,
    int     ReviewCount,
    string  ThumbnailUrl);

// Catalog/Application/Queries/GetProductListQueryHandler.cs
public class GetProductListQueryHandler(MarketNestDbContext db)
    : BasePagedQueryHandler<GetProductListQuery, ProductListItemDto>(db),
      IQueryHandler<GetProductListQuery, PagedResult<ProductListItemDto>>
{
    public Task<PagedResult<ProductListItemDto>> Handle(
        GetProductListQuery request, CancellationToken ct)
        => GetPagedListAsync(request, ct);

    protected override IQueryable<ProductListItemDto> BuildQuery(GetProductListQuery q)
    {
        var query = Db.Products.AsNoTracking()
            .Where(p => p.Status == ProductStatus.Active);

        if (q.StoreId.HasValue)   query = query.Where(p => p.StoreId == q.StoreId);
        if (q.Category is not null) query = query.Where(p => p.Category.Code == q.Category);
        if (!string.IsNullOrWhiteSpace(q.Search))
            query = query.Where(p => EF.Functions.ILike(p.Title, $"%{q.Search}%"));
        if (q.MinPrice.HasValue)  query = query.Where(p => p.Variants.Any(v => v.Price >= q.MinPrice));
        if (q.MaxPrice.HasValue)  query = query.Where(p => p.Variants.Any(v => v.Price <= q.MaxPrice));

        return query.Select(p => new ProductListItemDto(
            p.Id, p.Title, p.Store.Slug,
            p.Variants.Min(v => v.Price),
            p.Variants.Min(v => (decimal?)v.CompareAtPrice),
            p.AverageRating, p.ReviewCount,
            p.ThumbnailUrl ?? "/images/placeholder.webp"));
    }

    protected override IQueryable<ProductListItemDto> ApplySort(
        IQueryable<ProductListItemDto> q, GetProductListQuery req)
        => (req.SortBy, req.SortDesc) switch
        {
            ("price",  false) => q.OrderBy(x => x.Price),
            ("price",  true)  => q.OrderByDescending(x => x.Price),
            ("rating", false) => q.OrderBy(x => x.Rating),
            ("rating", true)  => q.OrderByDescending(x => x.Rating),
            _                 => q.OrderByDescending(x => x.ReviewCount) // default: popularity
        };
}
```

---

## 4. Base Command Handlers

### 4.1 Two Tiers of Command Handlers

```
SimpleCommandHandler   ← Pure CRUD, no domain events, no business logic
                         (configuration screens, lookup table management)

CommandHandler         ← Full DDD: validate → load aggregate → execute → dispatch events
                         (order placement, dispute opening, payout processing)
```

### 4.2 SimpleCommandHandler — For CRUD Screens

```csharp
// Core/Common/Commands/SimpleCommandHandler.cs
/// <summary>
/// For simple CRUD operations with no domain events, no complex invariants.
/// Use for: category management, config tables, admin lookups.
/// NOT for: orders, payments, disputes — use full CommandHandler + Aggregate.
/// </summary>
public abstract class SimpleCommandHandler<TCommand, TResult>
    : ICommandHandler<TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    public abstract Task<Result<TResult, Error>> Handle(TCommand command, CancellationToken ct);

    // Provided helpers — avoid boilerplate in derived classes
    protected static Result<TResult, Error> Ok(TResult value)     => Result.Success<TResult, Error>(value);
    protected static Result<TResult, Error> Fail(Error error)     => Result.Failure<TResult, Error>(error);
    protected static Result<Unit, Error>    Ok()                  => Result.Success<Unit, Error>(Unit.Value);
    protected static Result<Unit, Error>    Fail(string code, string msg)
        => Result.Failure<Unit, Error>(new Error(code, msg, ErrorType.Validation));
}
```

```csharp
// Example: Catalog/Application/Commands/UpdateCategoryCommand.cs
// Simple CRUD — no domain events needed
public record UpdateCategoryCommand(Guid CategoryId, string DisplayName, bool IsActive)
    : ICommand<Unit>;

public class UpdateCategoryCommandHandler(MarketNestDbContext db)
    : SimpleCommandHandler<UpdateCategoryCommand, Unit>
{
    public override async Task<Result<Unit, Error>> Handle(
        UpdateCategoryCommand cmd, CancellationToken ct)
    {
        var category = await db.Categories.FindAsync([cmd.CategoryId], ct);
        if (category is null) return Fail(Errors.Category.NotFound(cmd.CategoryId));

        category.Update(cmd.DisplayName, cmd.IsActive);
        await db.SaveChangesAsync(ct);
        return Ok();
    }
}
```

### 4.3 Full CommandHandler — For Domain Operations

```csharp
// Core/Common/Commands/CommandHandler.cs
/// <summary>
/// For domain operations: load aggregate → invoke domain method → save → events dispatched.
/// Always goes through Repository (which handles event dispatch).
/// </summary>
public abstract class CommandHandler<TCommand, TResult>
    : ICommandHandler<TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    public abstract Task<Result<TResult, Error>> Handle(TCommand command, CancellationToken ct);

    protected static Result<TResult, Error> Ok(TResult value) => Result.Success<TResult, Error>(value);
    protected static Result<TResult, Error> Fail(Error error) => Result.Failure<TResult, Error>(error);
    protected static Result<Unit, Error>    Ok()              => Result.Success<Unit, Error>(Unit.Value);
}
```

```csharp
// Example: Orders/Application/Commands/PlaceOrderCommandHandler.cs
// Full DDD — loads aggregates, invokes domain logic, dispatches events
public class PlaceOrderCommandHandler(
    IOrderRepository orders,
    ICartRepository carts,
    ICartReservationService reservations,
    IPaymentGateway payments)
    : CommandHandler<PlaceOrderCommand, PlaceOrderResult>
{
    public override async Task<Result<PlaceOrderResult, Error>> Handle(
        PlaceOrderCommand cmd, CancellationToken ct)
    {
        var cart = await carts.GetByKeyOrThrowAsync(cmd.CartId, ct);

        // Domain validation
        if (cart.BuyerId != cmd.BuyerId) return Fail(Errors.Cart.NotOwned);
        if (!cart.Items.Any())           return Fail(Errors.Cart.Empty);

        // Build order (domain logic)
        var orderResult = Order.Create(cart, cmd.ShippingAddress);
        if (orderResult.IsFailure) return Fail(orderResult.Error);

        var order = orderResult.Value;

        // Payment (stub Phase 1)
        var payment = await payments.CaptureAsync(order, cmd.PaymentMethod, ct);
        if (payment.IsFailure) return Fail(payment.Error);

        // Persist — AggregateRepository dispatches OrderPlacedEvent
        orders.Add(order);
        await orders.SaveChangesAsync(ct);

        // Release cart reservations
        await reservations.ReleaseAllAsync(cmd.BuyerId, ct);
        cart.MarkCheckedOut();
        await carts.SaveChangesAsync(ct);

        return Ok(new PlaceOrderResult(order.Id, order.Total));
    }
}
```

---

## 5. Common Service Interfaces

### 5.1 ICurrentUserService

```csharp
// Core/Common/Services/ICurrentUserService.cs
public interface ICurrentUserService
{
    Guid   UserId   { get; }
    string Email    { get; }
    string Role     { get; }
    bool   IsAuthenticated { get; }
    bool   IsAdmin   => Role == "Admin";
    bool   IsSeller  => Role == "Seller";
    bool   IsBuyer   => Role == "Buyer";

    /// <summary>Throws UnauthorizedException if not authenticated</summary>
    Guid RequireUserId();
}

// Web/Infrastructure/CurrentUserService.cs
public class CurrentUserService(IHttpContextAccessor accessor) : ICurrentUserService
{
    private ClaimsPrincipal? User => accessor.HttpContext?.User;

    public bool   IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;
    public Guid   UserId   => Guid.Parse(User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? Guid.Empty.ToString());
    public string Email    => User?.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
    public string Role     => User?.FindFirstValue(ClaimTypes.Role)  ?? string.Empty;

    public Guid RequireUserId()
        => IsAuthenticated ? UserId : throw new UnauthorizedException();
}
```

### 5.2 IDateTimeService

```csharp
// Core/Common/Services/IDateTimeService.cs
/// <summary>Abstraction over DateTime.UtcNow — enables deterministic testing</summary>
public interface IDateTimeService
{
    DateTime UtcNow { get; }
    DateOnly TodayUtc => DateOnly.FromDateTime(UtcNow);
}

// Production implementation
public class DateTimeService : IDateTimeService
{
    public DateTime UtcNow => DateTime.UtcNow;
}

// Test implementation
public class FakeDateTimeService(DateTime fixedTime) : IDateTimeService
{
    public DateTime UtcNow => fixedTime;
}
```

### 5.3 ISlugService

```csharp
// Core/Common/Services/ISlugService.cs
public interface ISlugService
{
    string Generate(string input);
    Task<string> GenerateUniqueAsync(string input, Func<string, Task<bool>> existsCheck);
}

// Infrastructure/Services/SlugService.cs
public class SlugService : ISlugService
{
    public string Generate(string input)
    {
        var slug = input.ToLowerInvariant();
        slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
        slug = Regex.Replace(slug, @"\s+", "-");
        slug = Regex.Replace(slug, @"-+", "-").Trim('-');
        return slug[..Math.Min(slug.Length, 50)];
    }

    public async Task<string> GenerateUniqueAsync(string input, Func<string, Task<bool>> existsCheck)
    {
        var base_ = Generate(input);
        var candidate = base_;
        var counter = 1;
        while (await existsCheck(candidate))
            candidate = $"{base_}-{counter++}";
        return candidate;
    }
}
```

### 5.4 IStorageService (File Uploads)

```csharp
// Core/Common/Services/IStorageService.cs
public interface IStorageService
{
    Task<StorageResult> UploadAsync(
        Stream content,
        string fileName,
        string contentType,
        string folder,           // e.g. "products", "storefronts", "disputes"
        CancellationToken ct = default);

    Task DeleteAsync(string fileUrl, CancellationToken ct = default);
}

public record StorageResult(string Url, string FileName, long SizeBytes);

// Phase 1: Local disk storage
public class LocalDiskStorageService(IWebHostEnvironment env, IHttpContextAccessor accessor)
    : IStorageService
{
    public async Task<StorageResult> UploadAsync(
        Stream content, string fileName, string contentType, string folder, CancellationToken ct)
    {
        var ext       = Path.GetExtension(fileName);
        var safeName  = $"{Guid.NewGuid()}{ext}";
        var dir       = Path.Combine(env.WebRootPath, "uploads", folder);
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, safeName);

        await using var file = File.Create(path);
        await content.CopyToAsync(file, ct);

        var baseUrl = $"{accessor.HttpContext!.Request.Scheme}://{accessor.HttpContext.Request.Host}";
        return new StorageResult($"{baseUrl}/uploads/{folder}/{safeName}", safeName, new FileInfo(path).Length);
    }

    public Task DeleteAsync(string fileUrl, CancellationToken ct)
    {
        // Extract path from URL and delete
        // ...
        return Task.CompletedTask;
    }
}

// Phase 2+: Swap with Azure Blob / S3 — same interface, no callers change
```

---

## 6. EF Core Common Configurations

### 6.1 Soft Delete Interceptor

```csharp
// Infrastructure/Persistence/Interceptors/SoftDeleteInterceptor.cs
public class SoftDeleteInterceptor : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct)
    {
        if (eventData.Context is null) return base.SavingChangesAsync(eventData, result, ct);

        foreach (var entry in eventData.Context.ChangeTracker.Entries<ISoftDeletable>())
        {
            if (entry.State == EntityState.Deleted)
            {
                entry.State = EntityState.Modified;
                entry.Entity.IsDeleted = true;
                entry.Entity.DeletedAt = DateTime.UtcNow;
            }
        }

        return base.SavingChangesAsync(eventData, result, ct);
    }
}

// Interface on any entity that should soft-delete
public interface ISoftDeletable
{
    bool      IsDeleted { get; set; }
    DateTime? DeletedAt { get; set; }
}
```

### 6.2 Audit Trail Interceptor

```csharp
// Infrastructure/Persistence/Interceptors/AuditInterceptor.cs
public class AuditInterceptor(ICurrentUserService currentUser) : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct)
    {
        foreach (var entry in eventData.Context!.ChangeTracker.Entries<IAuditable>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = DateTime.UtcNow;
                    entry.Entity.CreatedBy = currentUser.IsAuthenticated ? currentUser.UserId : null;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    entry.Entity.UpdatedBy = currentUser.IsAuthenticated ? currentUser.UserId : null;
                    break;
            }
        }

        return base.SavingChangesAsync(eventData, result, ct);
    }
}

public interface IAuditable
{
    DateTime  CreatedAt { get; set; }
    Guid?     CreatedBy { get; set; }
    DateTime? UpdatedAt { get; set; }
    Guid?     UpdatedBy { get; set; }
}
```

### 6.3 Base Entity Configurations

```csharp
// Core/Common/Persistence/BaseEntityConfiguration.cs
public abstract class BaseEntityConfiguration<TEntity, TKey>
    : IEntityTypeConfiguration<TEntity>
    where TEntity : Entity<TKey>
{
    public virtual void Configure(EntityTypeBuilder<TEntity> builder)
    {
        builder.HasKey(e => e.Id);

        // Audit columns if entity implements IAuditable
        if (typeof(IAuditable).IsAssignableFrom(typeof(TEntity)))
        {
            builder.Property("CreatedAt").IsRequired();
            builder.Property("UpdatedAt");
        }

        // Soft delete filter if entity implements ISoftDeletable
        if (typeof(ISoftDeletable).IsAssignableFrom(typeof(TEntity)))
        {
            builder.Property("IsDeleted").HasDefaultValue(false);
            builder.HasQueryFilter(e => !EF.Property<bool>(e, "IsDeleted"));
        }
    }
}
```

---

## 7. MediatR Pipeline Behaviors

```csharp
// Validation → Logging → Performance → (Exception is middleware, not pipeline)

// 1. Validation Behavior (runs FluentValidation before any handler)
public class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (!validators.Any()) return await next();

        var context = new ValidationContext<TRequest>(request);
        var failures = validators
            .Select(v => v.Validate(context))
            .SelectMany(r => r.Errors)
            .Where(e => e is not null)
            .ToList();

        if (failures.Count > 0)
            throw new ValidationException(failures);

        return await next();
    }
}

// 2. Performance Behavior (warns on slow requests)
public class PerformanceBehavior<TRequest, TResponse>(ILogger<TRequest> logger)
    : IPipelineBehavior<TRequest, TResponse>
{
    private readonly Stopwatch _timer = new();

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        _timer.Start();
        var response = await next();
        _timer.Stop();

        if (_timer.ElapsedMilliseconds > 500) // warn if > 500ms
            logger.LogWarning("Slow request: {RequestName} took {Elapsed}ms {@Request}",
                typeof(TRequest).Name, _timer.ElapsedMilliseconds, request);

        return response;
    }
}

// 3. Logging Behavior (optional — structured request/response logging)
public class LoggingBehavior<TRequest, TResponse>(ILogger<TRequest> logger)
    : IPipelineBehavior<TRequest, TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        logger.LogDebug("Handling {RequestName} {@Request}", typeof(TRequest).Name, request);
        var response = await next();
        logger.LogDebug("Handled {RequestName}", typeof(TRequest).Name);
        return response;
    }
}

// Register pipeline in Program.cs
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(PerformanceBehavior<,>));
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
});
```

---

## 8. Utility Services

### 8.1 IEmailTemplateRenderer

```csharp
// Notifications/Application/IEmailTemplateRenderer.cs
public interface IEmailTemplateRenderer
{
    Task<string> RenderAsync(string templateName, object model);
}

// Templates live in: Notifications/Templates/{templateName}.cshtml
// Renders Razor template to HTML string using RazorLight or IViewRenderService
```

### 8.2 IPaginationHelper (for Razor Pages)

```csharp
// Web/Infrastructure/PaginationHelper.cs
public static class PaginationHelper
{
    /// <summary>Generate page numbers with ellipsis: [1] ... [4] [5] [6] ... [10]</summary>
    public static IEnumerable<PageItem> GetPageItems(int currentPage, int totalPages, int windowSize = 2)
    {
        var items = new List<PageItem>();
        if (totalPages <= 1) return items;

        // Always show first
        items.Add(new PageItem(1, currentPage == 1));

        var start = Math.Max(2, currentPage - windowSize);
        var end   = Math.Min(totalPages - 1, currentPage + windowSize);

        if (start > 2) items.Add(PageItem.Ellipsis);
        for (var i = start; i <= end; i++)
            items.Add(new PageItem(i, currentPage == i));
        if (end < totalPages - 1) items.Add(PageItem.Ellipsis);

        if (totalPages > 1) items.Add(new PageItem(totalPages, currentPage == totalPages));

        return items;
    }
}

public record PageItem(int? Number, bool IsCurrent, bool IsEllipsis = false)
{
    public static PageItem Ellipsis => new(null, false, true);
}
```

---

## 9. Module Registration Convention

Each module exposes a single extension method. `Program.cs` stays clean:

```csharp
// Each module: {Module}/Infrastructure/DependencyInjection.cs
public static class CatalogModule
{
    public static IServiceCollection AddCatalogModule(this IServiceCollection services)
    {
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IStorefrontRepository, StorefrontRepository>();
        services.AddScoped<IDataSeeder, CategorySeeder>();
        services.AddScoped<IDataSeeder, DemoDataSeeder>();
        // Register validators, event handlers...
        return services;
    }
}

// Program.cs — clean and obvious
builder.Services
    .AddInfrastructure(builder.Configuration)  // DB, Redis, JWT, Interceptors
    .AddIdentityModule()
    .AddCatalogModule()
    .AddCartModule()
    .AddOrdersModule()
    .AddPaymentsModule()
    .AddReviewsModule()
    .AddDisputesModule()
    .AddNotificationsModule();
```
