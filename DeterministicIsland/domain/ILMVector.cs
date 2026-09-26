using System.Text.Json.Serialization;

namespace DeterministicIsland.domain
{
    // Ch. 10 / §12.3.1: every vector that takes part in a Deterministic Island carries a
    // fully populated Determinism block; ordinary (stochastic) vectors leave it null.
    public sealed record DeterminismMetadata
    {
        public required bool IsDeterministic { get; init; }
        public required string Version { get; init; }
        public required DateTime ValidFrom { get; init; }

        // Exclusive end of validity; null while the vector is the current truth (§12.4.4).
        public DateTime? ValidTo { get; init; }

        // §12.4.4: every update requires human sign-off.
        public required string ApprovedBy { get; init; }

        // §12.3.3 Intra-Vector Routing: a vector with IsDeterministic = false may defer to
        // another vector; resolution follows the pointer and uses the target's determinism.
        public Guid? PointerTo { get; init; }

        // §24.4.1 Derived island: the exact input vectors this value was computed from,
        // and the name of the pure function that computed it (e.g. "minimum").
        public IReadOnlyList<Guid>? DerivedFrom { get; init; }
        public string? DerivedBy { get; init; }

        public bool IsValidAt(DateTime instant) =>
            instant >= ValidFrom && (ValidTo is null || instant < ValidTo);
    }

    public sealed record ILMVector
    {
        public required Guid VectorId { get; init; }

        // For the safety limits of this POC, Embedding[0] is the decoded numeric value (§17.4).
        public required double[] Embedding { get; init; }

        public DeterminismMetadata? Determinism { get; init; }

        // Derived from Embedding, so it is not serialized; a pointer has no value of its own.
        [JsonIgnore]
        public double Value => Embedding[0];
    }

    // A vault query together with the vector it resolved to: the provenance of a decision.
    public sealed record VaultFact(string Query, ILMVector Vector)
    {
        public string Describe() =>
            $"{Query} = {Vector.Value} ({Vector.Determinism!.Version}" +
            (Vector.Determinism.DerivedBy is { } rule ? $", derived: {rule} of {Vector.Determinism.DerivedFrom!.Count} inputs" : "") +
            $", approved by {Vector.Determinism.ApprovedBy})";
    }
}
