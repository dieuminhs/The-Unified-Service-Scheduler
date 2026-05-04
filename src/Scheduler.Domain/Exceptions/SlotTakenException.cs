namespace Scheduler.Domain.Exceptions;

public sealed class SlotTakenException : DomainException
{
    public override string Code => "SLOT_TAKEN";
    public SlotTakenException(string message = "All qualified resources are booked for the requested window.") : base(message) { }
}
