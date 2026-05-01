using MarketNest.Auditing.Domain;
using MarketNest.Base.Common;
using MarketNest.Base.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace MarketNest.Auditing.Infrastructure;

/// <summary>
///     Read-only DbContext for the Auditing module.
///     Configured with <c>QueryTrackingBehavior.NoTracking</c> — no migrations.
///     Used exclusively by <see cref="BaseQuery{TEntity,TKey}"/> subclasses.
/// </summary>
public class AuditingReadDbContext(DbContextOptions<AuditingReadDbContext> options) : DbContext(options)
{
    public DbSet<AuditLog> AuditLogs { get; set; } = null!;
    public DbSet<LoginEvent> LoginEvents { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(TableConstants.Schema.Auditing);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AuditingDbContext).Assembly);
        modelBuilder.ApplyDddPropertyAccessConventions();
        base.OnModelCreating(modelBuilder);
    }
}

