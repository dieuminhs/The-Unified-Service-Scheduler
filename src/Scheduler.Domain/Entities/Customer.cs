using Scheduler.Domain.Common;

namespace Scheduler.Domain.Entities;

public sealed class Customer : EntityBase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }

    public ICollection<Vehicle> Vehicles { get; set; } = new List<Vehicle>();
}
