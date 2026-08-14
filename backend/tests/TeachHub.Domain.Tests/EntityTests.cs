using Shouldly;
using TeachHub.Domain.Common;

namespace TeachHub.Domain.Tests;

public class EntityTests
{
    private sealed class Sample : Entity;

    [Fact]
    public void Id_moi_tao_la_uuid_v7()
    {
        // Nibble version của UUID nằm ở byte thứ 7 (little-endian .NET), giá trị 7.
        var id = new Sample().Id;
        id.Version.ShouldBe(7);
    }

    [Fact]
    public void Id_tang_dan_theo_thoi_gian_o_thu_tu_byte_cua_postgres()
    {
        // Postgres sắp xếp uuid theo byte big-endian. Nếu Id không tăng dần theo
        // thứ tự đó thì index B-tree sẽ phân mảnh khi bảng submissions lớn dần.
        var t0 = new DateTimeOffset(2026, 8, 13, 0, 0, 0, TimeSpan.Zero);
        var truoc = Guid.CreateVersion7(t0);
        var sau   = Guid.CreateVersion7(t0.AddMinutes(1));

        static string Hex(Guid g) => Convert.ToHexString(g.ToByteArray(bigEndian: true));

        string.CompareOrdinal(Hex(truoc), Hex(sau)).ShouldBeLessThan(0);
    }

    [Fact]
    public void Hai_entity_khong_bao_gio_trung_id()
    {
        var ids = Enumerable.Range(0, 10_000).Select(_ => new Sample().Id).ToHashSet();
        ids.Count.ShouldBe(10_000);
    }
}
