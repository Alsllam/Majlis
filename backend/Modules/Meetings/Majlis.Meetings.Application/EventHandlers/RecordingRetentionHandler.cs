using Majlis.Framework.Application.Storage;
using Majlis.Framework.Domain.Events;
using Majlis.Framework.Domain.Repositories;
using Majlis.Meetings.Domain.Constants;
using Majlis.Meetings.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wolverine.Attributes;

namespace Majlis.Meetings.Application.EventHandlers;

/// <summary>
/// Jobs tick: recordings transcribed longer ago than <c>Meetings:RecordingRetentionDays</c> are deleted from storage
/// (the transcript and the minutes stay). Each meeting is saved on its own, so a failure half-way is resumed next night.
/// </summary>
[WolverineHandler]
public static partial class RecordingRetentionHandler
{
    public static async Task Handle(RecordingRetentionSweepRequested m, IRepository<Meeting, Guid> meetings, IBlobStorage blobs, IUnitOfWork unitOfWork, IOptions<MeetingsOptions> options, ILogger<Meeting> logger, CancellationToken cancellationToken)
    {
        var days = options.Value.RecordingRetentionDays;
        if (days <= 0)
        {
            return;
        }

        var cutoff = m.At.AddDays(-days);
        var due = await meetings.QueryTracked().IgnoreQueryFilters()
            .Where(x => x.RecordingBlobPath != null && x.TranscribedAt != null && x.TranscribedAt <= cutoff)
            .Take(200)
            .ToListAsync(cancellationToken);
        foreach (var meeting in due)
        {
            await blobs.DeleteAsync(MeetingsFieldDefinitions.RecordingsContainer, meeting.RecordingBlobPath!, cancellationToken);
            meeting.ClearRecording();
            await meetings.UpdateAsync(meeting, autoSave: false, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        if (due.Count > 0)
        {
            Log.Purged(logger, due.Count, days);
        }
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 6201, Level = LogLevel.Information, Message = "Deleted {Count} meeting recordings older than {Days} days")]
        public static partial void Purged(ILogger logger, int count, int days);
    }
}
