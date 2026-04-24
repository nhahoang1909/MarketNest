# MarketNest — Advanced Patterns: Transaction, UoW, Authorization, File Upload

> Version: 0.1 | Status: Draft | Date: 2026-04  
> Covers: `[Transaction]` attribute, `[Access]` permission attribute, Unit of Work with domain event collection, and the file upload pipeline (validate → scan → store → return ID).

---

## 1. Transaction Attribute + Write/Read Controller Split

### 1.1 Controller Split Convention

```
Controllers/
├── Read/                           ← GET only, no transactions, cacheable
│   ├── ProductReadController.cs
│   ├── OrderReadController.cs
│   └── StorefrontReadController.cs
│
└── Write/                          ← POST/PUT/PATCH/DELETE, always transactional
    ├── OrderWriteController.cs
    ├── CartWriteController.cs
    └── DisputeWriteController.cs

Razor Pages (HTMX): same handler file — read in OnGet*, write in OnPost*
  OnGetAsync()      → no transaction, uses read service / query handler
  OnPostAsync()     → [Transaction] applied automatically via convention filter
```

### 1.2 `[Transaction]` Attribute + Filter

```csharp
// Core/Common/Attributes/TransactionAttribute.cs
/// <summary>
/// Marks an endpoint or page handler as requiring a database transaction.
/// Applied via TransactionActionFilter — wraps handler in BeginTransaction / Commit / Rollback.
/// Usage:
///   [Transaction]                          ← default isolation
///   [Transaction(IsolationLevel.Serializable)] ← explicit isolation
///   [Transaction(IsolationLevel.ReadCommitted, timeoutSeconds: 30)]
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public class TransactionAttribute(
    IsolationLevel isolation = IsolationLevel.ReadCommitted,
    int timeoutSeconds = 30) : Attribute
{
    public IsolationLevel Isolation       { get; } = isolation;
    public int            TimeoutSeconds  { get; } = timeoutSeconds;
}

// Web/Infrastructure/Filters/TransactionActionFilter.cs
/// <summary>
/// Wraps controller actions marked [Transaction] in a DB transaction.
/// Commits on success, rolls back on any exception.
/// Domain events are dispatched AFTER commit (see UnitOfWork).
/// </summary>
public class TransactionActionFilter(
    MarketNestDbContext db,
    IUnitOfWork uow,
    ILogger<TransactionActionFilter> logger) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var transactionAttr = context.ActionDescriptor
            .EndpointMetadata
            .OfType<TransactionAttribute>()
            .FirstOrDefault();

        // No [Transaction] attribute — pass through
        if (transactionAttr is null)
        {
            await next();
            return;
        }

        var timeout = TimeSpan.FromSeconds(transactionAttr.TimeoutSeconds);

        await using var transaction = await db.Database.BeginTransactionAsync(
            transactionAttr.Isolation, context.HttpContext.RequestAborted);

        try
        {
            var executed = await next();

            if (executed.Exception is not null && !executed.ExceptionHandled)
            {
                await transaction.RollbackAsync();
                logger.LogWarning("Transaction rolled back due to exception in {Action}",
                    context.ActionDescriptor.DisplayName);
                return;
            }

            // Flush pending changes before commit
            await db.SaveChangesAsync(context.HttpContext.RequestAborted);
            await transaction.CommitAsync();

            // Dispatch domain events AFTER successful commit
            await uow.DispatchPostCommitEventsAsync(context.HttpContext.RequestAborted);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            logger.LogError(ex, "Transaction rolled back in {Action}", context.ActionDescriptor.DisplayName);
            throw;
        }
    }
}
```

### 1.3 Razor Pages Transaction Filter Convention

```csharp
// Web/Infrastructure/Filters/RazorPageTransactionFilter.cs
/// <summary>
/// For Razor Pages: applies transaction to ALL OnPost* handlers automatically.
/// No need to add [Transaction] manually — convention-based.
/// Override with [NoTransaction] to opt out.
/// </summary>
public class RazorPageTransactionFilter(
    MarketNestDbContext db,
    IUnitOfWork uow) : IAsyncPageFilter
{
    public async Task OnPageHandlerExecutionAsync(
        PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        var isWriteHandler = context.HandlerMethod?.Name.StartsWith("OnPost", StringComparison.OrdinalIgnoreCase) == true
                          || context.HandlerMethod?.Name.StartsWith("OnPut", StringComparison.OrdinalIgnoreCase) == true
                          || context.HandlerMethod?.Name.StartsWith("OnDelete", StringComparison.OrdinalIgnoreCase) == true;

        var noTransaction = context.HandlerMethod?.MethodInfo
            .GetCustomAttribute<NoTransactionAttribute>() is not null;

        if (!isWriteHandler || noTransaction)
        {
            await next();
            return;
        }

        await using var tx = await db.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, context.HttpContext.RequestAborted);

        try
        {
            var result = await next();
            if (result.Exception is null || result.ExceptionHandled)
            {
                await db.SaveChangesAsync(context.HttpContext.RequestAborted);
                await tx.CommitAsync();
                await uow.DispatchPostCommitEventsAsync(context.HttpContext.RequestAborted);
            }
            else
            {
                await tx.RollbackAsync();
            }
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context) => Task.CompletedTask;
}

// Opt-out attribute
[AttributeUsage(AttributeTargets.Method)]
public class NoTransactionAttribute : Attribute { }

// Registration in Program.cs
builder.Services.AddRazorPages(opt =>
{
    opt.Conventions.AddFolderApplicationModelConvention("/", model =>
    {
        model.Filters.Add<RazorPageTransactionFilter>();
    });
});
```

---

## 2. Unit of Work — Domain Event Collection

### 2.1 IUnitOfWork Contract

```csharp
// Core/Common/Persistence/IUnitOfWork.cs
/// <summary>
/// Coordinates SaveChanges and domain event lifecycle across a request.
///
/// Domain event lifecycle:
///   1. Aggregate raises event → stored in aggregate's DomainEvents list
///   2. UoW.CollectPreCommitEvents()  → pulled from aggregates BEFORE SaveChanges
///      (available for in-transaction side effects, e.g. update a read model)
///   3. SaveChanges runs
///   4. UoW.CollectPostCommitEvents() → pulled AFTER commit
///      (safe to publish externally — DB state is durably committed)
///   5. UoW.DispatchPostCommitEventsAsync() → publishes to IPublisher / MassTransit
///
/// Why split pre/post commit?
///   Pre-commit events:  update derived tables, enforce cross-aggregate invariants in same TX
///   Post-commit events: send emails, push to queue, update search index — should NOT roll back
/// </summary>
public interface IUnitOfWork
{
    /// <summary>Pull domain events from all tracked aggregates (before SaveChanges)</summary>
    IReadOnlyList<IDomainEvent> CollectPreCommitEvents();

    /// <summary>Dispatch all post-commit events via publisher</summary>
    Task DispatchPostCommitEventsAsync(CancellationToken ct = default);

    /// <summary>SaveChanges + collect + dispatch in one call (for non-transaction scenarios)</summary>
    Task<int> CommitAsync(CancellationToken ct = default);
}
```

### 2.2 UnitOfWork Implementation

```csharp
// Infrastructure/Persistence/UnitOfWork.cs
public class UnitOfWork(
    MarketNestDbContext db,
    IDomainEventPublisher publisher,
    ILogger<UnitOfWork> logger) : IUnitOfWork
{
    // Domain events are collected here after commit — ready to dispatch
    private readonly List<IDomainEvent> _postCommitEvents = [];

    public IReadOnlyList<IDomainEvent> CollectPreCommitEvents()
    {
        var events = db.ChangeTracker
            .Entries<AggregateRoot>()
            .SelectMany(e => e.Entity.DomainEvents)
            .ToList();

        // Separate into pre/post commit based on event type
        // Pre-commit: events implementing IPreCommitEvent (run inside transaction)
        // Post-commit: all others (dispatch after commit)

        var preCommit  = events.OfType<IPreCommitEvent>().Cast<IDomainEvent>().ToList();
        var postCommit = events.Except(preCommit.Cast<IDomainEvent>()).ToList();

        _postCommitEvents.AddRange(postCommit);

        // DON'T clear domain events yet — EF might need a second pass
        return preCommit;
    }

    private void ClearAllDomainEvents()
    {
        foreach (var entry in db.ChangeTracker.Entries<AggregateRoot>())
            entry.Entity.ClearDomainEvents();
    }

    public async Task DispatchPostCommitEventsAsync(CancellationToken ct = default)
    {
        if (_postCommitEvents.Count == 0) return;

        logger.LogDebug("Dispatching {Count} post-commit domain events", _postCommitEvents.Count);

        foreach (var domainEvent in _postCommitEvents)
        {
            try
            {
                await publisher.PublishAsync(domainEvent, ct);
                logger.LogDebug("Dispatched domain event: {EventType}", domainEvent.GetType().Name);
            }
            catch (Exception ex)
            {
                // Post-commit events MUST NOT cause the transaction to fail
                // Log and continue — use Outbox pattern in Phase 3 for reliability
                logger.LogError(ex, "Failed to dispatch domain event: {EventType}",
                    domainEvent.GetType().Name);
            }
        }

        _postCommitEvents.Clear();
        ClearAllDomainEvents();
    }

    public async Task<int> CommitAsync(CancellationToken ct = default)
    {
        // 1. Collect pre-commit events
        var preCommitEvents = CollectPreCommitEvents();

        // 2. Dispatch pre-commit events (within same transaction if one is active)
        foreach (var evt in preCommitEvents)
            await publisher.PublishAsync(evt, ct);

        // 3. Save
        var result = await db.SaveChangesAsync(ct);

        // 4. Dispatch post-commit events
        await DispatchPostCommitEventsAsync(ct);

        return result;
    }
}
```

### 2.3 AggregateRoot — Domain Event Collection

```csharp
// Core/Common/AggregateRoot.cs
public abstract class AggregateRoot : Entity<Guid>
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void RaiseDomainEvent(IDomainEvent domainEvent)
        => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}

// Pre-commit event marker (runs INSIDE the transaction)
public interface IPreCommitEvent { }

// Example: pre-commit = update inventory read model synchronously
public record InventoryReservedEvent(Guid VariantId, int ReservedQty)
    : IDomainEvent, IPreCommitEvent;  // ← runs before commit, inside same TX

// Example: post-commit = send email notification
public record OrderPlacedEvent(Guid OrderId, Guid BuyerId, decimal Total)
    : IDomainEvent;                   // ← runs after commit (safe to fail)
```

### 2.4 BaseRepository — NO SaveChanges (UoW owns it)

```csharp
// Core/Common/Persistence/BaseRepository.cs
/// <summary>
/// Repositories do NOT call SaveChanges.
/// SaveChanges is called by:
///   1. TransactionActionFilter (wraps controller/page handler)
///   2. IUnitOfWork.CommitAsync() for non-transaction scenarios
///   3. Tests call db.SaveChangesAsync() directly
/// </summary>
public abstract class BaseRepository<TEntity, TKey>(MarketNestDbContext db)
    where TEntity : Entity<TKey>
{
    protected readonly MarketNestDbContext Db = db;
    protected DbSet<TEntity> Set => Db.Set<TEntity>();

    public virtual Task<TEntity?> GetByKeyAsync(TKey id, CancellationToken ct = default)
        => Set.FirstOrDefaultAsync(e => e.Id!.Equals(id), ct);

    public virtual async Task<TEntity> GetByKeyOrThrowAsync(TKey id, CancellationToken ct = default)
        => await GetByKeyAsync(id, ct)
           ?? throw new NotFoundException(typeof(TEntity).Name, id?.ToString() ?? "");

    public virtual Task<bool> ExistsAsync(TKey id, CancellationToken ct = default)
        => Set.AnyAsync(e => e.Id!.Equals(id), ct);

    public virtual Task<IReadOnlyList<TEntity>> ListAsync(
        Expression<Func<TEntity, bool>>? predicate = null, CancellationToken ct = default)
    {
        var query = Set.AsNoTracking();
        if (predicate is not null) query = query.Where(predicate);
        return query.ToListAsync(ct).ContinueWith(t => (IReadOnlyList<TEntity>)t.Result);
    }

    public virtual Task<TEntity?> FirstOrDefaultAsync(
        Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default)
        => Set.AsNoTracking().FirstOrDefaultAsync(predicate, ct);

    public virtual void Add(TEntity entity)    => Set.Add(entity);
    public virtual void AddRange(IEnumerable<TEntity> entities) => Set.AddRange(entities);
    public virtual void Update(TEntity entity) => Set.Update(entity);
    public virtual void Remove(TEntity entity) => Set.Remove(entity);

    // ❌ No SaveChangesAsync here — UnitOfWork owns persistence
}
```

---

## 3. `[Access]` Permission-Based Authorization

### 3.1 Permission Enum & Attribute

```csharp
// Core/Common/Authorization/Permission.cs
/// <summary>
/// Granular permission system on top of RBAC.
/// Format: {Resource}.{Action}
/// Assigned to roles in PermissionMatrix (see below).
/// </summary>
public enum Permission
{
    // ── Orders ────────────────────────────────────────
    Order_Read   = 100,
    Order_Write  = 101,
    Order_Cancel = 102,
    Order_Refund = 103,

    // ── Products ──────────────────────────────────────
    Product_Read   = 200,
    Product_Write  = 201,
    Product_Delete = 202,

    // ── Storefront ────────────────────────────────────
    Storefront_Read       = 300,
    Storefront_Write      = 301,
    Storefront_Suspend    = 302,  // Admin only

    // ── Disputes ──────────────────────────────────────
    Dispute_Read      = 400,
    Dispute_Open      = 401,
    Dispute_Respond   = 402,
    Dispute_Arbitrate = 403,    // Admin only

    // ── Reviews ───────────────────────────────────────
    Review_Read  = 500,
    Review_Write = 501,
    Review_Hide  = 502,         // Admin only

    // ── Payouts ───────────────────────────────────────
    Payout_Read    = 600,
    Payout_Process = 601,       // Admin only

    // ── Users ─────────────────────────────────────────
    User_Read    = 700,
    User_Suspend = 701,         // Admin only
    User_Delete  = 702,         // Admin only

    // ── Platform Config ───────────────────────────────
    Config_Read  = 800,
    Config_Write = 801,         // Admin only
}

// Core/Common/Authorization/AccessAttribute.cs
/// <summary>
/// Declarative permission check.
/// Usage:
///   [Access(Permission.Order_Write)]
///   [Access(Permission.Dispute_Arbitrate)]
///   [Access(Permission.Product_Write, Permission.Storefront_Write)]  ← requires ALL
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public class AccessAttribute(params Permission[] permissions) : Attribute
{
    public Permission[] Permissions { get; } = permissions;
    
    /// <summary>If true, requires ALL permissions. If false (default: ANY), requires at least one.</summary>
    public bool RequireAll { get; init; } = false;
}
```

### 3.2 Permission Matrix (Role → Permissions)

```csharp
// Core/Common/Authorization/PermissionMatrix.cs
/// <summary>
/// Single source of truth: which roles have which permissions.
/// Change here = change everywhere. No scattered role checks.
/// </summary>
public static class PermissionMatrix
{
    private static readonly Dictionary<string, HashSet<Permission>> _rolePermissions = new()
    {
        ["Buyer"] =
        [
            Permission.Order_Read,
            Permission.Order_Write,
            Permission.Order_Cancel,
            Permission.Product_Read,
            Permission.Storefront_Read,
            Permission.Dispute_Read,
            Permission.Dispute_Open,
            Permission.Review_Read,
            Permission.Review_Write,
        ],
        ["Seller"] =
        [
            Permission.Order_Read,
            Permission.Order_Write,       // confirm/ship own orders
            Permission.Product_Read,
            Permission.Product_Write,
            Permission.Product_Delete,
            Permission.Storefront_Read,
            Permission.Storefront_Write,
            Permission.Dispute_Read,
            Permission.Dispute_Respond,
            Permission.Payout_Read,
            Permission.Review_Read,
        ],
        ["Admin"] =
        [
            .. Enum.GetValues<Permission>()  // Admin has ALL permissions
        ],
    };

    public static bool HasPermission(string role, Permission permission)
        => _rolePermissions.TryGetValue(role, out var permissions)
           && permissions.Contains(permission);

    public static IEnumerable<Permission> GetPermissionsForRole(string role)
        => _rolePermissions.TryGetValue(role, out var permissions)
            ? permissions
            : [];
}
```

### 3.3 Authorization Handler

```csharp
// Web/Infrastructure/Authorization/AccessAuthorizationHandler.cs
public class AccessRequirement(Permission[] permissions, bool requireAll) : IAuthorizationRequirement
{
    public Permission[] Permissions { get; } = permissions;
    public bool         RequireAll  { get; } = requireAll;
}

public class AccessAuthorizationHandler : AuthorizationHandler<AccessRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, AccessRequirement requirement)
    {
        if (!context.User.Identity?.IsAuthenticated ?? true)
        {
            context.Fail();
            return Task.CompletedTask;
        }

        var role = context.User.FindFirstValue(ClaimTypes.Role) ?? "";

        var check = requirement.RequireAll
            ? requirement.Permissions.All(p => PermissionMatrix.HasPermission(role, p))
            : requirement.Permissions.Any(p => PermissionMatrix.HasPermission(role, p));

        if (check)
            context.Succeed(requirement);
        else
            context.Fail(new AuthorizationFailureReason(this,
                $"Missing permission(s): {string.Join(", ", requirement.Permissions)}"));

        return Task.CompletedTask;
    }
}

// Web/Infrastructure/Filters/AccessFilter.cs
/// <summary>
/// Action filter that reads [Access] attribute and enforces permissions.
/// Registered globally — works on both controllers and Razor Pages.
/// </summary>
public class AccessFilter(IAuthorizationService authService) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var accessAttrs = context.ActionDescriptor
            .EndpointMetadata
            .OfType<AccessAttribute>()
            .ToList();

        if (!accessAttrs.Any()) return;

        foreach (var attr in accessAttrs)
        {
            var requirement = new AccessRequirement(attr.Permissions, attr.RequireAll);
            var result = await authService.AuthorizeAsync(
                context.HttpContext.User,
                resource: null,
                requirements: [requirement]);

            if (!result.Succeeded)
            {
                context.Result = new ForbidResult();
                return;
            }
        }
    }
}
```

### 3.4 Usage Examples

```csharp
// Minimal API controller
[ApiController]
[Route("api/orders")]
public class OrderWriteController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    [Transaction]
    [Access(Permission.Order_Write)]
    public async Task<IActionResult> PlaceOrder([FromBody] PlaceOrderRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(request.ToCommand(User.GetUserId()), ct);
        return result.Match(Ok, err => BadRequest(err));
    }

    [HttpDelete("{orderId:guid}")]
    [Transaction]
    [Access(Permission.Order_Cancel)]
    public async Task<IActionResult> CancelOrder(Guid orderId, CancellationToken ct)
    {
        var result = await mediator.Send(new CancelOrderCommand(orderId, User.GetUserId()), ct);
        return result.Match(_ => NoContent(), err => BadRequest(err));
    }

    [HttpPost("{orderId:guid}/refund")]
    [Transaction(IsolationLevel.Serializable)]       // strict isolation for financial ops
    [Access(Permission.Order_Refund)]
    public async Task<IActionResult> RefundOrder(Guid orderId, [FromBody] RefundRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new RefundOrderCommand(orderId, req.Amount, req.Reason, User.GetUserId()), ct);
        return result.Match(Ok, err => BadRequest(err));
    }
}

// Admin-only dispute arbitration
[HttpPost("{disputeId:guid}/resolve")]
[Transaction]
[Access(Permission.Dispute_Arbitrate)]   // only Admin role has this
public async Task<IActionResult> ResolveDispute(Guid disputeId, [FromBody] ResolveDisputeRequest req)
{ ... }

// Razor Pages — automatic transaction + explicit access
[Access(Permission.Product_Write)]
public class ProductEditorModel : BasePageModel
{
    public async Task<IActionResult> OnPostAsync()
    {
        // [Transaction] applied automatically to all OnPost* by RazorPageTransactionFilter
        // [Access] checked by AccessFilter before handler runs
    }
}

// Require ALL permissions (compound check)
[Access(Permission.Order_Read, Permission.Payout_Read, RequireAll = true)]
public IActionResult GetOrderWithPayout() { ... }
```

### 3.5 Permission Check in Application Layer

```csharp
// For resource-based checks (e.g., seller can only edit OWN products):
// Use ICurrentUserService + explicit domain check in handler — not just permission attribute

public class UpdateProductCommandHandler(
    IProductRepository products,
    ICurrentUserService currentUser) : CommandHandler<UpdateProductCommand, Unit>
{
    public override async Task<Result<Unit, Error>> Handle(UpdateProductCommand cmd, CancellationToken ct)
    {
        var product = await products.GetByKeyOrThrowAsync(cmd.ProductId, ct);

        // Resource ownership check — permission attribute only checks role, not ownership
        if (product.StoreId != await GetSellerStoreId(currentUser.RequireUserId(), ct)
            && !currentUser.IsAdmin)
            return Fail(Error.Forbidden("You can only edit your own products"));

        // ... proceed
    }
}
```

---

## 4. File Upload Pipeline

### 4.1 Blob Storage Strategy by Phase

| Phase | Dev | Production |
|-------|-----|-----------|
| 1 | Local disk (`wwwroot/uploads/`) | Local disk or MinIO (Chainguard) |
| 2 | MinIO in Docker Compose | **Cloudflare R2** (S3-compatible, free 10GB/mo, zero egress) |
| 4 (K8s) | MinIO sidecar | Cloudflare R2 or SeaweedFS in-cluster |

**Why Cloudflare R2 for production:**
- Free tier: 10 GB storage, 1M Class A ops, 10M Class B ops/month
- Zero egress fees (unlike S3/Azure)
- S3-compatible API → same .NET `AWSSDK.S3` client, just different endpoint
- When you join a company using Azure Blob — same interface swap, 1 line config change

### 4.2 File Upload Domain Rules

```csharp
// Core/Common/Files/FileUploadRules.cs
public static class FileUploadRules
{
    // Allowed types per use context
    public static readonly AllowedFileTypes ProductImages = new(
        MaxCount: 5,
        MaxSizeBytes: 5 * 1024 * 1024,  // 5MB each
        AllowedMimeTypes: ["image/jpeg", "image/png", "image/webp"],
        AllowedExtensions: [".jpg", ".jpeg", ".png", ".webp"]);

    public static readonly AllowedFileTypes StorefrontBanner = new(
        MaxCount: 1,
        MaxSizeBytes: 2 * 1024 * 1024,  // 2MB
        AllowedMimeTypes: ["image/jpeg", "image/png", "image/webp"],
        AllowedExtensions: [".jpg", ".jpeg", ".png", ".webp"]);

    public static readonly AllowedFileTypes DisputeEvidence = new(
        MaxCount: 5,
        MaxSizeBytes: 10 * 1024 * 1024,  // 10MB
        AllowedMimeTypes: ["image/jpeg", "image/png", "image/webp", "application/pdf"],
        AllowedExtensions: [".jpg", ".jpeg", ".png", ".webp", ".pdf"]);

    public static readonly AllowedFileTypes UserDocuments = new(
        MaxCount: 3,
        MaxSizeBytes: 10 * 1024 * 1024,
        AllowedMimeTypes: ["application/pdf", "image/jpeg", "image/png"],
        AllowedExtensions: [".pdf", ".jpg", ".jpeg", ".png"]);
}

public record AllowedFileTypes(
    int MaxCount,
    long MaxSizeBytes,
    string[] AllowedMimeTypes,
    string[] AllowedExtensions);
```

### 4.3 File Upload Pipeline

```
Upload Request (IFormFile[])
        │
        ▼
1. [FileValidationFilter]         ← MIME type, extension, size, count
        │ fail → 400 BadRequest
        ▼
2. [AntivirusScanStep]            ← ClamAV scan (dev: skip; prod: required)
        │ infected → 422 + quarantine
        ▼
3. [ImageOptimizationStep]        ← Resize to max dimensions, convert to WebP (images only)
        │
        ▼
4. [StorageUploadStep]            ← Upload to IStorageService (local/R2/SeaweedFS)
        │ fail → 500 + cleanup
        ▼
5. [FileRecordStep]               ← Save UploadedFile record to DB, return FileId (Guid)
        │
        ▼
Return: { fileId: Guid, url: string, thumbnailUrl?: string }
        │
Caller stores fileId in domain entity (never the URL directly)
```

### 4.4 UploadedFile Entity

```csharp
// Core/Files/Domain/UploadedFile.cs
/// <summary>
/// Every uploaded file gets a record in the DB.
/// Domain entities store FileId (Guid), never raw URLs.
/// URL is resolved at query time via IStorageService.GetUrlAsync(fileId)
/// This allows CDN migration without DB updates.
/// </summary>
public class UploadedFile : Entity<Guid>, IAuditable
{
    public string      StorageKey       { get; private set; } = "";   // bucket/folder/filename.webp
    public string      OriginalFileName { get; private set; } = "";
    public string      ContentType      { get; private set; } = "";
    public long        SizeBytes        { get; private set; }
    public FileContext Context          { get; private set; }          // Products, Disputes, etc.
    public Guid        UploadedByUserId { get; private set; }
    public bool        IsOrphaned       { get; private set; }          // not linked to any entity yet
    public DateTime    ExpiresAt        { get; private set; }          // orphan cleanup after 1hr

    // IAuditable
    public DateTime  CreatedAt { get; set; }
    public Guid?     CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid?     UpdatedBy { get; set; }

    public static UploadedFile Create(
        string storageKey,
        string originalFileName,
        string contentType,
        long sizeBytes,
        FileContext context,
        Guid uploadedByUserId)
        => new()
        {
            Id               = Guid.NewGuid(),
            StorageKey       = storageKey,
            OriginalFileName = originalFileName,
            ContentType      = contentType,
            SizeBytes        = sizeBytes,
            Context          = context,
            UploadedByUserId = uploadedByUserId,
            IsOrphaned       = true,                // starts orphaned
            ExpiresAt        = DateTime.UtcNow.AddHours(1),  // TTL for cleanup
        };

    public void Claim()  // called when entity links to this file
    {
        IsOrphaned = false;
        ExpiresAt  = DateTime.MaxValue;
    }
}

public enum FileContext { ProductImage, StorefrontBanner, DisputeEvidence, UserDocument, ReviewPhoto }
```

### 4.5 IFileUploadService — Main Pipeline Orchestrator

```csharp
// Core/Files/Application/IFileUploadService.cs
public interface IFileUploadService
{
    Task<Result<FileUploadResult, Error>> UploadAsync(
        IFormFile file,
        FileContext context,
        Guid uploadedByUserId,
        CancellationToken ct = default);

    Task<Result<IReadOnlyList<FileUploadResult>, Error>> UploadManyAsync(
        IFormFileCollection files,
        FileContext context,
        AllowedFileTypes rules,
        Guid uploadedByUserId,
        CancellationToken ct = default);
}

public record FileUploadResult(
    Guid   FileId,
    string Url,
    string? ThumbnailUrl,
    string OriginalFileName,
    long   SizeBytes);
```

### 4.6 FileUploadService Implementation

```csharp
// Infrastructure/Files/FileUploadService.cs
public class FileUploadService(
    IStorageService storage,
    IAntivirusScanner antivirus,
    IImageProcessor imageProcessor,
    IFileRepository fileRepository,
    IDateTimeService clock,
    ILogger<FileUploadService> logger) : IFileUploadService
{
    public async Task<Result<FileUploadResult, Error>> UploadAsync(
        IFormFile file, FileContext context, Guid uploadedBy, CancellationToken ct = default)
    {
        // ── Step 1: Validate ─────────────────────────────
        var rules = FileUploadRules.GetRulesForContext(context);
        var validation = ValidateFile(file, rules);
        if (validation.IsFailure) return validation.Error;

        // ── Step 2: Virus Scan ───────────────────────────
        var scanResult = await antivirus.ScanAsync(file.OpenReadStream(), ct);
        if (scanResult.IsInfected)
        {
            logger.LogWarning("Virus detected in upload from {UserId}: {Threat}",
                uploadedBy, scanResult.ThreatName);
            return Error.Conflict("FILE.VIRUS_DETECTED",
                "File failed security scan and was rejected");
        }

        // ── Step 3: Process (images only) ────────────────
        Stream processedStream;
        string contentType;
        string extension;

        if (file.ContentType.StartsWith("image/"))
        {
            var processed = await imageProcessor.OptimizeAsync(file.OpenReadStream(), ct);
            processedStream = processed.Stream;
            contentType     = "image/webp";
            extension       = ".webp";
        }
        else
        {
            processedStream = file.OpenReadStream();
            contentType     = file.ContentType;
            extension       = Path.GetExtension(file.FileName);
        }

        // ── Step 4: Upload to Storage ────────────────────
        var fileName   = $"{Guid.NewGuid()}{extension}";
        var folder     = context.ToString().ToLowerInvariant();
        var storageKey = $"{folder}/{fileName}";

        StorageResult storageResult;
        try
        {
            storageResult = await storage.UploadAsync(
                processedStream, fileName, contentType, folder, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Storage upload failed for {Context}", context);
            return Error.Unexpected("Storage upload failed");
        }

        // ── Step 5: Persist record ───────────────────────
        var uploadedFile = UploadedFile.Create(
            storageKey:        storageKey,
            originalFileName:  file.FileName,
            contentType:       contentType,
            sizeBytes:         storageResult.SizeBytes,
            context:           context,
            uploadedByUserId:  uploadedBy);

        fileRepository.Add(uploadedFile);
        // Note: SaveChanges happens via UnitOfWork / TransactionFilter

        // Return immediately with the pre-signed URL
        return new FileUploadResult(
            FileId:           uploadedFile.Id,
            Url:              storageResult.Url,
            ThumbnailUrl:     null,   // TODO: generate thumbnail in Phase 2
            OriginalFileName: file.FileName,
            SizeBytes:        storageResult.SizeBytes);
    }

    private static Result<Unit, Error> ValidateFile(IFormFile file, AllowedFileTypes rules)
    {
        if (file.Length == 0)
            return Error.Conflict("FILE.EMPTY", "File is empty");

        if (file.Length > rules.MaxSizeBytes)
            return Error.Conflict("FILE.TOO_LARGE",
                $"File exceeds maximum size of {rules.MaxSizeBytes / 1024 / 1024}MB");

        // Validate MIME type from content (not just extension)
        var detectedMime = MimeDetector.Detect(file.OpenReadStream());
        if (!rules.AllowedMimeTypes.Contains(detectedMime))
            return Error.Conflict("FILE.INVALID_TYPE",
                $"File type '{detectedMime}' is not allowed");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!rules.AllowedExtensions.Contains(ext))
            return Error.Conflict("FILE.INVALID_EXTENSION",
                $"Extension '{ext}' is not allowed");

        return Result.Success();
    }
}
```

### 4.7 IStorageService — S3-Compatible Implementation

```csharp
// Infrastructure/Files/Storage/S3StorageService.cs
/// <summary>
/// Works with: Cloudflare R2, AWS S3, MinIO — same code, different config.
/// Config (appsettings.json):
/// {
///   "Storage": {
///     "Provider": "S3",
///     "Endpoint": "https://{accountId}.r2.cloudflarestorage.com",  ← R2
///     "AccessKey": "...",
///     "SecretKey": "...",
///     "BucketName": "marketnest-uploads",
///     "PublicBaseUrl": "https://cdn.marketnest.com"    ← custom domain on R2
///   }
/// }
/// </summary>
public class S3StorageService(IOptions<StorageOptions> options, IAmazonS3 s3) : IStorageService
{
    private readonly StorageOptions _opts = options.Value;

    public async Task<StorageResult> UploadAsync(
        Stream content, string fileName, string contentType, string folder, CancellationToken ct)
    {
        var key = $"{folder}/{fileName}";

        var request = new PutObjectRequest
        {
            BucketName  = _opts.BucketName,
            Key         = key,
            InputStream = content,
            ContentType = contentType,
            // Cache public assets aggressively
            Headers = { CacheControl = "public, max-age=31536000, immutable" }
        };

        await s3.PutObjectAsync(request, ct);

        var url = $"{_opts.PublicBaseUrl}/{key}";
        return new StorageResult(url, fileName, content.Length);
    }

    public async Task DeleteAsync(string storageKey, CancellationToken ct)
    {
        await s3.DeleteObjectAsync(new DeleteObjectRequest
        {
            BucketName = _opts.BucketName,
            Key        = storageKey
        }, ct);
    }

    /// <summary>Generate pre-signed URL for private files (dispute evidence, documents)</summary>
    public string GetPresignedUrl(string storageKey, TimeSpan expiry)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _opts.BucketName,
            Key        = storageKey,
            Expires    = DateTime.UtcNow.Add(expiry),
            Verb       = HttpVerb.GET
        };
        return s3.GetPreSignedURL(request);
    }
}

// Docker Compose: add MinIO for local dev
// services:
//   minio:
//     image: cgr.dev/chainguard/minio   ← Chainguard-maintained (MinIO community archived)
//     command: server /data --console-address :9001
//     ports: ["9000:9000", "9001:9001"]
//     environment:
//       MINIO_ROOT_USER: marketnest
//       MINIO_ROOT_PASSWORD: marketnest_secret
//     volumes:
//       - minio_data:/data
```

### 4.8 Antivirus Scanner — ClamAV

```csharp
// Infrastructure/Files/Security/ClamAvScanner.cs
public class ClamAvScanner(IOptions<ClamAvOptions> options) : IAntivirusScanner
{
    public async Task<ScanResult> ScanAsync(Stream fileStream, CancellationToken ct)
    {
        if (!options.Value.Enabled)
            return ScanResult.Clean; // Skip in dev if ClamAV not configured

        try
        {
            using var client = new ClamClient(options.Value.Host, options.Value.Port);
            var result = await client.SendAndScanFileAsync(fileStream);

            return result.Result switch
            {
                ClamScanResults.Clean    => ScanResult.Clean,
                ClamScanResults.VirusDetected => new ScanResult(true, result.InfectedFiles?.First().VirusName),
                _ => ScanResult.Clean // Error scanning — allow with warning log
            };
        }
        catch (Exception)
        {
            return ScanResult.Clean; // Fail open in dev; fail closed in prod via config
        }
    }
}

public record ScanResult(bool IsInfected, string? ThreatName = null)
{
    public static readonly ScanResult Clean = new(false);
}

// Docker Compose: add ClamAV (Phase 2+)
// services:
//   clamav:
//     image: clamav/clamav:latest
//     ports: ["3310:3310"]
//     volumes:
//       - clamav_data:/var/lib/clamav
```

### 4.9 Orphan File Cleanup Job

```csharp
// Infrastructure/Files/Jobs/OrphanFileCleanupJob.cs
/// <summary>
/// Deletes UploadedFile records that were never claimed (IsOrphaned=true, ExpiresAt < now).
/// Runs every hour. Prevents storage accumulation from abandoned upload flows.
/// </summary>
public class OrphanFileCleanupJob(
    IFileRepository files,
    IStorageService storage,
    IUnitOfWork uow,
    ILogger<OrphanFileCleanupJob> logger) : IBackgroundJob
{
    public string JobId          => "files.orphan-cleanup";
    public string CronExpression => "0 * * * *";  // every hour
    public bool   RunOnStartup   => false;

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var orphans = await files.GetExpiredOrphansAsync(DateTime.UtcNow, ct);
        if (!orphans.Any()) return;

        logger.LogInformation("Cleaning up {Count} orphaned files", orphans.Count);

        foreach (var file in orphans)
        {
            try
            {
                await storage.DeleteAsync(file.StorageKey, ct);
                files.Remove(file);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to delete orphaned file {FileId}", file.Id);
            }
        }

        await uow.CommitAsync(ct);
    }
}
```

### 4.10 File Upload Endpoint (Razor Page Handler)

```csharp
// Pages/Files/Upload.cshtml.cs — dedicated upload endpoint
[Access(Permission.Product_Write)]  // or relevant permission
public class FileUploadModel(
    IFileUploadService fileService,
    ICurrentUserService currentUser) : BasePageModel
{
    // Called via HTMX from image upload component
    // Returns JSON with { fileId, url } for Alpine.js to store in hidden input
    [NoTransaction]  // file upload handles its own persistence
    public async Task<IActionResult> OnPostAsync(
        IFormFileCollection files,
        [FromQuery] FileContext context,
        CancellationToken ct)
    {
        if (!files.Any())
            return BadRequest("No files provided");

        var rules = FileUploadRules.GetRulesForContext(context);

        if (files.Count > rules.MaxCount)
            return BadRequest($"Maximum {rules.MaxCount} files allowed");

        var results = new List<FileUploadResult>();
        foreach (var file in files)
        {
            var result = await fileService.UploadAsync(file, context, currentUser.RequireUserId(), ct);
            if (result.IsFailure)
                return UnprocessableEntity(result.Error);
            results.Add(result.Value);
        }

        // SaveChanges here since [NoTransaction] bypasses the filter
        await unitOfWork.CommitAsync(ct);

        return Ok(results);
    }
}
```

---

## 5. Registration Summary

```csharp
// Program.cs — all pieces wired together
builder.Services
    // Filters (global)
    .AddControllers(opt =>
    {
        opt.Filters.Add<TransactionActionFilter>();
        opt.Filters.Add<AccessFilter>();
    })

    // Unit of Work
    .AddScoped<IUnitOfWork, UnitOfWork>()

    // File services
    .AddScoped<IFileUploadService, FileUploadService>()
    .AddScoped<IAntivirusScanner, ClamAvScanner>()
    .AddScoped<IImageProcessor, SkiaSharpImageProcessor>()   // or ImageSharp

    // Storage (switch via config: "local" | "s3")
    .AddStorageService(builder.Configuration)

    // Authorization handler
    .AddScoped<IAuthorizationHandler, AccessAuthorizationHandler>()

    // Background jobs
    .AddScoped<IBackgroundJob, OrphanFileCleanupJob>();

// Storage factory
public static IServiceCollection AddStorageService(
    this IServiceCollection services, IConfiguration config)
{
    var provider = config["Storage:Provider"] ?? "local";
    return provider switch
    {
        "s3" => services
            .AddAWSService<IAmazonS3>()
            .Configure<StorageOptions>(config.GetSection("Storage"))
            .AddScoped<IStorageService, S3StorageService>(),

        _ => services
            .AddScoped<IStorageService, LocalDiskStorageService>()
    };
}
```
