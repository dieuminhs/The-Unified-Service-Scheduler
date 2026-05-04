using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Scheduler.Application.Abstractions.Idempotency;
using Scheduler.Application.Abstractions.Persistence;
using Scheduler.Domain.Abstractions;
using Scheduler.Infrastructure.Idempotency;
using Scheduler.Infrastructure.Persistence;
using Scheduler.Infrastructure.Persistence.Interceptors;
using Scheduler.Infrastructure.Repositories;
using Scheduler.Infrastructure.Time;

namespace Scheduler.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddSchedulerInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<AuditAndSoftDeleteInterceptor>();

        services.AddDbContext<SchedulerDbContext>((sp, opts) =>
        {
            var conn = config.GetConnectionString("Default") ?? "Data Source=scheduler.db";
            opts.UseSqlite(conn);
            opts.AddInterceptors(sp.GetRequiredService<AuditAndSoftDeleteInterceptor>());
        });

        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddScoped<AppointmentRepository>();
        services.AddScoped<IAppointmentReader>(sp => sp.GetRequiredService<AppointmentRepository>());
        services.AddScoped<IAppointmentWriter>(sp => sp.GetRequiredService<AppointmentRepository>());

        services.AddScoped<ITechnicianRepository, TechnicianRepository>();
        services.AddScoped<IServiceBayRepository, ServiceBayRepository>();
        services.AddScoped<IDealershipRepository, DealershipRepository>();
        services.AddScoped<IServiceTypeRepository, ServiceTypeRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IVehicleRepository, VehicleRepository>();
        services.AddScoped<IIdempotencyStore, IdempotencyStore>();

        return services;
    }
}
