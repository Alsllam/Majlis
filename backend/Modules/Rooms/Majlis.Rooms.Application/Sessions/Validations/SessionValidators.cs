using FluentValidation;
using Majlis.Rooms.Application.Sessions.DTOs;
using Majlis.Rooms.Domain.Constants;

namespace Majlis.Rooms.Application.Sessions.Validations;

public sealed class InstructSessionValidator : AbstractValidator<InstructSessionDto>
{
    public InstructSessionValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty().WithMessage("General:Fields:Required");
        RuleFor(x => x.ClientRequestId).NotEmpty().WithMessage("General:Fields:Required");
        RuleFor(x => x.Text).Must(t => !string.IsNullOrWhiteSpace(t)).WithMessage("General:Fields:Required")
            .MaximumLength(RoomsFieldDefinitions.MaxInstructionLength).WithMessage("General:Fields:MaxLength");
        RuleFor(x => x.Language).Must(l => l is null or "ar" or "en").WithMessage("General:Fields:Invalid");
    }
}

public sealed class OfferHandOffValidator : AbstractValidator<OfferHandOffDto>
{
    public OfferHandOffValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty().WithMessage("General:Fields:Required");
        RuleFor(x => x.ToUserId).NotEmpty().WithMessage("General:Fields:Required");
        RuleFor(x => x.Note).MaximumLength(RoomsFieldDefinitions.MaxNoteLength).WithMessage("General:Fields:MaxLength");
    }
}
