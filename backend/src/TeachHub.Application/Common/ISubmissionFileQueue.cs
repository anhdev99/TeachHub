namespace TeachHub.Application.Common;

/// <summary>
/// Port for pushing submitted-file processing onto a background worker. It is an interface in
/// the Application tier because Hangfire lives in Infrastructure: calling
/// <c>BackgroundJob.Enqueue</c> directly from a handler would pull Hangfire into this tier and
/// force every handler test to stand up a job server.
/// </summary>
public interface ISubmissionFileQueue
{
    /// <summary>Queues extraction + structure validation onto the dedicated ZIP queue (ADR-005).</summary>
    void EnqueueProcessing(Guid submissionFileId);

    /// <summary>
    /// Queues thumbnail generation for an image file, on the DEFAULT queue — not the "zip"
    /// queue. The zip queue deliberately runs only a few workers so that one malicious archive
    /// cannot monopolise them; pushing thumbnail work in there fills up that very narrow queue
    /// by hand, and the whole class's .zip submissions end up waiting behind a few hundred
    /// images.
    /// </summary>
    void EnqueueThumbnail(Guid submissionFileId);
}
