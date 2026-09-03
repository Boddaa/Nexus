using FluentValidation;
using Nexus.Application.DTOs.Notes;

namespace Nexus.Application.Features.Notes.Validators;

public class CreateNoteRequestValidator : AbstractValidator<CreateNoteRequest>
{
    private static readonly string[] AllowedContentTypes = { "markdown", "text", "html" };

    public CreateNoteRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Note title is required.")
            .MaximumLength(200).WithMessage("Note title cannot exceed 200 characters.");

        RuleFor(x => x.Content)
            .NotNull().WithMessage("Content must not be null.");

        RuleFor(x => x.ContentType)
            .NotEmpty().WithMessage("Content type is required.")
            .Must(t => AllowedContentTypes.Contains(t.ToLowerInvariant()))
            .WithMessage("Content type must be 'markdown', 'text', or 'html'.");
    }
}

public class UpdateNoteRequestValidator : AbstractValidator<UpdateNoteRequest>
{
    private static readonly string[] AllowedContentTypes = { "markdown", "text", "html" };

    public UpdateNoteRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Note title is required.")
            .MaximumLength(200).WithMessage("Note title cannot exceed 200 characters.");

        RuleFor(x => x.Content)
            .NotNull().WithMessage("Content must not be null.");

        RuleFor(x => x.ContentType)
            .NotEmpty().WithMessage("Content type is required.")
            .Must(t => AllowedContentTypes.Contains(t.ToLowerInvariant()))
            .WithMessage("Content type must be 'markdown', 'text', or 'html'.");
    }
}
