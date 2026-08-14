using System.Diagnostics.CodeAnalysis;

namespace TeachHub.Application.Common;

// Invalid → 400 và PayloadTooLarge → 413 là hai trạng thái các phase sau BẮT BUỘC phải có:
// Phase 3 dùng Invalid khi thiếu slot bắt buộc, PayloadTooLarge khi file vượt dung lượng slot.
// Thiếu chúng ở đây thì Phase 3 và 4b không biên dịch được.
public enum ResultStatus { Ok, Invalid, NotFound, Conflict, Forbidden, Unauthorized, PayloadTooLarge }

public sealed class Result
{
    private Result(ResultStatus status, string? error) => (Status, Error) = (status, error);

    public ResultStatus Status { get; }
    public string? Error { get; }
    public bool IsSuccess => Status == ResultStatus.Ok;

    public static Result Ok()                               => new(ResultStatus.Ok, null);
    public static Result Invalid(string error)              => new(ResultStatus.Invalid, error);
    public static Result NotFound(string? error = null)     => new(ResultStatus.NotFound, error);
    public static Result Conflict(string error)             => new(ResultStatus.Conflict, error);
    public static Result Forbidden(string? error = null)    => new(ResultStatus.Forbidden, error);
    public static Result Unauthorized(string? error = null) => new(ResultStatus.Unauthorized, error);
    public static Result PayloadTooLarge(string error)      => new(ResultStatus.PayloadTooLarge, error);
}

public sealed class Result<T>
{
    private Result(ResultStatus status, T? value, string? error) =>
        (Status, Value, Error) = (status, value, error);

    public ResultStatus Status { get; }
    public T? Value { get; }
    public string? Error { get; }

    // [MemberNotNullWhen] là BẮT BUỘC, không phải trang trí. Value khai là T? nên nếu thiếu
    // attribute này, mọi chỗ viết theo khuôn
    //     if (!kq.IsSuccess) return ToActionResult(kq);
    //     return File(kq.Value.Content, ...);
    // đều đứt build với CS8602 vì TreatWarningsAsErrors đang bật.
    [MemberNotNullWhen(true, nameof(Value))]
    public bool IsSuccess => Status == ResultStatus.Ok;

    public static Result<T> Ok(T value)                        => new(ResultStatus.Ok, value, null);
    public static Result<T> Invalid(string error)              => new(ResultStatus.Invalid, default, error);
    public static Result<T> NotFound(string? error = null)     => new(ResultStatus.NotFound, default, error);
    public static Result<T> Conflict(string error)             => new(ResultStatus.Conflict, default, error);
    public static Result<T> Forbidden(string? error = null)    => new(ResultStatus.Forbidden, default, error);
    public static Result<T> Unauthorized(string? error = null) => new(ResultStatus.Unauthorized, default, error);
    public static Result<T> PayloadTooLarge(string error)      => new(ResultStatus.PayloadTooLarge, default, error);
}
