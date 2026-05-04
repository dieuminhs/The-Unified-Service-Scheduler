namespace Scheduler.Domain.Exceptions;

public sealed class TechnicianUnqualifiedException : DomainException
{
    public override string Code => "TECHNICIAN_UNQUALIFIED";
    public TechnicianUnqualifiedException(string message = "No qualified technician exists for this service.") : base(message) { }
}
