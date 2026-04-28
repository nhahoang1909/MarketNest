using MarketNest.Base.Domain;

namespace MarketNest.Orders.Domain;

/// <summary>
///     Singleton configuration entity for order lifecycle policy windows.
///     Owned by the Orders module; stored in <c>orders.order_policy_config</c>.
///     Admin writes via <see cref="IOrderPolicyConfigWriter" /> contract.
///     Always Id=1 (singleton row pattern).
/// </summary>
public class OrderPolicyConfig : Entity<int>
{
    // ── Defaults ────────────────────────────────────────────────────────
    private const int DefaultSellerConfirmWindowHours = 48;
    private const int DefaultAutoDeliverAfterShippedDays = 30;
    private const int DefaultAutoCompleteAfterDeliveredDays = 3;
    private const int DefaultDisputeWindowAfterDeliveredDays = 3;

    // ── Validation bounds ───────────────────────────────────────────────
    private const int MinConfirmWindowHours = 1;
    private const int MaxConfirmWindowHours = 168; // 1 week
    private const int MinWindowDays = 1;
    private const int MaxWindowDays = 365;

    public int SellerConfirmWindowHours { get; private set; } = DefaultSellerConfirmWindowHours;
    public int AutoDeliverAfterShippedDays { get; private set; } = DefaultAutoDeliverAfterShippedDays;
    public int AutoCompleteAfterDeliveredDays { get; private set; } = DefaultAutoCompleteAfterDeliveredDays;
    public int DisputeWindowAfterDeliveredDays { get; private set; } = DefaultDisputeWindowAfterDeliveredDays;
    public Guid? UpdatedByAdminId { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Required by EF Core.</summary>
    private OrderPolicyConfig() { }

    /// <summary>Creates the singleton row with all default values.</summary>
    public static OrderPolicyConfig CreateDefault() => new()
    {
        Id = 1,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    /// <summary>
    ///     Updates all policy windows atomically.
    ///     Returns <see cref="Error" /> if any value is out of range.
    /// </summary>
    public Result<Unit, Error> Update(UpdateOrderPolicyRequest req, Guid adminId)
    {
        if (req.SellerConfirmWindowHours is < MinConfirmWindowHours or > MaxConfirmWindowHours)
            return Result<Unit, Error>.Failure(
                new Error("ORDER_POLICY.INVALID_CONFIRM_WINDOW",
                    $"Confirm window must be {MinConfirmWindowHours}–{MaxConfirmWindowHours} hours"));

        if (req.AutoDeliverAfterShippedDays is < MinWindowDays or > MaxWindowDays)
            return Result<Unit, Error>.Failure(
                new Error("ORDER_POLICY.INVALID_DELIVER_DAYS",
                    $"Auto-deliver days must be {MinWindowDays}–{MaxWindowDays}"));

        if (req.AutoCompleteAfterDeliveredDays is < MinWindowDays or > MaxWindowDays)
            return Result<Unit, Error>.Failure(
                new Error("ORDER_POLICY.INVALID_COMPLETE_DAYS",
                    $"Auto-complete days must be {MinWindowDays}–{MaxWindowDays}"));

        if (req.DisputeWindowAfterDeliveredDays is < MinWindowDays or > MaxWindowDays)
            return Result<Unit, Error>.Failure(
                new Error("ORDER_POLICY.INVALID_DISPUTE_DAYS",
                    $"Dispute window days must be {MinWindowDays}–{MaxWindowDays}"));

        SellerConfirmWindowHours = req.SellerConfirmWindowHours;
        AutoDeliverAfterShippedDays = req.AutoDeliverAfterShippedDays;
        AutoCompleteAfterDeliveredDays = req.AutoCompleteAfterDeliveredDays;
        DisputeWindowAfterDeliveredDays = req.DisputeWindowAfterDeliveredDays;
        UpdatedByAdminId = adminId;
        UpdatedAt = DateTimeOffset.UtcNow;

        return Result<Unit, Error>.Success(Unit.Value);
    }
}

