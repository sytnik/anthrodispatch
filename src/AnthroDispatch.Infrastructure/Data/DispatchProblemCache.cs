using AnthroDispatch.Domain.Entities.Dispatch;

namespace AnthroDispatch.Infrastructure.Data;

/// <summary>
/// In-memory cache for built DispatchProblem objects.
/// Allows the /api/optimization/run endpoint to consume a problem id returned
/// by /api/datasets/{id}/build-dispatch-problem without re-running the builder.
/// </summary>
public sealed class DispatchProblemCache
{
    private readonly Dictionary<Guid, DispatchProblem> _store = new();

    public void Store(DispatchProblem problem)
        => _store[problem.Id] = problem;

    public DispatchProblem? Get(Guid id)
        => _store.GetValueOrDefault(id);

    public bool Contains(Guid id) => _store.ContainsKey(id);
}