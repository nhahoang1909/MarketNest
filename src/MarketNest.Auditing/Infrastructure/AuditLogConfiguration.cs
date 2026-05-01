using MarketNest.Auditing.Domain;
using MarketNest.Base.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MarketNest.Auditing.Infrastructure;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable(TableConstants.AuditingTable.AuditLog, TableConstants.Schema.Auditing);

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.EventType).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ActorEmail).HasMaxLength(255);
        builder.Property(x => x.ActorRole).HasMaxLength(32);
        builder.Property(x => x.EntityType).HasMaxLength(64);
        builder.Property(x => x.OldValues).HasColumnType("jsonb");
        builder.Property(x => x.NewValues).HasColumnType("jsonb");
        builder.Property(x => x.Metadata).HasColumnType("jsonb");
        builder.Property(x => x.OccurredAt).IsRequired();

        builder.HasIndex(x => new { x.ActorId, x.OccurredAt })
            .IsDescending(false, true);
        builder.HasIndex(x => new { x.EntityType, x.EntityId, x.OccurredAt })
            .IsDescending(false, false, true);
        builder.HasIndex(x => new { x.EventType, x.OccurredAt })
            .IsDescending(false, true);
        builder.HasIndex(x => x.OccurredAt)
            .IsDescending();
    }
}
