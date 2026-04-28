using MarketNest.Base.Common;
using MarketNest.Promotions.Domain;

namespace MarketNest.Promotions.Application;

public interface IGetVouchersPagedQuery
{
    Task<PagedResult<VoucherDto>> ExecuteAsync(GetVouchersPagedQuery query, CancellationToken ct = default);
}
