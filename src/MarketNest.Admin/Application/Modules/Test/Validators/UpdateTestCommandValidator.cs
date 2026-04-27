using FluentValidation;

namespace MarketNest.Admin.Application;

public class UpdateTestCommandValidator : AbstractValidator<UpdateTestCommand>
{
    public UpdateTestCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Id must not be empty.");

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200)
            .WithMessage("Name must not be empty and must be 200 characters or fewer.");

        RuleFor(x => x.Value.Code)
            .NotEmpty()
            .MaximumLength(50)
            .WithMessage("Value.Code must not be empty and must be 50 characters or fewer.");

        RuleFor(x => x.Value.Amount)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Value.Amount must be non-negative.");
    }
}
