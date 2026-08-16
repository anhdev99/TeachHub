namespace TeachHub.Application.Common;

/// <summary>
/// Port for pushing source-code fingerprinting onto a background worker. Same reason as
/// <see cref="ISubmissionFileQueue"/>: Hangfire lives in Infrastructure, and calling
/// <c>BackgroundJob.Enqueue</c> directly from a handler would drag it into the Application tier
/// and force every handler test to stand up a job server.
///
/// This has to run in the background rather than on the request thread: a 40 MB archive holding
/// 300 source files takes several seconds to hash, and the NFR "upload responds in under 2
/// seconds" leaves no room to wait.
/// </summary>
public interface ISimilarityQueue
{
    /// <summary>
    /// Queues hashing for one submitted file. Call this AFTER extraction has finished
    /// (<c>ProcessSubmissionFileJob</c>), because for a .zip the directory tree IS the list of
    /// files to hash — run it earlier and <c>FileTree</c> is still null, so the job has no idea
    /// what is inside.
    /// </summary>
    void EnqueueFingerprinting(Guid submissionFileId);
}
