namespace TeachHub.Domain.Groups;

public static class GroupAllocator
{
    /// <summary>
    /// Splits as evenly as possible. The remainder is spread across the existing groups instead
    /// of being gathered into one lonely group — 13 students at a group size of 4 must come out
    /// as 4/3/3/3 and not as 4/4/4/1, because one student doing a group assignment alone is a
    /// configuration bug, not a valid result.
    ///
    /// <paramref name="seed"/> is fixed so that "preview" and "confirm" produce the same result:
    /// the instructor presses shuffle, looks at the list, and only then saves — the outcome must
    /// not change between those two moments.
    /// </summary>
    public static List<List<Guid>> Allocate(IReadOnlyList<Guid> enrollmentIds, int groupSize, int seed)
    {
        if (enrollmentIds.Count == 0) return [];
        if (groupSize < 1) throw new ArgumentOutOfRangeException(nameof(groupSize));

        var shuffled = enrollmentIds.ToList();
        var rng = new Random(seed);

        for (var i = shuffled.Count - 1; i > 0; i--)    // Fisher–Yates
        {
            var j = rng.Next(i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }

        // Round the group count DOWN: better one group with an extra member than a group left short.
        var groupCount = Math.Max(1, shuffled.Count / groupSize);
        var groups = Enumerable.Range(0, groupCount).Select(_ => new List<Guid>()).ToList();

        // Deal round-robin: the gap between the largest and smallest group is always ≤ 1.
        for (var i = 0; i < shuffled.Count; i++) groups[i % groupCount].Add(shuffled[i]);

        return groups;
    }
}
