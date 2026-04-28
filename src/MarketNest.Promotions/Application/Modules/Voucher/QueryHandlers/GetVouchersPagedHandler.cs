using MarketNest.Base.Common;

namespace MarketNest.Promotions.Application;

public class GetVouchersPagedHandler(IGetVouchersPagedQuery query)
    : IQueryHandler<GetVouchersPagedQuery, PagedResult<VoucherDto>>
{
    public Task<PagedResult<VoucherDto>> Handle(GetVouchersPagedQuery request, CancellationToken cancellationToken)
        => query.ExecuteAsync(request, cancellationToken);
}
