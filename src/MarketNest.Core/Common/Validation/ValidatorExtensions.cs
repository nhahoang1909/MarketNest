namespace MarketNest.Core.Common.Validation;

using FluentValidation;

/// <summary>
/// Reusable FluentValidation rule extensions for common patterns.
/// </summary>
public static class ValidatorExtensions
{
    public static IRuleBuilderOptions<T, string> MustBeSlug<T>(this IRuleBuilder<T, string> rule)
        => rule
            .NotEmpty()
            .Matches(@"^[a-z0-9-]{3,50}$")
            .WithMessage("Must be 3-50 lowercase letters, numbers, or hyphens");

    public static IRuleBuilderOptions<T, decimal> MustBePositiveMoney<T>(this IRuleBuilder<T, decimal> rule)
        => rule
            .GreaterThan(0)
            .WithMessage("Amount must be greater than 0")
            .LessThanOrEqualTo(999_999.99m)
            .WithMessage("Amount exceeds maximum allowed value");

    public static IRuleBuilderOptions<T, string> MustBeValidEmail<T>(this IRuleBuilder<T, string> rule)
        => rule
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(254);

    public static IRuleBuilderOptions<T, Guid> MustBeValidId<T>(this IRuleBuilder<T, Guid> rule)
        => rule
            .NotEqual(Guid.Empty)
            .WithMessage("ID cannot be empty");

    public static IRuleBuilderOptions<T, int> MustBeValidQuantity<T>(this IRuleBuilder<T, int> rule)
        => rule
            .InclusiveBetween(1, 99)
            .WithMessage("Quantity must be between 1 and 99");
}
