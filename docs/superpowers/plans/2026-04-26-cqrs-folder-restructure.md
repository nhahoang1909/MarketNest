# CQRS Folder Restructure + Read/Write Separation — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restructure the MarketNest.Admin module so Application handlers never reference DbContext directly — CommandHandlers use `ITestRepository`, QueryHandlers use `ITestQuery`/`IGetTestsPagedQuery`, backed by abstract base classes and a separate `AdminReadDbContext`.

**Architecture:** `IBaseQuery<T,K>` in Core mirrors `IBaseRepository<T,K>`. Abstract `BaseRepository`/`BaseQuery` classes in `Infrastructure/Persistence/` provide EF Core boilerplate so concrete classes only override what's module-specific. Each API resource has a `ReadController` (GET) and `WriteController` (POST/PUT/DELETE), both extending `ApiV1ControllerBase`.

**Tech Stack:** .NET 10, EF Core 10 (PostgreSQL), MediatR 12, xUnit + FluentAssertions

---

## File Map

### Created
| Path | Responsibility |
|---|---|
| `Core/Common/Queries/IBaseQuery.cs` | Read-only query contract (mirrors IBaseRepository) |
| `Admin/Infrastructure/Persistence/Configurations/TestEntityConfiguration.cs` | IEntityTypeConfiguration for TestEntity |
| `Admin/Infrastructure/Persistence/Configurations/TestSubEntityConfiguration.cs` | IEntityTypeConfiguration for TestSubEntity |
| `Admin/Infrastructure/Persistence/AdminReadDbContext.cs` | Read context — NoTracking global |
| `Admin/Infrastructure/Persistence/BaseRepository.cs` | Abstract EF Core write base |
| `Admin/Infrastructure/Persistence/BaseQuery.cs` | Abstract EF Core read base |
| `Admin/Application/Submodule/Test/Repositories/ITestRepository.cs` | Write contract |
| `Admin/Application/Submodule/Test/Queries/ITestQuery.cs` | Simple read contract |
| `Admin/Application/Submodule/Test/Queries/IGetTestsPagedQuery.cs` | Complex paged read contract |
| `Admin/Infrastructure/Repositories/Test/TestRepository.cs` | Concrete write impl |
| `Admin/Infrastructure/Queries/Test/TestQuery.cs` | Concrete read impl |
| `Admin/Infrastructure/Api/Common/ApiV1ControllerBase.cs` | Shared controller base |
| `Admin/Infrastructure/Api/Common/ReadApiV1ControllerBase.cs` | Read controller base |
| `Admin/Infrastructure/Api/Common/WriteApiV1ControllerBase.cs` | Write controller base |
| `Admin/Infrastructure/Api/Test/TestReadController.cs` | GET endpoints |
| `Admin/Infrastructure/Api/Test/TestWriteController.cs` | POST/PUT/DELETE endpoints |
| `Admin/Application/Timer/TestTimer/TestTimerJob.cs` | Background job (moved from Web) |

### Moved (same namespace — no other file references break)
| From | To |
|---|---|
| `Admin/Application/Commands/CreateTestCommand.cs` | `Admin/Application/Submodule/Test/Commands/CreateTestCommand.cs` |
| `Admin/Application/Commands/UpdateTestCommand.cs` | `Admin/Application/Submodule/Test/Commands/UpdateTestCommand.cs` |
| `Admin/Application/Queries/GetTestByIdQuery.cs` | `Admin/Application/Submodule/Test/Queries/GetTestByIdQuery.cs` |
| `Admin/Application/Queries/GetTestsPagedQuery.cs` | `Admin/Application/Submodule/Test/Queries/GetTestsPagedQuery.cs` |
| `Admin/Application/Queries/TestDto.cs` | `Admin/Application/Submodule/Test/Queries/TestDto.cs` |
| `Admin/Application/Handlers/CreateTestHandler.cs` | `Admin/Application/Submodule/Test/CommandHandlers/CreateTestHandler.cs` |
| `Admin/Application/Handlers/UpdateTestHandler.cs` | `Admin/Application/Submodule/Test/CommandHandlers/UpdateTestHandler.cs` |
| `Admin/Application/Handlers/GetTestByIdHandler.cs` | `Admin/Application/Submodule/Test/QueryHandlers/GetTestByIdHandler.cs` |
| `Admin/Application/Handlers/GetTestsPagedHandler.cs` | `Admin/Application/Submodule/Test/QueryHandlers/GetTestsPagedHandler.cs` |
| `Admin/Domain/Entities/TestEntity.cs` | `Admin/Domain/Submodule/Test/Entities/TestEntity.cs` |
| `Admin/Domain/Entities/TestSubEntity.cs` | `Admin/Domain/Submodule/Test/Entities/TestSubEntity.cs` |
| `Admin/Domain/ValueObjects/TestValueObject.cs` | `Admin/Domain/Submodule/Test/ValueObjects/TestValueObject.cs` |

### Modified
| Path | Change |
|---|---|
| `Admin/Infrastructure/Persistence/AdminDbContext.cs` | Switch to `ApplyConfigurationsFromAssembly` |
| `Admin/Infrastructure/Api/TestsController.cs` | Delete — replaced by Read/Write controllers |
| `Web/BackgroundJobs/Test/TestTimerJob.cs` | Delete — moved to Admin module |
| `Web/Program.cs` | Register AdminReadDbContext, ITestRepository, ITestQuery, IGetTestsPagedQuery, update job registration |

---

## Task 1: Add `IBaseQuery<TEntity, TKey>` to Core

**Files:**
- Create: `src/MarketNest.Core/Common/Queries/IBaseQuery.cs`

- [ ] **Step 1: Create the interface**

```csharp
// src/MarketNest.Core/Common/Queries/IBaseQuery.cs
using System.Linq.Expressions;

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

- [ ] **Step 2: Build**

```bash
dotnet build
```
Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
git add src/MarketNest.Core/Common/Queries/IBaseQuery.cs
git commit -m "feat(core): add IBaseQuery<TEntity, TKey> interface"
```

---

## Task 2: Extract Entity Configurations + Update AdminDbContext

**Files:**
  - Create: `src/MarketNest.Admin/Infrastructure/Persistence/Configurations/TestEntityConfiguration.cs`
  - Create: `src/MarketNest.Admin/Infrastructure/Persistence/Configurations/TestSubEntityConfiguration.cs`
  - Modify: `src/MarketNest.Admin/Infrastructure/Persistence/AdminDbContext.cs`

- [ ] **Step 1: Create TestEntityConfiguration**

```csharp
// src/MarketNest.Admin/Infrastructure/Persistence/Configurations/TestEntityConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MarketNest.Admin.Domain;
using MarketNest.Core.Common;

namespace MarketNest.Admin.Infrastructure;

public class TestEntityConfiguration : IEntityTypeConfiguration<TestEntity>
{
    public void Configure(EntityTypeBuilder<TestEntity> b)
    {
        b.ToTable("Tests");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Name).IsRequired().HasMaxLength(200);

        b.OwnsOne(m => m.Value, vo =>
        {
            vo.Property(v => v.Code).HasColumnName("Value_Code").HasMaxLength(50);
            vo.Property(v => v.Amount).HasColumnName("Value_Amount");
        });

        b.Navigation(x => x.SubEntities).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
```

- [ ] **Step 2: Create TestSubEntityConfiguration**

```csharp
// src/MarketNest.Admin/Infrastructure/Persistence/Configurations/TestSubEntityConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MarketNest.Admin.Domain;

namespace MarketNest.Admin.Infrastructure;

public class TestSubEntityConfiguration : IEntityTypeConfiguration<TestSubEntity>
{
    public void Configure(EntityTypeBuilder<TestSubEntity> b)
    {
        b.ToTable("TestSubEntities");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.ParentId).IsRequired();
        b.Property(x => x.Title).IsRequired().HasMaxLength(200);

        b.HasOne<TestEntity>()
            .WithMany("SubEntities")
            .HasForeignKey("ParentId")
            .HasPrincipalKey("Id")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

- [ ] **Step 3: Update AdminDbContext to use ApplyConfigurationsFromAssembly**

Replace `OnModelCreating` in `src/MarketNest.Admin/Infrastructure/Persistence/AdminDbContext.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using MarketNest.Core.Common.Persistence;
using MarketNest.Core.Common;
using MarketNest.Admin.Domain;

namespace MarketNest.Admin.Infrastructure;

public class AdminDbContext : DbContext, IModuleDbContext
{
    public AdminDbContext(DbContextOptions<AdminDbContext> options) : base(options) { }

    public string SchemaName => TableConstants.Schema.Admin;
    public string ContextName => "MarketNest.Admin";
    public DbContext AsDbContext() => this;

    public DbSet<TestEntity> Tests { get; set; } = null!;
    public DbSet<TestSubEntity> TestSubEntities { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(TableConstants.Schema.Admin);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AdminDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
```

- [ ] **Step 4: Build and verify**

```bash
dotnet build
```
Expected: 0 errors. Schema should be identical — only the config location changed.

- [ ] **Step 5: Commit**

```bash
git add src/MarketNest.Admin/Infrastructure/Persistence/
git commit -m "refactor(admin): extract entity configurations to IEntityTypeConfiguration classes"
```

---

## Task 3: Add AdminReadDbContext

**Files:**
 - Create: `src/MarketNest.Admin/Infrastructure/Persistence/AdminReadDbContext.cs`
- Modify: `src/MarketNest.Web/Program.cs`

- [ ] **Step 1: Create AdminReadDbContext**

```csharp
// src/MarketNest.Admin/Infrastructure/Persistence/AdminReadDbContext.cs
using Microsoft.EntityFrameworkCore;
using MarketNest.Admin.Domain;

namespace MarketNest.Admin.Infrastructure;

public class AdminReadDbContext(DbContextOptions<AdminReadDbContext> options) : DbContext(options)
{
    public DbSet<TestEntity> Tests { get; set; } = null!;
    public DbSet<TestSubEntity> TestSubEntities { get; set; } = null!;

    protected override void OnConfiguring(DbContextOptionsBuilder b)
        => b.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(TableConstants.Schema.Admin);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AdminDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
```

Note: `AdminReadDbContext` does NOT implement `IModuleDbContext` — it never runs migrations.

- [ ] **Step 2: Register AdminReadDbContext in Program.cs**

In `src/MarketNest.Web/Program.cs`, find the Admin module section and add after the existing `AddModuleDbContext<AdminDbContext>` line:

```csharp
// ── Admin Module (tests) ─────────────────────────────────────────
builder.Services.AddModuleDbContext<MarketNest.Admin.Infrastructure.AdminDbContext>(opts =>
    opts.UseNpgsql(builder.Configuration.GetConnectionString(AppConstants.DefaultConnectionStringName)));

// Read context — NoTracking, no migrations
builder.Services.AddDbContext<MarketNest.Admin.Infrastructure.AdminReadDbContext>(opts =>
    opts.UseNpgsql(builder.Configuration.GetConnectionString(AppConstants.DefaultConnectionStringName)));
```

- [ ] **Step 3: Build**

```bash
dotnet build
```
Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
git add src/MarketNest.Admin/Infrastructure/Persistence/AdminReadDbContext.cs
git add src/MarketNest.Web/Program.cs
git commit -m "feat(admin): add AdminReadDbContext with global NoTracking"
```

---

## Task 4: Add BaseRepository and BaseQuery Abstract Classes

**Files:**
 - Create: `src/MarketNest.Admin/Infrastructure/Persistence/BaseRepository.cs`
 - Create: `src/MarketNest.Admin/Infrastructure/Persistence/BaseQuery.cs`

- [ ] **Step 1: Create BaseRepository**

```csharp
// src/MarketNest.Admin/Infrastructure/Persistence/BaseRepository.cs
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using MarketNest.Core.Common;
using MarketNest.Core.Common.Persistence;

namespace MarketNest.Admin.Infrastructure;

public abstract class BaseRepository<TEntity, TKey>(AdminDbContext db)
    : IBaseRepository<TEntity, TKey>
    where TEntity : Entity<TKey>
{
    protected AdminDbContext Db => db;

    public virtual Task<TEntity?> GetByKeyAsync(TKey id, CancellationToken ct = default)
        => db.Set<TEntity>().FirstOrDefaultAsync(e => e.Id!.Equals(id), ct);

    public virtual async Task<TEntity> GetByKeyOrThrowAsync(TKey id, CancellationToken ct = default)
        => await GetByKeyAsync(id, ct)
           ?? throw new KeyNotFoundException($"{typeof(TEntity).Name} '{id}' not found");

    public virtual Task<bool> ExistsAsync(TKey id, CancellationToken ct = default)
        => db.Set<TEntity>().AnyAsync(e => e.Id!.Equals(id), ct);

    public virtual void Add(TEntity entity)    => db.Set<TEntity>().Add(entity);
    public virtual void Update(TEntity entity) => db.Set<TEntity>().Update(entity);
    public virtual void Remove(TEntity entity) => db.Set<TEntity>().Remove(entity);

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => db.SaveChangesAsync(ct);
}
```

- [ ] **Step 2: Create BaseQuery**

```csharp
// src/MarketNest.Admin/Infrastructure/Persistence/BaseQuery.cs
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using MarketNest.Core.Common;
using MarketNest.Core.Common.Queries;

namespace MarketNest.Admin.Infrastructure;

public abstract class BaseQuery<TEntity, TKey>(AdminReadDbContext db)
    : IBaseQuery<TEntity, TKey>
    where TEntity : Entity<TKey>
{
    protected AdminReadDbContext Db => db;

    public virtual Task<TEntity?> GetByKeyAsync(TKey id, CancellationToken ct = default)
        => db.Set<TEntity>().FirstOrDefaultAsync(e => e.Id!.Equals(id), ct);

    public virtual async Task<TEntity> GetByKeyOrThrowAsync(TKey id, CancellationToken ct = default)
        => await GetByKeyAsync(id, ct)
           ?? throw new KeyNotFoundException($"{typeof(TEntity).Name} '{id}' not found");

    public virtual Task<bool> ExistsAsync(TKey id, CancellationToken ct = default)
        => db.Set<TEntity>().AnyAsync(e => e.Id!.Equals(id), ct);

    public virtual async Task<IReadOnlyList<TEntity>> ListAsync(CancellationToken ct = default)
        => await db.Set<TEntity>().ToListAsync(ct);

    public virtual Task<TEntity?> FirstOrDefaultAsync(
        Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default)
        => db.Set<TEntity>().FirstOrDefaultAsync(predicate, ct);

    public virtual Task<int> CountAsync(
        Expression<Func<TEntity, bool>>? predicate = null, CancellationToken ct = default)
        => predicate is null
            ? db.Set<TEntity>().CountAsync(ct)
            : db.Set<TEntity>().CountAsync(predicate, ct);
}
```

- [ ] **Step 3: Build**

```bash
dotnet build
```
Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
git add src/MarketNest.Admin/Infrastructure/Persistence/BaseRepository.cs
git add src/MarketNest.Admin/Infrastructure/Persistence/BaseQuery.cs
git commit -m "feat(admin): add BaseRepository and BaseQuery abstract classes"
```

---

## Task 5: Add Application Layer Interfaces

**Files:**
 - Create: `src/MarketNest.Admin/Application/Submodule/Test/Repositories/ITestRepository.cs`
 - Create: `src/MarketNest.Admin/Application/Submodule/Test/Queries/ITestQuery.cs`
 - Create: `src/MarketNest.Admin/Application/Submodule/Test/Queries/IGetTestsPagedQuery.cs`

- [ ] **Step 1: Create ITestRepository**

```csharp
// src/MarketNest.Admin/Application/Submodule/Test/Repositories/ITestRepository.cs
using MarketNest.Core.Common.Persistence;
using MarketNest.Admin.Domain;

namespace MarketNest.Admin.Application;

public interface ITestRepository : IBaseRepository<TestEntity, Guid>
{
    void RemoveSubEntities(IEnumerable<TestSubEntity> entities);
    void AddSubEntity(TestSubEntity entity);
}
```

`RemoveSubEntities` and `AddSubEntity` are needed because `UpdateTestHandler` replaces child entities — EF Core requires explicit context operations for owned children not managed via aggregate collection mutators.

- [ ] **Step 2: Create ITestQuery**

```csharp
// src/MarketNest.Admin/Application/Submodule/Test/Queries/ITestQuery.cs
using MarketNest.Core.Common.Queries;
using MarketNest.Admin.Domain;

namespace MarketNest.Admin.Application;

public interface ITestQuery : IBaseQuery<TestEntity, Guid>;
```

Simple reads only — `GetByKeyAsync`, `ExistsAsync`, etc. via the base interface.

- [ ] **Step 3: Create IGetTestsPagedQuery**

```csharp
// src/MarketNest.Admin/Application/Submodule/Test/Queries/IGetTestsPagedQuery.cs
using MarketNest.Core.Common.Queries;

namespace MarketNest.Admin.Application;

public interface IGetTestsPagedQuery
{
    Task<PagedResult<TestDto>> ExecuteAsync(GetTestsPagedQuery request, CancellationToken ct);
}
```

- [ ] **Step 4: Build**

```bash
dotnet build
```
Expected: 0 errors. (`TestDto` and `GetTestsPagedQuery` still exist at their old paths — same namespace, no conflict.)

- [ ] **Step 5: Commit**

```bash
git add src/MarketNest.Admin/Application/Submodule/
git commit -m "feat(admin): add ITestRepository, ITestQuery, IGetTestsPagedQuery interfaces"
```

---

## Task 6: Implement TestRepository

- **Files:**
- Create: `src/MarketNest.Admin/Infrastructure/Repositories/Test/TestRepository.cs`
- Modify: `src/MarketNest.Web/Program.cs`

- [ ] **Step 1: Create TestRepository**

```csharp
// src/MarketNest.Admin/Infrastructure/Repositories/Test/TestRepository.cs
using Microsoft.EntityFrameworkCore;
using MarketNest.Admin.Domain;

namespace MarketNest.Admin.Infrastructure;

public class TestRepository(AdminDbContext db)
    : BaseRepository<TestEntity, Guid>(db), ITestRepository
{
    // Override to load aggregate with children — update handler needs them tracked
    public override Task<TestEntity?> GetByKeyAsync(Guid id, CancellationToken ct = default)
        => Db.Tests.Include(x => x.SubEntities).FirstOrDefaultAsync(x => x.Id == id, ct);

    public void RemoveSubEntities(IEnumerable<TestSubEntity> entities)
        => Db.TestSubEntities.RemoveRange(entities);

    public void AddSubEntity(TestSubEntity entity)
        => Db.TestSubEntities.Add(entity);
}
```

- [ ] **Step 2: Register in Program.cs**

In the Admin module section of `src/MarketNest.Web/Program.cs`, add after the AdminReadDbContext registration:

```csharp
builder.Services.AddScoped<MarketNest.Admin.Application.ITestRepository,
    MarketNest.Admin.Infrastructure.TestRepository>();
```

- [ ] **Step 3: Build**

```bash
dotnet build
```
Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
git add src/MarketNest.Admin/Infrastructure/Repositories/
git add src/MarketNest.Web/Program.cs
git commit -m "feat(admin): implement TestRepository extending BaseRepository"
```

---

## Task 7: Implement TestQuery

- **Files:**
- Create: `src/MarketNest.Admin/Infrastructure/Queries/Test/TestQuery.cs`
- Modify: `src/MarketNest.Web/Program.cs`

- [ ] **Step 1: Create TestQuery**

```csharp
// src/MarketNest.Admin/Infrastructure/Queries/Test/TestQuery.cs
using Microsoft.EntityFrameworkCore;
using MarketNest.Admin.Domain;
using MarketNest.Core.Common.Queries;

namespace MarketNest.Admin.Infrastructure;

public class TestQuery(AdminReadDbContext db)
    : BaseQuery<TestEntity, Guid>(db), ITestQuery, IGetTestsPagedQuery
{
    // GetByKeyAsync override: load with includes for read projections
    public override Task<TestEntity?> GetByKeyAsync(Guid id, CancellationToken ct = default)
        => Db.Tests.Include(x => x.SubEntities).FirstOrDefaultAsync(x => x.Id == id, ct);

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
            Items = items,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = total
        };
    }
}
```

- [ ] **Step 2: Register in Program.cs**

Add after the `ITestRepository` registration:

```csharp
builder.Services.AddScoped<MarketNest.Admin.Application.ITestQuery,
    MarketNest.Admin.Infrastructure.TestQuery>();
builder.Services.AddScoped<MarketNest.Admin.Application.IGetTestsPagedQuery,
    MarketNest.Admin.Infrastructure.TestQuery>();
```

Note: both interfaces resolve to the same `TestQuery` instance — DI creates one instance per scope that satisfies both.

- [ ] **Step 3: Build**

```bash
dotnet build
```
Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
git add src/MarketNest.Admin/Infrastructure/Queries/
git add src/MarketNest.Web/Program.cs
git commit -m "feat(admin): implement TestQuery extending BaseQuery"
```

---

## Task 8: Restructure Domain Layer

Move files to new folder structure. Namespaces are unchanged (`MarketNest.Admin.Domain`) so no other file references break.

**Files:** Move 3 files, delete old locations.

- [ ] **Step 1: Create new domain folders and move files**

```bash
mkdir -p "src/MarketNest.Admin/Domain/Submodule/Test/Entities"
mkdir -p "src/MarketNest.Admin/Domain/Submodule/Test/ValueObjects"
```

Copy content of each file, create at new path (same content, same namespace):

`src/MarketNest.Admin/Domain/Submodule/Test/Entities/TestEntity.cs` — same content as current `Domain/Entities/TestEntity.cs`

`src/MarketNest.Admin/Domain/Submodule/Test/Entities/TestSubEntity.cs` — same content as current `Domain/Entities/TestSubEntity.cs`

`src/MarketNest.Admin/Domain/Submodule/Test/ValueObjects/TestValueObject.cs` — same content as current `Domain/ValueObjects/TestValueObject.cs`

- [ ] **Step 2: Delete old domain files**

```bash
rm src/MarketNest.Admin/Domain/Entities/TestEntity.cs
rm src/MarketNest.Admin/Domain/Entities/TestSubEntity.cs
rm src/MarketNest.Admin/Domain/ValueObjects/TestValueObject.cs
```

Remove the now-empty directories if desired:
```bash
rmdir src/MarketNest.Admin/Domain/Entities
rmdir src/MarketNest.Admin/Domain/ValueObjects
```

- [ ] **Step 3: Build**

```bash
dotnet build
```
Expected: 0 errors — namespaces are identical, all references are by namespace not file path.

- [ ] **Step 4: Commit**

```bash
git add -A src/MarketNest.Admin/Domain/
git commit -m "refactor(admin): restructure Domain layer to Submodule/Test/ feature folders"
```

---

## Task 9: Restructure Application Layer (move commands, DTOs, MediatR queries)

Move existing Application files to new folder structure. Namespaces unchanged (`MarketNest.Admin.Application`).

**Files:** Move 5 files, delete old locations. Existing handlers are deleted and rewritten in Tasks 10-11.

- [ ] **Step 1: Create new Application folders**

```bash
mkdir -p "src/MarketNest.Admin/Application/Submodule/Test/Commands"
mkdir -p "src/MarketNest.Admin/Application/Submodule/Test/Queries"
mkdir -p "src/MarketNest.Admin/Application/Submodule/Test/CommandHandlers"
mkdir -p "src/MarketNest.Admin/Application/Submodule/Test/QueryHandlers"
mkdir -p "src/MarketNest.Admin/Application/Submodule/Test/Validators"
```

- [ ] **Step 2: Move commands (identical content, same namespace)**

`src/MarketNest.Admin/Application/Submodule/Test/Commands/CreateTestCommand.cs`:
```csharp
using MarketNest.Core.Common.Cqrs;
using MarketNest.Admin.Domain;

namespace MarketNest.Admin.Application;

public record CreateTestCommand(string Name, TestValueObject Value, IEnumerable<string>? SubTitles = null) : ICommand<Guid>;
```

`src/MarketNest.Admin/Application/Submodule/Test/Commands/UpdateTestCommand.cs`:
```csharp
using MarketNest.Core.Common.Cqrs;
using MarketNest.Admin.Domain;

namespace MarketNest.Admin.Application;

public record UpdateTestCommand(Guid Id, string Name, TestValueObject Value, IEnumerable<string>? SubTitles = null) : ICommand;
```

- [ ] **Step 3: Move MediatR query messages and DTOs**

`src/MarketNest.Admin/Application/Submodule/Test/Queries/GetTestByIdQuery.cs`:
```csharp
using MarketNest.Core.Common.Cqrs;

namespace MarketNest.Admin.Application;

public record GetTestByIdQuery(Guid Id) : IQuery<TestDto?>;
```

`src/MarketNest.Admin/Application/Submodule/Test/Queries/GetTestsPagedQuery.cs`:
```csharp
using MarketNest.Core.Common.Cqrs;
using MarketNest.Core.Common.Queries;

namespace MarketNest.Admin.Application;

public record GetTestsPagedQuery : PagedQuery, IQuery<PagedResult<TestDto>>
{
    public string? SearchName { get; init; }
}
```

`src/MarketNest.Admin/Application/Submodule/Test/Queries/TestDto.cs`:
```csharp
using MarketNest.Admin.Domain;

namespace MarketNest.Admin.Application;

public record TestSubDto(Guid Id, string Title);

public record TestDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public TestValueObject Value { get; init; } = new();
    public IReadOnlyList<TestSubDto> SubEntities { get; init; } = [];
}
```

- [ ] **Step 4: Delete old Application files**

```bash
rm src/MarketNest.Admin/Application/Commands/CreateTestCommand.cs
rm src/MarketNest.Admin/Application/Commands/UpdateTestCommand.cs
rm src/MarketNest.Admin/Application/Queries/GetTestByIdQuery.cs
rm src/MarketNest.Admin/Application/Queries/GetTestsPagedQuery.cs
rm src/MarketNest.Admin/Application/Queries/TestDto.cs
rm src/MarketNest.Admin/Application/Handlers/CreateTestHandler.cs
rm src/MarketNest.Admin/Application/Handlers/UpdateTestHandler.cs
rm src/MarketNest.Admin/Application/Handlers/GetTestByIdHandler.cs
rm src/MarketNest.Admin/Application/Handlers/GetTestsPagedHandler.cs
```

- [ ] **Step 5: Build — expect failure until handlers are rewritten in Tasks 10-11**

```bash
dotnet build
```
Expected: errors about missing handler classes. This is expected — Tasks 10-11 will fix them.

- [ ] **Step 6: Commit**

```bash
git add -A src/MarketNest.Admin/Application/
git commit -m "refactor(admin): restructure Application layer to Submodule/Test/ feature folders"
```

---

## Task 10: Write New CommandHandlers

**Files:**
- Create: `src/MarketNest.Admin/Application/Submodule/Test/CommandHandlers/CreateTestHandler.cs`
- Create: `src/MarketNest.Admin/Application/Submodule/Test/CommandHandlers/UpdateTestHandler.cs`

- [ ] **Step 1: Create CreateTestHandler using ITestRepository**

```csharp
// src/MarketNest.Admin/Application/Submodule/Test/CommandHandlers/CreateTestHandler.cs
using MarketNest.Core.Common;
using MarketNest.Core.Common.Cqrs;
using MarketNest.Admin.Domain;

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

- [ ] **Step 2: Create UpdateTestHandler using ITestRepository**

```csharp
// src/MarketNest.Admin/Application/Submodule/Test/CommandHandlers/UpdateTestHandler.cs
using MediatR;
using MarketNest.Core.Common;
using MarketNest.Core.Common.Cqrs;
using MarketNest.Admin.Domain;

namespace MarketNest.Admin.Application;

public class UpdateTestHandler(ITestRepository repository) : ICommandHandler<UpdateTestCommand, Unit>
{
    public async Task<Result<Unit, Error>> Handle(UpdateTestCommand request, CancellationToken ct)
    {
        var entity = await repository.GetByKeyAsync(request.Id, ct);
        if (entity is null)
            return Result<Unit, Error>.Failure(
                Error.NotFound(nameof(TestEntity), request.Id.ToString()));

        entity.Update(request.Name, request.Value);

        repository.RemoveSubEntities(entity.SubEntities.ToList());

        if (request.SubTitles is not null)
            foreach (var title in request.SubTitles)
                repository.AddSubEntity(new TestSubEntity(Guid.NewGuid(), request.Id, title));

        await repository.SaveChangesAsync(ct);
        return Result<Unit, Error>.Success(Unit.Value);
    }
}
```

- [ ] **Step 3: Build**

```bash
dotnet build
```
Expected: fewer errors — command handlers resolved. Query handler errors remain until Task 11.

- [ ] **Step 4: Commit**

```bash
git add src/MarketNest.Admin/Application/Submodule/Test/CommandHandlers/
git commit -m "feat(admin): rewrite command handlers to use ITestRepository"
```

---

## Task 11: Write New QueryHandlers

**Files:**
- Create: `src/MarketNest.Admin/Application/Submodule/Test/QueryHandlers/GetTestByIdHandler.cs`
- Create: `src/MarketNest.Admin/Application/Submodule/Test/QueryHandlers/GetTestsPagedHandler.cs`

- [ ] **Step 1: Create GetTestByIdHandler using ITestQuery**

```csharp
// src/MarketNest.Admin/Application/Submodule/Test/QueryHandlers/GetTestByIdHandler.cs
using MarketNest.Core.Common.Cqrs;

namespace MarketNest.Admin.Application;

public class GetTestByIdHandler(ITestQuery query) : IQueryHandler<GetTestByIdQuery, TestDto?>
{
    public async Task<TestDto?> Handle(GetTestByIdQuery request, CancellationToken ct)
    {
        var entity = await query.GetByKeyAsync(request.Id, ct);
        if (entity is null) return null;

        return new TestDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Value = entity.Value,
            SubEntities = entity.SubEntities.Select(s => new TestSubDto(s.Id, s.Title)).ToList()
        };
    }
}
```

- [ ] **Step 2: Create GetTestsPagedHandler using IGetTestsPagedQuery**

```csharp
// src/MarketNest.Admin/Application/Submodule/Test/QueryHandlers/GetTestsPagedHandler.cs
using MarketNest.Core.Common.Cqrs;
using MarketNest.Core.Common.Queries;

namespace MarketNest.Admin.Application;

public class GetTestsPagedHandler(IGetTestsPagedQuery query)
    : IQueryHandler<GetTestsPagedQuery, PagedResult<TestDto>>
{
    public Task<PagedResult<TestDto>> Handle(GetTestsPagedQuery request, CancellationToken ct)
        => query.ExecuteAsync(request, ct);
}
```

- [ ] **Step 3: Build — should be clean**

```bash
dotnet build
```
Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
git add src/MarketNest.Admin/Application/Submodule/Test/QueryHandlers/
git commit -m "feat(admin): rewrite query handlers to use ITestQuery and IGetTestsPagedQuery"
```

---

## Task 12: Add Controller Base Classes

**Files:**
- Create: `src/MarketNest.Admin/Infrastructure/Api/Common/ApiV1ControllerBase.cs`
- Create: `src/MarketNest.Admin/Infrastructure/Api/Common/ReadApiV1ControllerBase.cs`
- Create: `src/MarketNest.Admin/Infrastructure/Api/Common/WriteApiV1ControllerBase.cs`

- [ ] **Step 1: Create ApiV1ControllerBase**

```csharp
// src/MarketNest.Admin/Infrastructure/Api/Common/ApiV1ControllerBase.cs
using MediatR;
using Microsoft.AspNetCore.Mvc;
using MarketNest.Core.Common;

namespace MarketNest.Admin.Infrastructure;

[ApiController]
public abstract class ApiV1ControllerBase(IMediator mediator) : ControllerBase
{
    protected IMediator Mediator => mediator;

    protected IActionResult MapError(Error error) => error.Type switch
    {
        ErrorType.NotFound     => NotFound(new { error.Code, error.Message }),
        ErrorType.Conflict     => Conflict(new { error.Code, error.Message }),
        ErrorType.Validation   => BadRequest(new { error.Code, error.Message }),
        ErrorType.Unauthorized => Unauthorized(new { error.Code, error.Message }),
        ErrorType.Forbidden    => Forbid(),
        _                      => Problem(error.Message)
    };
}
```

- [ ] **Step 2: Create ReadApiV1ControllerBase and WriteApiV1ControllerBase**

```csharp
// src/MarketNest.Admin/Infrastructure/Api/Common/ReadApiV1ControllerBase.cs
using MediatR;

namespace MarketNest.Admin.Infrastructure;

public abstract class ReadApiV1ControllerBase(IMediator mediator)
    : ApiV1ControllerBase(mediator);
```

```csharp
// src/MarketNest.Admin/Infrastructure/Api/Common/WriteApiV1ControllerBase.cs
using MediatR;

namespace MarketNest.Admin.Infrastructure;

public abstract class WriteApiV1ControllerBase(IMediator mediator)
    : ApiV1ControllerBase(mediator);
```

- [ ] **Step 3: Build**

```bash
dotnet build
```
Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
git add src/MarketNest.Admin/Infrastructure/Api/Common/
git commit -m "feat(admin): add ApiV1ControllerBase, ReadApiV1ControllerBase, WriteApiV1ControllerBase"
```

---

## Task 13: Replace TestsController with Read/Write Controllers

**Files:**
- Create: `src/MarketNest.Admin/Infrastructure/Api/Test/TestReadController.cs`
- Create: `src/MarketNest.Admin/Infrastructure/Api/Test/TestWriteController.cs`
- Delete: `src/MarketNest.Admin/Infrastructure/Api/TestsController.cs`

- [ ] **Step 1: Create TestReadController**

```csharp
// src/MarketNest.Admin/Infrastructure/Api/Test/TestReadController.cs
using MediatR;
using Microsoft.AspNetCore.Mvc;
using MarketNest.Admin.Application;

namespace MarketNest.Admin.Infrastructure;

[Route("api/v1/admin/tests")]
public class TestReadController(IMediator mediator) : ReadApiV1ControllerBase(mediator)
{
    [HttpGet]
    public async Task<IActionResult> GetPaged(
        [FromQuery] GetTestsPagedQuery query, CancellationToken ct)
        => Ok(await Mediator.Send(query, ct));

    [HttpGet("{id:guid}", Name = "GetTestById")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var dto = await Mediator.Send(new GetTestByIdQuery(id), ct);
        return dto is null ? NotFound() : Ok(dto);
    }
}
```

- [ ] **Step 2: Create TestWriteController**

```csharp
// src/MarketNest.Admin/Infrastructure/Api/Test/TestWriteController.cs
using MediatR;
using Microsoft.AspNetCore.Mvc;
using MarketNest.Admin.Application;
using MarketNest.Admin.Domain;

namespace MarketNest.Admin.Infrastructure;

[Route("api/v1/admin/tests")]
public class TestWriteController(IMediator mediator) : WriteApiV1ControllerBase(mediator)
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTestRequest req, CancellationToken ct)
    {
        var cmd = new CreateTestCommand(
            req.Name,
            new TestValueObject { Code = req.ValueCode, Amount = req.ValueAmount },
            req.SubTitles);
        var result = await Mediator.Send(cmd, ct);
        if (result.IsFailure) return MapError(result.Error);
        return CreatedAtRoute("GetTestById", new { id = result.Value }, new { id = result.Value });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id, [FromBody] UpdateTestRequest req, CancellationToken ct)
    {
        var cmd = new UpdateTestCommand(
            id,
            req.Name,
            new TestValueObject { Code = req.ValueCode, Amount = req.ValueAmount },
            req.SubTitles);
        var result = await Mediator.Send(cmd, ct);
        if (result.IsFailure) return MapError(result.Error);
        return NoContent();
    }

    public record CreateTestRequest(
        string Name, string ValueCode, decimal ValueAmount,
        IEnumerable<string>? SubTitles = null);

    public record UpdateTestRequest(
        string Name, string ValueCode, decimal ValueAmount,
        IEnumerable<string>? SubTitles = null);
}
```

- [ ] **Step 3: Delete old TestsController**

```bash
rm src/MarketNest.Admin/Infrastructure/Api/TestsController.cs
```

- [ ] **Step 4: Add API v1 admin prefix to route whitelist**

In `src/MarketNest.Web/Infrastructure/AppRoutes.cs`:

Add a constant inside the `Api` class:
```csharp
public static class Api
{
    public const string SetLanguage = "/api/set-language";
    public const string OpenApiDoc = "/openapi";
    public const string ScalarDocs = "/scalar";
    public const string AdminV1Prefix = "/api/v1/admin";  // add this
}
```

Add to `WhitelistedPrefixes`:
```csharp
Api.SetLanguage,
Api.OpenApiDoc,
Api.ScalarDocs,
Api.AdminV1Prefix,  // add this
```

- [ ] **Step 5: Build**

```bash
dotnet build
```
Expected: 0 errors.

- [ ] **Step 6: Commit**

```bash
git add src/MarketNest.Admin/Infrastructure/Api/
git commit -m "feat(admin): replace TestsController with TestReadController and TestWriteController"
```

---

## Task 14: Move TestTimerJob to Admin Module

**Files:**
 - Create: `src/MarketNest.Admin/Application/Timer/TestTimer/TestTimerJob.cs`
- Delete: `src/MarketNest.Web/BackgroundJobs/Test/TestTimerJob.cs`
- Modify: `src/MarketNest.Web/Program.cs`

- [ ] **Step 1: Create TestTimerJob in Admin module**

```csharp
// src/MarketNest.Admin/Application/Timer/TestTimer/TestTimerJob.cs
using MarketNest.Core.BackgroundJobs;
using MarketNest.Core.Logging;

namespace MarketNest.Admin.Application;

public class TestTimerJob(IAppLogger<TestTimerJob> logger) : IBackgroundJob
{
    public JobDescriptor Descriptor { get; } = new(
        JobKey: "admin.test.timer",
        DisplayName: "Admin demo timer job",
        OwningModule: "Admin",
        Type: JobType.Timer,
        Schedule: null,
        IsEnabled: true,
        IsRetryable: false,
        MaxRetryCount: 0,
        Description: "A demo job that logs a message and completes.");

    public Task ExecuteAsync(JobExecutionContext context, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("TestTimerJob executed: {ExecutionId}", context.ExecutionId);
        return Task.CompletedTask;
    }
}
```

Note: switched from `ILogger<T>` to `IAppLogger<T>` per project convention.

- [ ] **Step 2: Update Program.cs — update job registration**

Find this line in `src/MarketNest.Web/Program.cs`:
```csharp
builder.Services.AddSingleton<IBackgroundJob, MarketNest.Web.BackgroundJobs.Test.TestTimerJob>();
```

Replace with:
```csharp
builder.Services.AddSingleton<IBackgroundJob, MarketNest.Admin.Application.TestTimerJob>();
```

- [ ] **Step 3: Delete old TestTimerJob**

```bash
rm src/MarketNest.Web/BackgroundJobs/Test/TestTimerJob.cs
rmdir src/MarketNest.Web/BackgroundJobs/Test
```

- [ ] **Step 4: Build**

```bash
dotnet build
```
Expected: 0 errors.

- [ ] **Step 5: Commit**

```bash
git add src/MarketNest.Admin/Application/Timer/
git add src/MarketNest.Web/BackgroundJobs/
git add src/MarketNest.Web/Program.cs
git commit -m "feat(admin): move TestTimerJob from Web host into Admin module"
```

---

## Task 15: Final Build Verification

- [ ] **Step 1: Clean build**

```bash
dotnet build --no-incremental
```
Expected: 0 errors, 0 warnings (TreatWarningsAsErrors is on).

- [ ] **Step 2: Run tests**

```bash
dotnet test
```
Expected: all tests pass (currently no Admin-specific tests).

- [ ] **Step 3: Start the app and verify API**

```bash
dotnet run --project src/MarketNest.Web
```

Then verify endpoints (use the Scalar UI at `http://localhost:5000/scalar` or curl):

```bash
# Create
curl -X POST http://localhost:5000/api/v1/admin/tests \
  -H "Content-Type: application/json" \
  -d '{"name":"Test1","valueCode":"CODE1","valueAmount":100,"subTitles":["Sub A","Sub B"]}'

# Get paged
curl http://localhost:5000/api/v1/admin/tests

# Get by id (use id from create response)
curl http://localhost:5000/api/v1/admin/tests/{id}

# Update
curl -X PUT http://localhost:5000/api/v1/admin/tests/{id} \
  -H "Content-Type: application/json" \
  -d '{"name":"Updated","valueCode":"CODE2","valueAmount":200}'
```

Expected: all 4 endpoints return correct HTTP status codes.

---

## Task 16: Update Docs

**Files:**
- Modify: `docs/architecture.md`
- Modify: `AGENTS.md`

- [ ] **Step 1: Update architecture.md**

Add a new section "Module Layer Patterns" (or update existing) documenting:

```markdown
## Module Layer Patterns

### Read/Write DbContext Split
Each module has two DbContexts:
- `{Module}DbContext` — write context, change tracking ON, implements `IModuleDbContext`, runs migrations
- `{Module}ReadDbContext` — read context, `NoTracking` globally, does NOT implement `IModuleDbContext`

Both contexts use the same `IEntityTypeConfiguration<T>` classes via `ApplyConfigurationsFromAssembly(typeof({Module}DbContext).Assembly)`.

### Query Contracts
- `IBaseQuery<TEntity, TKey>` (Core) — simple reads: GetByKey, Exists, List, FirstOrDefault, Count
- `I{Entity}Query` (Application) — extends IBaseQuery, simple module-specific reads
- `IGet{UseCase}Query` (Application) — complex reads (projections, pagination, joins) get a dedicated interface

Rule: any query involving DTO projection, pagination, or multi-table joins MUST use a dedicated `IGet{UseCase}Query` interface, not a method on `I{Entity}Query`.

### Repository Contracts
- `IBaseRepository<TEntity, TKey>` (Core) — write operations: Add, Update, Remove, SaveChanges, GetByKey
- `I{Entity}Repository` (Application) — extends IBaseRepository, adds aggregate-specific operations

### Abstract Base Classes (Infrastructure/Persistence)
- `BaseRepository<TEntity, TKey>` — default EF Core impl of IBaseRepository using `AdminDbContext`
- `BaseQuery<TEntity, TKey>` — default EF Core impl of IBaseQuery using `AdminReadDbContext`
Concrete classes override only what's module-specific (e.g., loading with `Include`).

### Controller Base Classes (Infrastructure/Api/Common)
- `ApiV1ControllerBase` — shared base: `IMediator`, `MapError(Error)` helper
- `ReadApiV1ControllerBase` — extends base, used by GET-only controllers
- `WriteApiV1ControllerBase` — extends base, used by POST/PUT/DELETE controllers

### Feature Folder Layout (within layers)
```
Application/Submodule/{Feature}/
  Commands/            # ICommand<T> records
  CommandHandlers/     # ICommandHandler implementations
  QueryHandlers/       # IQueryHandler implementations
  Queries/             # IQuery<T> records + IBaseQuery interfaces + DTOs
  Repositories/        # IBaseRepository interfaces
  Validators/          # FluentValidation validators
  DomainEventHandlers/ # IDomainEventHandler implementations
  IntegrationEventHandlers/ # Integration event handlers

Infrastructure/
  Api/{Feature}/       # ReadController + WriteController
  Queries/{Feature}/   # IBaseQuery implementations
  Repositories/{Feature}/ # IBaseRepository implementations
  Persistence/         # DbContexts, BaseRepository, BaseQuery, Configurations/
```
```

- [ ] **Step 2: Update AGENTS.md**

Add enforcement rules for the new patterns. Find the agent enforcement section and add:

```markdown
## Query + Repository Pattern Enforcement

Before writing any handler:
- CommandHandlers MUST inject `I{Entity}Repository`, never `{Module}DbContext` directly
- QueryHandlers for simple reads MUST inject `I{Entity}Query : IBaseQuery<T,K>`
- QueryHandlers for complex reads (pagination, projection, joins) MUST inject a dedicated `IGet{UseCase}Query` interface
- Query interface implementations live in `Infrastructure/Queries/{Feature}/`
- Repository implementations live in `Infrastructure/Repositories/{Feature}/`

Read context rules:
- `{Module}ReadDbContext` is ONLY injected by `BaseQuery` subclasses
- `{Module}DbContext` (write) is ONLY injected by `BaseRepository` subclasses
- Neither context is injected by any Application layer class

Controller base classes:
- All read controllers extend `ReadApiV1ControllerBase`
- All write controllers extend `WriteApiV1ControllerBase`
- Route prefix: `api/v1/{module}/{resource}` — NOT `api/{module}/{resource}`
```

- [ ] **Step 3: Commit**

```bash
git add docs/architecture.md AGENTS.md
git commit -m "docs: update architecture.md and AGENTS.md with new module layer patterns"
```
