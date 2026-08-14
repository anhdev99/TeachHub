using System.Security.Cryptography;

namespace TeachHub.Domain.Teaching;

public static class JoinCode
{
    // Bỏ 0 O 1 I L để không đọc nhầm khi giảng viên đọc to mã trên lớp.
    private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    public static string Generate(int length = 6) =>
        string.Concat(Enumerable.Range(0, length)
            .Select(_ => Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)]));
}
