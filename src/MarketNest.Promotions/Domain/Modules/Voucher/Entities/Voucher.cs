namespace MarketNest.Promotions.Domain;

public class Voucher : AggregateRoot<Guid>
{
#pragma warning disable CS8618 // Non-nullable field — EF Core uses this constructor
    protected Voucher() { }
#pragma warning restore CS8618

    public VoucherCode Code { get; private set; }
    public VoucherScope Scope { get; private set; }
    public Guid? StoreId { get; private set; }          // null = platform-wide voucher
    public Guid CreatedByUserId { get; private set; }

    public VoucherDiscountType DiscountType { get; private set; }
    public VoucherApplyFor ApplyFor { get; private set; }
    public decimal DiscountValue { get; private set; }
    public Money? MaxDiscountCap { get; private set; }   // null = no cap on discount amount

    public Money? MinOrderValue { get; private set; }    // null = no minimum order required
    public DateTimeOffset EffectiveDate { get; private set; }
    public DateTimeOffset ExpiryDate { get; private set; }

    public int? UsageLimit { get; private set; }          // null = unlimited total uses
    public int? UsageLimitPerUser { get; private set; }   // null = unlimited per-user uses
    public int UsageCount { get; private set; }

    public VoucherStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

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
        DateTimeOffset effectiveDate,
        DateTimeOffset expiryDate,
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
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        voucher.EnsureInvariants();
        voucher.AddDomainEvent(new VoucherCreatedEvent(voucher.Id, code.Value));
        return voucher;
    }

    public Result<bool, Error> Activate()
    {
        if (Status != VoucherStatus.Draft && Status != VoucherStatus.Paused)
            return Result<bool, Error>.Failure(new Error("PROMOTIONS.VOUCHER_CANNOT_ACTIVATE",
                "Voucher can only be activated from Draft or Paused status."));

        Status = VoucherStatus.Active;
        UpdatedAt = DateTimeOffset.UtcNow;
        EnsureInvariants();
        AddDomainEvent(new VoucherActivatedEvent(Id));
        return Result<bool, Error>.Success(true);
    }

    public Result<bool, Error> Pause()
    {
        if (Status != VoucherStatus.Active)
            return Result<bool, Error>.Failure(new Error("PROMOTIONS.VOUCHER_CANNOT_PAUSE",
                "Only Active vouchers can be paused."));

        Status = VoucherStatus.Paused;
        UpdatedAt = DateTimeOffset.UtcNow;
        EnsureInvariants();
        AddDomainEvent(new VoucherPausedEvent(Id));
        return Result<bool, Error>.Success(true);
    }

    public void MarkExpired()
    {
        Status = VoucherStatus.Expired;
        UpdatedAt = DateTimeOffset.UtcNow;
        EnsureInvariants();
        AddDomainEvent(new VoucherExpiredEvent(Id));
    }

    public void MarkDepleted()
    {
        Status = VoucherStatus.Depleted;
        UpdatedAt = DateTimeOffset.UtcNow;
        EnsureInvariants();
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
        UpdatedAt = DateTimeOffset.UtcNow;

        if (UsageLimit.HasValue && UsageCount >= UsageLimit.Value)
            MarkDepleted();

        EnsureInvariants();
        AddDomainEvent(new VoucherAppliedEvent(Id, orderId, userId, discountApplied));
        return Result<VoucherUsage, Error>.Success(usage);
    }

    public void ReverseUsage(Guid orderId)
    {
        UsageCount = Math.Max(0, UsageCount - 1);
        UpdatedAt = DateTimeOffset.UtcNow;
        EnsureInvariants();
        AddDomainEvent(new VoucherUsageReversedEvent(Id, orderId));
    }

    public bool HasUsages => _usages.Count > 0;

    // ── Invariants ─────────────────────────────────────────────────────

    protected override void EnsureInvariants()
    {
        if (DiscountValue <= 0)
            throw new DomainException("Voucher discount value must be positive.");

        if (EffectiveDate >= ExpiryDate)
            throw new DomainException("Voucher effective date must be before expiry date.");

        if (UsageCount < 0)
            throw new DomainException("Voucher usage count cannot be negative.");

        if (UsageLimit.HasValue && UsageLimit.Value <= 0)
            throw new DomainException("Voucher usage limit, when set, must be positive.");

        if (UsageLimitPerUser.HasValue && UsageLimitPerUser.Value <= 0)
            throw new DomainException("Voucher per-user usage limit, when set, must be positive.");

        if (DiscountType == VoucherDiscountType.PercentageOff && DiscountValue > 100)
            throw new DomainException("Voucher percentage discount cannot exceed 100%.");

        if (Scope == VoucherScope.Shop && StoreId is null)
            throw new DomainException("Shop-scoped voucher must have a StoreId.");

        if (Scope == VoucherScope.Platform && StoreId is not null)
            throw new DomainException("Platform-scoped voucher must not have a StoreId.");
    }
}
