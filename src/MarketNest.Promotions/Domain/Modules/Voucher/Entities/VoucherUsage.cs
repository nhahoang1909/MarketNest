namespace MarketNest.Promotions.Domain;

public class VoucherUsage : Entity<Guid>
{
    protected VoucherUsage() { }

    public Guid VoucherId { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid UserId { get; private set; }
    public Money DiscountApplied { get; private set; } = null!;
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
