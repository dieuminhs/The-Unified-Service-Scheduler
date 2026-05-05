using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Scheduler.Api.Configuration;
using Scheduler.Api.Middleware;
using Scheduler.Api.Telemetry;
using Scheduler.Application;
using Scheduler.Infrastructure;
using Scheduler.Infrastructure.Persistence;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Logging — Serilog over the host
builder.Host.UseSerilog((ctx, sp, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(new Serilog.Formatting.Compact.CompactJsonFormatter()));

// Configuration objects
builder.Services.Configure<BookingOptions>(builder.Configuration.GetSection(BookingOptions.SectionName));
builder.Services.Configure<SeedingOptions>(builder.Configuration.GetSection(SeedingOptions.SectionName));

// Application + Infrastructure
builder.Services.AddSchedulerApplication();
builder.Services.AddSchedulerInfrastructure(builder.Configuration);

// MVC + ProblemDetails
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Health checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<SchedulerDbContext>(tags: new[] { "ready" });

// OpenTelemetry — traces only; filter out infra noise paths
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("Scheduler.Api"))
    .WithTracing(t => t
        .AddSource(SchedulerActivitySource.Name)
        .AddAspNetCoreInstrumentation(o => o.Filter = ctx => !IsNoisePath(ctx.Request.Path))
        .AddHttpClientInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddConsoleExporter());

static bool IsNoisePath(PathString path) =>
    path.StartsWithSegments("/health") ||
    path.StartsWithSegments("/swagger") ||
    path.StartsWithSegments("/_framework") ||
    path == "/" ||
    path == "/favicon.ico";

var app = builder.Build();

// Apply migrations on startup
using (var scope = app.Services.CreateScope())
{
    var ctx = scope.ServiceProvider.GetRequiredService<SchedulerDbContext>();
    await ctx.Database.MigrateAsync();

    var seedingOptions = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<SeedingOptions>>().Value;
    if (seedingOptions.Enabled)
    {
        await Scheduler.Api.Seed.SeedData.SeedAsync(ctx);
    }
}

app.UseSerilogRequestLogging(options =>
{
    options.GetLevel = (httpContext, _, ex) =>
    {
        if (ex != null || httpContext.Response.StatusCode >= 500)
            return Serilog.Events.LogEventLevel.Error;
        if (IsNoisePath(httpContext.Request.Path))
            return Serilog.Events.LogEventLevel.Verbose;
        return Serilog.Events.LogEventLevel.Information;
    };
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseMiddleware<IdempotencyMiddleware>();

app.MapControllers();
app.MapHealthChecks("/health/live", new() { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new() { Predicate = c => c.Tags.Contains("ready") });

await app.RunAsync();

public partial class Program { }
