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

        public bool IsValidAt(DateTime instant) =>
            instant >= ValidFrom && (ValidTo is null || instant < ValidTo);
    }

    public sealed record ILMVector
    {
        public required Guid VectorId { get; init; }

        // For the safety limits of this POC, Embedding[0] is the decoded numeric value (§17.4).
        public required double[] Embedding { get; init; }

        public DeterminismMetadata? Determinism { get; init; }

        public double Value => Embedding[0];
    }
}
