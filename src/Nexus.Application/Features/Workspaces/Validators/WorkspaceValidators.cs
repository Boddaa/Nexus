using FluentValidation;
using Nexus.Application.DTOs.Workspaces;

namespace Nexus.Application.Features.Workspaces.Validators;

public class CreateWorkspaceRequestValidator : AbstractValidator<CreateWorkspaceRequest>
{
    public CreateWorkspaceRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Workspace name is required.")
            .MaximumLength(100).WithMessage("Workspace name cannot exceed 100 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Description cannot exceed 500 characters.");

        RuleFor(x => x.Icon)
            .NotEmpty().WithMessage("Icon is required.")
            .MaximumLength(10);

        RuleFor(x => x.ColorHex)
            .NotEmpty().WithMessage("Color is required.")
            .Matches("^#(?:[0-9a-fA-F]{3}){1,2}$").WithMessage("Valid HEX color is required.");
    }
}

public class UpdateWorkspaceRequestValidator : AbstractValidator<UpdateWorkspaceRequest>
{
    public UpdateWorkspaceRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Workspace name is required.")
            .MaximumLength(100);

        RuleFor(x => x.Description)
            .MaximumLength(500);

        RuleFor(x => x.Icon)
            .NotEmpty().MaximumLength(10);

        RuleFor(x => x.ColorHex)
            .NotEmpty().Matches("^#(?:[0-9a-fA-F]{3}){1,2}$");
    }
}
