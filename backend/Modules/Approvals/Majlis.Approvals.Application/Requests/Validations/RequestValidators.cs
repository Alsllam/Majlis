using System.Text.Json;
using FluentValidation;
using Majlis.Approvals.Application.Requests.DTOs;
using Majlis.Approvals.Domain.Constants;

namespace Majlis.Approvals.Application.Requests.Validations;

public sealed class CreateApprovalRequestValidator : AbstractValidator<CreateApprovalRequestDto>
{
    public CreateApprovalRequestValidator()
    {
        RuleFor(x => x.WorkspaceId).NotEmpty().WithMessage("General:Fields:Required");
        RuleFor(x => x.RoomId).NotEmpty().WithMessage("General:Fields:Required");
        RuleFor(x => x.SessionId).NotEmpty().WithMessage("General:Fields:Required");
        RuleFor(x => x.Tool).NotEmpty().WithMessage("General:Fields:Required")
            .Must(KnownTools.Risk.ContainsKey).WithMessage(ApprovalsErrors.UnknownTool);
        RuleFor(x => x.Args).Must(a => a.ValueKind == JsonValueKind.Object).WithMessage(ApprovalsErrors.InvalidArgs)
            .Must(a => a.GetRawText().Length <= ApprovalsFieldDefinitions.MaxArgsLength).WithMessage("General:Fields:MaxLength");
        RuleFor(x => x.Summary).NotEmpty().WithMessage("General:Fields:Required")
            .MaximumLength(ApprovalsFieldDefinitions.MaxSummaryLength).WithMessage("General:Fields:MaxLength");
        RuleFor(x => x.Reason).MaximumLength(ApprovalsFieldDefinitions.MaxReasonLength).WithMessage("General:Fields:MaxLength");
    }
}

public sealed class ApproveRequestValidator : AbstractValidator<ApproveRequestDto>
{
    public ApproveRequestValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("General:Fields:Required");
        RuleFor(x => x.EditedArgs).Must(a => a is null || a.Value.ValueKind == JsonValueKind.Object).WithMessage(ApprovalsErrors.InvalidArgs);
        RuleFor(x => x.Note).MaximumLength(ApprovalsFieldDefinitions.MaxNoteLength).WithMessage("General:Fields:MaxLength");
    }
}

public sealed class RejectRequestValidator : AbstractValidator<RejectRequestDto>
{
    public RejectRequestValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("General:Fields:Required");
        RuleFor(x => x.Reason).NotEmpty().WithMessage(ApprovalsErrors.ReasonRequired)
            .MaximumLength(ApprovalsFieldDefinitions.MaxReasonLength).WithMessage("General:Fields:MaxLength");
    }
}
