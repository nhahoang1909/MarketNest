# Design Spec: CQRS Folder Restructure + Read/Write Separation

**Date:** 2026-04-26  
**Status:** Approved  
**Scope:** MarketNest.Admin module (template for all future modules)

---

## 1. Goals

1. Remove Application → Infrastructure dependency — neither CommandHandlers nor QueryHandlers reference DbContext directly.
2. CommandHandlers inject `ITestRepository` (Application interface, backed by abstract `BaseRepository<T,K>`).
3. QueryHandlers inject either `ITestQuery` (for simple reads) or `IGetTestsPagedQuery` (per complex query contract).
4. Introduce `IBaseQuery<TEntity, TKey>` in Core — simple reads only. Complex queries get their own interfaces.
5. Abstract base classes `BaseRepository<T,K>` and `BaseQuery<T,K>` in Infrastructure so subclasses don't re-implement boilerplate.
6. Separate `AdminDbContext` (write) from `AdminReadDbContext` (read, global NoTracking).
7. Split handlers folder into `CommandHandlers/`, `QueryHandlers/`, `DomainEventHandlers/`, `IntegrationEventHandlers/`.
8. Split controllers into `ReadController` / `WriteController`, each extending a typed base class.
9. Move module-specific background jobs from `MarketNest.Web` into the owning module.

---

## 2. Folder Structure (Target)

### MarketNest.Admin

```
src/MarketNest.Admin/
├── Application/
│   ├── Common/
│   ├── Submodule/
│   │   ├── Test/
│   │   │   ├── Commands/
│   │   │   │   ├── CreateTestCommand.cs
│   │   │   │   └── UpdateTestCommand.cs
│   │   │   ├── CommandHandlers/
│   │   │   │   ├── CreateTestHandler.cs
│   │   │   │   └── UpdateTestHandler.cs
│   │   │   ├── QueryHandlers/
│   │   │   │   ├── GetTestByIdHandler.cs
│   │   │   │   └── GetTestsPagedHandler.cs
│   │   │   ├── DomainEventHandlers/          # future — handles domain events raised by TestEntity
│   │   │   ├── IntegrationEventHandlers/     # future — handles cross-module integration events
│   │   │   ├── Queries/
│   │   │   │   ├── GetTestByIdQuery.cs       # IQuery<TestDto?>
│   │   │   │   ├── GetTestsPagedQuery.cs     # IQuery<PagedResult<TestDto>>
│   │   │   │   ├── ITestQuery.cs             # extends IBaseQuery<TestEntity, Guid> — simple reads
│   │   │   │   ├── IGetTestsPagedQuery.cs    # complex query contract — separate interface
│   │   │   │   └── TestDto.cs
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
    │   ├── Common/
    │   │   ├── ApiV1ControllerBase.cs        # shared base: IMediator, ToActionResult helper
    │   │   ├── ReadApiV1ControllerBase.cs    # base for all read controllers
    │   │   └── WriteApiV1ControllerBase.cs   # base for all write controllers
    │   └── Test/
    │       ├── TestReadController.cs         # extends ReadApiV1ControllerBase
    │       └── TestWriteController.cs        # extends WriteApiV1ControllerBase
    ├── Queries/
    │   └── Test/
    │       └── TestQuery.cs                  # extends BaseQuery<TestEntity,Guid>, implements ITestQuery + IGetTestsPagedQuery
    ├── Repositories/
    │   └── Test/
    │       └── TestRepository.cs             # extends BaseRepository<TestEntity,Guid>, implements ITestRepository
    ├── Persistence/
    │   ├── AdminDbContext.cs                 # write, IModuleDbContext, runs migrations
    │   ├── AdminReadDbContext.cs             # read, NoTracking global
    │   ├── BaseRepository.cs                 # abstract EF Core base for write repositories
    │   ├── BaseQuery.cs                      # abstract EF Core base for read queries
    │   └── Configurations/
    │       ├── TestEntityConfiguration.cs
    │       └── TestSubEntityConfiguration.cs
    └── Seeders/
        └── AdminDataSeeder.cs
```

### MarketNest.Web — BackgroundJobs (trimmed)

```
MarketNest.Web/BackgroundJobs/
  NpgsqlJobExecutionStore.cs
  ServiceCollectionJobRegistry.cs
  Hosting/
    JobRunnerHostedService.cs
```

### MarketNest.Core — new file

```
src/MarketNest.Core/Common/Queries/
  IBaseQuery.cs    # new — simple reads only
  PagedQuery.cs    # existing
  PagedResult.cs   # existing
```

---

## 3. Interface Contracts

### 3.1 `IBaseQuery<TEntity, TKey>` (Core) — simple reads only

```csharp
namespace MarketNest.Core.Common.Queries;

public interface IBaseQuery<TEntity, TKey> where TEntity : Entity<TKey>
{
    Task<TEntity?> GetByKeyAsync(TKey id, CancellationToken ct = default);
    Task<TEntity> GetByKeyOrThrowAsync(TKey id, CancellationToken ct = default);
    Task<bool> ExistsAsync(TKey id, CancellationToken ct = default);
    Task<IReadOnlyList<TEntity>> ListAsync(CancellationToken ct = default);
    Task<TEntity?> FirstOrDefaultAsync(
        Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default);
    Task<int> CountAsync(
        Expression<Func<TEntity, bool>>? predicate = null, CancellationToken ct = default);
}
```

### 3.2 Module query interfaces (Application layer)

`ITestQuery` extends `IBaseQuery` for simple lookups. Complex queries get **their own interface** — one per use case:

```csharp
// Application/Submodule/Test/Queries/ITestQuery.cs
namespace MarketNest.Admin.Application;

public interface ITestQuery : IBaseQuery<TestEntity, Guid>;
// No complex methods here — IBaseQuery base methods only.
```

```csharp
// Application/Submodule/Test/Queries/IGetTestsPagedQuery.cs
namespace MarketNest.Admin.Application;

public interface IGetTestsPagedQuery
{
    Task<PagedResult<TestDto>> ExecuteAsync(GetTestsPagedQuery request, CancellationToken ct);
}
```

**Rule:** Any query that involves projection to a DTO, pagination, joins, or filtering beyond a single predicate gets its own `IGet{UseCase}Query` interface, not a method on `ITestQuery`.

### 3.3 `ITestRepository` (Application layer)

```csharp
// Application/Submodule/Test/Repositories/ITestRepository.cs
namespace MarketNest.Admin.Application;

public interface ITestRepository : IBaseRepository<TestEntity, Guid>;
```

Add extra methods only if a CommandHandler needs them (e.g., loading with includes):

```csharp
public interface ITestRepository : IBaseRepository<TestEntity, Guid>
{
    Task<TestEntity?> GetWithSubEntitiesAsync(Guid id, CancellationToken ct = default);
}
```

---

## 4. Abstract Base Classes (Infrastructure)

Placed in `Infrastructure/Persistence/` per module. Avoids each concrete class re-implementing the same boilerplate. When multiple modules exist, extract to a shared infrastructure package.

### 4.1 `BaseRepository<TEntity, TKey>`

```csharp
// Infrastructure/Persistence/BaseRepository.cs
namespace MarketNest.Admin.Infrastructure;

public abstract class BaseRepository<TEntity, TKey>(AdminDbContext db)
    : IBaseRepository<TEntity, TKey>
    where TEntity : Entity<TKey>
{
    protected AdminDbContext Db => db;

    public virtual Task<TEntity?> GetByKeyAsync(TKey id, CancellationToken ct)
        => db.Set<TEntity>().FindAsync([id], ct).AsTask();

    public virtual async Task<TEntity> GetByKeyOrThrowAsync(TKey id, CancellationToken ct)
        => await GetByKeyAsync(id, ct)
           ?? throw new KeyNotFoundException($"{typeof(TEntity).Name} {id} not found");

    public virtual Task<bool> ExistsAsync(TKey id, CancellationToken ct)
        => db.Set<TEntity>().AnyAsync(e => EF.Property<TKey>(e, "Id")!.Equals(id), ct);

    public virtual void Add(TEntity entity)    => db.Set<TEntity>().Add(entity);
    public virtual void Update(TEntity entity) => db.Set<TEntity>().Update(entity);
    public virtual void Remove(TEntity entity) => db.Set<TEntity>().Remove(entity);

    public Task<int> SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
```

Concrete class only overrides when needed:

```csharp
// Infrastructure/Repositories/Test/TestRepository.cs
namespace MarketNest.Admin.Infrastructure;

public class TestRepository(AdminDbContext db)
    : BaseRepository<TestEntity, Guid>(db), ITestRepository
{
    // Override to load aggregate with children
    public override Task<TestEntity?> GetByKeyAsync(Guid id, CancellationToken ct)
        => Db.Tests.Include(x => x.SubEntities).FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<TestEntity?> GetWithSubEntitiesAsync(Guid id, CancellationToken ct)
        => GetByKeyAsync(id, ct);
}
```

### 4.2 `BaseQuery<TEntity, TKey>`

```csharp
// Infrastructure/Persistence/BaseQuery.cs
namespace MarketNest.Admin.Infrastructure;

public abstract class BaseQuery<TEntity, TKey>(AdminReadDbContext db)
    : IBaseQuery<TEntity, TKey>
    where TEntity : Entity<TKey>
{
    protected AdminReadDbContext Db => db;

    public virtual Task<TEntity?> GetByKeyAsync(TKey id, CancellationToken ct)
        => db.Set<TEntity>().FirstOrDefaultAsync(
               e => EF.Property<TKey>(e, "Id")!.Equals(id), ct);

    public virtual async Task<TEntity> GetByKeyOrThrowAsync(TKey id, CancellationToken ct)
        => await GetByKeyAsync(id, ct)
           ?? throw new KeyNotFoundException($"{typeof(TEntity).Name} {id} not found");

    public virtual Task<bool> ExistsAsync(TKey id, CancellationToken ct)
        => db.Set<TEntity>().AnyAsync(
               e => EF.Property<TKey>(e, "Id")!.Equals(id), ct);

    public virtual async Task<IReadOnlyList<TEntity>> ListAsync(CancellationToken ct)
        => await db.Set<TEntity>().ToListAsync(ct);

    public virtual Task<TEntity?> FirstOrDefaultAsync(
        Expression<Func<TEntity, bool>> predicate, CancellationToken ct)
        => db.Set<TEntity>().FirstOrDefaultAsync(predicate, ct);

    public virtual Task<int> CountAsync(
        Expression<Func<TEntity, bool>>? predicate, CancellationToken ct)
        => predicate is null
            ? db.Set<TEntity>().CountAsync(ct)
            : db.Set<TEntity>().CountAsync(predicate, ct);
}
```

Concrete class implements `ITestQuery` (via base) **and** each complex interface separately:

```csharp
// Infrastructure/Queries/Test/TestQuery.cs
namespace MarketNest.Admin.Infrastructure;

public class TestQuery(AdminReadDbContext db)
    : BaseQuery<TestEntity, Guid>(db), ITestQuery, IGetTestsPagedQuery
{
    public async Task<PagedResult<TestDto>> ExecuteAsync(
        GetTestsPagedQuery request, CancellationToken ct)
    {
        var query = Db.Tests.Include(x => x.SubEntities).AsQueryable();
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
            Items = items, Page = request.Page,
            PageSize = request.PageSize, TotalCount = total
        };
    }
}
```

---

## 5. Handlers (split by type)

### CommandHandlers

```csharp
// Application/Submodule/Test/CommandHandlers/CreateTestHandler.cs
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

### QueryHandlers

Simple read — injects `ITestQuery`:

```csharp
// Application/Submodule/Test/QueryHandlers/GetTestByIdHandler.cs
namespace MarketNest.Admin.Application;

public class GetTestByIdHandler(ITestQuery query) : IQueryHandler<GetTestByIdQuery, TestDto?>
{
    public async Task<TestDto?> Handle(GetTestByIdQuery request, CancellationToken ct)
    {
        var entity = await query.GetByKeyAsync(request.Id, ct);
        return entity is null ? null : new TestDto
        {
            Id = entity.Id, Name = entity.Name, Value = entity.Value,
            SubEntities = entity.SubEntities.Select(s => new TestSubDto(s.Id, s.Title)).ToList()
        };
    }
}
```

Complex read — injects dedicated interface:

```csharp
// Application/Submodule/Test/QueryHandlers/GetTestsPagedHandler.cs
namespace MarketNest.Admin.Application;

public class GetTestsPagedHandler(IGetTestsPagedQuery query)
    : IQueryHandler<GetTestsPagedQuery, PagedResult<TestDto>>
{
    public Task<PagedResult<TestDto>> Handle(GetTestsPagedQuery request, CancellationToken ct)
        => query.ExecuteAsync(request, ct);
}
```

---

## 6. Controller Base Classes

```csharp
// Infrastructure/Api/Common/ApiV1ControllerBase.cs
namespace MarketNest.Admin.Infrastructure;

[ApiController]
public abstract class ApiV1ControllerBase(IMediator mediator) : ControllerBase
{
    protected IMediator Mediator => mediator;

    protected IActionResult ToActionResult<T>(Result<T, Error> result) =>
        result.Match<IActionResult>(
            value  => Ok(value),
            error  => error.Type switch
            {
                ErrorType.NotFound     => NotFound(new { error.Code, error.Message }),
                ErrorType.Conflict     => Conflict(new { error.Code, error.Message }),
                ErrorType.Validation   => BadRequest(new { error.Code, error.Message }),
                ErrorType.Unauthorized => Unauthorized(new { error.Code, error.Message }),
                ErrorType.Forbidden    => Forbid(),
                _                      => Problem(error.Message)
            });
}
```

```csharp
// Infrastructure/Api/Common/ReadApiV1ControllerBase.cs
namespace MarketNest.Admin.Infrastructure;

public abstract class ReadApiV1ControllerBase(IMediator mediator)
    : ApiV1ControllerBase(mediator);

// Infrastructure/Api/Common/WriteApiV1ControllerBase.cs
public abstract class WriteApiV1ControllerBase(IMediator mediator)
    : ApiV1ControllerBase(mediator);
```

Controllers:

```csharp
// Infrastructure/Api/Test/TestReadController.cs
namespace MarketNest.Admin.Infrastructure;

[Route("api/v1/admin/tests")]
public class TestReadController(IMediator mediator) : ReadApiV1ControllerBase(mediator)
{
    [HttpGet]
    public Task<IActionResult> GetPaged([FromQuery] GetTestsPagedQuery query, CancellationToken ct)
        => Mediator.Send(query, ct).ContinueWith(t => (IActionResult)Ok(t.Result), ct);

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => Ok(await Mediator.Send(new GetTestByIdQuery(id), ct));
}

// Infrastructure/Api/Test/TestWriteController.cs
[Route("api/v1/admin/tests")]
public class TestWriteController(IMediator mediator) : WriteApiV1ControllerBase(mediator)
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTestRequest req, CancellationToken ct)
    {
        var result = await Mediator.Send(new CreateTestCommand(req.Name, req.Value, req.SubTitles), ct);
        return ToActionResult(result.Map(id => new { id }));
    }
}
```

---

## 7. DbContext Split

### AdminDbContext (write)

- Change tracking ON (default).
- Implements `IModuleDbContext` — `DatabaseInitializer` uses this for migrations.
- `ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly())` — shared configs.
- Only `BaseRepository` subclasses inject this.

### AdminReadDbContext (read)

- `UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)` globally.
- Does **not** implement `IModuleDbContext` — no migrations.
- `ApplyConfigurationsFromAssembly(typeof(AdminDbContext).Assembly)` — same entity configs as write context.
- Only `BaseQuery` subclasses inject this.

---

## 8. Background Job Relocation

| Before | After |
|---|---|
| `MarketNest.Web/BackgroundJobs/Test/TestTimerJob.cs` | `MarketNest.Admin/Application/Timer/TestTimer/TestTimerJob.cs` |

`Program.cs` must scan the Admin assembly for `IBackgroundJob` implementations.

---

## 9. Namespace Rules

All files use flat layer-level namespaces regardless of sub-folder depth:

| File path | Namespace |
|---|---|
| `Admin/Application/Submodule/Test/CommandHandlers/CreateTestHandler.cs` | `MarketNest.Admin.Application` |
| `Admin/Application/Submodule/Test/QueryHandlers/GetTestByIdHandler.cs` | `MarketNest.Admin.Application` |
| `Admin/Application/Submodule/Test/Queries/ITestQuery.cs` | `MarketNest.Admin.Application` |
| `Admin/Application/Submodule/Test/Queries/IGetTestsPagedQuery.cs` | `MarketNest.Admin.Application` |
| `Admin/Application/Submodule/Test/Repositories/ITestRepository.cs` | `MarketNest.Admin.Application` |
| `Admin/Application/Timer/TestTimer/TestTimerJob.cs` | `MarketNest.Admin.Application` |
| `Admin/Infrastructure/Api/Common/ApiV1ControllerBase.cs` | `MarketNest.Admin.Infrastructure` |
| `Admin/Infrastructure/Api/Test/TestReadController.cs` | `MarketNest.Admin.Infrastructure` |
| `Admin/Infrastructure/Queries/Test/TestQuery.cs` | `MarketNest.Admin.Infrastructure` |
| `Admin/Infrastructure/Repositories/Test/TestRepository.cs` | `MarketNest.Admin.Infrastructure` |
| `Admin/Infrastructure/Persistence/BaseRepository.cs` | `MarketNest.Admin.Infrastructure` |
| `Admin/Infrastructure/Persistence/BaseQuery.cs` | `MarketNest.Admin.Infrastructure` |
| `Core/Common/Queries/IBaseQuery.cs` | `MarketNest.Core.Common.Queries` |

---

## 10. Docs to Update

- `docs/architecture.md` — IBaseQuery/IBaseRepository pattern, BaseQuery/BaseRepository abstract classes, ReadDbContext/WriteDbContext split, feature-folder layout, Read/WriteApiV1ControllerBase
- `AGENTS.md` — enforcement rules for the new patterns
- `docs/code-rules.md` §2.7 — namespace examples updated to reflect new folder structure
