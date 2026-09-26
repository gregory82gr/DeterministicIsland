using DeterministicIsland.Audit;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DeterministicIsland.Events
{
    // Infrastructure (§23.3.2): writes every control decision to the audit trail.
    // The Arbiter publishes ControlDecisionMade and does not know that auditing exists.
    public sealed class AuditLogListener : IDomainEventListener<ControlDecisionMade>
    {
        private readonly IAuditLog _auditLog;

        public AuditLogListener(IAuditLog auditLog) => _auditLog = auditLog;

        public void Handle(ControlDecisionMade domainEvent) => _auditLog.Record(domainEvent.Record);
    }

    // Keeps every vault event in memory (tests, and the event summary of the demo).
    public sealed class InMemoryEventRecorder : IDomainEventListener<IslandAdded>, IDomainEventListener<IslandTriggered>
    {
        private readonly List<IDomainEvent> _events = new();
        public IReadOnlyList<IDomainEvent> Events => _events;

        public void Handle(IslandAdded domainEvent) => _events.Add(domainEvent);
        public void Handle(IslandTriggered domainEvent) => _events.Add(domainEvent);
    }

    // §19.5: an append-only JSON Lines record of every fact registered and every fact that
    // governed a decision, one {"Event": ..., "Data": ...} object per line.
    public sealed class JsonLinesEventLog : IDomainEventListener<IslandAdded>, IDomainEventListener<IslandTriggered>
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            Converters = { new JsonStringEnumConverter() }
        };

        public string Path { get; }

        public JsonLinesEventLog(string path) => Path = path;

        public void Handle(IslandAdded domainEvent) => Write(nameof(IslandAdded), domainEvent);
        public void Handle(IslandTriggered domainEvent) => Write(nameof(IslandTriggered), domainEvent);

        private void Write<TEvent>(string name, TEvent domainEvent) =>
            File.AppendAllText(Path, JsonSerializer.Serialize(new { Event = name, Data = domainEvent }, Options) + Environment.NewLine);
    }
}
