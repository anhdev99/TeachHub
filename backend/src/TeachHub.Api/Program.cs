using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.RateLimiting;
using Scalar.AspNetCore;
using Serilog;
using TeachHub.Api.Common;
using TeachHub.Application;
using TeachHub.Application.Common;
using TeachHub.Infrastructure;
using TeachHub.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration));

// TimeProvider tiêm khắp nơi: mọi logic phụ thuộc thời gian (hạn nộp, phạt trễ,
// streak, lịch ôn tập) phải test được mà không cần Thread.Sleep.
builder.Services.AddSingleton(TimeProvider.System);

// PHẢI CẤU HÌNH CẢ HAI CHỖ — mỗi cái lo một nửa, thiếu cái nào cũng hỏng một nửa:
//  · AddJsonOptions            → cách MVC serialize phản hồi THẬT lúc chạy
//  · ConfigureHttpJsonOptions  → cách Microsoft.AspNetCore.OpenApi sinh SCHEMA trong /openapi/v1.json
// Chúng đọc hai IOptions khác nhau và KHÔNG kế thừa nhau.
builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        // snake_case để khớp đúng hợp đồng API trong SRS mục 7
        o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
        o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
});

builder.Services.AddProblemDetails();          // RFC 9457
builder.Services.AddExceptionHandler<ValidationExceptionHandler>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddApplication();
builder.Services.AddOpenApi();                 // sinh OpenAPI 3.1, .NET 10 đọc được cả Controllers

// Đăng ký NGAY TỪ PHASE 0, dù mãi Phase 3 mới nộp bài: Phase 1 đã gắn
// [EnableRateLimiting("upload")] lên endpoint upload bài giảng. Attribute không có
// bộ đăng ký tương ứng là metadata trơ — không chặn gì, và cũng không báo lỗi gì.
builder.Services.AddRateLimiter(o =>
{
    o.AddFixedWindowLimiter("upload", w =>
    {
        w.PermitLimit = 30;
        w.Window = TimeSpan.FromMinutes(1);
        w.QueueLimit = 0;
    });

    // register và bootstrap là hai endpoint ẩn danh DUY NHẤT ghi vào bảng người dùng,
    // còn invites/{token} là chỗ dò token. Cả ba phải có trần.
    o.AddFixedWindowLimiter("auth", w =>
    {
        w.PermitLimit = 20;
        w.Window = TimeSpan.FromMinutes(1);
        w.QueueLimit = 0;
    });
});

builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddAuth(builder.Configuration);
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseSerilogRequestLogging();
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Fallback policy áp cho MỌI endpoint không khai báo gì về authorization, kể cả
// MapHealthChecks và MapOpenApi. Quên .AllowAnonymous() cho /health thì load balancer
// đọc 401 là "service chết" và rút container ra khỏi vòng phục vụ.
app.MapOpenApi().AllowAnonymous();          // ở mọi môi trường: CI cần nó để sinh client TS
if (app.Environment.IsDevelopment())
    app.MapScalarApiReference().AllowAnonymous();   // UI duyệt API tại /scalar/v1

app.MapHealthChecks("/health").AllowAnonymous();

// CHỈ seed ở Development/Testing. Ở Production dùng POST /api/auth/bootstrap để tạo
// giảng viên đầu tiên — seed tài khoản có mật khẩu công khai lên máy chủ thật là
// cách nhanh nhất để mất toàn bộ điểm số của mọi lớp.
// Seed:Enabled cho phép test dựng host trên hệ thống thật sự trống để kiểm bootstrap.
if (!app.Environment.IsProduction() && app.Configuration.GetValue("Seed:Enabled", true))
    await app.Services.SeedAsync();
else
    await app.Services.MigrateOnlyAsync();

app.Run();

// Cho phép WebApplicationFactory<Program> nhìn thấy host này.
public partial class Program;
