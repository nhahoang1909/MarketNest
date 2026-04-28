using MarketNest.Base.Common;
using MarketNest.Promotions.Domain;

namespace MarketNest.Promotions.Application;

public record GetVouchersPagedQuery(
    VoucherScope? Scope = null,
    VoucherStatus? Status = null,
    Guid? StoreId = null) : PagedQuery, IQuery<PagedResult<VoucherDto>>;
