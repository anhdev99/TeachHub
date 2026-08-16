namespace TeachHub.Domain.Integrity;

/// <param name="JaccardPercent">
/// |A ∩ B| / |A ∪ B|. The "how alike are these two submissions" number, in the symmetric sense.
/// </param>
/// <param name="ContainmentPercent">
/// |A ∩ B| / min(|A|, |B|). The "how completely does the smaller submission sit inside the other
/// one" number.
/// </param>
public sealed record SimilarityScore(
    int SharedHashes, int TotalA, int TotalB, decimal JaccardPercent, decimal ContainmentPercent);

/// <summary>
/// Scores how much two fingerprint sets overlap. Pure function, no I/O.
///
/// Reports BOTH numbers rather than picking one, because they catch two different behaviours:
/// Jaccard gets diluted when one submission is far longer, so "copy one whole function then write
/// another 300 lines yourself" slips through if you only look at Jaccard. Containment divides by
/// the smaller set and does catch that case, but in exchange it raises false alarms on very short
/// files. Showing only one of the two means either missing cases or flagging innocent ones — and
/// both kinds of error cost the instructor their trust in the warning list.
///
/// Uses SETS (duplicates removed), not multisets: a submission that repeats one stretch ten times
/// must not be allowed to inflate its own similarity score.
/// </summary>
public static class SimilarityScorer
{
    public static SimilarityScore Compare(IEnumerable<long> a, IEnumerable<long> b)
    {
        var setA = new HashSet<long>(a);
        var setB = new HashSet<long>(b);

        // An empty file, a file with nothing but comments, or a file shorter than one k-gram all
        // give an empty set. Return 0 right here instead of dividing by zero below.
        if (setA.Count == 0 || setB.Count == 0)
            return new SimilarityScore(0, setA.Count, setB.Count, 0m, 0m);

        var smaller = setA.Count <= setB.Count ? setA : setB;
        var larger = ReferenceEquals(smaller, setA) ? setB : setA;

        var shared = smaller.Count(h => larger.Contains(h));
        var union = setA.Count + setB.Count - shared;

        return new SimilarityScore(
            shared, setA.Count, setB.Count,
            Percent(shared, union),
            Percent(shared, smaller.Count));
    }

    /// <summary>
    /// The positions of <paramref name="a"/>'s k-grams that also appear in the other submission —
    /// the input to the side-by-side comparison screen. Returns token positions rather than hash
    /// values: a hash value cannot tell you which stretch on screen has to be highlighted.
    /// </summary>
    public static IReadOnlyList<int> MatchedPositions(
        IReadOnlyList<Fingerprint> a, IEnumerable<long> hashesOfOther)
    {
        var other = new HashSet<long>(hashesOfOther);
        return [.. a.Where(f => other.Contains(f.Hash)).Select(f => f.Position)];
    }

    private static decimal Percent(int numerator, int denominator) =>
        denominator <= 0 ? 0m : Math.Round(numerator * 100m / denominator, 2, MidpointRounding.AwayFromZero);
}

/// <summary>
/// The warning threshold. NOT hard-coded in the handler: an introductory assignment built on
/// shared starter code matches 70–90% entirely legitimately, while on a final-semester project
/// 40% is already worth a second look. One fixed threshold for every assignment produces a
/// warning list so long that the instructor stops reading it — losing the feature in the hardest
/// way to notice.
///
/// The value below is only a starting point. The plan calls for calibrating it against real data:
/// run it over last semester's submissions, look at the distribution, and only then pick a
/// threshold per assignment type.
/// </summary>
public static class SimilarityThresholds
{
    public const decimal DefaultWarnPercent = 60m;

    /// <summary>
    /// Below this many fingerprints, DO NOT report at all. A ten-line file produces only a handful
    /// of fingerprints, and two ten-line files that both print "Hello World" will match 100% in a
    /// way that means nothing.
    /// </summary>
    public const int MinFingerprints = 8;

    public static bool ShouldWarn(decimal percent, decimal threshold) => percent >= threshold;
}
