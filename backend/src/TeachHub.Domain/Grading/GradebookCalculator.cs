namespace TeachHub.Domain.Grading;

/// <summary>Điểm cộng đi vào đâu: thẳng điểm tổng, hay vào một cột điểm.</summary>
public enum BonusTarget { FinalTotal, GradeColumn }

/// <summary>
/// Một lần cấp điểm cộng của MỘT sinh viên, đã tước phần nghiệp vụ (lý do, người cấp).
/// <c>Id</c> và <c>GrantedAt</c> bắt buộc có mặt: chúng là khóa sắp xếp tất định của BonusLedger.
/// </summary>
public sealed record BonusEntry(
    Guid Id, BonusTarget Target, Guid? ColumnId, decimal Points, DateTimeOffset GrantedAt);

public sealed record GradebookSettings
{
    public BonusTarget DefaultTarget { get; init; } = BonusTarget.GradeColumn;
    public Guid? DefaultColumnId { get; init; }
    public decimal? MaxBonusPerStudent { get; init; }   // null = không trần riêng
    public decimal MaxScore { get; init; } = 10m;
    public int DecimalPlaces { get; init; } = 1;

    public static GradebookSettings Default => new();
}

public sealed record ValidationOutcome(bool IsValid, string? Error);

/// <summary>
/// Kết quả điểm cộng đã CHỐT SỔ một lần: đã sắp xếp tất định và đã áp trần. Chỉ tạo được qua
/// <see cref="Build"/>. Mọi hàm tính điểm nhận Ledger này chứ không nhận danh sách bonus thô —
/// nhờ đó ô sổ điểm và điểm tổng không thể lệch nhau, và hai lần đọc không thể ra hai kết quả.
/// </summary>
public sealed record BonusLedger
{
    public required IReadOnlyDictionary<Guid, decimal> ByColumnId { get; init; }
    public decimal ToFinalTotal { get; init; }

    /// <summary>
    /// Tổng điểm cộng có hiệu lực sau khi áp trần, GỒM CẢ phần gắn vào cột. Dùng cho tooltip
    /// và thống kê — KHÔNG phải số hiện ở cột "Điểm cộng" của sổ điểm.
    ///
    /// Cột "Điểm cộng" ở cả màn hình lẫn file Excel chỉ hiện <see cref="ToFinalTotal"/>: phần
    /// cộng vào cột đã nằm sẵn trong con số của chính cột đó rồi, hiện lại lần nữa là đếm hai
    /// lần, và giảng viên cộng tay để đối chiếu sẽ ra sai.
    /// </summary>
    public decimal TotalGranted { get; init; }

    public static BonusLedger Empty { get; } = new() { ByColumnId = new Dictionary<Guid, decimal>() };

    public decimal ForColumn(Guid columnId) => ByColumnId.TryGetValue(columnId, out var v) ? v : 0m;

    /// <summary>
    /// Sắp xếp theo (GrantedAt, Id) rồi áp trần <c>MaxBonusPerStudent</c> MỘT LẦN, cắt từ lần
    /// cấp mới nhất. Không sắp xếp thì trần cắt theo thứ tự phần tử của danh sách gọi tới —
    /// mà thứ tự đó phụ thuộc câu truy vấn, nên cùng một tập điểm cộng đọc hai lần cho hai
    /// kết quả khác nhau.
    /// </summary>
    public static BonusLedger Build(IEnumerable<BonusEntry> bonuses, GradebookSettings settings)
    {
        var theoThuTu = bonuses
            .Where(b => b.Points > 0m)                 // điểm cộng luôn > 0; phòng dữ liệu cũ
            .OrderBy(b => b.GrantedAt).ThenBy(b => b.Id)
            .ToList();

        var conLai = settings.MaxBonusPerStudent;
        var theoCot = new Dictionary<Guid, decimal>();
        decimal vaoTong = 0m;
        decimal tong = 0m;

        foreach (var b in theoThuTu)
        {
            var diem = b.Points;

            if (conLai is { } tran)
            {
                if (tran <= 0m) break;                 // hết trần: mọi lần cấp sau không còn hiệu lực
                diem = Math.Min(diem, tran);
                conLai = tran - diem;
            }

            tong += diem;

            if (b.Target == BonusTarget.GradeColumn && b.ColumnId is { } cotId)
                theoCot[cotId] = theoCot.TryGetValue(cotId, out var daCo) ? daCo + diem : diem;
            else if (b.Target == BonusTarget.FinalTotal)
                vaoTong += diem;

            // Target = GradeColumn mà ColumnId null là dữ liệu hỏng (CHECK constraint đã chặn
            // ở DB). Bỏ qua trong im lặng thay vì ném ngoại lệ làm hỏng sổ điểm của cả lớp.
        }

        return new BonusLedger { ByColumnId = theoCot, ToFinalTotal = vaoTong, TotalGranted = tong };
    }
}

/// <summary>
/// Toàn bộ phép tính điểm nằm ở đây, thuần túy, không I/O (ADR-006). Điểm cuối KHÔNG lưu cứng:
/// trọng số và quy tắc tổng hợp đổi được giữa kỳ, giá trị lưu cứng sẽ lệch một cách âm thầm.
/// </summary>
public static class GradebookCalculator
{
    public static ValidationOutcome ValidateColumns(IReadOnlyList<GradeColumn> columns)
    {
        if (columns.Count == 0) return new(false, "Chưa có cột điểm nào.");

        var trung = columns.GroupBy(c => c.Key).FirstOrDefault(g => g.Count() > 1);
        if (trung is not null) return new(false, $"Khóa cột “{trung.Key}” bị trùng.");

        var tong = columns.Sum(c => c.WeightPercent);

        return tong == 100m
            ? new(true, null)
            : new(false, $"Tổng trọng số các cột phải bằng 100%, hiện tại là {tong}%.");
    }

    /// <summary>Bước 1 — điểm thô của một cột: sửa tay thì lấy điểm tay, không thì gộp các nguồn.</summary>
    public static decimal RawColumnScore(
        GradeColumn column, bool isManualOverride, decimal manualScore,
        IReadOnlyList<SourceScore> sources, GradebookSettings settings)
        => isManualOverride || column.Aggregation.Kind == AggregationKind.Manual
            ? Math.Clamp(manualScore, 0m, settings.MaxScore)
            : GradeAggregator.Apply(column.Aggregation, sources, settings.MaxScore);

    /// <summary>Bước 3 — điểm một cột SAU khi cộng điểm cộng gắn vào cột đó, kẹp [0, MaxScore].</summary>
    public static decimal ColumnScore(
        GradeColumn column, decimal rawScore, BonusLedger ledger, GradebookSettings settings)
        => Math.Clamp(rawScore + ledger.ForColumn(column.Id), 0m, settings.MaxScore);

    /// <summary>Bước 4–6, lớp không đặt điều kiện điểm sàn.</summary>
    public static decimal FinalScore(
        IReadOnlyList<GradeColumn> columns,
        IReadOnlyDictionary<Guid, decimal> rawColumnScores,
        BonusLedger ledger,
        GradebookSettings settings)
        => FinalScore(columns, rawColumnScores, ledger, [], settings);

    /// <summary>
    /// Bước 4–6. THỨ TỰ LÀ NGHIỆP VỤ, không phải chi tiết cài đặt: cộng điểm cộng vào từng cột
    /// rồi kẹp cột TRƯỚC, nhân trọng số, cộng điểm cộng đích final_total, RỒI mới áp điều kiện
    /// điểm sàn — điểm cộng không được phép kéo sinh viên qua ngưỡng chặn. Và làm tròn đúng
    /// MỘT lần ở bước cuối: làm tròn từng cột rồi cộng lại lệch tới 0.2–0.3 điểm trên lớp 6 cột.
    /// </summary>
    public static decimal FinalScore(
        IReadOnlyList<GradeColumn> columns,
        IReadOnlyDictionary<Guid, decimal> rawColumnScores,
        BonusLedger ledger,
        IReadOnlyList<GradeCondition> conditions,
        GradebookSettings settings)
    {
        if (columns.Count == 0) return 0m;

        var diemCot = new Dictionary<Guid, decimal>(columns.Count);
        decimal tong = 0m;

        foreach (var c in columns)
        {
            var goc = rawColumnScores.TryGetValue(c.Id, out var v) ? v : 0m;   // chưa chấm = 0
            var diem = ColumnScore(c, goc, ledger, settings);
            diemCot[c.Id] = diem;
            tong += diem * c.WeightPercent / 100m;
        }

        // Điểm cộng đích final_total không đi qua trọng số nào. Điểm cộng trỏ tới cột đã bị xóa
        // nằm trong ledger nhưng không cột nào đọc tới — bị bỏ qua, không làm hỏng cả sổ điểm.
        tong += ledger.ToFinalTotal;

        tong = GradeConditionEvaluator.Apply(tong, columns, diemCot, conditions).Score;
        tong = Math.Clamp(tong, 0m, settings.MaxScore);

        // AwayFromZero, KHÔNG phải mặc định ToEven của .NET: 8.25 phải thành 8.3.
        return Math.Round(tong, settings.DecimalPlaces, MidpointRounding.AwayFromZero);
    }
}
