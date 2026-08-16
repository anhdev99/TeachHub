using System.Security.Cryptography;
using System.Text;

namespace TeachHub.Domain.Grading;

/// <summary>
/// Hashes the contents of a gradebook snapshot. A pure function: the same string in gives the
/// same string out, on every machine and on every run — that is the entire value of
/// <see cref="GradeSnapshot.PayloadHash"/>.
/// </summary>
public static class GradeSnapshotHash
{
    /// <summary>
    /// SHA-256 as LOWERCASE hex, exactly 64 characters.
    ///
    /// The encoding is hard-pinned to UTF-8 rather than taken from the server default: the
    /// gradebook is full of accented Vietnamese names, and hashing with the machine's encoding
    /// makes one and the same snapshot produce two different hashes on two servers — the
    /// snapshot loses all of its value as a reference and nobody notices.
    /// </summary>
    public static string Compute(string payloadJson) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(payloadJson)));
}
