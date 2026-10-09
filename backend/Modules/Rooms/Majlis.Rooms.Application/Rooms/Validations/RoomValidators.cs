using FluentValidation;
using Majlis.Rooms.Application.Rooms.DTOs;
using Majlis.Rooms.Domain.Constants;

namespace Majlis.Rooms.Application.Rooms.Validations;

public sealed class CreateRoomValidator : AbstractValidator<CreateRoomDto>
{
    public CreateRoomValidator()
    {
        RuleFor(x => x.WorkspaceId).NotEmpty().WithMessage("General:Fields:Required");
        RuleFor(x => x.Name).NotEmpty().WithMessage("General:Fields:Required")
            .MaximumLength(RoomsFieldDefinitions.MaxRoomNameLength).WithMessage("General:Fields:MaxLength");
        RuleFor(x => x.Purpose).MaximumLength(RoomsFieldDefinitions.MaxPurposeLength).WithMessage("General:Fields:MaxLength");
        RuleFor(x => x.Visibility).IsInEnum().WithMessage("General:Fields:Invalid");
    }
}

public sealed class AddRoomParticipantValidator : AbstractValidator<AddRoomParticipantDto>
{
    public AddRoomParticipantValidator()
    {
        RuleFor(x => x.RoomId).NotEmpty().WithMessage("General:Fields:Required");
        RuleFor(x => x.UserId).NotEmpty().WithMessage("General:Fields:Required");
        RuleFor(x => x.DisplayName).NotEmpty().WithMessage("General:Fields:Required")
            .MaximumLength(RoomsFieldDefinitions.MaxDisplayNameLength).WithMessage("General:Fields:MaxLength");
        RuleFor(x => x.Role).IsInEnum().WithMessage("General:Fields:Invalid");
    }
}
