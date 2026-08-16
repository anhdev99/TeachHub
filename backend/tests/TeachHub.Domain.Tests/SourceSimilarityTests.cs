using Shouldly;
using TeachHub.Domain.Integrity;

namespace TeachHub.Domain.Tests;

/// <summary>
/// Phát hiện trùng lặp mã nguồn (Phase 6a). Toàn bộ giá trị của thuật toán này nằm ở bước
/// CHUẨN HÓA: không bỏ chú thích và không quy mọi tên biến về một token thì chỉ cần đổi tên
/// biến là qua mặt được, và ta có một tính năng tạo cảm giác an toàn giả — tệ hơn hẳn so với
/// không có tính năng nào, vì giảng viên sẽ tin vào con số nó đưa ra.
///
/// Cả file chạy dưới 100 ms, không I/O.
/// </summary>
public class SourceSimilarityTests
{
    // Hai bài Java làm CÙNG một việc bằng CÙNG một cách; bài thứ hai chỉ đổi tên lớp, tên hàm,
    // tên biến, đổi chú thích và gộp dòng. Đây chính là kịch bản chép bài thật ngoài đời.
    private const string JavaGoc = """
        public class Solution {
            // tinh tong mang
            public static int total(int[] numbers) {
                int sum = 0;
                for (int i = 0; i < numbers.length; i++) {
                    sum += numbers[i];
                }
                return sum;
            }
        }
        """;

    private const string JavaDoiTenBien = """
        public class Bai1 {
            /* cong don cac phan tu cua mang
               (viet lai cho de hieu) */
            public static int cong(int[] a) {
                int s = 0;
                for (int j = 0; j < a.length; j++) { s += a[j]; }
                return s;
            }
        }
        """;

    private const string PythonKhacHan = """
        def is_prime(n):
            # kiem tra so nguyen to
            if n < 2:
                return False
            for d in range(2, n):
                if n % d == 0:
                    return False
            return True
        """;

    private static List<long> Van(string nguon) =>
        [.. Winnowing.Compute(SourceNormalizer.Normalize(nguon)).Select(f => f.Hash)];

    // ── Chuẩn hóa ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Doi_ten_bien_va_ten_ham_KHONG_lam_thay_doi_chuoi_token()
    {
        // Đây là bài kiểm tra quan trọng nhất của cả Phase 6a. Nếu nó đỏ thì mọi con số
        // similarity phía sau đều vô nghĩa.
        SourceNormalizer.Normalize(JavaGoc)
            .ShouldBe(SourceNormalizer.Normalize(JavaDoiTenBien));
    }

    [Fact]
    public void Tu_khoa_duoc_giu_nguyen_con_dinh_danh_thanh_mot_token_chung()
    {
        // Quy CẢ từ khóa về một token thì "if (a) b(); else c();" và "int x = y + z;" trông
        // giống nhau tới mức mọi bài đều trùng nhau — cảnh báo nhiều tới mức không ai đọc nữa.
        var token = SourceNormalizer.Normalize("if (soLuong > 0) tong = tong + 1;");

        token.ShouldBe(["if", "(", "V", ">", "N", ")", "V", "=", "V", "+", "N", ";"]);
    }

    [Fact]
    public void Chu_thich_mot_dong_va_nhieu_dong_bi_bo_het()
    {
        SourceNormalizer.Normalize("""
            // chú thích một dòng
            int a; /* chú thích
                      nhiều dòng */ int b;   # chú thích kiểu Python
            """)
            .ShouldBe(["int", "V", ";", "int", "V", ";"]);
    }

    [Fact]
    public void Chuoi_ky_tu_bi_quy_ve_mot_token_chung()
    {
        // Đổi lời chào trong printf là cách né đơn giản nhất mà sinh viên nghĩ ra.
        SourceNormalizer.Normalize("""print("Xin chào");""")
            .ShouldBe(SourceNormalizer.Normalize("""print("Hello world, 2026");"""));
    }

    [Fact]
    public void Chuoi_ba_dau_nhay_cua_Python_khong_lam_vo_bo_tach_token()
    {
        // Quét ngây thơ theo từng dấu nháy kép sẽ hiểu ba dấu nháy thành "chuỗi rỗng + định
        // danh + chuỗi rỗng", và toàn bộ phần thân hàm sau đó bị lệch token.
        var nguon = "def f():\n    \"\"\"tài liệu hàm\"\"\"\n    return 1\n";

        SourceNormalizer.Normalize(nguon)
            .ShouldBe(["def", "V", "(", ")", ":", "S", "return", "N"]);
    }

    [Fact]
    public void So_moi_dinh_dang_deu_quy_ve_mot_token()
    {
        SourceNormalizer.Normalize("x = 0xFF + 3.14e-2 + 100L;")
            .ShouldBe(["V", "=", "N", "+", "N", "+", "N", ";"]);
    }

    [Fact]
    public void Khoang_trang_va_xuong_dong_khong_anh_huong()
    {
        SourceNormalizer.Normalize("int a=1;")
            .ShouldBe(SourceNormalizer.Normalize("int   a\n\t=\n1 ;\n"));
    }

    // ── Winnowing ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Ham_bam_token_KHONG_dung_GetHashCode_cua_runtime()
    {
        // string.GetHashCode() ngẫu nhiên hóa theo tiến trình. Dùng nó thì fingerprint lưu
        // hôm nay không khớp fingerprint tính lại sau lần khởi động sau — bảng đầy dữ liệu
        // mà mọi so sánh đều ra 0%, và không có lỗi nào để lần.
        // Test tính lại FNV-1a 64-bit ĐỘC LẬP rồi so, chứ không gọi lại chính hàm đang kiểm.
        const string token = "for";
        var mong = 0xCBF29CE484222325UL;
        foreach (var c in token)
        {
            mong ^= c;
            mong = unchecked(mong * 0x100000001B3UL);
        }

        Winnowing.HashToken(token).ShouldBe(unchecked((long)mong));
    }

    [Fact]
    public void Bam_hai_lan_cung_dau_vao_ra_cung_ket_qua()
    {
        Van(JavaGoc).ShouldBe(Van(JavaGoc));
    }

    [Fact]
    public void Rolling_hash_khop_voi_cach_tinh_truc_tiep_tung_k_gram()
    {
        var token = SourceNormalizer.Normalize(JavaGoc);

        var lan = Winnowing.KGramHashes(token, 5);
        var thang = Enumerable.Range(0, token.Count - 5 + 1)
            .Select(i => Winnowing.HashKGram(token.Skip(i).Take(5).ToList()))
            .ToList();

        lan.ShouldBe(thang);
    }

    [Fact]
    public void File_rong_khong_sinh_dau_van_tay_nao_va_khong_no()
    {
        Winnowing.Compute([]).ShouldBeEmpty();
        Winnowing.Compute(SourceNormalizer.Normalize("")).ShouldBeEmpty();
        Winnowing.Compute(SourceNormalizer.Normalize("   \n\n  // chỉ có chú thích\n")).ShouldBeEmpty();
    }

    [Fact]
    public void File_ngan_hon_mot_k_gram_khong_sinh_dau_van_tay()
    {
        // 3 token với k = 5: không có k-gram nào. Trả rỗng chứ không được ném.
        Winnowing.Compute(["int", "V", ";"]).ShouldBeEmpty();
    }

    [Fact]
    public void File_it_hon_mot_cua_so_van_chon_duoc_dau_van_tay()
    {
        // 6 token, k = 5 ⇒ 2 k-gram, nhỏ hơn cửa sổ w = 4. Bỏ qua hẳn thì mọi hàm ngắn đều
        // vô hình với hệ thống, và chép một hàm ngắn là cách né dễ nhất còn lại.
        Winnowing.Compute(["int", "V", "=", "N", ";", "}"]).ShouldNotBeEmpty();
    }

    [Fact]
    public void Vi_tri_tra_ve_tro_dung_vao_k_gram_da_chon()
    {
        var token = SourceNormalizer.Normalize(JavaGoc);
        var bam = Winnowing.KGramHashes(token, 5);

        foreach (var f in Winnowing.Compute(token))
        {
            f.Position.ShouldBeInRange(0, bam.Count - 1);
            bam[f.Position].ShouldBe(f.Hash);
        }
    }

    [Fact]
    public void Winnowing_chon_it_hon_han_so_k_gram()
    {
        // Không giảm được số lượng thì bảng source_fingerprints phình bằng đúng số token của
        // cả lớp, và Gap #14 (index trên hash) không cứu nổi.
        var token = SourceNormalizer.Normalize(JavaGoc);

        Winnowing.Compute(token).Count
            .ShouldBeLessThan(Winnowing.KGramHashes(token, 5).Count);
    }

    // ── Chấm điểm trùng lặp ──────────────────────────────────────────────────────────────

    [Fact]
    public void Hai_bai_giong_nhau_chi_khac_ten_bien_bi_cham_trung_rat_cao()
    {
        var kq = SimilarityScorer.Compare(Van(JavaGoc), Van(JavaDoiTenBien));

        kq.ContainmentPercent.ShouldBe(100m);
        kq.JaccardPercent.ShouldBe(100m);
    }

    [Fact]
    public void Hai_bai_khac_han_bi_cham_trung_thap()
    {
        var kq = SimilarityScorer.Compare(Van(JavaGoc), Van(PythonKhacHan));

        kq.ContainmentPercent.ShouldBeLessThan(20m);
    }

    [Fact]
    public void So_sanh_hai_tap_rong_khong_chia_cho_0()
    {
        var kq = SimilarityScorer.Compare([], []);

        kq.ContainmentPercent.ShouldBe(0m);
        kq.JaccardPercent.ShouldBe(0m);
        kq.SharedHashes.ShouldBe(0);
    }

    [Fact]
    public void Mot_ben_rong_thi_khong_trung_gi_ca()
    {
        SimilarityScorer.Compare(Van(JavaGoc), []).ContainmentPercent.ShouldBe(0m);
        SimilarityScorer.Compare([], Van(JavaGoc)).ContainmentPercent.ShouldBe(0m);
    }

    [Fact]
    public void Chep_mot_doan_vao_bai_dai_hon_van_bi_phat_hien_qua_Containment()
    {
        // Jaccard chia cho HỢP của hai tập nên loãng đi khi bài chép dài hơn nhiều bài gốc.
        // Containment chia cho tập NHỎ hơn, nên "chép nguyên hàm rồi viết thêm 300 dòng của
        // mình" vẫn hiện ra — đây là lý do hệ thống báo cáo cả hai con số.
        var daiHon = JavaDoiTenBien + "\n" + PythonKhacHan + "\n" + PythonKhacHan;

        var kq = SimilarityScorer.Compare(Van(JavaGoc), Van(daiHon));

        kq.ContainmentPercent.ShouldBe(100m);
        kq.JaccardPercent.ShouldBeLessThan(kq.ContainmentPercent);
    }

    [Fact]
    public void Vi_tri_trung_dung_de_to_sang_doan_giong_nhau()
    {
        var a = Winnowing.Compute(SourceNormalizer.Normalize(JavaGoc));
        var b = Van(JavaDoiTenBien).ToHashSet();

        SimilarityScorer.MatchedPositions(a, b).Count.ShouldBe(a.Count);
        SimilarityScorer.MatchedPositions(a, []).ShouldBeEmpty();
    }

    // ── Ngưỡng cảnh báo ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Nguong_canh_bao_la_tham_so_chu_khong_phai_hang_so_chon_bua()
    {
        // Bài nhập môn dùng chung starter code trùng 70–90% một cách hoàn toàn chính đáng.
        // Ngưỡng cứng ở 70 sẽ gắn cờ cả lớp ngay tuần đầu.
        SimilarityThresholds.ShouldWarn(85m, 70m).ShouldBeTrue();
        SimilarityThresholds.ShouldWarn(85m, 95m).ShouldBeFalse();
        SimilarityThresholds.ShouldWarn(95m, 95m).ShouldBeTrue();
    }

    [Fact]
    public void Nhan_dien_duoc_tep_ma_nguon_de_khong_bam_ca_anh_va_pdf()
    {
        SourceFileTypes.IsSourceCode("Main.java").ShouldBeTrue();
        SourceFileTypes.IsSourceCode("src/utils/helper.PY").ShouldBeTrue();
        SourceFileTypes.IsSourceCode("bao-cao.pdf").ShouldBeFalse();
        SourceFileTypes.IsSourceCode("anh.png").ShouldBeFalse();
        SourceFileTypes.IsSourceCode("khong-co-duoi").ShouldBeFalse();
    }
}
