using FluentValidation;
using Majlis.Knowledge.Application.Documents.DTOs;
using Majlis.Knowledge.Domain.Constants;

namespace Majlis.Knowledge.Application.Documents.Validations;

public sealed class BeginUploadValidator : AbstractValidator<BeginUploadDto>
{
    public BeginUploadValidator()
    {
        RuleFor(x => x.WorkspaceId).NotEmpty().WithMessage("General:Fields:Required");
        RuleFor(x => x.FileName).NotEmpty().WithMessage("General:Fields:Required")
            .MaximumLength(KnowledgeFieldDefinitions.MaxFileNameLength).WithMessage("General:Fields:MaxLength")
            .Must(n => !n.Contains('/') && !n.Contains('\\')).WithMessage("General:Fields:InvalidCharacters");
        RuleFor(x => x.ContentType).NotEmpty().WithMessage("General:Fields:Required")
            .Must(KnowledgeFieldDefinitions.ContentTypes.ContainsKey).WithMessage(KnowledgeErrors.TypeNotSupported);
        RuleFor(x => x.SizeBytes).GreaterThan(0).WithMessage("General:Fields:Invalid");
        RuleFor(x => x.Title).MaximumLength(KnowledgeFieldDefinitions.MaxTitleLength).WithMessage("General:Fields:MaxLength");
        RuleFor(x => x.Type).IsInEnum().WithMessage("General:Fields:Invalid");
        RuleFor(x => x.Language).Must(l => l is null or "ar" or "en").WithMessage("General:Fields:Invalid");
    }
}

public sealed class UpdateDocumentValidator : AbstractValidator<UpdateDocumentDto>
{
    public UpdateDocumentValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("General:Fields:Required");
        RuleFor(x => x.Title).NotEmpty().WithMessage("General:Fields:Required")
            .MaximumLength(KnowledgeFieldDefinitions.MaxTitleLength).WithMessage("General:Fields:MaxLength");
        RuleFor(x => x.Type).IsInEnum().WithMessage("General:Fields:Invalid");
        RuleFor(x => x.Tags).MaximumLength(KnowledgeFieldDefinitions.MaxTagsLength).WithMessage("General:Fields:MaxLength");
    }
}
