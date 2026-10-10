using FluentValidation;
using Majlis.Tasks.Application.Tasks.DTOs;
using Majlis.Tasks.Domain.Constants;

namespace Majlis.Tasks.Application.Tasks.Validations;

public sealed class CreateTaskValidator : AbstractValidator<CreateTaskDto>
{
    public CreateTaskValidator()
    {
        RuleFor(x => x.WorkspaceId).NotEmpty().WithMessage("General:Fields:Required");
        RuleFor(x => x.Title).NotEmpty().WithMessage("General:Fields:Required")
            .MaximumLength(TasksFieldDefinitions.MaxTitleLength).WithMessage("General:Fields:MaxLength");
        RuleFor(x => x.Description).MaximumLength(TasksFieldDefinitions.MaxDescriptionLength).WithMessage("General:Fields:MaxLength");
        RuleFor(x => x.Priority).IsInEnum().WithMessage("General:Fields:Invalid");
    }
}

public sealed class UpdateTaskValidator : AbstractValidator<UpdateTaskDto>
{
    public UpdateTaskValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("General:Fields:Required");
        RuleFor(x => x.Title).NotEmpty().WithMessage("General:Fields:Required")
            .MaximumLength(TasksFieldDefinitions.MaxTitleLength).WithMessage("General:Fields:MaxLength");
        RuleFor(x => x.Description).MaximumLength(TasksFieldDefinitions.MaxDescriptionLength).WithMessage("General:Fields:MaxLength");
        RuleFor(x => x.Priority).IsInEnum().WithMessage("General:Fields:Invalid");
    }
}

public sealed class SetTaskStatusValidator : AbstractValidator<SetTaskStatusDto>
{
    public SetTaskStatusValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("General:Fields:Required");
        RuleFor(x => x.Status).IsInEnum().WithMessage("General:Fields:Invalid");
    }
}
