using Majlis.Knowledge.Domain.Entities;
using Majlis.Knowledge.Domain.Enums;

namespace Majlis.Knowledge.Tests.Domain;

public class DocumentTests
{
    [Fact]
    public void MarkIndexed_ShouldSupersedeOlderVersions_AndMakeTheNewOneCurrent()
    {
        var document = new Document(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, "عقد", DocumentType.Contract, null, DocumentOrigin.Upload);
        var v1 = document.AddVersion(Guid.NewGuid(), "a.pdf", "application/pdf", 10, "p1");
        v1.Confirm(10, DateTime.UtcNow);
        document.MarkIndexed(v1.Id, 3, "sha1", "ar", 2, DateTime.UtcNow);
        var v2 = document.AddVersion(Guid.NewGuid(), "a.pdf", "application/pdf", 12, "p2");
        v2.Confirm(12, DateTime.UtcNow);

        document.MarkIndexed(v2.Id, 4, "sha2", null, 3, DateTime.UtcNow);

        Assert.Equal(v2.Id, document.CurrentVersionId);
        Assert.Equal(IngestionStatus.Superseded, v1.Status);
        Assert.Equal(IngestionStatus.Indexed, v2.Status);
        Assert.Equal("ar", document.Language);
        Assert.Equal(2, v2.Number);
    }

    [Fact]
    public void AclGroups_ShouldScopeToTheRoom_WhenTheDocumentIsRoomOnly()
    {
        var workspace = Guid.NewGuid();
        var room = Guid.NewGuid();
        Assert.Equal([$"ws:{workspace:D}"], new Document(Guid.NewGuid(), Guid.NewGuid(), workspace, null, "x", DocumentType.Other, null, DocumentOrigin.Upload).AclGroups);
        Assert.Equal([$"room:{room:D}"], new Document(Guid.NewGuid(), Guid.NewGuid(), workspace, room, "x", DocumentType.Other, null, DocumentOrigin.Upload).AclGroups);
    }
}
