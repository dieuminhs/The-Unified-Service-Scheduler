namespace Scheduler.Domain.Exceptions;

public sealed class IdempotencyReplayMismatchException : DomainException
{
    public override string Code => "IDEMPOTENCY_REPLAY_MISMATCH";
    public IdempotencyReplayMismatchException(string message = "Idempotency-Key reused with a different request body.") : base(message) { }
}
