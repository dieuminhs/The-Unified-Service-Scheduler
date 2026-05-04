using Scheduler.Application.Services.Abstractions;

namespace Scheduler.Application.Services;

public sealed class ResourceSelector : IResourceSelector
{
    public T? Pick<T>(IReadOnlyList<T> orderedCandidates) where T : class =>
        orderedCandidates.Count == 0 ? null : orderedCandidates[0];
}
