namespace TeachHub.Domain.Grading;

/// <summary>Quy tắc gộp điểm TRONG một cột. Chọn từ danh sách, không phải công thức gõ tay.</summary>
public enum AggregationKind
{
    Manual,            // nhập tay hoàn toàn, không tự tổng hợp
    Average,           // trung bình cộng các nguồn
    WeightedAverage,   // dùng GradeColumnSource.WeightWithinColumn
    Sum,               // cộng dồn rồi kẹp [0, 10]
    Max,
    Min,
    DropLowestN,       // bỏ N điểm thấp nhất rồi trung bình
    BestNOfM,          // lấy N bài tốt nhất rồi trung bình
}

/// <summary>Lưu jsonb trên GradeColumn. Không có biểu thức tự do nên không thể gõ sai cú pháp.</summary>
public sealed class GradeAggregation
{
    public AggregationKind Kind { get; set; } = AggregationKind.Average;

    /// <summary>Số bài bỏ đi (DropLowestN) hoặc số bài lấy (BestNOfM). Các Kind khác bỏ qua.</summary>
    public int N { get; set; }

    /// <summary>Bài chưa chấm tính là 0 (mặc định, đúng về mặt cuối kỳ) hay bị loại khỏi mẫu số.</summary>
    public bool TreatMissingAsZero { get; set; } = true;
}

/// <summary>
/// Một nguồn điểm ĐÃ LẤY VỀ cho một sinh viên. <paramref name="SourceMaxScore"/> là thang điểm
/// gốc của nguồn (rubric tổng 20 điểm thì là 20) — bắt buộc phải có để chuẩn hóa.
/// </summary>
public sealed record SourceScore(decimal? RawScore, decimal SourceMaxScore, decimal? WeightWithinColumn = null);

/// <summary>
/// Hàm thuần, không I/O. Toàn bộ quy tắc gộp điểm trong một cột nằm ở đây.
/// MỌI nguồn được quy về thang 10 TRƯỚC khi gộp — bài rubric thang 20 được 16 thì vào cột là 8.0,
/// không phải 16 rồi bị kẹp thành 10.
/// </summary>
public static class GradeAggregator
{
    public static decimal Apply(GradeAggregation rule, IReadOnlyList<SourceScore> sources, decimal maxScore = 10m)
    {
        // Cột Manual không đi qua đây trong luồng thật — điểm lấy thẳng từ GradeRecord.
        // Trả 0 để nếu ai đó gọi nhầm thì thấy 0 rõ ràng, chứ không phải một con số trông có vẻ đúng.
        if (rule.Kind == AggregationKind.Manual) return 0m;

        var items = ChuanHoa(rule, sources, maxScore);
        if (items.Count == 0) return 0m;                 // không nguồn nào → 0, KHÔNG chia cho 0

        var giaTri = items.Select(i => i.Value).ToList();

        var ketQua = rule.Kind switch
        {
            AggregationKind.Average => giaTri.Sum() / giaTri.Count,
            AggregationKind.WeightedAverage => TrungBinhCoTrongSo(items),
            AggregationKind.Sum => giaTri.Sum(),
            AggregationKind.Max => giaTri.Max(),
            AggregationKind.Min => giaTri.Min(),
            AggregationKind.DropLowestN => BoNThapNhat(giaTri, rule.N),
            AggregationKind.BestNOfM => LayNTotNhat(giaTri, rule.N),
            _ => 0m,
        };

        return Math.Clamp(ketQua, 0m, maxScore);
    }

    /// <summary>
    /// Quy mọi nguồn về thang 10. Bài tập có MaxScore riêng (rubric tổng có thể là 20):
    /// gộp thẳng điểm thô rồi kẹp ở 10 sẽ cho điểm sai theo hướng nới tay.
    /// </summary>
    private static List<(decimal Value, decimal Weight)> ChuanHoa(
        GradeAggregation rule, IReadOnlyList<SourceScore> sources, decimal maxScore)
    {
        var ketQua = new List<(decimal Value, decimal Weight)>(sources.Count);

        foreach (var nguon in sources)
        {
            var trongSo = nguon.WeightWithinColumn ?? 1m;

            if (nguon.RawScore is not { } tho)
            {
                if (rule.TreatMissingAsZero) ketQua.Add((0m, trongSo));
                continue;                                 // false → loại khỏi cả tử lẫn mẫu
            }

            var tranNguon = nguon.SourceMaxScore > 0m ? nguon.SourceMaxScore : maxScore;
            ketQua.Add((Math.Clamp(tho / tranNguon * maxScore, 0m, maxScore), trongSo));
        }

        return ketQua;
    }

    private static decimal TrungBinhCoTrongSo(List<(decimal Value, decimal Weight)> items)
    {
        var tongTrongSo = items.Sum(i => i.Weight);

        return tongTrongSo <= 0m
            ? items.Sum(i => i.Value) / items.Count       // trọng số để trống hết = chia đều
            : items.Sum(i => i.Value * i.Weight) / tongTrongSo;
    }

    private static decimal BoNThapNhat(List<decimal> giaTri, int n)
    {
        // Giữ lại ít nhất 1 bài: N ≥ số nguồn KHÔNG được bỏ sạch rồi chia cho 0.
        var bo = Math.Clamp(n, 0, giaTri.Count - 1);
        var conLai = giaTri.Order().Skip(bo).ToList();
        return conLai.Sum() / conLai.Count;
    }

    private static decimal LayNTotNhat(List<decimal> giaTri, int n)
    {
        var lay = Math.Clamp(n, 1, giaTri.Count);
        var top = giaTri.OrderDescending().Take(lay).ToList();
        return top.Sum() / top.Count;
    }
}
