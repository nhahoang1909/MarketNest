using FluentValidation;
using MarketNest.Catalog.Domain;

namespace MarketNest.Catalog.Application;

public class SetSalePriceCommandValidator : AbstractValidator<SetSalePriceCommand>
{
    public SetSalePriceCommandValidator()
    {
        RuleFor(x => x.VariantId)
            .NotEmpty().WithMessage(DomainConstants.Validation.IdEmptyMessage);

        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage(DomainConstants.Validation.IdEmptyMessage);

        RuleFor(x => x.SalePrice)
            .MustBePositiveMoney()
            .Must(v => decimal.Round(v, 2) == v)
                .WithMessage("Sale price must have at most 2 decimal places.");

        RuleFor(x => x.SaleStart)
            .LessThan(x => x.SaleEnd)
                .WithMessage("Sale start must be before sale end.");

        RuleFor(x => x.SaleEnd)
            .GreaterThan(_ => DateTimeOffset.UtcNow)
                .WithMessage("Sale end must be in the future.");

        RuleFor(x => x)
            .Must(x => (x.SaleEnd - x.SaleStart).TotalDays <= CatalogConstants.Sale.MaxDurationDays)
            .WithMessage($"Sale period cannot exceed {CatalogConstants.Sale.MaxDurationDays} days.")
            .OverridePropertyName("SaleEnd");
    }
}

