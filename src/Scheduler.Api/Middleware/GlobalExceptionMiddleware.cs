using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Scheduler.Application.Contracts;
using Scheduler.Domain.Exceptions;

namespace Scheduler.Api.Middleware;

public sealed class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next; _logger = logger;
    }

    public async Task Invoke(HttpContext ctx)
    {
        try
        {
            await _next(ctx);
        }
        catch (ValidationException vex)
        {
            await Write(ctx, 400, ProblemCodes.ValidationFailed, "Validation failed.",
                vex.Errors.Select(e => new { e.PropertyName, e.ErrorMessage }));
        }
        catch (DomainException dex)
        {
            var status = dex switch
            {
                SlotTakenException => 409,
                AlreadyCancelledException => 409,
                IdempotencyReplayMismatchException => 409,
                ResourceNotFoundException => 404,
                TechnicianUnqualifiedException => 422,
                OutsideOpeningHoursException => 422,
                StartInPastException => 422,
                _ => 400
            };
            await Write(ctx, status, dex.Code, dex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            await Write(ctx, 500, "INTERNAL_ERROR", "An unexpected error occurred.");
        }
    }

    private static async Task Write(HttpContext ctx, int status, string code, string detail, object? extras = null)
    {
        var corrId = ctx.Items[CorrelationIdMiddleware.HeaderName] as string ?? Guid.NewGuid().ToString("N");
        var problem = new ProblemDetails
        {
            Status = status,
            Title = code,
            Detail = detail,
            Type = $"https://scheduler.example.com/errors/{code.ToLowerInvariant().Replace('_', '-')}"
        };
        problem.Extensions["code"] = code;
        problem.Extensions["correlationId"] = corrId;
        if (extras is not null) problem.Extensions["errors"] = extras;

        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/problem+json";
        await ctx.Response.WriteAsync(JsonSerializer.Serialize(problem));
    }
}
