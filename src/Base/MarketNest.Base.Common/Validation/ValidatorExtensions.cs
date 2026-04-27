using FluentValidation;

namespace MarketNest.Base.Common;

/// <summary>
///     Reusable FluentValidation rule extensions for common patterns.
/// </summary>
public static class ValidatorExtensions
{
    public static IRuleBuilderOptions<T, string> MustBeSlug<T>(this IRuleBuilder<T, string> rule)
        => rule
            .NotEmpty()
            .Matches(DomainConstants.Validation.SlugPattern)
            .WithMessage(DomainConstants.Validation.SlugErrorMessage);

    public static IRuleBuilderOptions<T, decimal> MustBePositiveMoney<T>(this IRuleBuilder<T, decimal> rule)
        => rule
            .GreaterThan(0)
            .WithMessage(DomainConstants.Validation.MoneyPositiveMessage)
            .LessThanOrEqualTo(DomainConstants.Validation.MaxMoneyAmount)
            .WithMessage(DomainConstants.Validation.MoneyMaxMessage);

    public static IRuleBuilderOptions<T, string> MustBeValidEmail<T>(this IRuleBuilder<T, string> rule)
        => rule
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(DomainConstants.Validation.MaxEmailLength);

    public static IRuleBuilderOptions<T, Guid> MustBeValidId<T>(this IRuleBuilder<T, Guid> rule)
        => rule
            .NotEqual(Guid.Empty)
            .WithMessage(DomainConstants.Validation.IdEmptyMessage);

    public static IRuleBuilderOptions<T, int> MustBeValidQuantity<T>(this IRuleBuilder<T, int> rule)
        => rule
            .InclusiveBetween(DomainConstants.Validation.MinQuantity, DomainConstants.Validation.MaxQuantity)
            .WithMessage(DomainConstants.Validation.QuantityRangeMessage);
}
