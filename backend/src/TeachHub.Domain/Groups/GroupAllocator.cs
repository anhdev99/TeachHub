namespace TeachHub.Domain.Groups;

public static class GroupAllocator
{
    /// <summary>
    /// Chia đều nhất có thể. Phần dư được rải sang các nhóm sẵn có thay vì gom thành một
    /// nhóm lẻ loi — 13 sinh viên nhóm 4 phải ra 4/3/3/3 chứ không phải 4/4/4/1, vì một
    /// sinh viên làm bài tập nhóm một mình là lỗi cấu hình chứ không phải kết quả hợp lệ.
    ///
    /// <paramref name="seed"/> cố định để "xem trước" và "xác nhận" cho cùng kết quả:
    /// giảng viên bấm chia ngẫu nhiên, nhìn danh sách, rồi mới lưu — kết quả không được
    /// đổi giữa hai lần đó.
    /// </summary>
    public static List<List<Guid>> Allocate(IReadOnlyList<Guid> enrollmentIds, int groupSize, int seed)
    {
        if (enrollmentIds.Count == 0) return [];
        if (groupSize < 1) throw new ArgumentOutOfRangeException(nameof(groupSize));

        var xao = enrollmentIds.ToList();
        var rng = new Random(seed);

        for (var i = xao.Count - 1; i > 0; i--)          // Fisher–Yates
        {
            var j = rng.Next(i + 1);
            (xao[i], xao[j]) = (xao[j], xao[i]);
        }

        // Làm tròn XUỐNG số nhóm: thà một nhóm đông hơn một người còn hơn để lại nhóm thiếu.
        var soNhom = Math.Max(1, xao.Count / groupSize);
        var nhom = Enumerable.Range(0, soNhom).Select(_ => new List<Guid>()).ToList();

        // Chia vòng tròn: chênh lệch giữa nhóm lớn nhất và nhỏ nhất luôn ≤ 1.
        for (var i = 0; i < xao.Count; i++) nhom[i % soNhom].Add(xao[i]);

        return nhom;
    }
}
