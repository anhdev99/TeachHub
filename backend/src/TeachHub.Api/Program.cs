using System.Text.Json;
using System.Text.Json.Serialization;
using Hangfire;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi;
using Scalar.AspNetCore;
using Serilog;
using TeachHub.Api.Common;
using TeachHub.Application;
using TeachHub.Application.Common;
using TeachHub.Infrastructure;
using TeachHub.Infrastructure.Jobs;
using TeachHub.Infrastructure.Storage;
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
builder.Services.AddControllers(o =>
    {
        // API này chỉ nói JSON. Bỏ StringOutputFormatter đi thì mỗi endpoint khai đúng một
        // content-type trong OpenAPI, thay vì kèm thêm text/plain — client TypeScript sinh ra
        // sẽ có union thừa và người dùng phải tự đoán nhánh nào là thật.
        // Không dùng [Produces("application/json")] để đạt việc này: attribute đó ép content-type
        // cho CẢ phản hồi lỗi và làm 4xx/5xx mất nhãn application/problem+json theo RFC 9457.
        o.OutputFormatters.RemoveType<Microsoft.AspNetCore.Mvc.Formatters.StringOutputFormatter>();

        // Formatter JSON còn khai bí danh cũ "text/json". Bỏ nốt để mỗi endpoint chỉ có
        // đúng một content-type trong tài liệu.
        foreach (var f in o.OutputFormatters.OfType<Microsoft.AspNetCore.Mvc.Formatters.SystemTextJsonOutputFormatter>())
            f.SupportedMediaTypes.Remove("text/json");
        foreach (var f in o.InputFormatters.OfType<Microsoft.AspNetCore.Mvc.Formatters.SystemTextJsonInputFormatter>())
            f.SupportedMediaTypes.Remove("text/json");
    })
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
builder.Services.AddOpenApi(o =>
{
    // .NET 10 khai mọi số là type: ["integer","string"] kèm pattern, vì System.Text.Json
    // CÓ THỂ đọc số từ chuỗi. Hệ quả: openapi-typescript sinh ra `order_index: number | string`
    // cho mọi trường số, và frontend phải Number(...) ở khắp nơi hoặc chịu lỗi biên dịch —
    // với sổ điểm ở Phase 4b thì đó là hàng chục chỗ. API này luôn TRẢ VỀ số thật, nên bỏ
    // biến thể "string" khỏi tài liệu. Giữ nguyên cờ Null để trường nullable vẫn đúng.
    o.AddSchemaTransformer((schema, _, _) =>
    {
        if (schema.Type is { } t
            && (t & JsonSchemaType.String) != 0
            && (t & (JsonSchemaType.Integer | JsonSchemaType.Number)) != 0)
        {
            schema.Type = t & ~JsonSchemaType.String;
            schema.Pattern = null;
        }
        return Task.CompletedTask;
    });
});

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
builder.Services.AddJobs(builder.Configuration);
builder.Services.AddStorage(builder.Configuration);
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

// Dashboard Hangfire là middleware chứ không phải action nên không có class để gắn
// [Authorize]; RequireAuthorization trên endpoint routing là cách duy nhất đúng ở đây.
if (app.Configuration.GetValue("Hangfire:Enabled", true))
    app.MapHangfireDashboard("/jobs").RequireAuthorization(Policies.Instructor);

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
