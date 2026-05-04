using System.Diagnostics;
using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using Scheduler.Api.Configuration;
using Scheduler.Api.Telemetry;
using Scheduler.Application.Abstractions.Idempotency;
using Scheduler.Application.Abstractions.Persistence;
using Scheduler.Application.Contracts.Requests;
using Scheduler.Application.Contracts.Responses;
using Scheduler.Application.Mapping;
using Scheduler.Application.Services.Abstractions;
using Scheduler.Domain.Exceptions;

namespace Scheduler.Api.Controllers;

[ApiController]
[Route("api/v1/appointments")]
public sealed class AppointmentsController : ControllerBase
{
    private readonly IBookingService _booking;
    private readonly ICancellationService _cancel;
    private readonly IReschedulingService _reschedule;
    private readonly IAppointmentReader _reader;
    private readonly IIdempotencyStore _idempotency;
    private readonly IValidator<BookAppointmentRequest> _bookValidator;
    private readonly IValidator<RescheduleAppointmentRequest> _rescheduleValidator;
    private readonly SchedulerMetrics _metrics;
    private readonly BookingOptions _options;
    private readonly ILogger<AppointmentsController> _logger;

    public AppointmentsController(
        IBookingService booking, ICancellationService cancel, IReschedulingService reschedule,
        IAppointmentReader reader, IIdempotencyStore idempotency,
        IValidator<BookAppointmentRequest> bookValidator,
        IValidator<RescheduleAppointmentRequest> rescheduleValidator,
        SchedulerMetrics metrics, IOptions<BookingOptions> options, ILogger<AppointmentsController> logger)
    {
        _booking = booking; _cancel = cancel; _reschedule = reschedule; _reader = reader; _idempotency = idempotency;
        _bookValidator = bookValidator; _rescheduleValidator = rescheduleValidator;
        _metrics = metrics; _options = options.Value; _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Book([FromBody] BookAppointmentRequest req, CancellationToken ct)
    {
        await _bookValidator.ValidateAndThrowAsync(req, ct);

        using var activity = SchedulerActivitySource.Instance.StartActivity("booking.attempt");
        activity?.SetTag("dealership.id", req.DealershipId);
        activity?.SetTag("service_type.id", req.ServiceTypeId);

        var sw = Stopwatch.StartNew();
        var attempt = 0;
        var pipeline = BuildRetryPipeline();

        try
        {
            var appointment = await pipeline.ExecuteAsync(async (cancel) =>
            {
                attempt++;
                activity?.SetTag("retry.attempt", attempt);
                return await _booking.BookAsync(req, cancel);
            }, ct);

            var response = appointment.ToResponse();
            var json = JsonSerializer.Serialize(response);

            if (HttpContext.Items["IdempotencyKey"] is string key && HttpContext.Items["IdempotencyBodyHash"] is string hash)
            {
                await _idempotency.PutAsync(key, hash, "201", json, appointment.Id, DateTime.UtcNow.AddHours(24), ct);
            }

            _metrics.BookingsTotal.Add(1,
                new KeyValuePair<string, object?>("dealership_id", req.DealershipId),
                new KeyValuePair<string, object?>("service_type_id", req.ServiceTypeId),
                new KeyValuePair<string, object?>("outcome", "confirmed"));
            if (attempt > 1) _metrics.BookingRetriesTotal.Add(1, new KeyValuePair<string, object?>("outcome", "resolved"));
            _metrics.BookingDurationSeconds.Record(sw.Elapsed.TotalSeconds, new KeyValuePair<string, object?>("outcome", "confirmed"));
            activity?.SetTag("outcome", "confirmed");
            activity?.SetTag("appointment.id", appointment.Id);

            return CreatedAtAction(nameof(GetById), new { id = appointment.Id }, response);
        }
        catch (SlotTakenException)
        {
            _metrics.BookingsTotal.Add(1,
                new KeyValuePair<string, object?>("dealership_id", req.DealershipId),
                new KeyValuePair<string, object?>("service_type_id", req.ServiceTypeId),
                new KeyValuePair<string, object?>("outcome", "conflict"));
            if (attempt > 1) _metrics.BookingRetriesTotal.Add(1, new KeyValuePair<string, object?>("outcome", "exhausted"));
            activity?.SetTag("outcome", "conflict");
            throw;
        }
        catch (TechnicianUnqualifiedException)
        {
            _metrics.BookingsTotal.Add(1, new KeyValuePair<string, object?>("outcome", "unqualified"));
            activity?.SetTag("outcome", "unqualified");
            throw;
        }
        catch (OutsideOpeningHoursException)
        {
            _metrics.BookingsTotal.Add(1, new KeyValuePair<string, object?>("outcome", "outside_hours"));
            activity?.SetTag("outcome", "outside_hours");
            throw;
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var a = await _reader.GetByIdAsync(id, ct);
        return a is null ? NotFound() : Ok(a.ToResponse());
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] Guid? dealershipId, [FromQuery] Guid? customerId, [FromQuery] Guid? technicianId,
        [FromQuery] DateTime? fromUtc, [FromQuery] DateTime? toUtc,
        [FromQuery] int skip = 0, [FromQuery] int take = 50,
        CancellationToken ct = default)
    {
        if (take > 100) take = 100;
        var rows = await _reader.ListAsync(dealershipId, customerId, technicianId, fromUtc, toUtc, skip, take, ct);
        return Ok(rows.Select(a => a.ToResponse()));
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        await _cancel.CancelAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/reschedule")]
    public async Task<IActionResult> Reschedule(Guid id, [FromBody] RescheduleAppointmentRequest req, CancellationToken ct)
    {
        await _rescheduleValidator.ValidateAndThrowAsync(req, ct);

        var pipeline = BuildRetryPipeline();
        var rescheduled = await pipeline.ExecuteAsync(async (cancel) => await _reschedule.RescheduleAsync(id, req, cancel), ct);

        return Ok(rescheduled.ToResponse());
    }

    private ResiliencePipeline BuildRetryPipeline() =>
        new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder().Handle<DbUpdateException>().Handle<SlotTakenException>(),
                MaxRetryAttempts = _options.MaxRetries,
                Delay = TimeSpan.FromMilliseconds(_options.RetryBaseDelayMs),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true
            })
            .Build();
}
