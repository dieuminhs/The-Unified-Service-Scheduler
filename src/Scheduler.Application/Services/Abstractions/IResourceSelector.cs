namespace Scheduler.Application.Services.Abstractions;

public interface IResourceSelector
{
    T? Pick<T>(IReadOnlyList<T> orderedCandidates) where T : class;
}
