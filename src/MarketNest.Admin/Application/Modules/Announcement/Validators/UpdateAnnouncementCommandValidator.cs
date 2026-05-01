using FluentValidation;

namespace MarketNest.Admin.Application;

public class UpdateAnnouncementCommandValidator : AbstractValidator<UpdateAnnouncementCommand>
{
    public UpdateAnnouncementCommandValidator()
    {
        RuleFor(x => x.Id).MustBeValidId();

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage(ValidationMessages.Required("Title"))
            .MaximumLength(FieldLimits.InlineStandard.MaxLength)
            .WithMessage(ValidationMessages.MaxLength("Title", FieldLimits.InlineStandard.MaxLength));

        RuleFor(x => x.Message)
            .NotEmpty().WithMessage(ValidationMessages.Required("Message"))
            .MaximumLength(FieldLimits.MultilineLong.MaxLength)
            .WithMessage(ValidationMessages.MaxLength("Message", FieldLimits.MultilineLong.MaxLength));

        RuleFor(x => x.Type)
            .IsInEnum().WithMessage(ValidationMessages.InvalidFormat("Type", "Info, Promotion, Warning, or Urgent"));

        RuleFor(x => x.StartDateUtc)
            .NotEmpty().WithMessage(ValidationMessages.Required("StartDateUtc"));

        RuleFor(x => x.EndDateUtc)
            .NotEmpty().WithMessage(ValidationMessages.Required("EndDateUtc"))
            .GreaterThan(x => x.StartDateUtc)
            .WithMessage(ValidationMessages.DateMustBeAfter("EndDateUtc", "StartDateUtc"));

        RuleFor(x => x.LinkUrl)
            .MaximumLength(FieldLimits.Url.MaxLength)
            .WithMessage(ValidationMessages.MaxLength("LinkUrl", FieldLimits.Url.MaxLength))
            .When(x => x.LinkUrl is not null);

        RuleFor(x => x.LinkText)
            .MaximumLength(FieldLimits.InlineShort.MaxLength)
            .WithMessage(ValidationMessages.MaxLength("LinkText", FieldLimits.InlineShort.MaxLength))
            .When(x => x.LinkText is not null);

        RuleFor(x => x.SortOrder)
            .GreaterThanOrEqualTo(0)
            .WithMessage(ValidationMessages.MinValue("SortOrder", 0));
    }
}

