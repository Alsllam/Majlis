using FluentValidation;
using Majlis.Meetings.Application.Meetings.DTOs;
using Majlis.Meetings.Domain.Constants;
using Majlis.Meetings.Domain.Enums;

namespace Majlis.Meetings.Application.Meetings.Validations;

public sealed class CreateMeetingValidator : AbstractValidator<CreateMeetingDto>
{
    public CreateMeetingValidator()
    {
        RuleFor(x => x.WorkspaceId).NotEmpty().WithMessage("General:Fields:Required");
        RuleFor(x => x.RoomId).NotEmpty().WithMessage("General:Fields:Required");
        RuleFor(x => x.Title).NotEmpty().WithMessage("General:Fields:Required").MaximumLength(MeetingsFieldDefinitions.MaxTitleLength).WithMessage("General:Fields:MaxLength");
        RuleFor(x => x.HeldAt).NotEmpty().WithMessage("General:Fields:Required");
        RuleFor(x => x.Language).Must(l => l is "ar" or "en").WithMessage("General:Fields:Invalid");
        RuleFor(x => x.Text).NotEmpty().When(x => x.Source != MeetingSource.Recording).WithMessage(MeetingsErrors.TextRequired)
            .MaximumLength(MeetingsFieldDefinitions.MaxTranscriptLength).WithMessage("General:Fields:MaxLength");
        RuleForEach(x => x.Attendees).MaximumLength(MeetingsFieldDefinitions.MaxOwnerLength).WithMessage("General:Fields:MaxLength");
    }
}

public sealed class BeginRecordingUploadValidator : AbstractValidator<BeginRecordingUploadDto>
{
    public BeginRecordingUploadValidator()
    {
        RuleFor(x => x.MeetingId).NotEmpty().WithMessage("General:Fields:Required");
        RuleFor(x => x.FileName).NotEmpty().WithMessage("General:Fields:Required").MaximumLength(255).WithMessage("General:Fields:MaxLength");
        RuleFor(x => x.ContentType).Must(MeetingsFieldDefinitions.RecordingContentTypes.ContainsKey).WithMessage(MeetingsErrors.RecordingTypeNotSupported);
        RuleFor(x => x.SizeBytes).GreaterThan(0).WithMessage("General:Fields:Required");
    }
}

public sealed class UpdateMinutesValidator : AbstractValidator<UpdateMinutesDto>
{
    public UpdateMinutesValidator()
    {
        RuleFor(x => x.MeetingId).NotEmpty().WithMessage("General:Fields:Required");
        RuleFor(x => x.Summary).NotEmpty().WithMessage("General:Fields:Required").MaximumLength(MeetingsFieldDefinitions.MaxSummaryLength).WithMessage("General:Fields:MaxLength");
        RuleForEach(x => x.Decisions).MaximumLength(MeetingsFieldDefinitions.MaxItemTextLength).WithMessage("General:Fields:MaxLength");
        RuleForEach(x => x.OpenQuestions).MaximumLength(MeetingsFieldDefinitions.MaxItemTextLength).WithMessage("General:Fields:MaxLength");
        RuleForEach(x => x.ActionItems).ChildRules(item =>
        {
            item.RuleFor(i => i.Text).MaximumLength(MeetingsFieldDefinitions.MaxItemTextLength).WithMessage("General:Fields:MaxLength");
            item.RuleFor(i => i.OwnerName).MaximumLength(MeetingsFieldDefinitions.MaxOwnerLength).WithMessage("General:Fields:MaxLength");
        });
    }
}
