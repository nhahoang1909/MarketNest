using MarketNest.Promotions.Domain;

namespace MarketNest.Promotions.Application;

public interface IVoucherQuery : IBaseQuery<Voucher, Guid>
{
    Task<Voucher?> GetByCodeAsync(string code, CancellationToken ct = default);
}
