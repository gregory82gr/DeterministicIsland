using DeterministicIsland.domain;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DeterministicIsland.Audit
{
    public enum ArbiterDecision { AiApproved, AiShielded, IslandOverride, Scram, HumanEscalation }

    public sealed record IslandSnapshot(string Name, IslandPriority Priority, double EnforcedOutput, IReadOnlyList<string> Sources);

    // One record per control cycle: the frozen runtime parameters that produced the decision.
    public sealed record AuditRecord
    {
        public required DateTime Timestamp { get; init; }
        public required SensorReading Reading { get; init; }
        public required bool CausalityLockEngaged { get; init; }
        public required double AiProposal { get; init; }
        public required string AiOrigin { get; init; }
        public required IReadOnlyList<IslandSnapshot> TriggeredIslands { get; init; }
        public required ArbiterDecision Decision { get; init; }
        public double? FinalValveOpening { get; init; }
        public required string Origin { get; init; }

        // §12.4.3: disagreement between deterministic sources, or a request the system
        // could not answer deterministically, must be reviewed by a human.
        public required bool RequiresHumanReview { get; init; }
        public string? Reason { get; init; }
    }

    public interface IAuditLog
    {
        void Record(AuditRecord record);
    }

    public sealed class InMemoryAuditLog : IAuditLog
    {
        private readonly List<AuditRecord> _records = new();
        public IReadOnlyList<AuditRecord> Records => _records;
        public void Record(AuditRecord record) => _records.Add(record);
    }

    // Append-only JSON Lines file: one serialized AuditRecord per line.
    public sealed class JsonLinesAuditLog : IAuditLog
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            Converters = { new JsonStringEnumConverter() }
        };

        public string Path { get; }

        public JsonLinesAuditLog(string path) => Path = path;

        public void Record(AuditRecord record) =>
            File.AppendAllText(Path, JsonSerializer.Serialize(record, Options) + Environment.NewLine);
    }
}
