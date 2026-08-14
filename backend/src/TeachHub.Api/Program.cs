using TeachHub.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// TimeProvider tiêm khắp nơi: mọi logic phụ thuộc thời gian (hạn nộp, phạt trễ,
// streak, lịch ôn tập) phải test được mà không cần Thread.Sleep.
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddPersistence(builder.Configuration);

var app = builder.Build();

app.Run();

// Cho phép WebApplicationFactory<Program> nhìn thấy host này.
public partial class Program;
