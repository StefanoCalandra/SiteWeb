using FluentValidation;

namespace SiteWeb.Api.CQRS;

public class CreateBookingCommandValidator : AbstractValidator<CreateBookingCommand>
{
    public CreateBookingCommandValidator()
    {
        RuleFor(x => x.ResourceName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.End).GreaterThan(x => x.Start);
        RuleFor(x => x.CreatedBy).NotEmpty();
    }
}
