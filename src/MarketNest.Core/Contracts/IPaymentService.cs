using MarketNest.Core.Common;
using MarketNest.Core.ValueObjects;
using MediatR;

namespace MarketNest.Core.Contracts;

/// <summary>
///     Implemented by Payments module; consumed by Orders module.
/// </summary>
public interface IPaymentService
{
    Task<Result<Guid, Error>> CaptureAsync(
        Guid orderId,
        Money amount,
        string paymentMethod,
        CancellationToken ct = default);

    Task<Result<Unit, Error>> RefundAsync(
        Guid paymentId,
        Money amount,
        string reason,
        CancellationToken ct = default);
}
