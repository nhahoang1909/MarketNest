# Design Spec: CQRS Folder Restructure + Read/Write Separation

**Date:** 2026-04-26  
**Status:** Approved  
**Scope:** MarketNest.Admin module (template for all future modules)

---

## 1. Goals

1. Remove Application → Infrastructure dependency — neither CommandHandlers nor QueryHandlers reference DbContext directly.
2. CommandHandlers inject `ITestRepository` (Application interface) — write side mirrors existing `IBaseRepository<TEntity, TKey>` pattern.
3. QueryHandlers inject `ITestQuery` (Application interface) — read side uses new `IBaseQuery<TEntity, TKey>` pattern.
4. Introduce `IBaseQuery<TEntity, TKey>` in Core to mirror `IBaseRepository<TEntity, TKey>`.
5. Separate `AdminDbContext` (write, change tracking) from `AdminReadDbContext` (read, NoTracking global).
6. Split controllers into `ReadController` (GET) and `WriteController` (POST/PUT/DELETE).
7. Reorganize folder structure to vertical-slice-within-layer: feature folders inside each layer.
8. Move module-specific background jobs from `MarketNest.Web` into the owning module.

---

## 2. Folder Structure (Target)

### MarketNest.Admin

```
src/MarketNest.Admin/
├── Application/
│   ├── Common/                          # shared DTOs, behaviors scoped to Admin
│   ├── Submodule/
│   │   ├── Test/
│   │   │   ├── Commands/
│   │   │   │   ├── CreateTestCommand.cs
│   │   │   │   └── UpdateTestCommand.cs
│   │   │   ├── Handlers/
│   │   │   │   ├── CreateTestHandler.cs
│   │   │   │   ├── UpdateTestHandler.cs
│   │   │   │   ├── GetTestByIdHandler.cs
│   │   │   │   └── GetTestsPagedHandler.cs
│   │   │   ├── Queries/
│   │   │   │   ├── GetTestByIdQuery.cs       # IQuery<TestDto?> — MediatR message
│   │   │   │   ├── GetTestsPagedQuery.cs     # IQuery<PagedResult<TestDto>>
│   │   │   │   ├── TestDto.cs
│   │   │   │   └── ITestQuery.cs             # extends IBaseQuery<TestEntity, Guid>
│   │   │   ├── Repositories/
│   │   │   │   └── ITestRepository.cs        # extends IBaseRepository<TestEntity, Guid>
│   │   │   └── Validators/
│   │   │       └── CreateTestCommandValidator.cs
│   │   └── Configuration/                   # platform settings domain (future)
│   └── Timer/
│       └── TestTimer/
│           └── TestTimerJob.cs              # moved from MarketNest.Web
├── Domain/
│   ├── Common/
│   └── Submodule/
│       ├── Test/
│       │   ├── Entities/
│       │   │   ├── TestEntity.cs
│       │   │   └── TestSubEntity.cs
│       │   └── ValueObjects/
│       │       └── TestValueObject.cs
│       └── Configuration/                   # future
└── Infrastructure/
    ├── Api/
    │   └── Test/
    │       ├── TestReadController.cs         # GET only
    │       └── TestWriteController.cs        # POST / PUT / DELETE only
    ├── Queries/
    │   └── Test/
    │       └── TestQuery.cs                  # implements ITestQuery, uses AdminReadDbContext
    ├── Repositories/
    │   └── Test/
    │       └── TestRepository.cs             # implements ITestRepository (Application layer), uses AdminDbContext
    ├── Persistence/
    │   ├── AdminDbContext.cs                 # write context, IModuleDbContext, runs migrations
    │   ├── AdminReadDbContext.cs             # read context, NoTracking global, NOT IModuleDbContext
    │   └── Configurations/
    │       ├── TestEntityConfiguration.cs    # IEntityTypeConfiguration<TestEntity>
    │       └── TestSubEntityConfiguration.cs # IEntityTypeConfiguration<TestSubEntity>
    └── Seeders/
        └── AdminDataSeeder.cs
```

### MarketNest.Web — BackgroundJobs (trimmed)

```
MarketNest.Web/BackgroundJobs/
  NpgsqlJobExecutionStore.cs       # infrastructure only
  ServiceCollectionJobRegistry.cs
  Hosting/
    JobRunnerHostedService.cs
  # TestTimerJob.cs removed — moved to MarketNest.Admin
```

### MarketNest.Core — new file

```
src/MarketNest.Core/Common/Queries/
  IBaseQuery.cs       # new
  PagedQuery.cs       # existing
  PagedResult.cs      # existing
```

---

## 3. `ITestRepository` (Write Side)

### Interface (Application layer)

```csharp
// Application/Submodule/Test/Repositories/ITestRepository.cs
namespace MarketNest.Admin.Application;

public interface ITestRepository : IBaseRepository<TestEntity, Guid>;
```

`IBaseRepository<TEntity, TKey>` already provides all write methods — no additional methods needed on `ITestRepository` unless a module requires custom queries for command use cases (e.g., `GetWithSubEntitiesAsync`).

### Infrastructure implementation

```csharp
// Infrastructure/Repositories/Test/TestRepository.cs
namespace MarketNest.Admin.Infrastructure;

public class TestRepository(AdminDbContext db) : ITestRepository
{
    public Task<TestEntity?> GetByKeyAsync(Guid id, CancellationToken ct)
        => db.Tests.Include(x => x.SubEntities).FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<TestEntity> GetByKeyOrThrowAsync(Guid id, CancellationToken ct)
        => await GetByKeyAsync(id, ct)
           ?? throw new KeyNotFoundException($"TestEntity {id} not found");

    public Task<bool> ExistsAsync(Guid id, CancellationToken ct)
        => db.Tests.AnyAsync(x => x.Id == id, ct);

    public void Add(TestEntity entity) => db.Tests.Add(entity);
    public void Update(TestEntity entity) => db.Tests.Update(entity);
    public void Remove(TestEntity entity) => db.Tests.Remove(entity);
    public Task<int> SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
```

### Updated CommandHandlers (no DbContext import)

```csharp
// Application/Submodule/Test/Handlers/CreateTestHandler.cs
namespace MarketNest.Admin.Application;

public class CreateTestHandler(ITestRepository repository) : ICommandHandler<CreateTestCommand, Guid>
{
    public async Task<Result<Guid, Error>> Handle(CreateTestCommand request, CancellationToken ct)
    {
        var id = Guid.NewGuid();
        var entity = new TestEntity(id, request.Name, request.Value);

        if (request.SubTitles is not null)
            foreach (var title in request.SubTitles)
                entity.AddSubEntity(new TestSubEntity(Guid.NewGuid(), id, title));

        repository.Add(entity);
        await repository.SaveChangesAsync(ct);
        return Result<Guid, Error>.Success(id);
    }
}
```

---

## 4. `IBaseQuery<TEntity, TKey>` (Core)

Mirrors `IBaseRepository<TEntity, TKey>` but read-only. No write methods, no `SaveChangesAsync`.

```csharp
namespace MarketNest.Core.Common.Queries;

public interface IBaseQuery<TEntity, TKey> where TEntity : Entity<TKey>
{
    Task<TEntity?> GetByKeyAsync(TKey id, CancellationToken ct = default);
    Task<TEntity> GetByKeyOrThrowAsync(TKey id, CancellationToken ct = default);
    Task<bool> ExistsAsync(TKey id, CancellationToken ct = default);
    Task<IReadOnlyList<TEntity>> ListAsync(CancellationToken ct = default);
    Task<TEntity?> FirstOrDefaultAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken ct = default);
    Task<int> CountAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        CancellationToken ct = default);
}
```

**Module-specific interface** (Application layer):

```csharp
// Application/Submodule/Test/Queries/ITestQuery.cs
namespace MarketNest.Admin.Application;

public interface ITestQuery : IBaseQuery<TestEntity, Guid>
{
    Task<PagedResult<TestDto>> GetPagedAsync(GetTestsPagedQuery request, CancellationToken ct);
}
```

**Infrastructure implementation:**

```csharp
// Infrastructure/Queries/Test/TestQuery.cs
namespace MarketNest.Admin.Infrastructure;

public class TestQuery(AdminReadDbContext db) : ITestQuery
{
    public Task<TestEntity?> GetByKeyAsync(Guid id, CancellationToken ct)
        => db.Tests.FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<TestEntity> GetByKeyOrThrowAsync(Guid id, CancellationToken ct)
        => await db.Tests.FirstOrDefaultAsync(x => x.Id == id, ct)
           ?? throw new KeyNotFoundException($"TestEntity {id} not found");

    public Task<bool> ExistsAsync(Guid id, CancellationToken ct)
        => db.Tests.AnyAsync(x => x.Id == id, ct);

    public Task<IReadOnlyList<TestEntity>> ListAsync(CancellationToken ct)
        => db.Tests.ToListAsync(ct).ContinueWith(t => (IReadOnlyList<TestEntity>)t.Result, ct);

    public Task<TestEntity?> FirstOrDefaultAsync(
        Expression<Func<TestEntity, bool>> predicate, CancellationToken ct)
        => db.Tests.FirstOrDefaultAsync(predicate, ct);

    public Task<int> CountAsync(
        Expression<Func<TestEntity, bool>>? predicate, CancellationToken ct)
        => predicate is null
            ? db.Tests.CountAsync(ct)
            : db.Tests.CountAsync(predicate, ct);

    public async Task<PagedResult<TestDto>> GetPagedAsync(
        GetTestsPagedQuery request, CancellationToken ct)
    {
        var query = db.Tests.Include(x => x.SubEntities).AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.SearchName))
            query = query.Where(x => x.Name.Contains(request.SearchName));

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(x => x.Name)
            .Skip(request.Skip).Take(request.PageSize)
            .Select(x => new TestDto
            {
                Id = x.Id,
                Name = x.Name,
                Value = x.Value,
                SubEntities = x.SubEntities.Select(s => new TestSubDto(s.Id, s.Title)).ToList()
            }).ToListAsync(ct);

        return new PagedResult<TestDto>
        {
            Items = items,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = total
        };
    }
}
```

**Updated QueryHandler** (Application, no EF Core import):

```csharp
// Application/Submodule/Test/Handlers/GetTestsPagedHandler.cs
namespace MarketNest.Admin.Application;

public class GetTestsPagedHandler(ITestQuery query)
    : IQueryHandler<GetTestsPagedQuery, PagedResult<TestDto>>
{
    public Task<PagedResult<TestDto>> Handle(GetTestsPagedQuery request, CancellationToken ct)
        => query.GetPagedAsync(request, ct);
}
```

---

## 4. DbContext Split

### AdminDbContext (write)

- Keeps change tracking ON (default).
- Implements `IModuleDbContext` — used by `DatabaseInitializer` for migrations and seeding.
- Uses `ApplyConfigurationsFromAssembly` — no more inline `OnModelCreating` entity configs.
- Only `TestRepository` (Infrastructure) injects `AdminDbContext`. CommandHandlers never reference it.

### AdminReadDbContext (read)

- `UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)` applied globally.
- Does **NOT** implement `IModuleDbContext` — no migrations.
- Uses `ApplyConfigurationsFromAssembly(typeof(AdminDbContext).Assembly)` — same configs, single source of truth.
- Query classes (`TestQuery`, etc.) inject `AdminReadDbContext`.

### Shared Configurations (Option A)

Entity configurations extracted from inline `OnModelCreating` into dedicated `IEntityTypeConfiguration<T>` classes under `Infrastructure/Persistence/Configurations/`. Both contexts call `ApplyConfigurationsFromAssembly(typeof(AdminDbContext).Assembly)`.

---

## 5. Read/Write Controller Split

Each API resource gets two controllers in `Infrastructure/Api/{Feature}/`:

| Controller | HTTP verbs | Depends on |
|---|---|---|
| `TestReadController` | GET | QueryHandlers → `ITestQuery` → `AdminReadDbContext` |
| `TestWriteController` | POST, PUT, DELETE | CommandHandlers → `ITestRepository` → `AdminDbContext` |

Both controllers inject only `IMediator`. The read/write separation is enforced end-to-end by the DbContext type used, not just by controller naming.

---

## 6. Background Job Relocation

| Before | After |
|---|---|
| `MarketNest.Web/BackgroundJobs/Test/TestTimerJob.cs` | `MarketNest.Admin/Application/Timer/TestTimer/TestTimerJob.cs` |

`MarketNest.Web/BackgroundJobs/` retains only infrastructure: `NpgsqlJobExecutionStore`, `ServiceCollectionJobRegistry`, `JobRunnerHostedService`.

DI registration in `Program.cs` must be updated to scan the Admin assembly for `IBackgroundJob` implementations.

---

## 7. Namespace Rules (unchanged)

All files follow the flat layer-level namespace rule regardless of sub-folder depth:

| File path | Namespace |
|---|---|
| `Admin/Application/Submodule/Test/Queries/ITestQuery.cs` | `MarketNest.Admin.Application` |
| `Admin/Application/Submodule/Test/Repositories/ITestRepository.cs` | `MarketNest.Admin.Application` |
| `Admin/Application/Timer/TestTimer/TestTimerJob.cs` | `MarketNest.Admin.Application` |
| `Admin/Infrastructure/Api/Test/TestReadController.cs` | `MarketNest.Admin.Infrastructure` |
| `Admin/Infrastructure/Queries/Test/TestQuery.cs` | `MarketNest.Admin.Infrastructure` |
| `Admin/Infrastructure/Repositories/Test/TestRepository.cs` | `MarketNest.Admin.Infrastructure` |
| `Admin/Infrastructure/Persistence/Configurations/TestEntityConfiguration.cs` | `MarketNest.Admin.Infrastructure` |
| `Core/Common/Queries/IBaseQuery.cs` | `MarketNest.Core.Common.Queries` |

---

## 8. Docs to Update

- `docs/architecture.md` — add IBaseQuery pattern, ReadDbContext/WriteDbContext split, feature-folder layout
- `AGENTS.md` — add IBaseQuery + query interface pattern to enforcement rules
- `docs/code-rules.md` §2.7 — update namespace examples to reflect new folder structure
