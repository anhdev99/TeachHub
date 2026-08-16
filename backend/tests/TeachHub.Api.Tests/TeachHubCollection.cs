namespace TeachHub.Api.Tests;

/// <summary>
/// Một container Postgres dùng chung cho TOÀN BỘ assembly test, thay vì mỗi test class một cái.
///
/// Với <c>IClassFixture</c>, 16 test class = 16 container Postgres được dựng và huỷ tuần tự;
/// suite đi từ 8 giây (Phase 0) lên 32 giây (Phase 2) và sẽ vượt 90 giây khi Phase 3/4a/4b thêm
/// khoảng 14 class nữa. Một suite chậm là suite người ta bắt đầu bỏ chạy — và vòng lặp TDD
/// của dự án này sống chết bằng việc chạy test thường xuyên.
///
/// Đánh đổi: các class trong cùng collection KHÔNG chạy song song với nhau và dùng chung
/// một database. Vì thế mọi test phải tự sinh dữ liệu riêng (mã môn, email, MSSV đều lấy
/// hậu tố UUID) — điều mà toàn bộ test hiện có đã làm sẵn.
/// </summary>
[CollectionDefinition(Ten)]
public sealed class TeachHubCollection : ICollectionFixture<TeachHubApiFactory>
{
    public const string Ten = "teachhub-api";
}

/// <summary>
/// Collection riêng cho host TẮT SEED: nó cần một database thật sự trống để kiểm bootstrap,
/// nên không dùng chung được với collection ở trên.
/// </summary>
[CollectionDefinition(Ten)]
public sealed class NoSeedCollection : ICollectionFixture<NoSeedApiFactory>
{
    public const string Ten = "teachhub-api-no-seed";
}

/// <summary>Collection riêng cho ma trận cách ly: fixture của nó seed sẵn "thế giới của giảng viên A".</summary>
[CollectionDefinition(Ten)]
public sealed class IsolationCollection : ICollectionFixture<Security.IsolationFixture>
{
    public const string Ten = "teachhub-isolation";
}

/// <summary>
/// Collection riêng cho ma trận cách ly SINH VIÊN ↔ SINH VIÊN: fixture của nó seed hai thế giới
/// sinh viên đối xứng, mỗi thế giới một lớp riêng trên một môn riêng.
///
/// Không dùng chung với <see cref="IsolationCollection"/>: ma trận sinh viên có các dòng GHI
/// chạy dưới danh nghĩa chính chủ (nộp bài, xóa tệp, vào nhóm) và chúng đổi dữ liệu seed — trộn
/// vào fixture của ma trận giảng viên là làm các dòng bên đó đỏ theo thứ tự chạy.
/// </summary>
[CollectionDefinition(Ten)]
public sealed class StudentIsolationCollection : ICollectionFixture<Security.StudentIsolationFixture>
{
    public const string Ten = "teachhub-student-isolation";
}
