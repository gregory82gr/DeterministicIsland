namespace DeterministicIsland.Islands
{
    // §12.3.2: locks every remaining source of non-determinism in inference —
    // model weights, runtime version, random seed and CPU-only execution.
    public sealed record FrozenSnapshotConfig
    {
        public required string ModelWeightsHash { get; init; }
        public required string RuntimeVersion { get; init; }
        public required int RandomSeed { get; init; }
        public required bool CpuOnlyInference { get; init; }
    }
}
