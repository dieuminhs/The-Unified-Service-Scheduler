using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Scheduler.Application.Services;
using Scheduler.Application.Services.Abstractions;

namespace Scheduler.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddSchedulerApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        services.AddScoped<IBookingService, BookingService>();
        services.AddScoped<ICancellationService, CancellationService>();
        services.AddScoped<IReschedulingService, ReschedulingService>();
        services.AddScoped<IAvailabilityService, AvailabilityService>();
        services.AddScoped<IQualificationMatcher, QualificationMatcher>();
        services.AddScoped<IResourceSelector, ResourceSelector>();
        services.AddScoped<IOpeningHoursValidator, OpeningHoursValidator>();

        return services;
    }
}
