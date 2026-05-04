namespace Scheduler.Domain.Exceptions;

public sealed class ResourceNotFoundException : DomainException
{
    public override string Code => "RESOURCE_NOT_FOUND";
    public ResourceNotFoundException(string resource, Guid id)
        : base($"{resource} {id} was not found or is inactive.") { }
}
