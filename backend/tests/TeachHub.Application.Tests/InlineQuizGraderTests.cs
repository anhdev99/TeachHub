using Shouldly;
using TeachHub.Application.Features.Learning;

namespace TeachHub.Application.Tests;

/// <summary>
/// Luật chấm câu hỏi nhúng phải nằm ở ĐÚNG MỘT chỗ, vì nó được đọc từ hai đường: đường trả
/// payload bài học (cờ <c>multi_select</c>) và đường chấm. Hai bản sao chép sẽ lệch nhau đúng
/// vào ngày ai đó thêm một đáp án đúng thứ hai cho một câu — và triệu chứng là câu đó chấm sai
/// cho cả lớp mà không có lỗi nào trong log.
/// </summary>
public class InlineQuizGraderTests
{
    [Fact]
    public void Mot_dap_an_dung_thi_khong_phai_cau_chon_nhieu()
    {
        InlineQuizGrader.IsMultiSelect([1]).ShouldBeFalse();
    }

    [Fact]
    public void Hai_dap_an_dung_tro_len_la_cau_chon_nhieu()
    {
        InlineQuizGrader.IsMultiSelect([0, 2]).ShouldBeTrue();
    }

    [Fact]
    public void Chon_dung_mot_dap_an_thi_dung()
    {
        InlineQuizGrader.IsCorrect([1], [1]).ShouldBeTrue();
    }

    [Fact]
    public void Chon_sai_thi_sai()
    {
        InlineQuizGrader.IsCorrect([1], [0]).ShouldBeFalse();
    }

    [Fact]
    public void Cau_chon_mot_ma_chon_hai_phuong_an_la_SAI_chu_khong_phai_dung_mot_nua()
    {
        // Cùng luật với QuizGrader.ChamMotDapAn của Phase 5: nhận bừa phương án đầu tiên là
        // nới tay cho người chọn hú họa.
        InlineQuizGrader.IsCorrect([1], [0, 1]).ShouldBeFalse();
    }

    [Fact]
    public void Cau_chon_nhieu_phai_chon_DU_moi_dap_an_dung()
    {
        InlineQuizGrader.IsCorrect([0, 2], [0]).ShouldBeFalse();
        InlineQuizGrader.IsCorrect([0, 2], [0, 2]).ShouldBeTrue();
        // Thứ tự gửi lên không có nghĩa: đây là tập hợp, không phải danh sách.
        InlineQuizGrader.IsCorrect([0, 2], [2, 0]).ShouldBeTrue();
    }

    [Fact]
    public void Chon_du_dap_an_dung_nhung_thua_mot_phuong_an_sai_thi_van_SAI()
    {
        InlineQuizGrader.IsCorrect([0, 2], [0, 1, 2]).ShouldBeFalse();
    }

    [Fact]
    public void Khong_tra_loi_thi_sai_chu_khong_no()
    {
        InlineQuizGrader.IsCorrect([1], null).ShouldBeFalse();
        InlineQuizGrader.IsCorrect([1], []).ShouldBeFalse();
    }

    [Fact]
    public void Chon_trung_lap_cung_mot_phuong_an_khong_lam_doi_ket_qua()
    {
        // Client gửi [1,1] là chuyện xảy ra khi người dùng bấm hai lần; đó vẫn là "chọn ô số 1".
        InlineQuizGrader.IsCorrect([1], [1, 1]).ShouldBeTrue();
    }

    [Fact]
    public void Cau_soan_thieu_dap_an_dung_thi_khong_bao_gio_cham_la_dung()
    {
        // Dữ liệu hỏng do soạn thiếu. Chấm "đúng" cho mọi bài làm ở đây là cách âm thầm nhất
        // để một câu hỏi vô nghĩa mở khoá bài kế tiếp.
        InlineQuizGrader.IsCorrect([], []).ShouldBeFalse();
        InlineQuizGrader.IsCorrect([], [0]).ShouldBeFalse();
    }
}
