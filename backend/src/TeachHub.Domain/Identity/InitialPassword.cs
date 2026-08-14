using System.Security.Cryptography;

namespace TeachHub.Domain.Identity;

/// <summary>
/// Mật khẩu ban đầu khi giảng viên tạo tài khoản cho cả lớp. Hiển thị đúng một lần lúc tạo;
/// hệ thống không lưu bản rõ.
///
/// KHÔNG BAO GIỜ suy ra từ MSSV, email hay họ tên. Bản đầu tiên của tính năng này dùng
/// <c>"Th@" + MSSV</c> với lập luận "vô hại vì MustChangePassword buộc đổi ngay" — lập luận đó
/// SAI: cờ đó chỉ chặn SAU khi đã đăng nhập thành công. MSSV nằm ngay trong danh sách lớp mà
/// cả lớp đọc được, nên bất kỳ sinh viên nào cũng đăng nhập được vào tài khoản bạn cùng lớp
/// trước lần đổi mật khẩu đầu tiên, rồi tự đặt mật khẩu mới và chiếm luôn tài khoản đó.
/// </summary>
public static class InitialPassword
{
    // Giảng viên sẽ đọc mật khẩu này to cho sinh viên gõ theo, nên bỏ mọi ký tự dễ nghe/đọc
    // nhầm — cả hoa lẫn thường: O/o lẫn với 0, I/i/L/l lẫn với 1.
    private const string Hoa = "ABCDEFGHJKMNPQRSTUVWXYZ";
    private const string Thuong = "abcdefghjkmnpqrstuvwxyz";
    private const string So = "23456789";
    private const string DacBiet = "@#$%&*";

    private const int DoDai = 14;

    public static string Generate()
    {
        var bang = Hoa + Thuong + So + DacBiet;

        // Bảo đảm có đủ mỗi nhóm một ký tự: Identity mặc định đòi chữ hoa, chữ thường và số.
        // Sinh ra mật khẩu bị chính UserManager từ chối thì tính năng hỏng câm.
        var kyTu = new List<char>
        {
            Hoa[RandomNumberGenerator.GetInt32(Hoa.Length)],
            Thuong[RandomNumberGenerator.GetInt32(Thuong.Length)],
            So[RandomNumberGenerator.GetInt32(So.Length)],
            DacBiet[RandomNumberGenerator.GetInt32(DacBiet.Length)],
        };

        while (kyTu.Count < DoDai)
            kyTu.Add(bang[RandomNumberGenerator.GetInt32(bang.Length)]);

        // Trộn bằng Fisher–Yates với nguồn ngẫu nhiên an toàn: không trộn thì bốn ký tự đầu
        // luôn theo đúng thứ tự hoa-thường-số-đặc biệt và không gian tìm kiếm hẹp lại.
        for (var i = kyTu.Count - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (kyTu[i], kyTu[j]) = (kyTu[j], kyTu[i]);
        }

        return new string([.. kyTu]);
    }
}
