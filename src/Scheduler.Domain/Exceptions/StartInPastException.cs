namespace Scheduler.Domain.Exceptions;

public sealed class StartInPastException : DomainException
{
    public override string Code => "START_IN_PAST";
    public StartInPastException(string message = "Booking start time must be in the future.") : base(message) { }
}
