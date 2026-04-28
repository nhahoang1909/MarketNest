using MarketNest.Promotions.Domain;

namespace MarketNest.Promotions.Application;

/// <summary>
///     Validates and calculates voucher discounts at checkout time.
///     Called by the Orders module CheckoutHandler via cross-module contract.
/// </summary>
public interface IVoucherService
{
    Task<DiscountResult> ValidateAsync(
        string code,
        Guid buyerId,
        decimal productSubtotal,
        decimal shippingFee,
        Guid? storeId = null,
        CancellationToken ct = default);
}
