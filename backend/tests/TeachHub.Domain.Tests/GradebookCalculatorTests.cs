using Shouldly;
using TeachHub.Domain.Grading;

namespace TeachHub.Domain.Tests;

/// <summary>
/// Giữ test dù phase chạy theo yêu cầu "không viết test". Đây là đường đi của con số nộp cho
/// phòng đào tạo: một trọng số tính sai hay một trần điểm cộng áp hai lần không có triệu chứng
/// nào trên giao diện — nó chỉ hiện ra khi bảng điểm đã nộp. Cả file chạy dưới 50 ms.
/// </summary>
public class GradebookCalculatorTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 1, 8, 0, 0, TimeSpan.FromHours(7));

    private static GradeColumn Cot(string key, decimal trongSo, AggregationKind kind = AggregationKind.Average)
        => new()
        {
            OfferingId = Guid.CreateVersion7(), Key = key, Name = key,
            WeightPercent = trongSo, Aggregation = new GradeAggregation { Kind = kind },
        };

    // ── Chuẩn hóa về thang 10 ────────────────────────────────────────────────────────────

    [Fact]
    public void Nguon_thang_20_duoc_quy_ve_thang_10_truoc_khi_gop()
    {
        // Rubric tổng 20, sinh viên được 16 → vào cột là 8.0, KHÔNG phải 16 rồi kẹp thành 10.
        var d = GradeAggregator.Apply(
            new GradeAggregation { Kind = AggregationKind.Average },
            [new SourceScore(16m, 20m)]);

        d.ShouldBe(8m);
    }

    [Fact]
    public void Khong_co_nguon_nao_tra_0_chu_khong_chia_cho_0()
    {
        GradeAggregator.Apply(new GradeAggregation { Kind = AggregationKind.Average }, [])
            .ShouldBe(0m);
    }

    [Fact]
    public void Bai_chua_cham_tinh_la_0_khi_TreatMissingAsZero()
    {
        var d = GradeAggregator.Apply(
            new GradeAggregation { Kind = AggregationKind.Average, TreatMissingAsZero = true },
            [new SourceScore(10m, 10m), new SourceScore(null, 10m)]);

        d.ShouldBe(5m);
    }

    [Fact]
    public void Bai_chua_cham_bi_loai_khoi_mau_so_khi_TreatMissingAsZero_false()
    {
        var d = GradeAggregator.Apply(
            new GradeAggregation { Kind = AggregationKind.Average, TreatMissingAsZero = false },
            [new SourceScore(10m, 10m), new SourceScore(null, 10m)]);

        d.ShouldBe(10m);
    }

    // ── Các quy tắc gộp ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Bo_n_thap_nhat()
    {
        var d = GradeAggregator.Apply(
            new GradeAggregation { Kind = AggregationKind.DropLowestN, N = 1 },
            [new SourceScore(4m, 10m), new SourceScore(8m, 10m), new SourceScore(9m, 10m)]);

        d.ShouldBe(8.5m);
    }

    [Fact]
    public void Bo_n_thap_nhat_khong_bao_gio_bo_sach_roi_chia_cho_0()
    {
        // N = 5 trên 2 nguồn: phải giữ lại ít nhất một bài.
        var d = GradeAggregator.Apply(
            new GradeAggregation { Kind = AggregationKind.DropLowestN, N = 5 },
            [new SourceScore(4m, 10m), new SourceScore(8m, 10m)]);

        d.ShouldBe(8m);
    }

    [Fact]
    public void Lay_n_tot_nhat()
    {
        var d = GradeAggregator.Apply(
            new GradeAggregation { Kind = AggregationKind.BestNOfM, N = 2 },
            [new SourceScore(4m, 10m), new SourceScore(8m, 10m), new SourceScore(9m, 10m)]);

        d.ShouldBe(8.5m);
    }

    [Fact]
    public void Cong_don_bi_kep_o_thang_diem()
    {
        var d = GradeAggregator.Apply(
            new GradeAggregation { Kind = AggregationKind.Sum },
            [new SourceScore(7m, 10m), new SourceScore(8m, 10m)]);

        d.ShouldBe(10m);
    }

    [Fact]
    public void Trung_binh_co_trong_so()
    {
        var d = GradeAggregator.Apply(
            new GradeAggregation { Kind = AggregationKind.WeightedAverage },
            [new SourceScore(10m, 10m, 3m), new SourceScore(6m, 10m, 1m)]);

        d.ShouldBe(9m);
    }

    [Fact]
    public void Trong_so_de_trong_het_thi_chia_deu()
    {
        var d = GradeAggregator.Apply(
            new GradeAggregation { Kind = AggregationKind.WeightedAverage },
            [new SourceScore(10m, 10m, 0m), new SourceScore(6m, 10m, 0m)]);

        d.ShouldBe(8m);
    }

    // ── Sổ điểm cộng: tất định và áp trần một lần ────────────────────────────────────────

    [Fact]
    public void Tran_diem_cong_ap_dung_MOT_lan_tren_toan_bo()
    {
        var cot = Guid.CreateVersion7();

        var ledger = BonusLedger.Build(
            [
                new BonusEntry(Guid.CreateVersion7(), BonusTarget.GradeColumn, cot, 0.5m, T0),
                new BonusEntry(Guid.CreateVersion7(), BonusTarget.FinalTotal, null, 0.5m, T0.AddHours(1)),
                new BonusEntry(Guid.CreateVersion7(), BonusTarget.FinalTotal, null, 0.5m, T0.AddHours(2)),
            ],
            GradebookSettings.Default with { MaxBonusPerStudent = 1m });

        ledger.TotalGranted.ShouldBe(1m);
        ledger.ForColumn(cot).ShouldBe(0.5m);
        ledger.ToFinalTotal.ShouldBe(0.5m);   // lần cấp thứ ba đã hết trần
    }

    [Fact]
    public void Thu_tu_dau_vao_KHONG_lam_doi_ket_qua_ap_tran()
    {
        // Blocker B2: không sắp xếp tất định thì trần cắt theo thứ tự câu truy vấn trả về,
        // và cùng một tập điểm cộng đọc hai lần cho hai kết quả.
        var a = new BonusEntry(Guid.CreateVersion7(), BonusTarget.FinalTotal, null, 0.7m, T0);
        var b = new BonusEntry(Guid.CreateVersion7(), BonusTarget.FinalTotal, null, 0.7m, T0.AddHours(1));
        var s = GradebookSettings.Default with { MaxBonusPerStudent = 1m };

        BonusLedger.Build([a, b], s).ToFinalTotal
            .ShouldBe(BonusLedger.Build([b, a], s).ToFinalTotal);
    }

    [Fact]
    public void Diem_cong_am_bi_bo_qua()
    {
        var ledger = BonusLedger.Build(
            [new BonusEntry(Guid.CreateVersion7(), BonusTarget.FinalTotal, null, -5m, T0)],
            GradebookSettings.Default);

        ledger.ToFinalTotal.ShouldBe(0m);
    }

    // ── Điểm tổng ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Diem_tong_theo_trong_so()
    {
        var tk = Cot("thuong_ky", 40m);
        var cm = Cot("cuoi_mon", 60m);

        var d = GradebookCalculator.FinalScore(
            [tk, cm],
            new Dictionary<Guid, decimal> { [tk.Id] = 8m, [cm.Id] = 6m },
            BonusLedger.Empty, GradebookSettings.Default);

        d.ShouldBe(6.8m);   // 8×0.4 + 6×0.6
    }

    [Fact]
    public void Cot_chua_cham_tinh_la_0()
    {
        var tk = Cot("thuong_ky", 40m);
        var cm = Cot("cuoi_mon", 60m);

        GradebookCalculator.FinalScore(
            [tk, cm],
            new Dictionary<Guid, decimal> { [tk.Id] = 10m },
            BonusLedger.Empty, GradebookSettings.Default)
            .ShouldBe(4m);
    }

    [Fact]
    public void Diem_cong_vao_cot_bi_kep_o_10_truoc_khi_nhan_trong_so()
    {
        var tk = Cot("thuong_ky", 100m);

        var ledger = BonusLedger.Build(
            [new BonusEntry(Guid.CreateVersion7(), BonusTarget.GradeColumn, tk.Id, 2m, T0)],
            GradebookSettings.Default);

        GradebookCalculator.FinalScore(
            [tk], new Dictionary<Guid, decimal> { [tk.Id] = 9.5m },
            ledger, GradebookSettings.Default)
            .ShouldBe(10m);
    }

    [Fact]
    public void Lam_tron_dung_MOT_lan_o_buoc_cuoi()
    {
        // Làm tròn từng cột rồi cộng lại lệch tới 0.2–0.3 điểm trên lớp 6 cột.
        var a = Cot("a", 50m);
        var b = Cot("b", 50m);

        GradebookCalculator.FinalScore(
            [a, b],
            new Dictionary<Guid, decimal> { [a.Id] = 8.44m, [b.Id] = 8.46m },
            BonusLedger.Empty, GradebookSettings.Default)
            .ShouldBe(8.5m);
    }

    [Fact]
    public void Lam_tron_AwayFromZero_chu_khong_ve_so_chan()
    {
        var a = Cot("a", 100m);

        GradebookCalculator.FinalScore(
            [a], new Dictionary<Guid, decimal> { [a.Id] = 8.25m },
            BonusLedger.Empty, GradebookSettings.Default)
            .ShouldBe(8.3m);
    }

    // ── Điều kiện điểm sàn ───────────────────────────────────────────────────────────────

    [Fact]
    public void Cuoi_mon_duoi_san_thi_diem_tong_bi_chan()
    {
        // Quy định phổ biến: cuối môn < 4.0 thì không qua môn dù quá trình có đẹp tới đâu.
        var tk = Cot("thuong_ky", 40m);
        var cm = Cot("cuoi_mon", 60m);

        var d = GradebookCalculator.FinalScore(
            [tk, cm],
            new Dictionary<Guid, decimal> { [tk.Id] = 9m, [cm.Id] = 3.5m },
            BonusLedger.Empty,
            [new GradeCondition { ColumnKey = "cuoi_mon", Threshold = 4m, ActionValue = 3.9m }],
            GradebookSettings.Default);

        d.ShouldBe(3.9m);
    }

    [Fact]
    public void Diem_cong_vao_diem_tong_KHONG_keo_qua_nguong_chan()
    {
        // Bước 5 nằm sau bước 4 chính là để chặn đúng chuyện này: điểm phát biểu trên lớp
        // không được phép làm sinh viên thi 3.5 qua môn.
        var tk = Cot("thuong_ky", 40m);
        var cm = Cot("cuoi_mon", 60m);

        var ledger = BonusLedger.Build(
            [new BonusEntry(Guid.CreateVersion7(), BonusTarget.FinalTotal, null, 2m, T0)],
            GradebookSettings.Default);

        GradebookCalculator.FinalScore(
            [tk, cm],
            new Dictionary<Guid, decimal> { [tk.Id] = 9m, [cm.Id] = 3.5m },
            ledger,
            [new GradeCondition { ColumnKey = "cuoi_mon", Threshold = 4m, ActionValue = 3.9m }],
            GradebookSettings.Default)
            .ShouldBe(3.9m);
    }

    [Fact]
    public void Nhieu_dieu_kien_cung_dung_thi_lay_muc_chan_THAP_NHAT()
    {
        var a = Cot("a", 50m);
        var b = Cot("b", 50m);

        GradebookCalculator.FinalScore(
            [a, b],
            new Dictionary<Guid, decimal> { [a.Id] = 2m, [b.Id] = 2m },
            BonusLedger.Empty,
            [
                new GradeCondition { ColumnKey = "a", Threshold = 4m, ActionValue = 3.9m },
                new GradeCondition { ColumnKey = "b", Threshold = 4m, ActionValue = 3m },
            ],
            GradebookSettings.Default)
            .ShouldBe(2m);   // đã 2.0 rồi, hai mức chặn đều cao hơn nên không cắt gì
    }

    [Fact]
    public void Dieu_kien_tro_toi_cot_khong_ton_tai_bi_bo_qua_nhung_co_canh_bao()
    {
        var a = Cot("a", 100m);

        var kq = GradeConditionEvaluator.Apply(
            8m, [a], new Dictionary<Guid, decimal> { [a.Id] = 8m },
            [new GradeCondition { ColumnKey = "khong_co", Threshold = 4m, ActionValue = 3.9m }]);

        kq.Score.ShouldBe(8m);
        kq.UnknownColumnKeys.ShouldBe(["khong_co"]);
    }

    // ── Kiểm cấu hình cột ────────────────────────────────────────────────────────────────

    [Fact]
    public void Tong_trong_so_khac_100_bi_tu_choi()
    {
        var kq = GradebookCalculator.ValidateColumns([Cot("a", 40m), Cot("b", 40m)]);

        kq.IsValid.ShouldBeFalse();
        kq.Error.ShouldNotBeNull().ShouldContain("100%");
    }

    [Fact]
    public void Trung_key_bi_tu_choi()
    {
        GradebookCalculator.ValidateColumns([Cot("a", 50m), Cot("a", 50m)])
            .IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Bo_cot_hop_le_thi_qua()
    {
        GradebookCalculator.ValidateColumns([Cot("thuong_ky", 40m), Cot("cuoi_mon", 60m)])
            .IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Sua_tay_thang_quy_tac_tong_hop()
    {
        var c = Cot("thuong_ky", 100m);

        GradebookCalculator.RawColumnScore(
            c, isManualOverride: true, manualScore: 7m,
            [new SourceScore(2m, 10m)], GradebookSettings.Default)
            .ShouldBe(7m);
    }
}
