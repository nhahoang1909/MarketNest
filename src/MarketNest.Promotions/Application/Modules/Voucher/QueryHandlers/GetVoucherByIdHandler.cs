namespace MarketNest.Promotions.Application;

public class GetVoucherByIdHandler(IVoucherQuery query) : IQueryHandler<GetVoucherByIdQuery, VoucherDto?>
{
    public async Task<VoucherDto?> Handle(GetVoucherByIdQuery request, CancellationToken cancellationToken)
    {
        var voucher = await query.GetByKeyAsync(request.VoucherId, cancellationToken);
        return voucher is null ? null : MapToDto(voucher);
    }

    private static VoucherDto MapToDto(Domain.Voucher v) =>
        new(v.Id, v.Code.Value, v.Scope, v.StoreId, v.DiscountType, v.ApplyFor,
            v.DiscountValue, v.MaxDiscountCap?.Amount, v.MinOrderValue?.Amount,
            v.EffectiveDate, v.ExpiryDate, v.UsageLimit, v.UsageLimitPerUser,
            v.UsageCount, v.Status, v.CreatedAt);
}
