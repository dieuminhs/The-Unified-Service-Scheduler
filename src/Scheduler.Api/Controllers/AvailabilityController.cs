using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Scheduler.Application.Contracts.Requests;
using Scheduler.Application.Services.Abstractions;

namespace Scheduler.Api.Controllers;

[ApiController]
[Route("api/v1/availability")]
public sealed class AvailabilityController : ControllerBase
{
    private readonly IAvailabilityService _service;
    private readonly IValidator<AvailabilityQueryRequest> _validator;

    public AvailabilityController(IAvailabilityService service, IValidator<AvailabilityQueryRequest> validator)
    {
        _service = service; _validator = validator;
    }

    [HttpGet("slots")]
    public async Task<IActionResult> Slots(
        [FromQuery] Guid dealershipId,
        [FromQuery] Guid serviceTypeId,
        [FromQuery] DateTime fromUtc,
        [FromQuery] DateTime toUtc,
        [FromQuery] int granularityMinutes = 15,
        CancellationToken ct = default)
    {
        var q = new AvailabilityQueryRequest(dealershipId, serviceTypeId, fromUtc, toUtc, granularityMinutes);
        await _validator.ValidateAndThrowAsync(q, ct);
        var slots = await _service.ListSlotsAsync(q, ct);
        return Ok(slots);
    }
}
