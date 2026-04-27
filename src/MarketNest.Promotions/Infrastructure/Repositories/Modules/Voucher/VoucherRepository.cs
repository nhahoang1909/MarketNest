using MarketNest.Promotions.Application;
using MarketNest.Promotions.Domain;

namespace MarketNest.Promotions.Infrastructure;

public class VoucherRepository(PromotionsDbContext db)
    : BaseRepository<Voucher, Guid>(db), IVoucherRepository
{
    public Task<Voucher?> GetByCodeAsync(string code, CancellationToken ct = default)
        => Db.Vouchers
            .Include(v => v.Usages)
            .FirstOrDefaultAsync(v => v.Code == new VoucherCode(code), ct);

    public async Task<IReadOnlyList<Voucher>> GetActiveExpiredAsync(DateTime utcNow, CancellationToken ct = default)
        => await Db.Vouchers
            .Where(v => v.Status == VoucherStatus.Active && v.ExpiryDate < utcNow)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Voucher>> GetActiveDepletedAsync(CancellationToken ct = default)
        => await Db.Vouchers
            .Where(v => v.Status == VoucherStatus.Active
                        && v.UsageLimit.HasValue
                        && v.UsageCount >= v.UsageLimit)
            .ToListAsync(ct);
}
