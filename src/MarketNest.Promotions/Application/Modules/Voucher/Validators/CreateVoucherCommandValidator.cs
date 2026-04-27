using FluentValidation;
using MarketNest.Promotions.Domain;

namespace MarketNest.Promotions.Application;

public class CreateVoucherCommandValidator : AbstractValidator<CreateVoucherCommand>
{
    public CreateVoucherCommandValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty()
            .Matches(@"^[A-Z0-9\-]{6,20}$")
            .WithMessage("Voucher code must be 6–20 uppercase letters, digits, or hyphens.");

        RuleFor(x => x.DiscountValue)
            .GreaterThan(0m);

        When(x => x.DiscountType == VoucherDiscountType.PercentageOff, () =>
            RuleFor(x => x.DiscountValue)
                .InclusiveBetween(1m, 100m)
                .WithMessage("Percentage discount must be between 1 and 100."));

        RuleFor(x => x.EffectiveDate)
            .LessThan(x => x.ExpiryDate)
            .WithMessage("EffectiveDate must be before ExpiryDate.");

        RuleFor(x => x.ExpiryDate)
            .GreaterThan(DateTime.UtcNow)
            .WithMessage("ExpiryDate must be in the future.");

        RuleFor(x => x)
            .Must(x => (x.ExpiryDate - x.EffectiveDate).TotalDays <= 730)
            .WithMessage("Validity period cannot exceed 2 years.");

        When(x => x.MaxDiscountCap.HasValue, () =>
            RuleFor(x => x)
                .Must(x => x.DiscountType == VoucherDiscountType.PercentageOff
                           && x.ApplyFor == VoucherApplyFor.ProductSubtotal)
                .WithMessage("MaxDiscountCap is only applicable for PercentageOff on ProductSubtotal."));

        When(x => x.Scope == VoucherScope.Shop, () =>
            RuleFor(x => x.StoreId)
                .NotNull()
                .WithMessage("StoreId is required for Shop vouchers."));
    }
}
