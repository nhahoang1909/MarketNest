namespace MarketNest.Promotions.Domain;

public class Voucher : AggregateRoot<Guid>
{
    protected Voucher() { }

    public VoucherCode Code { get; private set; } = null!;
    public VoucherScope Scope { get; private set; }
    public Guid? StoreId { get; private set; }
    public Guid CreatedByUserId { get; private set; }

    public VoucherDiscountType DiscountType { get; private set; }
    public VoucherApplyFor ApplyFor { get; private set; }
    public decimal DiscountValue { get; private set; }
    public Money? MaxDiscountCap { get; private set; }

    public Money? MinOrderValue { get; private set; }
    public DateTime EffectiveDate { get; private set; }
    public DateTime ExpiryDate { get; private set; }

    public int? UsageLimit { get; private set; }
    public int? UsageLimitPerUser { get; private set; }
    public int UsageCount { get; private set; }

    public VoucherStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private readonly List<VoucherUsage> _usages = [];
    public IReadOnlyList<VoucherUsage> Usages => _usages.AsReadOnly();

    public static Voucher Create(
        VoucherCode code,
        VoucherScope scope,
        Guid? storeId,
        Guid createdByUserId,
        VoucherDiscountType discountType,
        VoucherApplyFor applyFor,
        decimal discountValue,
        Money? maxDiscountCap,
        Money? minOrderValue,
        DateTime effectiveDate,
        DateTime expiryDate,
        int? usageLimit,
        int? usageLimitPerUser)
    {
        var voucher = new Voucher
        {
            Id = Guid.NewGuid(),
            Code = code,
            Scope = scope,
            StoreId = storeId,
            CreatedByUserId = createdByUserId,
            DiscountType = discountType,
            ApplyFor = applyFor,
            DiscountValue = discountValue,
            MaxDiscountCap = maxDiscountCap,
            MinOrderValue = minOrderValue,
            EffectiveDate = effectiveDate,
            ExpiryDate = expiryDate,
            UsageLimit = usageLimit,
            UsageLimitPerUser = usageLimitPerUser,
            UsageCount = 0,
            Status = VoucherStatus.Draft,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        voucher.AddDomainEvent(new VoucherCreatedEvent(voucher.Id, code.Value));
        return voucher;
    }

    public Result<bool, Error> Activate()
    {
        if (Status != VoucherStatus.Draft && Status != VoucherStatus.Paused)
            return Result<bool, Error>.Failure(new Error("PROMOTIONS.VOUCHER_CANNOT_ACTIVATE",
                "Voucher can only be activated from Draft or Paused status."));

        Status = VoucherStatus.Active;
        UpdatedAt = DateTime.UtcNow;
        AddDomainEvent(new VoucherActivatedEvent(Id));
        return Result<bool, Error>.Success(true);
    }

    public Result<bool, Error> Pause()
    {
        if (Status != VoucherStatus.Active)
            return Result<bool, Error>.Failure(new Error("PROMOTIONS.VOUCHER_CANNOT_PAUSE",
                "Only Active vouchers can be paused."));

        Status = VoucherStatus.Paused;
        UpdatedAt = DateTime.UtcNow;
        AddDomainEvent(new VoucherPausedEvent(Id));
        return Result<bool, Error>.Success(true);
    }

    public void MarkExpired()
    {
        Status = VoucherStatus.Expired;
        UpdatedAt = DateTime.UtcNow;
        AddDomainEvent(new VoucherExpiredEvent(Id));
    }

    public void MarkDepleted()
    {
        Status = VoucherStatus.Depleted;
        UpdatedAt = DateTime.UtcNow;
        AddDomainEvent(new VoucherDepletedEvent(Id));
    }

    public Result<VoucherUsage, Error> RecordUsage(Guid orderId, Guid userId, Money discountApplied)
    {
        if (Status != VoucherStatus.Active)
            return Result<VoucherUsage, Error>.Failure(new Error("PROMOTIONS.VOUCHER_NOT_ACTIVE", "Voucher is not active."));

        if (UsageLimit.HasValue && UsageCount >= UsageLimit.Value)
            return Result<VoucherUsage, Error>.Failure(new Error("PROMOTIONS.VOUCHER_DEPLETED", "Voucher has no remaining uses."));

        var usage = VoucherUsage.Create(Id, orderId, userId, discountApplied);
        _usages.Add(usage);
        UsageCount++;
        UpdatedAt = DateTime.UtcNow;

        if (UsageLimit.HasValue && UsageCount >= UsageLimit.Value)
            MarkDepleted();

        AddDomainEvent(new VoucherAppliedEvent(Id, orderId, userId, discountApplied));
        return Result<VoucherUsage, Error>.Success(usage);
    }

    public void ReverseUsage(Guid orderId)
    {
        UsageCount = Math.Max(0, UsageCount - 1);
        UpdatedAt = DateTime.UtcNow;
        AddDomainEvent(new VoucherUsageReversedEvent(Id, orderId));
    }

    public bool HasUsages => _usages.Count > 0;
}
