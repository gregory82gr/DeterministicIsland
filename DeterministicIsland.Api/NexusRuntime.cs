using DeterministicIsland;
using DeterministicIsland.Audit;
using DeterministicIsland.domain;
using DeterministicIsland.Events;
using DeterministicIsland.Islands;
using DeterministicIsland.ProbabilisticCore;

namespace DeterministicIsland.Api;

// Application layer (§23.3.1): wires the domain once for the lifetime of the service and
// serialises access to it. The vault and the arbiter are not thread-safe, and the book's
// Honest Boundary (§22.8) names concurrency as one of the things a teaching model omits.
public sealed class NexusRuntime
{
    public static readonly DateTime CommissioningDate = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    public const string CommissioningApprover = "Initial commissioning";

    private readonly object _gate = new();
    private readonly LastDecisionListener _lastDecision = new();

    public StaticVault Vault { get; }
    public CompleteSystemArbiter Arbiter { get; }
    public string VaultPath { get; }

    public NexusRuntime(IConfiguration configuration, IHostEnvironment environment)
    {
        string Resolve(string key, string fallback) =>
            Path.GetFullPath(configuration[$"Nexus:{key}"] ?? fallback, environment.ContentRootPath);

        VaultPath = Resolve("VaultPath", "nexus1-vault.jsonl");
        var eventLog = new JsonLinesEventLog(Resolve("EventsPath", "nexus1-events.jsonl"));
        var events = new DomainEventBus()
            .Subscribe(new AuditLogListener(new JsonLinesAuditLog(Resolve("AuditPath", "nexus1-audit.jsonl"))))
            .Subscribe(_lastDecision)
            .Subscribe<IslandAdded>(eventLog)
            .Subscribe<IslandTriggered>(eventLog);

        // §23.2: the vault is reloaded from its hash-chained file; an empty file is commissioned
        // once with the default safety limits. The API itself never writes safety limits.
        Vault = new StaticVault(events, new JsonLinesIslandRepository(VaultPath));
        if (Vault.VersionCount == 0)
            SafetyLimits.SeedDefaults(Vault, CommissioningDate, CommissioningApprover);

        Arbiter = new CompleteSystemArbiter(new MiniNeuralNetwork(), Vault, events);
    }

    // Runs one control cycle and returns its audit record, whether the arbiter acted or escalated.
    public AuditRecord Control(SensorReading reading, OperatorOverride? operatorOverride)
    {
        lock (_gate)
        {
            _lastDecision.Reset();
            try
            {
                Arbiter.Execute(reading, operatorOverride);
            }
            catch (HumanEscalationException)
            {
                // The escalation is itself a decision; it is in the record published below.
            }

            return _lastDecision.Last ?? throw new InvalidOperationException("The arbiter published no decision.");
        }
    }

    public ILMVector? Answer(string query, bool requireDeterminism)
    {
        lock (_gate)
            return Arbiter.Answer(query, requireDeterminism: requireDeterminism);
    }

    public IReadOnlyList<ILMVector> History(string query)
    {
        lock (_gate)
            return Vault.History(query).ToList();
    }

    private sealed class LastDecisionListener : IDomainEventListener<ControlDecisionMade>
    {
        public AuditRecord? Last { get; private set; }
        public void Reset() => Last = null;
        public void Handle(ControlDecisionMade domainEvent) => Last = domainEvent.Record;
    }
}
