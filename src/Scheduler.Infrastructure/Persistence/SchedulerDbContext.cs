using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Scheduler.Domain.Common;
using Scheduler.Domain.Entities;

namespace Scheduler.Infrastructure.Persistence;

public sealed class SchedulerDbContext : DbContext
{
    public SchedulerDbContext(DbContextOptions<SchedulerDbContext> options) : base(options) { }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<Dealership> Dealerships => Set<Dealership>();
    public DbSet<ServiceType> ServiceTypes => Set<ServiceType>();
    public DbSet<Technician> Technicians => Set<Technician>();
    public DbSet<ServiceBay> ServiceBays => Set<ServiceBay>();
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<TechnicianDealership> TechnicianDealerships => Set<TechnicianDealership>();
    public DbSet<ServiceBayDealership> ServiceBayDealerships => Set<ServiceBayDealership>();
    public DbSet<TechnicianSkill> TechnicianSkills => Set<TechnicianSkill>();
    public DbSet<ServiceTypeRequiredSkill> ServiceTypeRequiredSkills => Set<ServiceTypeRequiredSkill>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<IdempotencyEntry> IdempotencyEntries => Set<IdempotencyEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SchedulerDbContext).Assembly);

        // IdempotencyEntry's full configuration arrives in Task 12; until then, declare its key here.
        modelBuilder.Entity<IdempotencyEntry>().HasKey(x => x.Key);

        ApplySoftDeleteQueryFilter(modelBuilder);
    }

    private static void ApplySoftDeleteQueryFilter(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(EntityBase).IsAssignableFrom(entityType.ClrType))
                continue;

            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var propertyAccess = Expression.Property(parameter, nameof(EntityBase.IsDeleted));
            var notDeleted = Expression.Equal(propertyAccess, Expression.Constant(false));
            var lambda = Expression.Lambda(notDeleted, parameter);

            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
        }
    }
}
