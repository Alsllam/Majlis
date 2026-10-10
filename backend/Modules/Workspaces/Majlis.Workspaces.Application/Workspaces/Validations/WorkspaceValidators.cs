using FluentValidation;
using Majlis.Workspaces.Application.Workspaces.DTOs;
using Majlis.Workspaces.Domain.Constants;

namespace Majlis.Workspaces.Application.Workspaces.Validations;

public sealed class CreateWorkspaceValidator : AbstractValidator<CreateWorkspaceDto>
{
    public CreateWorkspaceValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("General:Fields:Required")
            .MaximumLength(WorkspacesFieldDefinitions.MaxNameLength).WithMessage("General:Fields:MaxLength");
        RuleFor(x => x.Description).MaximumLength(WorkspacesFieldDefinitions.MaxDescriptionLength).WithMessage("General:Fields:MaxLength");
        RuleFor(x => x.Icon).MaximumLength(WorkspacesFieldDefinitions.MaxIconLength).WithMessage("General:Fields:MaxLength");
        RuleFor(x => x.Color).Must(c => c is null || WorkspacesFieldDefinitions.Colors.Contains(c)).WithMessage(WorkspacesErrors.ColorInvalid);
    }
}

public sealed class UpdateWorkspaceValidator : AbstractValidator<UpdateWorkspaceDto>
{
    public UpdateWorkspaceValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("General:Fields:Required");
        RuleFor(x => x.Name).NotEmpty().WithMessage("General:Fields:Required")
            .MaximumLength(WorkspacesFieldDefinitions.MaxNameLength).WithMessage("General:Fields:MaxLength");
        RuleFor(x => x.Description).MaximumLength(WorkspacesFieldDefinitions.MaxDescriptionLength).WithMessage("General:Fields:MaxLength");
        RuleFor(x => x.Icon).MaximumLength(WorkspacesFieldDefinitions.MaxIconLength).WithMessage("General:Fields:MaxLength");
        RuleFor(x => x.Color).Must(c => c is null || WorkspacesFieldDefinitions.Colors.Contains(c)).WithMessage(WorkspacesErrors.ColorInvalid);
    }
}

public sealed class UpdateAgentInstructionsValidator : AbstractValidator<UpdateAgentInstructionsDto>
{
    public UpdateAgentInstructionsValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("General:Fields:Required");
        RuleFor(x => x.AgentInstructions).MaximumLength(WorkspacesFieldDefinitions.MaxInstructionsLength).WithMessage("General:Fields:MaxLength");
    }
}

public sealed class SetWorkspaceMemberValidator : AbstractValidator<SetWorkspaceMemberDto>
{
    public SetWorkspaceMemberValidator()
    {
        RuleFor(x => x.WorkspaceId).NotEmpty().WithMessage("General:Fields:Required");
        RuleFor(x => x.UserId).NotEmpty().WithMessage("General:Fields:Required");
        RuleFor(x => x.DisplayName).NotEmpty().WithMessage("General:Fields:Required")
            .MaximumLength(WorkspacesFieldDefinitions.MaxDisplayNameLength).WithMessage("General:Fields:MaxLength");
        RuleFor(x => x.Role).IsInEnum().WithMessage("General:Fields:Invalid");
    }
}
