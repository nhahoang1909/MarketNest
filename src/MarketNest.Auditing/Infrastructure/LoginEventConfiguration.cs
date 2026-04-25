using MarketNest.Auditing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MarketNest.Auditing.Infrastructure;

public class LoginEventConfiguration : IEntityTypeConfiguration<LoginEvent>
{
    public void Configure(EntityTypeBuilder<LoginEvent> builder)
    {
        builder.ToTable("login_events");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Email).HasMaxLength(255).IsRequired();
        builder.Property(x => x.IpAddress).HasMaxLength(45);
        builder.Property(x => x.UserAgent).HasMaxLength(512);
        builder.Property(x => x.FailureReason).HasMaxLength(128);
        builder.Property(x => x.OccurredAt).IsRequired();

        builder.HasIndex(x => new { x.UserId, x.OccurredAt })
            .IsDescending(false, true);
        builder.HasIndex(x => new { x.IpAddress, x.OccurredAt })
            .IsDescending(false, true);
    }
}

