using MarketNest.Auditing.Domain;
using MarketNest.Base.Common;
using MarketNest.Base.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace MarketNest.Auditing.Infrastructure;

/// <summary>
///     DbContext for the "auditing" schema. Manages audit_logs and login_events tables.
///     Separate from other module DbContexts to avoid audit writes blocking app transactions.
/// </summary>
public class AuditingDbContext(DbContextOptions<AuditingDbContext> options)
    : DbContext(options), IModuleDbContext
{
    public const string Schema = TableConstants.Schema.Auditing;

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<LoginEvent> LoginEvents => Set<LoginEvent>();

    public string SchemaName => Schema;
    public string ContextName => "Auditing";
    public DbContext AsDbContext() => this;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AuditingDbContext).Assembly);
        modelBuilder.ApplyDddPropertyAccessConventions();
    }
}
