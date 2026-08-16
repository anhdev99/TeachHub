using System.Text.Json;
using System.Text.Json.Serialization;
using Hangfire;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi;
using Scalar.AspNetCore;
using Serilog;
using TeachHub.Api.Common;
using TeachHub.Api.Middleware;
using TeachHub.Application;
using TeachHub.Application.Common;
using TeachHub.Infrastructure;
using TeachHub.Infrastructure.Jobs;
using TeachHub.Infrastructure.Storage;
using TeachHub.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration));

// TimeProvider is injected everywhere: every piece of time-dependent logic (due dates, late
// penalties, streaks, spaced-repetition schedules) must be testable without Thread.Sleep.
builder.Services.AddSingleton(TimeProvider.System);

// BOTH PLACES MUST BE CONFIGURED — each covers one half, and missing either breaks that half:
//  · AddJsonOptions            → how MVC serializes the ACTUAL response at runtime
//  · ConfigureHttpJsonOptions  → how Microsoft.AspNetCore.OpenApi generates the SCHEMA in /openapi/v1.json
// They read two different IOptions and do NOT inherit from one another.
builder.Services.AddControllers(o =>
    {
        // This API speaks JSON only. Removing StringOutputFormatter makes every endpoint declare
        // exactly one content-type in OpenAPI instead of also advertising text/plain — otherwise
        // the generated TypeScript client gets a spurious union and the caller has to guess which
        // branch is real.
        // Do NOT use [Produces("application/json")] to achieve this: that attribute forces the
        // content-type on error responses TOO, which strips the RFC 9457 application/problem+json
        // label off every 4xx/5xx.
        o.OutputFormatters.RemoveType<Microsoft.AspNetCore.Mvc.Formatters.StringOutputFormatter>();

        // The JSON formatter still advertises the legacy "text/json" alias. Drop it as well so
        // each endpoint carries exactly one content-type in the documentation.
        foreach (var f in o.OutputFormatters.OfType<Microsoft.AspNetCore.Mvc.Formatters.SystemTextJsonOutputFormatter>())
            f.SupportedMediaTypes.Remove("text/json");
        foreach (var f in o.InputFormatters.OfType<Microsoft.AspNetCore.Mvc.Formatters.SystemTextJsonInputFormatter>())
            f.SupportedMediaTypes.Remove("text/json");
    })
    .AddJsonOptions(o =>
    {
        // snake_case to match the API contract exactly as specified in SRS section 7
        o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));

        // ⛔ Do NOT set `DefaultIgnoreCondition = WhenWritingNull` here. It was here once, and it
        // made the API contract lie.
        //
        // This block configures the RUNTIME serializer, while `ConfigureHttpJsonOptions` below is
        // what generates the schema in /openapi/v1.json — two different IOptions, no inheritance
        // between them. Setting the flag on one side only: the schema declares `score` as a
        // REQUIRED field of type ["null","number"], while the actual response DROPS the key
        // entirely when the submission has not been graded.
        //
        // The damage is not a few missing bytes. `openapi-typescript` generates
        // `score: number | null`, so the frontend writes `score === null` to ask "has this been
        // graded?" — and that expression is NEVER true, because the real value is `undefined`.
        // TypeScript compiles cleanly; nothing fails at runtime. Case actually caught: the
        // "Return to student" button unlocked on an UNGRADED submission, i.e. it handed the
        // submission back to the student with no grade on it.
        //
        // The schema is the binding artifact: `null` means "no value yet", which is a different
        // statement from "this field does not exist". If a field needs hiding, mark it on that
        // DTO itself — do not set a global rule that skews every endpoint.
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
    // .NET 10 declares every number as type: ["integer","string"] plus a pattern, because
    // System.Text.Json CAN parse a number out of a string. Consequence: openapi-typescript
    // generates `order_index: number | string` for every numeric field, and the frontend has to
    // Number(...) everywhere or eat compile errors — for the Phase 4b gradebook that is dozens of
    // call sites. This API always RETURNS real numbers, so strip the "string" variant out of the
    // documentation. Keep the Null flag intact so nullable fields stay correct.
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

// Registered AS EARLY AS PHASE 0, even though submissions only arrive in Phase 3: Phase 1
// already put [EnableRateLimiting("upload")] on the lesson-material upload endpoint. An
// attribute with no matching registered limiter is inert metadata — it throttles nothing, and
// it reports no error either.
builder.Services.AddRateLimiter(o =>
{
    o.AddFixedWindowLimiter("upload", w =>
    {
        w.PermitLimit = 30;
        w.Window = TimeSpan.FromMinutes(1);
        w.QueueLimit = 0;
    });

    // register and bootstrap are the ONLY two anonymous endpoints that write to the users table,
    // and invites/{token} is where tokens can be probed. All three need a ceiling.
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

// AFTER authentication (we need to know who the caller is). Blocks every request from a user
// who still carries the MustChangePassword flag, except the two escape hatches change-password
// and logout.
app.UseMiddleware<MustChangePasswordMiddleware>();

app.MapControllers();

// The fallback policy applies to EVERY endpoint that declares nothing about authorization,
// including MapHealthChecks and MapOpenApi. Forget .AllowAnonymous() on /health and the load
// balancer reads the 401 as "service is dead" and pulls the container out of rotation.
app.MapOpenApi().AllowAnonymous();          // in every environment: CI needs it to generate the TS client
if (app.Environment.IsDevelopment())
    app.MapScalarApiReference().AllowAnonymous();   // API browser UI at /scalar/v1

app.MapHealthChecks("/health").AllowAnonymous();

// The Hangfire dashboard is middleware rather than an action, so there is no class to put
// [Authorize] on; RequireAuthorization on endpoint routing is the only correct hook here.
if (app.Configuration.GetValue("Hangfire:Enabled", true))
{
    app.MapHangfireDashboard("/jobs").RequireAuthorization(Policies.Instructor);

    // The spaced-repetition job runs at 1 AM every day (Phase 8). AddOrUpdate is idempotent, so
    // restarting the application does not duplicate the schedule — using Enqueue here would
    // create a NEW job on every deploy, and after a few months the dashboard fills up with
    // duplicates of the same job.
    app.Services.GetRequiredService<IRecurringJobManager>().AddOrUpdate<ReviewQueueJob>(
        ReviewQueueJob.RecurringJobId, j => j.RunAsync(CancellationToken.None), Cron.Daily(1));
}

// Seed in Development/Testing ONLY. In Production use POST /api/auth/bootstrap to create the
// first instructor — seeding an account with a publicly known password onto a real server is
// the fastest way to lose every grade of every class.
// Seed:Enabled lets tests spin up the host against a genuinely empty system to exercise bootstrap.
if (!app.Environment.IsProduction() && app.Configuration.GetValue("Seed:Enabled", true))
    await app.Services.SeedAsync();
else
    await app.Services.MigrateOnlyAsync();

app.Run();

// Makes this host visible to WebApplicationFactory<Program>.
public partial class Program;
