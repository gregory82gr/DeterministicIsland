using DeterministicIsland.domain;

namespace DeterministicIsland.Islands
{
    public interface IVectorStore
    {
        ILMVector? Get(Guid vectorId);
    }

    // =======================================================================
    // INTRA-VECTOR ROUTING (§12.3.3): follow Determinism.PointerTo until the chain reaches
    // a vector that is deterministic, or one that does not defer anywhere.
    // A cycle, an over-long chain and a dangling pointer all fail loudly; none of them may
    // fall back silently to an ungrounded, stochastic answer.
    // =======================================================================
    public static class VectorResolver
    {
        public static ILMVector Resolve(ILMVector start, IVectorStore store, int maxHops = 8)
        {
            var current = start;
            var visited = new HashSet<Guid> { current.VectorId };

            // An ordinary vector with Determinism == null falls straight through and is
            // returned as-is: it was never part of an island to begin with.
            while (current.Determinism is { IsDeterministic: false, PointerTo: Guid next })
            {
                if (!visited.Add(next))
                    throw new InvalidOperationException($"Cycle detected: vector {next} was already visited.");

                if (visited.Count > maxHops)
                    throw new InvalidOperationException("Pointer chain exceeded the maximum permitted hops.");

                current = store.Get(next)
                    ?? throw new InvalidOperationException($"Dangling pointer: vector {next} does not exist.");
            }

            return current;
        }
    }

    public static class GuardedResolver
    {
        // When the lock is engaged, the answer path must resolve to
        // Determinism.IsDeterministic == true, or the request fails loudly.
        // With asOf, the resolved vector must also be valid at that instant: a pointer to a
        // superseded version is stale and has to be re-registered by a human (§12.4.4).
        public static ILMVector ResolveUnderLock(ILMVector start, IVectorStore store, CausalityLock causalityLock,
            DateTime? asOf = null)
        {
            var resolved = VectorResolver.Resolve(start, store);
            var isResolved = resolved.Determinism is { IsDeterministic: true };

            if (causalityLock.IsEngaged && !isResolved)
                throw new DeterminismViolationException(
                    $"Vector {resolved.VectorId} has no deterministic resolution, but a Causality Lock is engaged for this response.");

            if (isResolved && asOf is DateTime instant && !resolved.Determinism!.IsValidAt(instant))
                throw new InvalidOperationException(
                    $"Stale pointer: vector {resolved.VectorId} ({resolved.Determinism.Version}) is not valid at {instant:O}.");

            // A derived island is only as current as its inputs (§24.4.1): if any input it was
            // computed from is no longer valid, the derived value is stale and must not be used.
            if (isResolved && asOf is DateTime at && resolved.Determinism!.DerivedFrom is { } inputs)
            {
                foreach (var inputId in inputs)
                {
                    var input = store.Get(inputId);
                    if (input?.Determinism is not { } d || !d.IsValidAt(at))
                        throw new InvalidOperationException(
                            $"Stale derived island: vector {resolved.VectorId} was computed from input {inputId}, which is not valid at {at:O}.");
                }
            }

            return resolved;
        }
    }
}
