using Microsoft.EntityFrameworkCore;

namespace MarketNest.Core.Common.Persistence;

/// <summary>
/// Marker interface for module-level DbContexts.
/// Each module registers its DbContext as <see cref="IModuleDbContext"/> so
/// <c>DatabaseInitializer</c> can iterate all contexts for migration &amp; seeding.
/// </summary>
public interface IModuleDbContext
{
    /// <summary>
    /// PostgreSQL schema name for this module (e.g., "identity", "catalog").
    /// Used as a logical grouping — each module owns its own schema.
    /// </summary>
    string SchemaName { get; }

    /// <summary>
    /// A friendly name for logging &amp; tracking (typically the module name).
    /// Used as the key in <c>__auto_migration_history</c>.
    /// </summary>
    string ContextName { get; }

    /// <summary>The underlying EF Core DbContext.</summary>
    DbContext AsDbContext();
}

