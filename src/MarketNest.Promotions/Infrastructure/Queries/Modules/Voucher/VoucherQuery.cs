using MarketNest.Base.Common;
using MarketNest.Promotions.Application;
using MarketNest.Promotions.Domain;

namespace MarketNest.Promotions.Infrastructure;

public class VoucherQuery(PromotionsReadDbContext db)
    : BaseQuery<Voucher, Guid>(db), IVoucherQuery, IGetVouchersPagedQuery
{
    public Task<Voucher?> GetByCodeAsync(string code, CancellationToken ct = default)
        => Db.Vouchers.FirstOrDefaultAsync(v => v.Code == new VoucherCode(code), ct);

    public async Task<PagedResult<VoucherDto>> ExecuteAsync(
        GetVouchersPagedQuery query, CancellationToken ct = default)
    {
        IQueryable<Voucher> q = Db.Vouchers.AsNoTracking();

        if (query.Scope.HasValue) q = q.Where(v => v.Scope == query.Scope.Value);
        if (query.Status.HasValue) q = q.Where(v => v.Status == query.Status.Value);
        if (query.StoreId.HasValue) q = q.Where(v => v.StoreId == query.StoreId.Value);

        int total = await q.CountAsync(ct);
        List<Voucher> items = await q
            .OrderByDescending(v => v.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        return new PagedResult<VoucherDto>
        {
            Items = items.Select(MapToDto).ToList(),
            TotalCount = total,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }

    private static VoucherDto MapToDto(Voucher v) =>
        new(v.Id, v.Code.Value, v.Scope, v.StoreId, v.DiscountType, v.ApplyFor,
            v.DiscountValue, v.MaxDiscountCap?.Amount, v.MinOrderValue?.Amount,
            v.EffectiveDate, v.ExpiryDate, v.UsageLimit, v.UsageLimitPerUser,
            v.UsageCount, v.Status, v.CreatedAt);
}
