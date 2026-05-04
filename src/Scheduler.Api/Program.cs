using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Metrics;
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
    .Enrich.WithMachineName()
    .Enrich.WithThreadId()
    .WriteTo.Console(new Serilog.Formatting.Compact.CompactJsonFormatter()));

// Configuration objects
builder.Services.Configure<BookingOptions>(builder.Configuration.GetSection(BookingOptions.SectionName));
builder.Services.Configure<SeedingOptions>(builder.Configuration.GetSection(SeedingOptions.SectionName));

// Application + Infrastructure
builder.Services.AddSchedulerApplication();
builder.Services.AddSchedulerInfrastructure(builder.Configuration);
builder.Services.AddSingleton<SchedulerMetrics>();

// MVC + ProblemDetails
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Health checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<SchedulerDbContext>(tags: new[] { "ready" });

// OpenTelemetry — traces + metrics
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("Scheduler.Api"))
    .WithTracing(t => t
        .AddSource(SchedulerActivitySource.Name)
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddConsoleExporter())
    .WithMetrics(m => m
        .AddMeter(SchedulerMetrics.MeterName)
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddPrometheusExporter());

var app = builder.Build();

// Apply migrations on startup
using (var scope = app.Services.CreateScope())
{
    var ctx = scope.ServiceProvider.GetRequiredService<SchedulerDbContext>();
    await ctx.Database.MigrateAsync();
}

app.UseSerilogRequestLogging();

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
app.MapPrometheusScrapingEndpoint();

await app.RunAsync();

public partial class Program { }
