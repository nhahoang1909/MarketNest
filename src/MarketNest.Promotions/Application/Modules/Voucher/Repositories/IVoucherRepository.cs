using MarketNest.Base.Infrastructure;
using MarketNest.Promotions.Domain;

namespace MarketNest.Promotions.Application;

public interface IVoucherRepository : IBaseRepository<Voucher, Guid>
{
    Task<Voucher?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<IReadOnlyList<Voucher>> GetActiveExpiredAsync(DateTimeOffset utcNow, CancellationToken ct = default);
    Task<IReadOnlyList<Voucher>> GetActiveDepletedAsync(CancellationToken ct = default);
}
