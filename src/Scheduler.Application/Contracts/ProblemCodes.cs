namespace Scheduler.Application.Contracts;

public static class ProblemCodes
{
    public const string SlotTaken = "SLOT_TAKEN";
    public const string TechnicianUnqualified = "TECHNICIAN_UNQUALIFIED";
    public const string OutsideOpeningHours = "OUTSIDE_OPENING_HOURS";
    public const string StartInPast = "START_IN_PAST";
    public const string ResourceNotFound = "RESOURCE_NOT_FOUND";
    public const string IdempotencyReplayMismatch = "IDEMPOTENCY_REPLAY_MISMATCH";
    public const string AlreadyCancelled = "ALREADY_CANCELLED";
    public const string ValidationFailed = "VALIDATION_FAILED";
}
