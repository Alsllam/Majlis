using Majlis.Framework.Domain.Exceptions;
using Majlis.Meetings.Domain.Entities;
using Majlis.Meetings.Domain.Enums;

namespace Majlis.Meetings.Tests.Domain;

public class MeetingTests
{
    private static Meeting English()
    {
        var meeting = new Meeting(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Weekly sync", new DateTime(2026, 10, 5, 9, 0, 0, DateTimeKind.Utc), "en", [" Sara ", "Khalid", "Sara"], MeetingSource.Transcript);
        meeting.SetText("Transcript");
        return meeting;
    }

    [Fact]
    public void ToMarkdown_ShouldRenderEnglishHeadings_WhenTheLanguageIsEnglish()
    {
        var meeting = English();
        meeting.ReceiveMinutes("We agreed.", ["Ship it"], [], [("Write the doc", "Sara", new DateOnly(2026, 10, 12)), ("  ", null, null)], "en", DateTime.UtcNow);

        var markdown = meeting.ToMarkdown();

        Assert.Equal(["Sara", "Khalid"], meeting.Attendees);
        Assert.Contains("Attendees: Sara، Khalid", markdown, StringComparison.Ordinal);
        Assert.Contains("## Summary\n\nWe agreed.", markdown, StringComparison.Ordinal);
        Assert.Contains("## Action items\n\n- Write the doc — Sara (2026-10-12)\n", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("Open questions", markdown, StringComparison.Ordinal);
        Assert.Single(meeting.Minutes!.ActionItems);
    }

    [Fact]
    public void StartSummarizing_ShouldRefuse_WhenThereIsNoTranscript()
    {
        var meeting = new Meeting(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "x", DateTime.UtcNow, "ar", [], MeetingSource.Recording);
        Assert.Throws<CustomValidationException>(meeting.StartSummarizing);
        Assert.Throws<CustomValidationException>(meeting.RequireMinutes);
    }

    [Fact]
    public void FailMinutes_ShouldKeepTheTranscript()
    {
        var meeting = English();
        meeting.StartSummarizing();
        meeting.FailMinutes("Meetings:Minutes:ModelError");
        Assert.Equal((MeetingStatus.Transcribed, "Transcript"), (meeting.Status, meeting.TranscriptText));
    }
}
