using FluentValidation;
using Nexus.Application.DTOs.Pages;

namespace Nexus.Application.Features.Pages.Validators;

public class CreatePageRequestValidator : AbstractValidator<CreatePageRequest>
{
    public CreatePageRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Page title is required.")
            .MaximumLength(200).WithMessage("Page title cannot exceed 200 characters.");

        RuleFor(x => x.Icon)
            .NotEmpty().WithMessage("Icon is required.")
            .MaximumLength(10).WithMessage("Icon cannot exceed 10 characters.");

        RuleFor(x => x.CoverImageUrl)
            .MaximumLength(2000).WithMessage("Cover image URL cannot exceed 2000 characters.");

        RuleFor(x => x.OrderIndex)
            .GreaterThanOrEqualTo(0).WithMessage("Order index must be non-negative.");
    }
}

public class UpdatePageRequestValidator : AbstractValidator<UpdatePageRequest>
{
    public UpdatePageRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Page title is required.")
            .MaximumLength(200).WithMessage("Page title cannot exceed 200 characters.");

        RuleFor(x => x.Icon)
            .NotEmpty().WithMessage("Icon is required.")
            .MaximumLength(10).WithMessage("Icon cannot exceed 10 characters.");

        RuleFor(x => x.CoverImageUrl)
            .MaximumLength(2000).WithMessage("Cover image URL cannot exceed 2000 characters.");

        RuleFor(x => x.OrderIndex)
            .GreaterThanOrEqualTo(0).WithMessage("Order index must be non-negative.");
    }
}

public class MovePageRequestValidator : AbstractValidator<MovePageRequest>
{
    public MovePageRequestValidator()
    {
        RuleFor(x => x.NewOrderIndex)
            .GreaterThanOrEqualTo(0).WithMessage("New order index must be non-negative.");
    }
}
