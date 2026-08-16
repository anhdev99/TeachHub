using Shouldly;
using TeachHub.Domain.Common;

namespace TeachHub.Domain.Tests;

public class EntityTests
{
    private sealed class Sample : Entity;

    [Fact]
    public void Newly_created_id_is_a_uuid_v7()
    {
        // The UUID version nibble sits in byte 7 (little-endian .NET layout), with value 7.
        var id = new Sample().Id;
        id.Version.ShouldBe(7);
    }

    [Fact]
    public void Id_increases_over_time_in_the_byte_order_postgres_uses()
    {
        // Postgres sorts uuid values by big-endian bytes. If Id does not increase in that order,
        // the B-tree index fragments as the submissions table grows.
        var t0 = new DateTimeOffset(2026, 8, 13, 0, 0, 0, TimeSpan.Zero);
        var earlier = Guid.CreateVersion7(t0);
        var later   = Guid.CreateVersion7(t0.AddMinutes(1));

        static string Hex(Guid g) => Convert.ToHexString(g.ToByteArray(bigEndian: true));

        string.CompareOrdinal(Hex(earlier), Hex(later)).ShouldBeLessThan(0);
    }

    [Fact]
    public void Two_entities_never_share_an_id()
    {
        var ids = Enumerable.Range(0, 10_000).Select(_ => new Sample().Id).ToHashSet();
        ids.Count.ShouldBe(10_000);
    }
}
