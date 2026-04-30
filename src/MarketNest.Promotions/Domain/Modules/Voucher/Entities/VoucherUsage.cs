namespace MarketNest.Promotions.Domain;

public class VoucherUsage : Entity<Guid>
{
#pragma warning disable CS8618 // Non-nullable field — EF Core uses this constructor
    protected VoucherUsage() { }
#pragma warning restore CS8618

    public Guid VoucherId { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid UserId { get; private set; }
    public Money DiscountApplied { get; private set; }
    public DateTimeOffset UsedAt { get; private set; }

    internal static VoucherUsage Create(Guid voucherId, Guid orderId, Guid userId, Money discountApplied) =>
        new()
        {
            Id = Guid.NewGuid(),
            VoucherId = voucherId,
            OrderId = orderId,
            UserId = userId,
            DiscountApplied = discountApplied,
            UsedAt = DateTimeOffset.UtcNow
        };
}
