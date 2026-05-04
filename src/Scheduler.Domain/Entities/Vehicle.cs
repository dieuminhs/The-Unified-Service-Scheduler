using Scheduler.Domain.Common;

namespace Scheduler.Domain.Entities;

public sealed class Vehicle : EntityBase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CustomerId { get; set; }
    public string Vin { get; set; } = string.Empty;
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int Year { get; set; }

    public Customer? Customer { get; set; }
}
