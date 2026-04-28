# LoggerMessage Refactor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Migrate all production logging to `[LoggerMessage]` source-generated delegates, eliminating CA1848/CA2254 suppressions and adding EventId tracking across all modules.

**Architecture:** Prerequisite extends `IAppLogger<T> : ILogger` so delegates can accept it directly. Four parallel agent groups (Infrastructure, Pages-existing, Auditing+Jobs, Pages-new/BeginApiScope) run after prerequisite. Cleanup strips dead IAppLogger methods and removes pragma.

**Tech Stack:** .NET 10, `[LoggerMessage]` source generator, `LogEventId` enum (`MarketNest.Base.Infrastructure`), `IAppLogger<T>`, xUnit

---

## Phase 0 — Prerequisite (blocks all other tasks)

### Task 0: Extend IAppLogger\<T\> + update LogEventId enum with exact Infrastructure entries

**Files:**
- Modify: `src/Base/MarketNest.Base.Infrastructure/Logging/IAppLogger.cs`
- Modify: `src/Base/MarketNest.Base.Infrastructure/Logging/AppLogger.cs`
- Modify: `src/Base/MarketNest.Base.Infrastructure/Logging/LogEventId.cs`

- [ ] **Step 1: Extend IAppLogger\<T\> interface**

Replace the `public interface IAppLogger<T>` declaration line:

```csharp
// BEFORE
public interface IAppLogger<T>

// AFTER
public interface IAppLogger<T> : ILogger
```

Full file after change (`src/Base/MarketNest.Base.Infrastructure/Logging/IAppLogger.cs`):

```csharp

namespace MarketNest.Base.Infrastructure;

/// <summary>
///     Thin logging abstraction used across all MarketNest modules.
///     Wraps ILogger with short method names: Info, Debug, Trace, Warn, Error, Critical.
///     Extends ILogger so [LoggerMessage] delegates can accept IAppLogger<T> directly.
///     Usage in a class:
///     public class OrderRepository(IAppLogger&lt;OrderRepository&gt; logger) { ... }
///     Log.InfoOrderPlaced(logger, orderId);
/// </summary>
public interface IAppLogger<T> : ILogger
{
    bool IsEnabled(LogLevel level);

    void Trace(string message);
    void Trace(string messageTemplate, params object?[] args);

    void Debug(string message);
    void Debug(string messageTemplate, params object?[] args);

    void Info(string message);
    void Info(string messageTemplate, params object?[] args);

    void Warn(string message);
    void Warn(string messageTemplate, params object?[] args);
    void Warn(Exception ex, string message);
    void Warn(Exception ex, string messageTemplate, params object?[] args);

#pragma warning disable CA1716
    void Error(Exception ex, string message);
    void Error(Exception ex, string messageTemplate, params object?[] args);
#pragma warning restore CA1716

    void Critical(Exception ex, string message);
    void Critical(Exception ex, string messageTemplate, params object?[] args);
}
```

- [ ] **Step 2: Add explicit ILogger implementation to AppLogger\<T\>**

Full file after change (`src/Base/MarketNest.Base.Infrastructure/Logging/AppLogger.cs`):

```csharp

namespace MarketNest.Base.Infrastructure;

/// <summary>
///     ILogger&lt;T&gt; wrapper that implements IAppLogger&lt;T&gt;.
///     CA2254 and CA1848 are suppressed here intentionally — the template variability is by design
///     since this class is the single delegation point for all log calls.
///     The explicit ILogger.Log implementation uses the core method (not extension methods)
///     so CA1848 does not fire on it — [LoggerMessage] delegates route through this path.
/// </summary>
#pragma warning disable CA2254, CA1848
public sealed class AppLogger<T>(ILogger<T> inner) : IAppLogger<T>
{
    // ── ILogger explicit implementation ──────────────────────────────────────
    // Uses inner.Log (core method, not extension methods) — CA1848 does not fire here.
    void ILogger.Log<TState>(LogLevel logLevel, EventId eventId, TState state,
        Exception? exception, Func<TState, Exception?, string> formatter)
        => inner.Log(logLevel, eventId, state, exception, formatter);

    bool ILogger.IsEnabled(LogLevel logLevel) => inner.IsEnabled(logLevel);

    IDisposable? ILogger.BeginScope<TState>(TState state) => inner.BeginScope(state);

    // ── IAppLogger<T> implementation ─────────────────────────────────────────
    public bool IsEnabled(LogLevel level) => inner.IsEnabled(level);

    public void Trace(string message) => inner.LogTrace(message);
    public void Trace(string messageTemplate, params object?[] args) => inner.LogTrace(messageTemplate, args);

    public void Debug(string message) => inner.LogDebug(message);
    public void Debug(string messageTemplate, params object?[] args) => inner.LogDebug(messageTemplate, args);

    public void Info(string message) => inner.LogInformation(message);
    public void Info(string messageTemplate, params object?[] args) => inner.LogInformation(messageTemplate, args);

    public void Warn(string message) => inner.LogWarning(message);
    public void Warn(string messageTemplate, params object?[] args) => inner.LogWarning(messageTemplate, args);
    public void Warn(Exception ex, string message) => inner.LogWarning(ex, message);
    public void Warn(Exception ex, string messageTemplate, params object?[] args) => inner.LogWarning(ex, messageTemplate, args);

#pragma warning disable CA1716
    public void Error(Exception ex, string message) => inner.LogError(ex, message);
    public void Error(Exception ex, string messageTemplate, params object?[] args) => inner.LogError(ex, messageTemplate, args);
#pragma warning restore CA1716

    public void Critical(Exception ex, string message) => inner.LogCritical(ex, message);
    public void Critical(Exception ex, string messageTemplate, params object?[] args) => inner.LogCritical(ex, messageTemplate, args);
}
#pragma warning restore CA2254, CA1848
```

- [ ] **Step 3: Update LogEventId Infrastructure region with exact entries**

Replace the entire `#region Infrastructure / Middleware — 1000-1999` section in `src/Base/MarketNest.Base.Infrastructure/Logging/LogEventId.cs`:

```csharp
    #region Infrastructure / Middleware — 1000-1999

    // DatabaseInitializer — 1000-1015
    DbInitStart = 1000,
    DbInitCompleted = 1001,
    DbInitNoContexts = 1002,
    DbInitModelUnchanged = 1003,
    DbInitNoMigrationFiles = 1004,
    DbInitHashChangedNoMigrations = 1005,
    DbInitApplyingMigrations = 1006,
    DbInitMigrationsApplied = 1007,
    DbInitMigrationCritical = 1008,
    DbInitNoSeeders = 1009,
    DbInitSeedEvaluating = 1010,
    DbInitSeedSkippedProd = 1011,
    DbInitSeedSkippedVersion = 1012,
    DbInitSeedRunning = 1013,
    DbInitSeedCompleted = 1014,
    DbInitSeedFailed = 1015,

    // DatabaseTracker — 1016-1022
    DbTrackerTablesEnsured = 1016,
    DbTrackerLockAcquired = 1017,
    DbTrackerLockReleased = 1018,
    DbTrackerHashSaved = 1019,
    DbTrackerSeedVersionSaved = 1020,

    // InProcessEventBus — 1021-1023
    // NOTE: starts at 1021 to avoid collision — leave gap at 1021 if needed
    EventBusPublishStart = 1040,
    EventBusPublishSuccess = 1041,
    EventBusPublishError = 1042,

    // MassTransitEventBus — 1050-1052 (Phase 3)
    MassTransitPublishStart = 1050,
    MassTransitPublishSuccess = 1051,
    MassTransitPublishError = 1052,

    // ApiContractGenerator — 1060-1062
    ApiContractFetchStart = 1060,
    ApiContractUpdated = 1061,
    ApiContractFetchFailed = 1062,

    // RouteWhitelistMiddleware — 1070
    RouteBlocked = 1070,

    // Reserved — 1071-1999

    #endregion
```

- [ ] **Step 4: Build to verify no compile errors**

```
dotnet build MarketNest.slnx
```

Expected: Build succeeded, 0 errors. If `IAppLogger<T> : ILogger` introduces an ambiguity on `IsEnabled` — the explicit `ILogger.IsEnabled` and the `IAppLogger<T>.IsEnabled(LogLevel)` have the same signature but different receivers. The explicit `bool ILogger.IsEnabled(LogLevel logLevel)` in AppLogger is explicit so there's no ambiguity. Verify the build passes.

- [ ] **Step 5: Run tests**

```
dotnet test MarketNest.slnx
```

Expected: All tests pass.

- [ ] **Step 6: Commit**

```bash
git add src/Base/MarketNest.Base.Infrastructure/Logging/IAppLogger.cs
git add src/Base/MarketNest.Base.Infrastructure/Logging/AppLogger.cs
git add src/Base/MarketNest.Base.Infrastructure/Logging/LogEventId.cs
git commit -m "feat(logging): extend IAppLogger<T> : ILogger for [LoggerMessage] compatibility

Adds explicit ILogger.Log/IsEnabled/BeginScope to AppLogger<T> delegating
to inner via core method (not extension methods — no CA1848 on these).
Updates LogEventId Infrastructure region with exact entries matching
DatabaseInitializer, DatabaseTracker, InProcessEventBus, ApiContractGenerator,
RouteWhitelistMiddleware actual log calls."
```

---

## Phase 1 — Parallel Execution (Tasks 1–4 run simultaneously after Task 0)

---

### Task 1: Refactor InProcessEventBus

**Files:**
- Modify: `src/MarketNest.Web/Infrastructure/InProcessEventBus.cs`

- [ ] **Step 1: Rewrite file with [LoggerMessage]**

```csharp
using MediatR;
using MarketNest.Base.Common;
using MarketNest.Base.Infrastructure;

namespace MarketNest.Web.Infrastructure;

/// <summary>
///     Phase 1 implementation — dispatches integration events in-process via MediatR.
///     Phase 3 migration: replace this registration with <c>MassTransitEventBus</c>.
/// </summary>
internal sealed partial class InProcessEventBus(IPublisher publisher, IAppLogger<InProcessEventBus> logger) : IEventBus
{
    public async Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken = default)
        where TEvent : class, IIntegrationEvent
    {
        var eventTypeName = integrationEvent.GetType().Name;
        Log.InfoPublishStart(logger, eventTypeName, integrationEvent.EventId);

        try
        {
            await publisher.Publish(integrationEvent, cancellationToken);
            Log.InfoPublishSuccess(logger, eventTypeName, integrationEvent.EventId);
        }
        catch (Exception ex)
        {
            Log.ErrorPublishFailed(logger, eventTypeName, integrationEvent.EventId, ex);
            throw;
        }
    }

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.EventBusPublishStart, LogLevel.Information,
            "Publishing integration event {EventType} (Id: {EventId})")]
        public static partial void InfoPublishStart(ILogger logger, string eventType, Guid eventId);

        [LoggerMessage((int)LogEventId.EventBusPublishSuccess, LogLevel.Information,
            "Successfully dispatched integration event {EventType} (Id: {EventId})")]
        public static partial void InfoPublishSuccess(ILogger logger, string eventType, Guid eventId);

        [LoggerMessage((int)LogEventId.EventBusPublishError, LogLevel.Error,
            "Failed to dispatch integration event {EventType} (Id: {EventId})")]
        public static partial void ErrorPublishFailed(ILogger logger, string eventType, Guid eventId, Exception ex);
    }
}
```

- [ ] **Step 2: Build**

```
dotnet build MarketNest.slnx
```

Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add src/MarketNest.Web/Infrastructure/InProcessEventBus.cs
git commit -m "refactor(logging): migrate InProcessEventBus to [LoggerMessage]"
```

---

### Task 2: Refactor DatabaseInitializer

**Files:**
- Modify: `src/MarketNest.Web/Infrastructure/DatabaseInitializer.cs`

- [ ] **Step 1: Add `partial` and nested Log class**

The full refactored file:

```csharp
using System.Diagnostics;
using Npgsql;
using MarketNest.Base.Infrastructure;
using MarketNest.Base.Common;
using Microsoft.EntityFrameworkCore;

namespace MarketNest.Web.Infrastructure;

/// <summary>
///     Applies pending EF Core migrations and runs <see cref="IDataSeeder" /> implementations on startup.
/// </summary>
public sealed partial class DatabaseInitializer(
    IServiceProvider serviceProvider,
    DatabaseTracker tracker,
    IAppLogger<DatabaseInitializer> logger,
    IHostEnvironment env)
{
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        var totalSw = Stopwatch.StartNew();
        Log.InfoStart(logger);

        await tracker.EnsureTrackingTablesExistAsync(ct);
        NpgsqlConnection lockConn = await tracker.AcquireAdvisoryLockAsync(ct);

        try
        {
            await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();
            await ApplyMigrationsAsync(scope.ServiceProvider, ct);
            await RunSeedersAsync(scope.ServiceProvider, ct);
        }
        finally
        {
            await tracker.ReleaseAdvisoryLockAsync(lockConn);
        }

        totalSw.Stop();
        Log.InfoCompleted(logger, totalSw.ElapsedMilliseconds);
    }

    private async Task ApplyMigrationsAsync(IServiceProvider scopedProvider, CancellationToken ct)
    {
        var moduleContexts = scopedProvider.GetServices<IModuleDbContext>().ToList();

        if (moduleContexts.Count == 0)
        {
            Log.InfoNoContexts(logger);
            return;
        }

        foreach (var module in moduleContexts)
        {
            var contextName = module.ContextName;
            var schemaName = module.SchemaName;
            var dbContext = module.AsDbContext();

            try
            {
#pragma warning disable EF1003
                await dbContext.Database.ExecuteSqlRawAsync(
                    "CREATE SCHEMA IF NOT EXISTS \"" + schemaName + "\"", ct);
#pragma warning restore EF1003

                var currentHash = ModelHasher.ComputeHash(dbContext.Model);
                string? storedHash = await tracker.GetLastModelHashAsync(contextName, ct);

                if (string.Equals(currentHash, storedHash, StringComparison.Ordinal))
                {
                    Log.InfoModelUnchanged(logger, contextName, currentHash[..12] + "…");
                    continue;
                }

                var pending = (await dbContext.Database.GetPendingMigrationsAsync(ct)).ToList();
                var applied = (await dbContext.Database.GetAppliedMigrationsAsync(ct)).ToList();
                var sw = Stopwatch.StartNew();

                if (applied.Count == 0 && pending.Count == 0)
                {
                    Log.InfoNoMigrationFiles(logger, contextName);
                    await dbContext.Database.EnsureCreatedAsync(ct);
                }
                else if (pending.Count == 0)
                {
                    Log.InfoHashChangedNoMigrations(logger, contextName);
                    await tracker.SaveModelHashAsync(contextName, currentHash, ct);
                    continue;
                }
                else
                {
                    Log.InfoApplyingMigrations(logger, contextName, pending.Count,
                        string.Join(", ", pending));
                    await dbContext.Database.MigrateAsync(ct);
                }

                sw.Stop();
                await tracker.SaveModelHashAsync(contextName, currentHash, ct);
                Log.InfoMigrationsApplied(logger, contextName, sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                Log.CriticalMigrationFailed(logger, contextName, ex);
                throw;
            }
        }
    }

    private async Task RunSeedersAsync(IServiceProvider scopedProvider, CancellationToken ct)
    {
        var seeders = scopedProvider
            .GetServices<IDataSeeder>()
            .OrderBy(s => s.Order)
            .ToList();

        if (seeders.Count == 0)
        {
            Log.InfoNoSeeders(logger);
            return;
        }

        Log.InfoSeedEvaluating(logger, seeders.Count);

        foreach (var seeder in seeders)
        {
            var seederName = seeder.Name;
            var seederVersion = seeder.Version;

            if (env.IsProduction() && !seeder.RunInProduction)
            {
                Log.DebugSeedSkippedProd(logger, seederName);
                continue;
            }

            try
            {
                string? storedVersion = await tracker.GetLastSeedVersionAsync(seederName, ct);

                if (string.Equals(seederVersion, storedVersion, StringComparison.Ordinal))
                {
                    Log.DebugSeedSkippedVersion(logger, seederName, seederVersion);
                    continue;
                }

                Log.InfoSeedRunning(logger, seederName,
                    storedVersion ?? "(first run)", seederVersion, seeder.Order);

                var sw = Stopwatch.StartNew();
                await seeder.SeedAsync(ct);
                sw.Stop();

                await tracker.SaveSeedVersionAsync(seederName, seederVersion, ct);
                Log.InfoSeedCompleted(logger, seederName, sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                Log.ErrorSeedFailed(logger, seederName, ex);
                if (!env.IsProduction())
                    throw;
            }
        }
    }

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.DbInitStart, LogLevel.Information,
            "Database initialization starting")]
        public static partial void InfoStart(ILogger logger);

        [LoggerMessage((int)LogEventId.DbInitCompleted, LogLevel.Information,
            "Database initialization completed in {ElapsedMs}ms")]
        public static partial void InfoCompleted(ILogger logger, long elapsedMs);

        [LoggerMessage((int)LogEventId.DbInitNoContexts, LogLevel.Information,
            "No module DbContexts registered — skipping migrations")]
        public static partial void InfoNoContexts(ILogger logger);

        [LoggerMessage((int)LogEventId.DbInitModelUnchanged, LogLevel.Information,
            "[{Context}] Model unchanged (hash={Hash}) — skipping migration")]
        public static partial void InfoModelUnchanged(ILogger logger, string context, string hash);

        [LoggerMessage((int)LogEventId.DbInitNoMigrationFiles, LogLevel.Information,
            "[{Context}] No migration files found — using EnsureCreated to build schema from model")]
        public static partial void InfoNoMigrationFiles(ILogger logger, string context);

        [LoggerMessage((int)LogEventId.DbInitHashChangedNoMigrations, LogLevel.Information,
            "[{Context}] No pending migrations, but model hash changed — updating tracker")]
        public static partial void InfoHashChangedNoMigrations(ILogger logger, string context);

        [LoggerMessage((int)LogEventId.DbInitApplyingMigrations, LogLevel.Information,
            "[{Context}] Applying {Count} pending migrations: {Migrations}")]
        public static partial void InfoApplyingMigrations(ILogger logger, string context, int count, string migrations);

        [LoggerMessage((int)LogEventId.DbInitMigrationsApplied, LogLevel.Information,
            "[{Context}] Migrations applied in {ElapsedMs}ms")]
        public static partial void InfoMigrationsApplied(ILogger logger, string context, long elapsedMs);

        [LoggerMessage((int)LogEventId.DbInitMigrationCritical, LogLevel.Critical,
            "[{Context}] Migration failed — application may be in an inconsistent state")]
        public static partial void CriticalMigrationFailed(ILogger logger, string context, Exception ex);

        [LoggerMessage((int)LogEventId.DbInitNoSeeders, LogLevel.Information,
            "No data seeders registered")]
        public static partial void InfoNoSeeders(ILogger logger);

        [LoggerMessage((int)LogEventId.DbInitSeedEvaluating, LogLevel.Information,
            "Evaluating {Count} data seeders")]
        public static partial void InfoSeedEvaluating(ILogger logger, int count);

        [LoggerMessage((int)LogEventId.DbInitSeedSkippedProd, LogLevel.Debug,
            "[{Seeder}] Skipped — not configured for production")]
        public static partial void DebugSeedSkippedProd(ILogger logger, string seeder);

        [LoggerMessage((int)LogEventId.DbInitSeedSkippedVersion, LogLevel.Debug,
            "[{Seeder}] Version unchanged ({Version}) — skipping")]
        public static partial void DebugSeedSkippedVersion(ILogger logger, string seeder, string version);

        [LoggerMessage((int)LogEventId.DbInitSeedRunning, LogLevel.Information,
            "[{Seeder}] Running (version {OldVersion} → {NewVersion}, order={Order})")]
        public static partial void InfoSeedRunning(ILogger logger, string seeder,
            string oldVersion, string newVersion, int order);

        [LoggerMessage((int)LogEventId.DbInitSeedCompleted, LogLevel.Information,
            "[{Seeder}] Completed in {ElapsedMs}ms")]
        public static partial void InfoSeedCompleted(ILogger logger, string seeder, long elapsedMs);

        [LoggerMessage((int)LogEventId.DbInitSeedFailed, LogLevel.Error,
            "[{Seeder}] Seed failed")]
        public static partial void ErrorSeedFailed(ILogger logger, string seeder, Exception ex);
    }
}
```

- [ ] **Step 2: Build**

```
dotnet build MarketNest.slnx
```

Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add src/MarketNest.Web/Infrastructure/DatabaseInitializer.cs
git commit -m "refactor(logging): migrate DatabaseInitializer to [LoggerMessage] (16 calls)"
```

---

### Task 3: Refactor DatabaseTracker

**Files:**
- Modify: `src/MarketNest.Web/Infrastructure/DatabaseTracker.cs`

- [ ] **Step 1: Add `partial` and replace 5 Debug log calls**

At the top of the class, add `partial`:
```csharp
public sealed partial class DatabaseTracker(
    IConfiguration configuration,
    IAppLogger<DatabaseTracker> logger)
```

Replace each `logger.Debug(...)` call:

| Old call | New call |
|----------|----------|
| `logger.Debug("Tracking tables ensured in schema '{Schema}'", Schema)` | `Log.DebugTablesEnsured(logger, Schema)` |
| `logger.Debug("Advisory lock {LockId} acquired", AdvisoryLockId)` | `Log.DebugLockAcquired(logger, AdvisoryLockId)` |
| `logger.Debug("Advisory lock {LockId} released", AdvisoryLockId)` | `Log.DebugLockReleased(logger, AdvisoryLockId)` |
| `logger.Debug("Model hash saved for context '{Context}': {Hash}", contextName, modelHash[..12] + "…")` | `Log.DebugHashSaved(logger, contextName, modelHash[..12] + "…")` |
| `logger.Debug("Seed version saved for '{Seeder}': {Version}", seederName, version)` | `Log.DebugSeedVersionSaved(logger, seederName, version)` |

Add at the end of the file (before closing brace of the class):

```csharp
    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.DbTrackerTablesEnsured, LogLevel.Debug,
            "Tracking tables ensured in schema '{Schema}'")]
        public static partial void DebugTablesEnsured(ILogger logger, string schema);

        [LoggerMessage((int)LogEventId.DbTrackerLockAcquired, LogLevel.Debug,
            "Advisory lock {LockId} acquired")]
        public static partial void DebugLockAcquired(ILogger logger, long lockId);

        [LoggerMessage((int)LogEventId.DbTrackerLockReleased, LogLevel.Debug,
            "Advisory lock {LockId} released")]
        public static partial void DebugLockReleased(ILogger logger, long lockId);

        [LoggerMessage((int)LogEventId.DbTrackerHashSaved, LogLevel.Debug,
            "Model hash saved for context '{Context}': {Hash}")]
        public static partial void DebugHashSaved(ILogger logger, string context, string hash);

        [LoggerMessage((int)LogEventId.DbTrackerSeedVersionSaved, LogLevel.Debug,
            "Seed version saved for '{Seeder}': {Version}")]
        public static partial void DebugSeedVersionSaved(ILogger logger, string seeder, string version);
    }
```

- [ ] **Step 2: Build**

```
dotnet build MarketNest.slnx
```

- [ ] **Step 3: Commit**

```bash
git add src/MarketNest.Web/Infrastructure/DatabaseTracker.cs
git commit -m "refactor(logging): migrate DatabaseTracker to [LoggerMessage] (5 calls)"
```

---

### Task 4: Refactor RouteWhitelistMiddleware

**Files:**
- Modify: `src/MarketNest.Web/Infrastructure/RouteWhitelistMiddleware.cs`

This file uses `ILogger<>` directly (not IAppLogger). Convert to IAppLogger + [LoggerMessage].

- [ ] **Step 1: Rewrite file**

```csharp
namespace MarketNest.Web.Infrastructure;

/// <summary>
///     Blocks any request whose path is not in <see cref="AppRoutes.WhitelistedPrefixes" />.
///     Must be registered AFTER UseStaticFiles so that CSS/JS/images are not affected.
/// </summary>
public sealed partial class RouteWhitelistMiddleware(
    RequestDelegate next,
    IAppLogger<RouteWhitelistMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        string path = context.Request.Path.Value ?? "/";

        if (!AppRoutes.IsAllowed(path))
        {
            Log.WarnRouteBlocked(logger, path);
            context.Response.Redirect(AppRoutes.NotFound);
            return;
        }

        await next(context);
    }

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.RouteBlocked, LogLevel.Warning,
            "Blocked request to non-whitelisted route: {Path}")]
        public static partial void WarnRouteBlocked(ILogger logger, string path);
    }
}
```

Note: DI registration in Program.cs injects `ILogger<RouteWhitelistMiddleware>` automatically via `UseMiddleware<>`. Since `IAppLogger<T>` is registered as open-generic in DI, this works automatically. Verify DI registration in `Program.cs` — if it uses `UseMiddleware<RouteWhitelistMiddleware>()`, no changes needed there.

- [ ] **Step 2: Build**

```
dotnet build MarketNest.slnx
```

- [ ] **Step 3: Commit**

```bash
git add src/MarketNest.Web/Infrastructure/RouteWhitelistMiddleware.cs
git commit -m "refactor(logging): migrate RouteWhitelistMiddleware to [LoggerMessage] + IAppLogger<T>"
```

---

### Task 5: Refactor ApiContractGenerator

**Files:**
- Modify: `src/MarketNest.Web/Infrastructure/ApiContractGenerator.cs`

Currently uses `ILogger<ApiContractGenerator>` + top-level partial class extension methods. Convert to `IAppLogger<>` + nested Log class with EventIds. Also has an unguarded `_logger.LogWarning(...)` that needs migrating.

- [ ] **Step 1: Change field declaration and constructor**

Change:
```csharp
// BEFORE
private readonly ILogger<ApiContractGenerator> _logger;
...
public ApiContractGenerator(
    IServiceProvider serviceProvider,
    IHttpClientFactory httpClientFactory,
    IWebHostEnvironment environment,
    ILogger<ApiContractGenerator> logger)
{
    ...
    _logger = logger;
}

// AFTER — use primary constructor + IAppLogger
public sealed partial class ApiContractGenerator(
    IServiceProvider serviceProvider,
    IHttpClientFactory httpClientFactory,
    IWebHostEnvironment environment,
    IAppLogger<ApiContractGenerator> logger) : BackgroundService
```

Remove the old constructor body and make the class use primary constructor parameters directly.

- [ ] **Step 2: Replace call sites**

| Old | New |
|-----|-----|
| `_logger.LogFetchingOpenApiSpec(openApiUrl)` | `Log.InfoFetchStart(logger, openApiUrl)` |
| `_logger.LogWarning("OpenAPI endpoint returned {StatusCode} ...", response.StatusCode)` | `Log.WarnFetchFailed(logger, (int)response.StatusCode)` |
| `_logger.LogApiContractUpdated(outputPath, endpointCount)` | `Log.InfoContractUpdated(logger, outputPath, endpointCount)` |

- [ ] **Step 3: Replace the top-level partial class with a nested Log class**

Remove:
```csharp
internal static partial class ApiContractGeneratorLogMessages
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Fetching OpenAPI spec from {Url}")]
    public static partial void LogFetchingOpenApiSpec(this ILogger logger, string url);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "api-contract.md updated at {Path} with {EndpointCount} endpoints")]
    public static partial void LogApiContractUpdated(this ILogger logger, string path, int endpointCount);
}
```

Add at end of `ApiContractGenerator` class (before closing `}`):

```csharp
    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.ApiContractFetchStart, LogLevel.Information,
            "Fetching OpenAPI spec from {Url}")]
        public static partial void InfoFetchStart(ILogger logger, string url);

        [LoggerMessage((int)LogEventId.ApiContractFetchFailed, LogLevel.Warning,
            "OpenAPI endpoint returned {StatusCode} — skipping api-contract.md generation")]
        public static partial void WarnFetchFailed(ILogger logger, int statusCode);

        [LoggerMessage((int)LogEventId.ApiContractUpdated, LogLevel.Information,
            "api-contract.md updated at {Path} with {EndpointCount} endpoints")]
        public static partial void InfoContractUpdated(ILogger logger, string path, int endpointCount);
    }
```

Also update `GenerateContractAsync` to use injected `logger` field (primary constructor parameter) instead of `_logger` field.

- [ ] **Step 4: Build**

```
dotnet build MarketNest.slnx
```

- [ ] **Step 5: Commit**

```bash
git add src/MarketNest.Web/Infrastructure/ApiContractGenerator.cs
git commit -m "refactor(logging): migrate ApiContractGenerator to [LoggerMessage] nested Log class"
```

---

### Task 6: Refactor MassTransitEventBus (commented Phase 3 code)

**Files:**
- Modify: `src/MarketNest.Web/Infrastructure/MassTransitEventBus.cs`

This file is entirely commented out (Phase 3). Update the commented code so it follows the [LoggerMessage] pattern when it gets uncommented in Phase 3.

- [ ] **Step 1: Update commented code**

Replace the existing commented file content with:

```csharp
// ──────────────────────────────────────────────────────────────────────────────
// Phase 3 ONLY — do NOT use in Phase 1.
// Uncomment and register when migrating from in-process events to RabbitMQ.
// ──────────────────────────────────────────────────────────────────────────────
//
// using MarketNest.Base.Common;
// using MarketNest.Base.Infrastructure;
// using MassTransit;
//
// namespace MarketNest.Web.Infrastructure;
//
// internal sealed partial class MassTransitEventBus(
//     IPublishEndpoint publishEndpoint,
//     IAppLogger<MassTransitEventBus> logger) : IEventBus
// {
//     public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
//         where TEvent : class, IIntegrationEvent
//     {
//         var eventTypeName = @event.GetType().Name;
//         Log.InfoPublishStart(logger, eventTypeName, @event.EventId);
//
//         await publishEndpoint.Publish(@event, cancellationToken);
//
//         Log.InfoPublishSuccess(logger, eventTypeName, @event.EventId);
//     }
//
//     private static partial class Log
//     {
//         [LoggerMessage((int)LogEventId.MassTransitPublishStart, LogLevel.Information,
//             "Publishing integration event {EventType} (Id: {EventId}) to RabbitMQ")]
//         public static partial void InfoPublishStart(ILogger logger, string eventType, Guid eventId);
//
//         [LoggerMessage((int)LogEventId.MassTransitPublishSuccess, LogLevel.Information,
//             "Successfully published integration event {EventType} (Id: {EventId}) to RabbitMQ")]
//         public static partial void InfoPublishSuccess(ILogger logger, string eventType, Guid eventId);
//     }
// }
```

- [ ] **Step 2: Commit**

```bash
git add src/MarketNest.Web/Infrastructure/MassTransitEventBus.cs
git commit -m "refactor(logging): update MassTransitEventBus Phase 3 stub to [LoggerMessage] pattern"
```

---

### Task 7: Refactor Auth Pages (Login, Register, ForgotPassword)

**Files:**
- Modify: `src/MarketNest.Web/Pages/Auth/Login.cshtml.cs`
- Modify: `src/MarketNest.Web/Pages/Auth/Register.cshtml.cs`
- Modify: `src/MarketNest.Web/Pages/Auth/ForgotPassword.cshtml.cs`

All three pages follow identical patterns: `logger.Info("API {Api} Start - CorrelationId={Cid}", ...)` for OnGet and OnPost.

- [ ] **Step 1: Rewrite Login.cshtml.cs**

```csharp
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarketNest.Web.Pages.Auth;

public partial class LoginModel(IAppLogger<LoginModel> logger) : PageModel
{
    public void OnGet()
        => Log.InfoOnGet(logger, HttpContext?.TraceIdentifier ?? "-");

    public void OnPost()
        => Log.InfoOnPost(logger, HttpContext?.TraceIdentifier ?? "-");

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.AuthLoginStart, LogLevel.Information,
            "Login OnGet Start - CorrelationId={CorrelationId}")]
        public static partial void InfoOnGet(ILogger logger, string correlationId);

        [LoggerMessage((int)LogEventId.AuthLoginStart + 1, LogLevel.Information,
            "Login OnPost Start - CorrelationId={CorrelationId}")]
        public static partial void InfoOnPost(ILogger logger, string correlationId);
    }
}
```

- [ ] **Step 2: Rewrite Register.cshtml.cs**

```csharp
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarketNest.Web.Pages.Auth;

public partial class RegisterModel(IAppLogger<RegisterModel> logger) : PageModel
{
    public void OnGet()
        => Log.InfoOnGet(logger, HttpContext?.TraceIdentifier ?? "-");

    public void OnPost()
        => Log.InfoOnPost(logger, HttpContext?.TraceIdentifier ?? "-");

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.AuthRegisterStart, LogLevel.Information,
            "Register OnGet Start - CorrelationId={CorrelationId}")]
        public static partial void InfoOnGet(ILogger logger, string correlationId);

        [LoggerMessage((int)LogEventId.AuthRegisterStart + 1, LogLevel.Information,
            "Register OnPost Start - CorrelationId={CorrelationId}")]
        public static partial void InfoOnPost(ILogger logger, string correlationId);
    }
}
```

- [ ] **Step 3: Rewrite ForgotPassword.cshtml.cs**

```csharp
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarketNest.Web.Pages.Auth;

public partial class ForgotPasswordModel(IAppLogger<ForgotPasswordModel> logger) : PageModel
{
    public void OnGet()
        => Log.InfoOnGet(logger, HttpContext?.TraceIdentifier ?? "-");

    public void OnPost()
        => Log.InfoOnPost(logger, HttpContext?.TraceIdentifier ?? "-");

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.AuthForgotPasswordStart, LogLevel.Information,
            "ForgotPassword OnGet Start - CorrelationId={CorrelationId}")]
        public static partial void InfoOnGet(ILogger logger, string correlationId);

        [LoggerMessage((int)LogEventId.AuthForgotPasswordStart + 1, LogLevel.Information,
            "ForgotPassword OnPost Start - CorrelationId={CorrelationId}")]
        public static partial void InfoOnPost(ILogger logger, string correlationId);
    }
}
```

- [ ] **Step 4: Build**

```
dotnet build MarketNest.slnx
```

- [ ] **Step 5: Commit**

```bash
git add src/MarketNest.Web/Pages/Auth/
git commit -m "refactor(logging): migrate Auth pages to [LoggerMessage]"
```

---

### Task 8: Refactor Account Pages (Orders + Settings + Disputes)

**Files:**
- Modify: `src/MarketNest.Web/Pages/Account/Orders/Index.cshtml.cs`
- Modify: `src/MarketNest.Web/Pages/Account/Orders/Detail.cshtml.cs`
- Modify: `src/MarketNest.Web/Pages/Account/Orders/Review.cshtml.cs`
- Modify: `src/MarketNest.Web/Pages/Account/Settings/Index.cshtml.cs`
- Modify: `src/MarketNest.Web/Pages/Account/Disputes/Index.cshtml.cs`
- Modify: `src/MarketNest.Web/Pages/Account/Disputes/Detail.cshtml.cs`

- [ ] **Step 1: Rewrite Account/Orders/Index.cshtml.cs**

```csharp
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarketNest.Web.Pages.Account.Orders;

public partial class IndexModel(IAppLogger<IndexModel> logger) : PageModel
{
    public void OnGet()
        => Log.InfoOnGet(logger, HttpContext?.TraceIdentifier ?? "-");

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.AccountOrdersIndexStart, LogLevel.Information,
            "AccountOrders OnGet Start - CorrelationId={CorrelationId}")]
        public static partial void InfoOnGet(ILogger logger, string correlationId);
    }
}
```

- [ ] **Step 2: Rewrite Account/Orders/Detail.cshtml.cs**

```csharp
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarketNest.Web.Pages.Account.Orders;

public partial class DetailModel(IAppLogger<DetailModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true)] public Guid OrderId { get; set; }

    public void OnGet()
        => Log.InfoOnGet(logger, OrderId, HttpContext?.TraceIdentifier ?? "-");

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.AccountOrdersDetailStart, LogLevel.Information,
            "AccountOrderDetail OnGet Start - OrderId={OrderId} CorrelationId={CorrelationId}")]
        public static partial void InfoOnGet(ILogger logger, Guid orderId, string correlationId);
    }
}
```

- [ ] **Step 3: Rewrite Account/Orders/Review.cshtml.cs**

```csharp
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarketNest.Web.Pages.Account.Orders;

public partial class ReviewModel(IAppLogger<ReviewModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true)] public Guid OrderId { get; set; }

    public void OnGet()
        => Log.InfoOnGet(logger, OrderId, HttpContext?.TraceIdentifier ?? "-");

    public void OnPost()
        => Log.InfoOnPost(logger, OrderId, HttpContext?.TraceIdentifier ?? "-");

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.AccountOrdersReviewStart, LogLevel.Information,
            "AccountOrderReview OnGet Start - OrderId={OrderId} CorrelationId={CorrelationId}")]
        public static partial void InfoOnGet(ILogger logger, Guid orderId, string correlationId);

        [LoggerMessage((int)LogEventId.AccountOrdersReviewStart + 1, LogLevel.Information,
            "AccountOrderReview OnPost Start - OrderId={OrderId} CorrelationId={CorrelationId}")]
        public static partial void InfoOnPost(ILogger logger, Guid orderId, string correlationId);
    }
}
```

- [ ] **Step 4: Rewrite Account/Settings/Index.cshtml.cs**

```csharp
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarketNest.Web.Pages.Account.Settings;

public partial class IndexModel(IAppLogger<IndexModel> logger) : PageModel
{
    public void OnGet()
        => Log.InfoOnGet(logger, HttpContext?.TraceIdentifier ?? "-");

    public void OnPost()
        => Log.InfoOnPost(logger, HttpContext?.TraceIdentifier ?? "-");

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.AccountSettingsStart, LogLevel.Information,
            "AccountSettings OnGet Start - CorrelationId={CorrelationId}")]
        public static partial void InfoOnGet(ILogger logger, string correlationId);

        [LoggerMessage((int)LogEventId.AccountSettingsStart + 1, LogLevel.Information,
            "AccountSettings OnPost Start - CorrelationId={CorrelationId}")]
        public static partial void InfoOnPost(ILogger logger, string correlationId);
    }
}
```

- [ ] **Step 5: Rewrite Account/Disputes/Index.cshtml.cs**

```csharp
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarketNest.Web.Pages.Account.Disputes;

public partial class IndexModel(IAppLogger<IndexModel> logger) : PageModel
{
    public void OnGet()
        => Log.InfoOnGet(logger, HttpContext?.TraceIdentifier ?? "-");

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.AccountDisputesIndexStart, LogLevel.Information,
            "AccountDisputes OnGet Start - CorrelationId={CorrelationId}")]
        public static partial void InfoOnGet(ILogger logger, string correlationId);
    }
}
```

- [ ] **Step 6: Rewrite Account/Disputes/Detail.cshtml.cs**

```csharp
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarketNest.Web.Pages.Account.Disputes;

public partial class DetailModel(IAppLogger<DetailModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true)] public Guid DisputeId { get; set; }

    public void OnGet()
        => Log.InfoOnGet(logger, DisputeId, HttpContext?.TraceIdentifier ?? "-");

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.AccountDisputesDetailStart, LogLevel.Information,
            "AccountDisputeDetail OnGet Start - DisputeId={DisputeId} CorrelationId={CorrelationId}")]
        public static partial void InfoOnGet(ILogger logger, Guid disputeId, string correlationId);
    }
}
```

- [ ] **Step 7: Build**

```
dotnet build MarketNest.slnx
```

- [ ] **Step 8: Commit**

```bash
git add src/MarketNest.Web/Pages/Account/
git commit -m "refactor(logging): migrate Account pages to [LoggerMessage]"
```

---

### Task 9: Refactor Admin Pages (8 files)

**Files:**
- Modify: `src/MarketNest.Web/Pages/Admin/Dashboard/Index.cshtml.cs`
- Modify: `src/MarketNest.Web/Pages/Admin/Config/Index.cshtml.cs`
- Modify: `src/MarketNest.Web/Pages/Admin/Config/Commission.cshtml.cs`
- Modify: `src/MarketNest.Web/Pages/Admin/Users/Index.cshtml.cs`
- Modify: `src/MarketNest.Web/Pages/Admin/Products/Index.cshtml.cs`
- Modify: `src/MarketNest.Web/Pages/Admin/Storefronts/Index.cshtml.cs`
- Modify: `src/MarketNest.Web/Pages/Admin/Disputes/Index.cshtml.cs`
- Modify: `src/MarketNest.Web/Pages/Admin/Notifications/Index.cshtml.cs`

- [ ] **Step 1: Rewrite Admin/Dashboard/Index.cshtml.cs**

```csharp
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarketNest.Web.Pages.Admin.Dashboard;

public partial class IndexModel(IAppLogger<IndexModel> logger) : PageModel
{
    public void OnGet()
        => Log.InfoOnGet(logger, HttpContext?.TraceIdentifier ?? "-");

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.AdminDashboardStart, LogLevel.Information,
            "AdminDashboard OnGet Start - CorrelationId={CorrelationId}")]
        public static partial void InfoOnGet(ILogger logger, string correlationId);
    }
}
```

- [ ] **Step 2: Rewrite Admin/Config/Index.cshtml.cs**

```csharp
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarketNest.Web.Pages.Admin.Config;

public partial class IndexModel(IAppLogger<IndexModel> logger) : PageModel
{
    public void OnGet()
        => Log.InfoOnGet(logger, HttpContext?.TraceIdentifier ?? "-");

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.AdminConfigIndexStart, LogLevel.Information,
            "AdminConfig OnGet Start - CorrelationId={CorrelationId}")]
        public static partial void InfoOnGet(ILogger logger, string correlationId);
    }
}
```

- [ ] **Step 3: Rewrite Admin/Config/Commission.cshtml.cs**

```csharp
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarketNest.Web.Pages.Admin.Config;

public partial class CommissionModel(IAppLogger<CommissionModel> logger) : PageModel
{
    public void OnGet()
        => Log.InfoOnGet(logger, HttpContext?.TraceIdentifier ?? "-");

    public void OnPost()
        => Log.InfoOnPost(logger, HttpContext?.TraceIdentifier ?? "-");

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.AdminConfigCommissionStart, LogLevel.Information,
            "AdminCommission OnGet Start - CorrelationId={CorrelationId}")]
        public static partial void InfoOnGet(ILogger logger, string correlationId);

        [LoggerMessage((int)LogEventId.AdminConfigCommissionStart + 1, LogLevel.Information,
            "AdminCommission OnPost Start - CorrelationId={CorrelationId}")]
        public static partial void InfoOnPost(ILogger logger, string correlationId);
    }
}
```

- [ ] **Step 4: Rewrite remaining 5 Admin pages**

Apply the same single-OnGet pattern to each file with these EventIds:

| File | Class | EventId |
|------|-------|---------|
| `Admin/Users/Index.cshtml.cs` | `IndexModel` (ns: `Admin.Users`) | `AdminUsersIndexStart` |
| `Admin/Products/Index.cshtml.cs` | `IndexModel` (ns: `Admin.Products`) | `AdminProductsIndexStart` |
| `Admin/Storefronts/Index.cshtml.cs` | `IndexModel` (ns: `Admin.Storefronts`) | `AdminStorefrontsIndexStart` |
| `Admin/Disputes/Index.cshtml.cs` | `IndexModel` (ns: `Admin.Disputes`) | `AdminDisputesIndexStart` |
| `Admin/Notifications/Index.cshtml.cs` | `IndexModel` (ns: `Admin.Notifications`) | `AdminNotificationsIndexStart` |

Each file follows this template (replace `AdminXxx` with the table values above):

```csharp
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarketNest.Web.Pages.Admin.Users; // adjust per file

public partial class IndexModel(IAppLogger<IndexModel> logger) : PageModel
{
    public void OnGet()
        => Log.InfoOnGet(logger, HttpContext?.TraceIdentifier ?? "-");

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.AdminUsersIndexStart, LogLevel.Information,
            "AdminUsers OnGet Start - CorrelationId={CorrelationId}")] // adjust description per file
        public static partial void InfoOnGet(ILogger logger, string correlationId);
    }
}
```

- [ ] **Step 5: Build**

```
dotnet build MarketNest.slnx
```

- [ ] **Step 6: Commit**

```bash
git add src/MarketNest.Web/Pages/Admin/
git commit -m "refactor(logging): migrate Admin pages to [LoggerMessage]"
```

---

### Task 10: Refactor Cart, Checkout, Error, and Index Pages

**Files:**
- Modify: `src/MarketNest.Web/Pages/Cart/Index.cshtml.cs`
- Modify: `src/MarketNest.Web/Pages/Checkout/Index.cshtml.cs`
- Modify: `src/MarketNest.Web/Pages/Error.cshtml.cs`
- Modify: `src/MarketNest.Web/Pages/Index.cshtml.cs`

- [ ] **Step 1: Rewrite Cart/Index.cshtml.cs**

```csharp
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarketNest.Web.Pages.Cart;

public partial class IndexModel(IAppLogger<IndexModel> logger) : PageModel
{
    public void OnGet()
        => Log.InfoOnGet(logger, HttpContext?.TraceIdentifier ?? "-");

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.CartIndexStart, LogLevel.Information,
            "Cart OnGet Start - CorrelationId={CorrelationId}")]
        public static partial void InfoOnGet(ILogger logger, string correlationId);
    }
}
```

- [ ] **Step 2: Rewrite Checkout/Index.cshtml.cs**

```csharp
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarketNest.Web.Pages.Checkout;

public partial class IndexModel(IAppLogger<IndexModel> logger) : PageModel
{
    public void OnGet()
        => Log.InfoOnGet(logger, HttpContext?.TraceIdentifier ?? "-");

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.CheckoutIndexStart, LogLevel.Information,
            "Checkout OnGet Start - CorrelationId={CorrelationId}")]
        public static partial void InfoOnGet(ILogger logger, string correlationId);
    }
}
```

- [ ] **Step 3: Rewrite Error.cshtml.cs**

```csharp
using System.Diagnostics;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarketNest.Web.Pages;

[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
[IgnoreAntiforgeryToken]
public partial class ErrorModel(IAppLogger<ErrorModel> logger) : PageModel
{
    public string? RequestId { get; set; }
    public string Timestamp { get; set; } = string.Empty;

    public void OnGet()
    {
        RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        Timestamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + " UTC";
        Log.InfoDisplayed(logger, RequestId);
    }

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.GlobalErrorDisplayed, LogLevel.Information,
            "Error page displayed - RequestId={RequestId}")]
        public static partial void InfoDisplayed(ILogger logger, string? requestId);
    }
}
```

- [ ] **Step 4: Rewrite Index.cshtml.cs**

```csharp
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarketNest.Web.Pages;

public partial class IndexModel(IAppLogger<IndexModel> logger) : PageModel
{
    public void OnGet()
        => Log.InfoOnGet(logger, HttpContext?.TraceIdentifier ?? "-");

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.GlobalIndexStart, LogLevel.Information,
            "Index OnGet Start - CorrelationId={CorrelationId}")]
        public static partial void InfoOnGet(ILogger logger, string correlationId);
    }
}
```

- [ ] **Step 5: Build**

```
dotnet build MarketNest.slnx
```

- [ ] **Step 6: Commit**

```bash
git add src/MarketNest.Web/Pages/Cart/Index.cshtml.cs
git add src/MarketNest.Web/Pages/Checkout/Index.cshtml.cs
git add src/MarketNest.Web/Pages/Error.cshtml.cs
git add src/MarketNest.Web/Pages/Index.cshtml.cs
git commit -m "refactor(logging): migrate Cart, Checkout, Error, Index pages to [LoggerMessage]"
```

---

### Task 11: Refactor AuditService

**Files:**
- Modify: `src/MarketNest.Auditing/Infrastructure/AuditService.cs`

- [ ] **Step 1: Rewrite file**

```csharp
using System.Text.Json;
using MarketNest.Auditing.Domain;
using MarketNest.Base.Common;
using MarketNest.Base.Infrastructure;

namespace MarketNest.Auditing.Infrastructure;

/// <summary>
///     Phase 1 implementation: writes audit entries directly to "auditing" schema in shared PostgreSQL.
///     Never throws — audit failures are logged but do not break the main request.
/// </summary>
public partial class AuditService(AuditingDbContext db, IAppLogger<AuditService> logger) : IAuditService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public async Task RecordAsync(AuditEntry entry, CancellationToken ct = default)
    {
        try
        {
            var log = AuditLog.Create(
                entry.EventType,
                entry.ActorId,
                entry.ActorEmail,
                entry.ActorRole,
                entry.EntityType,
                entry.EntityId,
                Serialize(entry.OldValues),
                Serialize(entry.NewValues),
                Serialize(entry.Metadata));

            db.AuditLogs.Add(log);
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            Log.ErrorRecordFailed(logger, entry.EventType, entry.EntityType, entry.EntityId, ex);
        }
    }

    public async Task RecordLoginAsync(LoginEntry entry, CancellationToken ct = default)
    {
        try
        {
            var loginEvent = LoginEvent.Create(
                entry.UserId,
                entry.Email,
                entry.IpAddress,
                entry.UserAgent,
                entry.Success,
                entry.FailureReason);

            db.LoginEvents.Add(loginEvent);
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            Log.ErrorLoginRecordFailed(logger, entry.UserId, entry.Success, ex);
        }
    }

    private static string? Serialize(object? value) =>
        value is null ? null : JsonSerializer.Serialize(value, JsonOptions);

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.AuditSaveError, LogLevel.Error,
            "Failed to record audit log: {EventType} {EntityType}:{EntityId}")]
        public static partial void ErrorRecordFailed(
            ILogger logger, string eventType, string? entityType, Guid? entityId, Exception ex);

        [LoggerMessage((int)LogEventId.AuditSaveError + 1, LogLevel.Error,
            "Failed to record login event: UserId={UserId} Success={Success}")]
        public static partial void ErrorLoginRecordFailed(
            ILogger logger, Guid? userId, bool success, Exception ex);
    }
}
```

Note: `entry.Email` is PII — removed from the log call. Now logging `UserId` and `Success` only.

- [ ] **Step 2: Build**

```
dotnet build MarketNest.slnx
```

- [ ] **Step 3: Commit**

```bash
git add src/MarketNest.Auditing/Infrastructure/AuditService.cs
git commit -m "refactor(logging): migrate AuditService to [LoggerMessage], remove PII from log"
```

---

### Task 12: Refactor AuditBehavior + AuditableInterceptor

**Files:**
- Modify: `src/MarketNest.Auditing/Infrastructure/AuditBehavior.cs`
- Modify: `src/MarketNest.Auditing/Infrastructure/AuditableInterceptor.cs`

- [ ] **Step 1: Rewrite AuditBehavior.cs**

Add `partial` to the class and replace the `logger.Error(...)` call:

```csharp
// Change class declaration:
public partial class AuditBehavior<TRequest, TResponse>(...) : IPipelineBehavior<TRequest, TResponse>

// Replace call site in catch block:
// BEFORE:
logger.Error(ex, "Failed to record audit for {RequestType}", typeof(TRequest).Name);

// AFTER:
Log.ErrorAuditFailed(logger, typeof(TRequest).Name, ex);
```

Add at end of `AuditBehavior<TRequest, TResponse>` class:

```csharp
    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.AuditBehaviorError, LogLevel.Error,
            "Failed to record audit for {RequestType}")]
        public static partial void ErrorAuditFailed(ILogger logger, string requestType, Exception ex);
    }
```

- [ ] **Step 2: Rewrite AuditableInterceptor.cs**

Add `partial` to the class and replace the `logger.Error(...)` call in `FlushAuditEntriesAsync`:

```csharp
// Change class declaration:
public partial class AuditableInterceptor(IAppLogger<AuditableInterceptor> logger) : SaveChangesInterceptor

// Replace call site in catch block:
// BEFORE:
logger.Error(ex, "Failed to flush audit entries after SaveChanges");

// AFTER:
Log.ErrorFlushFailed(logger, ex);
```

Add at end of `AuditableInterceptor` class (before the nested `PendingAuditEntry` record):

```csharp
    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.AuditInterceptorError, LogLevel.Error,
            "Failed to flush audit entries after SaveChanges")]
        public static partial void ErrorFlushFailed(ILogger logger, Exception ex);
    }
```

- [ ] **Step 3: Build**

```
dotnet build MarketNest.slnx
```

- [ ] **Step 4: Commit**

```bash
git add src/MarketNest.Auditing/Infrastructure/AuditBehavior.cs
git add src/MarketNest.Auditing/Infrastructure/AuditableInterceptor.cs
git commit -m "refactor(logging): migrate AuditBehavior + AuditableInterceptor to [LoggerMessage]"
```

---

### Task 13: Refactor JobRunnerHostedService

**Files:**
- Modify: `src/MarketNest.Web/Hosting/JobRunnerHostedService.cs`

This file uses `ILogger<>` directly (not IAppLogger). Convert to IAppLogger + [LoggerMessage].

- [ ] **Step 1: Rewrite file**

```csharp
using System.Collections.Concurrent;
using MarketNest.Base.Infrastructure;
using MarketNest.Base.Utility;

namespace MarketNest.Web.Hosting;

public partial class JobRunnerHostedService(
    IServiceProvider provider,
    IAppLogger<JobRunnerHostedService> logger,
    IJobRegistry registry) : BackgroundService
{
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(30);
    private readonly ConcurrentDictionary<string, byte> _running = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Log.InfoStarting(logger);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var jobs = registry.GetJobs().Where(j => j.Type == JobType.Timer && j.IsEnabled).ToList();
                foreach (var descriptor in jobs)
                {
                    if (_running.ContainsKey(descriptor.JobKey)) continue;
                    _ = Task.Run(() => RunOneAsync(descriptor, stoppingToken), stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Log.ErrorScheduling(logger, ex);
            }

            await Task.Delay(_pollInterval, stoppingToken);
        }

        Log.InfoStopping(logger);
    }

    private async Task RunOneAsync(JobDescriptor descriptor, CancellationToken stoppingToken)
    {
        if (!_running.TryAdd(descriptor.JobKey, 0)) return;
        try
        {
            using IServiceScope scope = provider.CreateScope();
            var job = scope.ServiceProvider.GetServices<IBackgroundJob>()
                .FirstOrDefault(j => j.Descriptor.JobKey == descriptor.JobKey);

            if (job is null)
            {
                Log.WarnJobNotFound(logger, descriptor.JobKey);
                return;
            }

            var ctx = new JobExecutionContext(Guid.Empty, descriptor.JobKey, null, JobTriggerSource.System, null,
                new Dictionary<string, string>());
            var store = scope.ServiceProvider.GetRequiredService<IJobExecutionStore>();
            var executionId = await store.CreateExecutionAsync(descriptor, ctx, stoppingToken);

            await store.MarkRunningAsync(executionId, DateTime.UtcNow, stoppingToken);
            try
            {
                var runCtx = new JobExecutionContext(executionId, descriptor.JobKey, null,
                    JobTriggerSource.System, null, new Dictionary<string, string>());
                await job.ExecuteAsync(runCtx, stoppingToken);
                await store.MarkSucceededAsync(executionId, DateTime.UtcNow, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                await store.MarkFailedAsync(executionId, DateTime.UtcNow, "Cancelled", null, CancellationToken.None);
            }
            catch (Exception ex)
            {
                Log.ErrorJobFailed(logger, descriptor.JobKey, ex);
                await store.MarkFailedAsync(executionId, DateTime.UtcNow, ex.Message, ex.ToString(),
                    CancellationToken.None);
            }
        }
        finally
        {
            _running.TryRemove(descriptor.JobKey, out _);
        }
    }

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.JobRunnerStarting, LogLevel.Information,
            "JobRunnerHostedService starting")]
        public static partial void InfoStarting(ILogger logger);

        [LoggerMessage((int)LogEventId.JobRunnerStopping, LogLevel.Information,
            "JobRunnerHostedService stopping")]
        public static partial void InfoStopping(ILogger logger);

        [LoggerMessage((int)LogEventId.JobRunnerJobFailed - 1, LogLevel.Warning,
            "No IBackgroundJob instance found for {JobKey}")]
        public static partial void WarnJobNotFound(ILogger logger, string jobKey);

        [LoggerMessage((int)LogEventId.JobRunnerJobFailed, LogLevel.Error,
            "Background job {JobKey} failed")]
        public static partial void ErrorJobFailed(ILogger logger, string jobKey, Exception ex);

        [LoggerMessage((int)LogEventId.JobRunnerJobFailed + 1, LogLevel.Error,
            "Error while scheduling background jobs")]
        public static partial void ErrorScheduling(ILogger logger, Exception ex);
    }
}
```

- [ ] **Step 2: Build**

```
dotnet build MarketNest.slnx
```

- [ ] **Step 3: Commit**

```bash
git add src/MarketNest.Web/Hosting/JobRunnerHostedService.cs
git commit -m "refactor(logging): migrate JobRunnerHostedService to [LoggerMessage] + IAppLogger<T>"
```

---

### Task 14: Refactor TestTimerJob

**Files:**
- Modify: `src/MarketNest.Admin/Application/Timer/TestTimer/TestTimerJob.cs`

- [ ] **Step 1: Rewrite file**

```csharp
using MarketNest.Base.Infrastructure;
using MarketNest.Base.Utility;

namespace MarketNest.Admin.Application;

public partial class TestTimerJob(IAppLogger<TestTimerJob> logger) : IBackgroundJob
{
    private const string JobKeyValue = "admin.test.timer";
    private const string ModuleName = "Admin";

    public JobDescriptor Descriptor { get; } = new(
        JobKeyValue,
        "Admin demo timer job",
        ModuleName,
        JobType.Timer,
        null,
        true,
        false,
        0,
        "A demo job that logs a message and completes.");

    public Task ExecuteAsync(JobExecutionContext context, CancellationToken cancellationToken = default)
    {
        Log.InfoExecuted(logger, context.ExecutionId);
        return Task.CompletedTask;
    }

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.TestTimerJobStart, LogLevel.Information,
            "TestTimerJob executed: ExecutionId={ExecutionId}")]
        public static partial void InfoExecuted(ILogger logger, Guid executionId);
    }
}
```

- [ ] **Step 2: Build**

```
dotnet build MarketNest.slnx
```

- [ ] **Step 3: Commit**

```bash
git add src/MarketNest.Admin/Application/Timer/TestTimer/TestTimerJob.cs
git commit -m "refactor(logging): migrate TestTimerJob to [LoggerMessage]"
```

---

### Task 15: Convert Seller/Products Pages (BeginApiScope → [LoggerMessage])

**Files:**
- Modify: `src/MarketNest.Web/Pages/Seller/Products/Create.cshtml.cs`
- Modify: `src/MarketNest.Web/Pages/Seller/Products/Edit.cshtml.cs`
- Modify: `src/MarketNest.Web/Pages/Seller/Products/Index.cshtml.cs`
- Modify: `src/MarketNest.Web/Pages/Seller/Products/Variants.cshtml.cs`

These pages use `logger.BeginApiScope(...)`. Replace with explicit [LoggerMessage] delegates.

- [ ] **Step 1: Rewrite Create.cshtml.cs**

```csharp
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarketNest.Web.Pages.Seller.Products;

public partial class CreateModel(IAppLogger<CreateModel> logger) : PageModel
{
    public void OnGet()
        => Log.InfoOnGet(logger, HttpContext?.TraceIdentifier ?? "-");

    public void OnPost()
        => Log.InfoOnPost(logger, HttpContext?.TraceIdentifier ?? "-");

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.SellerProductsCreateStart, LogLevel.Information,
            "SellerProductCreate OnGet Start - CorrelationId={CorrelationId}")]
        public static partial void InfoOnGet(ILogger logger, string correlationId);

        [LoggerMessage((int)LogEventId.SellerProductsCreateStart + 1, LogLevel.Information,
            "SellerProductCreate OnPost Start - CorrelationId={CorrelationId}")]
        public static partial void InfoOnPost(ILogger logger, string correlationId);
    }
}
```

- [ ] **Step 2: Rewrite Edit.cshtml.cs**

```csharp
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarketNest.Web.Pages.Seller.Products;

public partial class EditModel(IAppLogger<EditModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true)] public Guid ProductId { get; set; }

    public void OnGet()
        => Log.InfoOnGet(logger, ProductId, HttpContext?.TraceIdentifier ?? "-");

    public void OnPost()
        => Log.InfoOnPost(logger, ProductId, HttpContext?.TraceIdentifier ?? "-");

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.SellerProductsEditStart, LogLevel.Information,
            "SellerProductEdit OnGet Start - ProductId={ProductId} CorrelationId={CorrelationId}")]
        public static partial void InfoOnGet(ILogger logger, Guid productId, string correlationId);

        [LoggerMessage((int)LogEventId.SellerProductsEditStart + 1, LogLevel.Information,
            "SellerProductEdit OnPost Start - ProductId={ProductId} CorrelationId={CorrelationId}")]
        public static partial void InfoOnPost(ILogger logger, Guid productId, string correlationId);
    }
}
```

- [ ] **Step 3: Rewrite Index.cshtml.cs**

```csharp
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarketNest.Web.Pages.Seller.Products;

public partial class IndexModel(IAppLogger<IndexModel> logger) : PageModel
{
    public void OnGet()
        => Log.InfoOnGet(logger, HttpContext?.TraceIdentifier ?? "-");

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.SellerProductsIndexStart, LogLevel.Information,
            "SellerProductIndex OnGet Start - CorrelationId={CorrelationId}")]
        public static partial void InfoOnGet(ILogger logger, string correlationId);
    }
}
```

- [ ] **Step 4: Rewrite Variants.cshtml.cs**

```csharp
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarketNest.Web.Pages.Seller.Products;

public partial class VariantsModel(IAppLogger<VariantsModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true)] public Guid ProductId { get; set; }

    public void OnGet()
        => Log.InfoOnGet(logger, ProductId, HttpContext?.TraceIdentifier ?? "-");

    public void OnPost()
        => Log.InfoOnPost(logger, ProductId, HttpContext?.TraceIdentifier ?? "-");

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.SellerProductsVariantsStart, LogLevel.Information,
            "SellerProductVariants OnGet Start - ProductId={ProductId} CorrelationId={CorrelationId}")]
        public static partial void InfoOnGet(ILogger logger, Guid productId, string correlationId);

        [LoggerMessage((int)LogEventId.SellerProductsVariantsStart + 1, LogLevel.Information,
            "SellerProductVariants OnPost Start - ProductId={ProductId} CorrelationId={CorrelationId}")]
        public static partial void InfoOnPost(ILogger logger, Guid productId, string correlationId);
    }
}
```

- [ ] **Step 5: Build**

```
dotnet build MarketNest.slnx
```

- [ ] **Step 6: Commit**

```bash
git add src/MarketNest.Web/Pages/Seller/Products/
git commit -m "refactor(logging): convert Seller/Products pages from BeginApiScope to [LoggerMessage]"
```

---

### Task 16: Convert Shop + Search Pages

**Files:**
- Modify: `src/MarketNest.Web/Pages/Shop/Index.cshtml.cs`
- Modify: `src/MarketNest.Web/Pages/Shop/Products/Detail.cshtml.cs`
- Modify: `src/MarketNest.Web/Pages/Search/Index.cshtml.cs`

- [ ] **Step 1: Rewrite Shop/Index.cshtml.cs**

```csharp
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarketNest.Web.Pages.Shop;

public partial class IndexModel(IAppLogger<IndexModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true)] public string Slug { get; set; } = default!;

    public void OnGet()
        => Log.InfoOnGet(logger, Slug, HttpContext?.TraceIdentifier ?? "-");

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.ShopIndexStart, LogLevel.Information,
            "ShopIndex OnGet Start - Slug={Slug} CorrelationId={CorrelationId}")]
        public static partial void InfoOnGet(ILogger logger, string slug, string correlationId);
    }
}
```

- [ ] **Step 2: Rewrite Shop/Products/Detail.cshtml.cs**

```csharp
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarketNest.Web.Pages.Shop.Products;

public partial class DetailModel(IAppLogger<DetailModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true)] public string Slug { get; set; } = default!;
    [BindProperty(SupportsGet = true)] public Guid ProductId { get; set; }

    public void OnGet()
        => Log.InfoOnGet(logger, ProductId, HttpContext?.TraceIdentifier ?? "-");

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.ShopProductDetailStart, LogLevel.Information,
            "ShopProductDetail OnGet Start - ProductId={ProductId} CorrelationId={CorrelationId}")]
        public static partial void InfoOnGet(ILogger logger, Guid productId, string correlationId);
    }
}
```

- [ ] **Step 3: Rewrite Search/Index.cshtml.cs**

```csharp
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarketNest.Web.Pages.Search;

public partial class IndexModel(IAppLogger<IndexModel> logger) : PageModel
{
    public void OnGet()
        => Log.InfoOnGet(logger, HttpContext?.TraceIdentifier ?? "-");

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.SearchIndexStart, LogLevel.Information,
            "Search OnGet Start - CorrelationId={CorrelationId}")]
        public static partial void InfoOnGet(ILogger logger, string correlationId);
    }
}
```

- [ ] **Step 4: Build + Commit**

```
dotnet build MarketNest.slnx
```

```bash
git add src/MarketNest.Web/Pages/Shop/ src/MarketNest.Web/Pages/Search/
git commit -m "refactor(logging): convert Shop + Search pages from BeginApiScope to [LoggerMessage]"
```

---

### Task 17: Convert Seller/Orders + Orders/Confirmation Pages

**Files:**
- Modify: `src/MarketNest.Web/Pages/Seller/Orders/Index.cshtml.cs`
- Modify: `src/MarketNest.Web/Pages/Seller/Orders/Detail.cshtml.cs`
- Modify: `src/MarketNest.Web/Pages/Orders/Confirmation.cshtml.cs`

- [ ] **Step 1: Rewrite Seller/Orders/Index.cshtml.cs**

```csharp
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarketNest.Web.Pages.Seller.Orders;

public partial class IndexModel(IAppLogger<IndexModel> logger) : PageModel
{
    public void OnGet()
        => Log.InfoOnGet(logger, HttpContext?.TraceIdentifier ?? "-");

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.SellerOrdersIndexStart, LogLevel.Information,
            "SellerOrderIndex OnGet Start - CorrelationId={CorrelationId}")]
        public static partial void InfoOnGet(ILogger logger, string correlationId);
    }
}
```

- [ ] **Step 2: Rewrite Seller/Orders/Detail.cshtml.cs**

```csharp
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarketNest.Web.Pages.Seller.Orders;

public partial class DetailModel(IAppLogger<DetailModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true)] public Guid OrderId { get; set; }

    public void OnGet()
        => Log.InfoOnGet(logger, OrderId, HttpContext?.TraceIdentifier ?? "-");

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.SellerOrdersDetailStart, LogLevel.Information,
            "SellerOrderDetail OnGet Start - OrderId={OrderId} CorrelationId={CorrelationId}")]
        public static partial void InfoOnGet(ILogger logger, Guid orderId, string correlationId);
    }
}
```

- [ ] **Step 3: Rewrite Orders/Confirmation.cshtml.cs**

```csharp
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarketNest.Web.Pages.Orders;

public partial class ConfirmationModel(IAppLogger<ConfirmationModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true)] public Guid OrderId { get; set; }

    public void OnGet()
        => Log.InfoOnGet(logger, OrderId, HttpContext?.TraceIdentifier ?? "-");

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.OrdersConfirmationStart, LogLevel.Information,
            "OrderConfirmation OnGet Start - OrderId={OrderId} CorrelationId={CorrelationId}")]
        public static partial void InfoOnGet(ILogger logger, Guid orderId, string correlationId);
    }
}
```

- [ ] **Step 4: Build + Commit**

```
dotnet build MarketNest.slnx
```

```bash
git add src/MarketNest.Web/Pages/Seller/Orders/ src/MarketNest.Web/Pages/Orders/
git commit -m "refactor(logging): convert Seller/Orders + Orders/Confirmation to [LoggerMessage]"
```

---

### Task 18: Convert Seller Dashboard/Storefront/Reviews/Disputes/Payouts + NotFound

**Files:**
- Modify: `src/MarketNest.Web/Pages/Seller/Dashboard/Index.cshtml.cs`
- Modify: `src/MarketNest.Web/Pages/Seller/Storefront/Index.cshtml.cs`
- Modify: `src/MarketNest.Web/Pages/Seller/Reviews/Index.cshtml.cs`
- Modify: `src/MarketNest.Web/Pages/Seller/Disputes/Index.cshtml.cs`
- Modify: `src/MarketNest.Web/Pages/Seller/Payouts/Index.cshtml.cs`
- Modify: `src/MarketNest.Web/Pages/NotFound.cshtml.cs`

- [ ] **Step 1: Rewrite Seller/Dashboard/Index.cshtml.cs**

```csharp
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarketNest.Web.Pages.Seller.Dashboard;

public partial class IndexModel(IAppLogger<IndexModel> logger) : PageModel
{
    public void OnGet()
        => Log.InfoOnGet(logger, HttpContext?.TraceIdentifier ?? "-");

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.SellerDashboardStart, LogLevel.Information,
            "SellerDashboard OnGet Start - CorrelationId={CorrelationId}")]
        public static partial void InfoOnGet(ILogger logger, string correlationId);
    }
}
```

- [ ] **Step 2: Rewrite Seller/Storefront/Index.cshtml.cs**

```csharp
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarketNest.Web.Pages.Seller.Storefront;

public partial class IndexModel(IAppLogger<IndexModel> logger) : PageModel
{
    public void OnGet()
        => Log.InfoOnGet(logger, HttpContext?.TraceIdentifier ?? "-");

    public void OnPost()
        => Log.InfoOnPost(logger, HttpContext?.TraceIdentifier ?? "-");

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.SellerStorefrontStart, LogLevel.Information,
            "SellerStorefront OnGet Start - CorrelationId={CorrelationId}")]
        public static partial void InfoOnGet(ILogger logger, string correlationId);

        [LoggerMessage((int)LogEventId.SellerStorefrontStart + 1, LogLevel.Information,
            "SellerStorefront OnPost Start - CorrelationId={CorrelationId}")]
        public static partial void InfoOnPost(ILogger logger, string correlationId);
    }
}
```

- [ ] **Step 3: Rewrite Seller/Reviews/Index.cshtml.cs**

```csharp
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarketNest.Web.Pages.Seller.Reviews;

public partial class IndexModel(IAppLogger<IndexModel> logger) : PageModel
{
    public void OnGet()
        => Log.InfoOnGet(logger, HttpContext?.TraceIdentifier ?? "-");

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.SellerReviewsIndexStart, LogLevel.Information,
            "SellerReviews OnGet Start - CorrelationId={CorrelationId}")]
        public static partial void InfoOnGet(ILogger logger, string correlationId);
    }
}
```

- [ ] **Step 4: Rewrite Seller/Disputes/Index.cshtml.cs**

```csharp
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarketNest.Web.Pages.Seller.Disputes;

public partial class IndexModel(IAppLogger<IndexModel> logger) : PageModel
{
    public void OnGet()
        => Log.InfoOnGet(logger, HttpContext?.TraceIdentifier ?? "-");

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.SellerDisputesIndexStart, LogLevel.Information,
            "SellerDisputes OnGet Start - CorrelationId={CorrelationId}")]
        public static partial void InfoOnGet(ILogger logger, string correlationId);
    }
}
```

- [ ] **Step 5: Rewrite Seller/Payouts/Index.cshtml.cs**

```csharp
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarketNest.Web.Pages.Seller.Payouts;

public partial class IndexModel(IAppLogger<IndexModel> logger) : PageModel
{
    public void OnGet()
        => Log.InfoOnGet(logger, HttpContext?.TraceIdentifier ?? "-");

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.SellerPayoutsIndexStart, LogLevel.Information,
            "SellerPayouts OnGet Start - CorrelationId={CorrelationId}")]
        public static partial void InfoOnGet(ILogger logger, string correlationId);
    }
}
```

- [ ] **Step 6: Rewrite NotFound.cshtml.cs**

```csharp
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarketNest.Web.Pages;

[IgnoreAntiforgeryToken]
public partial class NotFoundModel(IAppLogger<NotFoundModel> logger) : PageModel
{
    public void OnGet()
    {
        Log.InfoDisplayed(logger, HttpContext?.TraceIdentifier ?? "-");
        Response.StatusCode = StatusCodes.Status404NotFound;
    }

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.GlobalNotFoundDisplayed, LogLevel.Information,
            "NotFound page displayed - CorrelationId={CorrelationId}")]
        public static partial void InfoDisplayed(ILogger logger, string correlationId);
    }
}
```

- [ ] **Step 7: Build + Commit**

```
dotnet build MarketNest.slnx
```

```bash
git add src/MarketNest.Web/Pages/Seller/Dashboard/
git add src/MarketNest.Web/Pages/Seller/Storefront/
git add src/MarketNest.Web/Pages/Seller/Reviews/
git add src/MarketNest.Web/Pages/Seller/Disputes/
git add src/MarketNest.Web/Pages/Seller/Payouts/
git add src/MarketNest.Web/Pages/NotFound.cshtml.cs
git commit -m "refactor(logging): convert Seller + NotFound pages from BeginApiScope to [LoggerMessage]"
```

---

### Task 19: Add logging to Admin handlers (currently have zero logging)

**Files:**
- Modify: `src/MarketNest.Admin/Application/Modules/Test/CommandHandlers/CreateTestHandler.cs`
- Modify: `src/MarketNest.Admin/Application/Modules/Test/CommandHandlers/UpdateTestHandler.cs`
- Modify: `src/MarketNest.Admin/Application/Modules/Test/QueryHandlers/GetTestByIdHandler.cs`
- Modify: `src/MarketNest.Admin/Application/Modules/Test/QueryHandlers/GetTestsPagedHandler.cs`

These handlers currently inject no logger. Add `IAppLogger<T>` via primary constructor + [LoggerMessage] delegates.

- [ ] **Step 1: Rewrite CreateTestHandler.cs**

```csharp
using MarketNest.Admin.Domain;
using MarketNest.Base.Common;
using MarketNest.Base.Infrastructure;

namespace MarketNest.Admin.Application;

public partial class CreateTestHandler(
    ITestRepository repository,
    IAppLogger<CreateTestHandler> logger) : ICommandHandler<CreateTestCommand, Guid>
{
    public async Task<Result<Guid, Error>> Handle(CreateTestCommand request, CancellationToken cancellationToken)
    {
        Log.InfoStart(logger, request.Name);

        var id = Guid.NewGuid();
        var entity = new TestEntity(id, request.Name, request.Value);

        repository.Add(entity);

        if (request.SubTitles is not null)
            foreach (string title in request.SubTitles)
                repository.AddSubEntity(new TestSubEntity(Guid.NewGuid(), id, title));

        await repository.SaveChangesAsync(cancellationToken);

        Log.InfoSuccess(logger, id);
        return Result<Guid, Error>.Success(id);
    }

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.AdminCreateTestStart, LogLevel.Information,
            "CreateTest Start - Name={Name}")]
        public static partial void InfoStart(ILogger logger, string name);

        [LoggerMessage((int)LogEventId.AdminCreateTestSuccess, LogLevel.Information,
            "CreateTest Success - Id={Id}")]
        public static partial void InfoSuccess(ILogger logger, Guid id);
    }
}
```

- [ ] **Step 2: Rewrite UpdateTestHandler.cs**

```csharp
using MarketNest.Admin.Domain;
using MarketNest.Base.Infrastructure;
using MediatR;

#pragma warning disable IDE0130
namespace MarketNest.Admin.Application;
#pragma warning restore IDE0130

public partial class UpdateTestHandler(
    ITestRepository repository,
    IAppLogger<UpdateTestHandler> logger) : ICommandHandler<UpdateTestCommand, Unit>
{
    public async Task<Result<Unit, Error>> Handle(UpdateTestCommand request, CancellationToken cancellationToken)
    {
        Log.InfoStart(logger, request.Id);

        TestEntity? entity = await repository.GetByKeyAsync(request.Id, cancellationToken);
        if (entity is null)
        {
            Log.WarnNotFound(logger, request.Id);
            return Result<Unit, Error>.Failure(
                Error.NotFound(nameof(TestEntity), request.Id.ToString()));
        }

        entity.Update(request.Name, request.Value);
        repository.RemoveSubEntities(entity.SubEntities.ToList());

        if (request.SubTitles is not null)
            foreach (string title in request.SubTitles)
                repository.AddSubEntity(new TestSubEntity(Guid.NewGuid(), request.Id, title));

        await repository.SaveChangesAsync(cancellationToken);

        Log.InfoSuccess(logger, request.Id);
        return Result<Unit, Error>.Success(Unit.Value);
    }

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.AdminUpdateTestStart, LogLevel.Information,
            "UpdateTest Start - Id={Id}")]
        public static partial void InfoStart(ILogger logger, Guid id);

        [LoggerMessage((int)LogEventId.AdminUpdateTestSuccess, LogLevel.Information,
            "UpdateTest Success - Id={Id}")]
        public static partial void InfoSuccess(ILogger logger, Guid id);

        [LoggerMessage((int)LogEventId.AdminUpdateTestError, LogLevel.Warning,
            "UpdateTest NotFound - Id={Id}")]
        public static partial void WarnNotFound(ILogger logger, Guid id);
    }
}
```

- [ ] **Step 3: Rewrite GetTestByIdHandler.cs**

```csharp
using MarketNest.Admin.Domain;
using MarketNest.Base.Common;
using MarketNest.Base.Infrastructure;

namespace MarketNest.Admin.Application;

public partial class GetTestByIdHandler(
    ITestQuery query,
    IAppLogger<GetTestByIdHandler> logger) : IQueryHandler<GetTestByIdQuery, TestDto?>
{
    public async Task<TestDto?> Handle(GetTestByIdQuery request, CancellationToken cancellationToken)
    {
        Log.InfoStart(logger, request.Id);

        TestEntity? entity = await query.GetByKeyAsync(request.Id, cancellationToken);
        if (entity is null)
        {
            Log.WarnNotFound(logger, request.Id);
            return null;
        }

        return new TestDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Value = entity.Value,
            SubEntities = entity.SubEntities.Select(s => new TestSubDto(s.Id, s.Title)).ToList()
        };
    }

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.AdminGetTestByIdStart, LogLevel.Information,
            "GetTestById Start - Id={Id}")]
        public static partial void InfoStart(ILogger logger, Guid id);

        [LoggerMessage((int)LogEventId.AdminGetTestByIdNotFound, LogLevel.Warning,
            "GetTestById NotFound - Id={Id}")]
        public static partial void WarnNotFound(ILogger logger, Guid id);
    }
}
```

- [ ] **Step 4: Rewrite GetTestsPagedHandler.cs**

```csharp
using MarketNest.Base.Common;
using MarketNest.Base.Infrastructure;

namespace MarketNest.Admin.Application;

public partial class GetTestsPagedHandler(
    IGetTestsPagedQuery query,
    IAppLogger<GetTestsPagedHandler> logger) : IQueryHandler<GetTestsPagedQuery, PagedResult<TestDto>>
{
    public Task<PagedResult<TestDto>> Handle(GetTestsPagedQuery request, CancellationToken cancellationToken)
    {
        Log.InfoStart(logger, request.Page, request.PageSize);
        return query.ExecuteAsync(request, cancellationToken);
    }

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.AdminGetTestsPagedStart, LogLevel.Information,
            "GetTestsPaged Start - Page={Page} PageSize={PageSize}")]
        public static partial void InfoStart(ILogger logger, int page, int pageSize);
    }
}
```

Note: `GetTestsPagedQuery` may not have `Page`/`PageSize` properties — check the actual query record and adjust the delegate signature accordingly. If it has no page parameters, use `InfoStart(ILogger logger)` with no extra params.

- [ ] **Step 5: Build**

```
dotnet build MarketNest.slnx
```

If `GetTestsPagedQuery` doesn't have `Page`/`PageSize`: simplify `InfoStart` to take no extra params and change the template to `"GetTestsPaged Start"`.

- [ ] **Step 6: Commit**

```bash
git add src/MarketNest.Admin/Application/Modules/Test/
git commit -m "feat(logging): add [LoggerMessage] logging to Admin test handlers"
```

---

## Phase 2 — Cleanup (after all Phase 1 tasks complete)

### Task 20: Remove ApiLoggingScope + ApiLoggingExtensions

**Files:**
- Modify: `src/Base/MarketNest.Base.Infrastructure/Logging/ApiLoggingExtensions.cs`

All pages that used `BeginApiScope` have been converted in Tasks 15–18. This file is now dead code.

- [ ] **Step 1: Verify no remaining BeginApiScope usages**

```
dotnet build MarketNest.slnx
```

Then search for any remaining references:

```bash
grep -r "BeginApiScope" src/ --include="*.cs"
```

Expected: no results. If any exist, convert them first using the pattern from Task 15 before proceeding.

- [ ] **Step 2: Delete the file**

Delete `src/Base/MarketNest.Base.Infrastructure/Logging/ApiLoggingExtensions.cs` entirely.

- [ ] **Step 3: Build**

```
dotnet build MarketNest.slnx
```

Expected: Build succeeded. If any compile errors for `ApiLoggingScope` or `BeginApiScope`, those files still need conversion — do not skip.

- [ ] **Step 4: Commit**

```bash
git rm src/Base/MarketNest.Base.Infrastructure/Logging/ApiLoggingExtensions.cs
git commit -m "refactor(logging): remove ApiLoggingScope + ApiLoggingExtensions (replaced by [LoggerMessage])"
```

---

### Task 21: Strip IAppLogger\<T\> methods + remove pragma

**Files:**
- Modify: `src/Base/MarketNest.Base.Infrastructure/Logging/IAppLogger.cs`
- Modify: `src/Base/MarketNest.Base.Infrastructure/Logging/AppLogger.cs`

- [ ] **Step 1: Verify no remaining .Info/.Warn/.Error/.Debug/.Trace/.Critical call sites**

```bash
grep -rn "\.\(Info\|Warn\|Error\|Debug\|Trace\|Critical\)(" src/ --include="*.cs" | grep -v "AppLogger.cs" | grep -v "IAppLogger.cs"
```

Expected: no results. If any exist, those files still need migration — do not strip the interface.

- [ ] **Step 2: Rewrite IAppLogger.cs to marker interface**

```csharp

namespace MarketNest.Base.Infrastructure;

/// <summary>
///     Marker interface for <see cref="ILogger" /> used across all MarketNest modules.
///     Enables open-generic DI registration: services.AddSingleton(typeof(IAppLogger&lt;&gt;), typeof(AppLogger&lt;&gt;)).
///     All logging goes through [LoggerMessage] delegates — see <see cref="LogEventId" /> for EventId registry.
/// </summary>
public interface IAppLogger<T> : ILogger { }
```

- [ ] **Step 3: Rewrite AppLogger.cs — remove pragma, remove shorthand methods**

```csharp

namespace MarketNest.Base.Infrastructure;

/// <summary>
///     <see cref="ILogger&lt;T&gt;" /> wrapper implementing <see cref="IAppLogger&lt;T&gt;" />.
///     All three explicit ILogger methods delegate to inner using the core Log method
///     (not extension methods) — CA1848 does not apply to the core method.
/// </summary>
public sealed class AppLogger<T>(ILogger<T> inner) : IAppLogger<T>
{
    void ILogger.Log<TState>(LogLevel logLevel, EventId eventId, TState state,
        Exception? exception, Func<TState, Exception?, string> formatter)
        => inner.Log(logLevel, eventId, state, exception, formatter);

    bool ILogger.IsEnabled(LogLevel logLevel) => inner.IsEnabled(logLevel);

    IDisposable? ILogger.BeginScope<TState>(TState state) => inner.BeginScope(state);
}
```

- [ ] **Step 4: Build**

```
dotnet build MarketNest.slnx
```

Expected: Build succeeded, **zero CA1848 / CA2254 warnings**, zero pragmas suppressing them.

- [ ] **Step 5: Run all tests**

```
dotnet test MarketNest.slnx
```

Expected: All tests pass.

- [ ] **Step 6: Commit**

```bash
git add src/Base/MarketNest.Base.Infrastructure/Logging/IAppLogger.cs
git add src/Base/MarketNest.Base.Infrastructure/Logging/AppLogger.cs
git commit -m "refactor(logging): strip IAppLogger<T> shorthand methods, remove CA1848/CA2254 pragma

IAppLogger<T> is now a pure marker interface extending ILogger.
AppLogger<T> retains only 3 explicit ILogger members via inner.Log (core method).
All production logging now routes through [LoggerMessage] delegates."
```

---

### Task 22: Final verification

- [ ] **Step 1: Full solution build**

```
dotnet build MarketNest.slnx --configuration Release
```

Expected: Build succeeded, 0 errors, 0 CA1848/CA2254 warnings.

- [ ] **Step 2: Full test suite**

```
dotnet test MarketNest.slnx
```

Expected: All tests pass.

- [ ] **Step 3: Verify no pragma suppressions for CA1848/CA2254**

```bash
grep -rn "CA1848\|CA2254" src/ --include="*.cs"
```

Expected: no results.

- [ ] **Step 4: Verify EventId uniqueness within each module block**

Check that no two [LoggerMessage] attributes in the same module's EventId range share the same integer value. Build warnings will catch duplicate EventIds automatically since the source generator enforces this.

- [ ] **Step 5: Update issues.md**

Update `docs/project_notes/issues.md` — change the LoggerMessage refactor entry status from `In Progress` to `Completed`.

- [ ] **Step 6: Final commit**

```bash
git add docs/project_notes/issues.md
git commit -m "chore: mark LoggerMessage refactor as completed in issues.md"
```
