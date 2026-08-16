namespace TeachHub.Domain.Integrity;

/// <summary>One fingerprint: the hash of a k-gram plus the position of that k-gram in the token sequence.</summary>
public readonly record struct Fingerprint(long Hash, int Position);

/// <summary>
/// The Winnowing algorithm (Schleimer, Wilkerson, Aiken — 2003): picks a small but GUARANTEED
/// subset of the k-grams to stand in for the whole file.
///
/// Why not hash every k-gram: a 500-line submission yields roughly 4000 tokens, that is 4000
/// k-grams. Multiply by 40 students × 8 assignments × several semesters and the
/// <c>source_fingerprints</c> table swells to tens of millions of rows, at which point not even
/// Gap #14 (an index on hash) can rescue the comparison query. Winnowing keeps about 2/(w+1) of
/// the k-grams while still guaranteeing this: any matching stretch of (w + k − 1) tokens or more
/// is CERTAIN to produce at least one shared fingerprint.
///
/// Pure function, no I/O.
/// </summary>
public static class Winnowing
{
    /// <summary>Tokens per k-gram. Raising k cuts false alarms, but misses short copied stretches.</summary>
    public const int DefaultK = 5;

    /// <summary>Size of the window the minimum is picked from.</summary>
    public const int DefaultWindow = 4;

    private const ulong FnvOffset = 0xCBF29CE484222325UL;
    private const ulong FnvPrime = 0x100000001B3UL;

    /// <summary>Base of the polynomial hash. Odd, so multiplication mod 2^64 does not lose the low bits.</summary>
    private const ulong Base = 1_000_003UL;

    /// <summary>
    /// FNV-1a 64-bit. NEVER use <c>string.GetHashCode()</c>: since .NET Core 3.0 it is randomized
    /// per process. Use it and the fingerprints stored today will not match the fingerprints
    /// recomputed after the next restart — a table full of data where every comparison comes out
    /// at 0%, with no exception and no log line to trace it from.
    /// </summary>
    public static long HashToken(string token)
    {
        var h = FnvOffset;

        foreach (var c in token)
        {
            h ^= c;
            h = unchecked(h * FnvPrime);
        }

        return unchecked((long)h);
    }

    /// <summary>Hashes one k-gram directly. Used to cross-check the rolling version in tests.</summary>
    public static long HashKGram(IReadOnlyList<string> kgram)
    {
        ulong h = 0;
        foreach (var t in kgram) h = unchecked(h * Base + (ulong)HashToken(t));
        return unchecked((long)h);
    }

    /// <summary>
    /// Hashes every k-gram with a rolling hash: O(n) instead of O(n·k). Both the subtraction and
    /// the multiplication run mod 2^64 thanks to <c>unchecked</c>, so the result lines up exactly
    /// with <see cref="HashKGram"/>.
    /// </summary>
    public static IReadOnlyList<long> KGramHashes(IReadOnlyList<string> tokens, int k = DefaultK)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        ArgumentOutOfRangeException.ThrowIfLessThan(k, 1);

        if (tokens.Count < k) return [];

        var v = new ulong[tokens.Count];
        for (var i = 0; i < tokens.Count; i++) v[i] = unchecked((ulong)HashToken(tokens[i]));

        ulong power = 1;                                     // Base^(k-1)
        for (var i = 0; i < k - 1; i++) power = unchecked(power * Base);

        var result = new long[tokens.Count - k + 1];

        ulong h = 0;
        for (var i = 0; i < k; i++) h = unchecked(h * Base + v[i]);
        result[0] = unchecked((long)h);

        for (var i = 1; i < result.Length; i++)
        {
            h = unchecked((h - v[i - 1] * power) * Base + v[i + k - 1]);
            result[i] = unchecked((long)h);
        }

        return result;
    }

    /// <summary>
    /// Picks fingerprints with a sliding window: each window contributes its smallest hash; on a
    /// tie, take the RIGHTMOST position. Picking the rightmost is a mandatory detail of the
    /// original algorithm — it makes two adjacent windows usually land on the same k-gram, which
    /// is how the duplicate gets dropped and the fingerprint count comes down. Switch it to
    /// leftmost and the number of rows written to the DB shoots up without any gain in detection.
    /// </summary>
    public static IReadOnlyList<Fingerprint> Compute(
        IReadOnlyList<string> tokens, int k = DefaultK, int w = DefaultWindow)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(w, 1);

        var hashes = KGramHashes(tokens, k);
        if (hashes.Count == 0) return [];

        // A file shorter than one window still has to produce fingerprints. Skip it entirely and
        // every short function becomes invisible to the system, and copying a short function is
        // the easiest remaining way to slip through.
        var window = Math.Min(w, hashes.Count);

        var result = new List<Fingerprint>();
        var selected = -1;

        for (var i = 0; i + window <= hashes.Count; i++)
        {
            var min = i;
            for (var j = i + 1; j < i + window; j++)
                if (hashes[j] <= hashes[min]) min = j;       // <= ⇒ rightmost on a tie

            if (min == selected) continue;

            selected = min;
            result.Add(new Fingerprint(hashes[min], min));
        }

        return result;
    }
}
