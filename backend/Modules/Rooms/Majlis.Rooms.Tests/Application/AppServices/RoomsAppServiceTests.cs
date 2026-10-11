using Majlis.Framework.Domain.Exceptions;
using Majlis.Rooms.Application.Rooms.DTOs;
using Majlis.Rooms.Domain.Constants;
using Majlis.Rooms.Domain.Enums;
using Majlis.Rooms.Tests.Application.TestInfrastructure;
using static Majlis.Rooms.Tests.Application.TestInfrastructure.RoomsTestHost;

namespace Majlis.Rooms.Tests.Application.AppServices;

public class RoomsAppServiceTests : IDisposable
{
    private readonly RoomsTestHost _host = new();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _host.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task CreateAsync_ShouldCreateRoomWithCreatorAsContributor_WhenCallerContributesToWorkspace()
    {
        await _host.SeedMembershipsAsync();
        using var scope = _host.Scope(Khalid);

        var id = await _host.Rooms(scope).CreateAsync(new CreateRoomDto { WorkspaceId = WorkspaceId, Name = "غرفة", Visibility = RoomVisibility.Private }, Ct);

        var room = await _host.Rooms(scope).GetAsync(new RoomIdDto(id), Ct);
        var participant = Assert.Single(room.Participants);
        Assert.Equal(Khalid.Id, participant.UserId);
        Assert.Equal(ParticipantRole.Contributor, participant.Role);
    }

    [Fact]
    public async Task CreateAsync_ShouldThrowForbidden_WhenCallerIsNotAWorkspaceMemberOrOnlyAViewer()
    {
        await _host.SeedMembershipsAsync();

        using (var outsider = _host.Scope(Outsider))
        {
            var ex = await Assert.ThrowsAsync<ForbiddenException>(() => _host.Rooms(outsider).CreateAsync(new CreateRoomDto { WorkspaceId = WorkspaceId, Name = "x" }, Ct));
            Assert.Equal(RoomsErrors.NotWorkspaceMember, ex.MessageKey);
        }

        using var viewer = _host.Scope(Noura);
        await Assert.ThrowsAsync<ForbiddenException>(() => _host.Rooms(viewer).CreateAsync(new CreateRoomDto { WorkspaceId = WorkspaceId, Name = "x" }, Ct));
    }

    [Fact]
    public async Task AddParticipantAsync_ShouldUseMembershipNameAndDowngradeViewers_WhenUserIsAWorkspaceMember()
    {
        var roomId = await _host.SeedRoomAsync();
        using var scope = _host.Scope(Sara);

        await _host.Rooms(scope).AddParticipantAsync(new AddRoomParticipantDto { RoomId = roomId, UserId = Noura.Id, Role = ParticipantRole.Contributor }, Ct);

        var room = await _host.Rooms(scope).GetAsync(new RoomIdDto(roomId), Ct);
        var noura = Assert.Single(room.Participants, p => p.UserId == Noura.Id);
        Assert.Equal(Noura.Name, noura.DisplayName);
        Assert.Equal(ParticipantRole.Observer, noura.Role); // a workspace viewer can only watch
    }

    [Fact]
    public async Task AddParticipantAsync_ShouldThrowValidation_WhenUserIsNotAWorkspaceMember()
    {
        var roomId = await _host.SeedRoomAsync();
        using var scope = _host.Scope(Sara);

        var ex = await Assert.ThrowsAsync<CustomValidationException>(() => _host.Rooms(scope).AddParticipantAsync(new AddRoomParticipantDto { RoomId = roomId, UserId = Outsider.Id }, Ct));
        Assert.Equal(RoomsErrors.UserNotWorkspaceMember, ex.MessageKey);
    }
}
