namespace Scheduler.Domain.Exceptions;

public sealed class AlreadyCancelledException : DomainException
{
    public override string Code => "ALREADY_CANCELLED";
    public AlreadyCancelledException(string message = "Appointment is already cancelled.") : base(message) { }
}
