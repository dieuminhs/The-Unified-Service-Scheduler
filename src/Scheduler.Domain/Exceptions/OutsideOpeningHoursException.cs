namespace Scheduler.Domain.Exceptions;

public sealed class OutsideOpeningHoursException : DomainException
{
    public override string Code => "OUTSIDE_OPENING_HOURS";
    public OutsideOpeningHoursException(string message = "The requested time is outside the dealership's opening hours.") : base(message) { }
}
