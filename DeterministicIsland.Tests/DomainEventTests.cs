using DeterministicIsland.Audit;
using DeterministicIsland.domain;
using DeterministicIsland.Events;
using DeterministicIsland.Islands;
using DeterministicIsland.ProbabilisticCore;

namespace DeterministicIsland.Tests;

public class DomainEventTests
{
    private static readonly DateTime T1 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime T2 = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

    private sealed class DecisionRecorder : IDomainEventListener<ControlDecisionMade>
    {
        public List<ControlDecisionMade> Decisions { get; } = new();
        public void Handle(ControlDecisionMade domainEvent) => Decisions.Add(domainEvent);
    }

    private readonly InMemoryEventRecorder _recorder = new();
    private readonly DecisionRecorder _decisions = new();
    private readonly DomainEventBus _bus;

    public DomainEventTests()
    {
        _bus = new DomainEventBus()
            .Subscribe<IslandAdded>(_recorder)
            .Subscribe<IslandTriggered>(_recorder)
            .Subscribe(_decisions);
    }

    private CompleteSystemArbiter Arbiter(StaticVault vault) => new(new MiniNeuralNetwork(), vault, _bus, () => T2);

    [Fact]
    public void Bus_DeliversAnEventOnlyToListenersOfItsType()
    {
        var bus = new DomainEventBus().Subscribe<IslandTriggered>(_recorder);

        bus.Publish(new IslandAdded("h", "q", new ILMVector { VectorId = Guid.NewGuid(), Embedding = new[] { 1.0 } }, null, null, T1));
        bus.Publish(new IslandTriggered("h", "q", Guid.NewGuid(), "v1", T1));

        Assert.IsType<IslandTriggered>(Assert.Single(_recorder.Events));
    }

    [Fact]
    public void Vault_RaisesIslandAddedForValuesPointersAndDerivedIslands()
    {
        var vault = new StaticVault(_bus);
        var primary = vault.Register("pt-1 limit", 45.0, T1, "Engineer");
        vault.RegisterPointer("pt-1 alias", primary.VectorId, T1, "Engineer");
        DerivedIslandFactory.DeriveMinimum(vault, "system limit", T1, "Engineer", "pt-1 limit");

        var added = _recorder.Events.OfType<IslandAdded>().ToList();
        Assert.Equal(new[] { "pt-1 limit", "pt-1 alias", "system limit" }, added.Select(e => e.Query));
        Assert.Equal(StaticVault.HashQuery("pt-1 limit"), added[0].Hash);
        Assert.Equal(primary.VectorId, added[1].Vector.Determinism!.PointerTo);
        Assert.Equal("minimum", added[2].DerivationRule);
        Assert.Equal(new[] { "pt-1 limit" }, added[2].DerivationInputs);
    }

    [Fact]
    public void AutomaticRecomputation_RaisesIslandAddedForTheDerivedIsland()
    {
        var vault = new StaticVault(_bus);
        vault.Register("pt-1 limit", 45.0, T1, "Engineer");
        DerivedIslandFactory.DeriveMinimum(vault, "system limit", T1, "Engineer", "pt-1 limit");

        vault.Register("pt-1 limit", 40.0, T2, "Engineer");

        var last = _recorder.Events.OfType<IslandAdded>().Last();
        Assert.Equal("system limit", last.Query);
        Assert.Equal("v2", last.Vector.Determinism!.Version);
        Assert.Contains("auto-recomputed", last.Vector.Determinism.ApprovedBy);
    }

    [Fact]
    public void Arbiter_RaisesIslandTriggeredForTheFactsOfTriggeredIslands()
    {
        var vault = new StaticVault();
        SafetyLimits.SeedDefaults(vault, T1, "Engineer");

        Arbiter(vault).Execute(new SensorReading(95.0, 4.0, "", false));

        var triggered = _recorder.Events.OfType<IslandTriggered>().Select(e => e.Query).ToList();
        Assert.Equal(new[] { SafetyLimits.CoolantTemperatureLimitCelsius, SafetyLimits.StructuralCoolingValveOpening }, triggered);
    }

    [Fact]
    public void Arbiter_RaisesNoIslandTriggeredWhenTheAiDecides()
    {
        var vault = new StaticVault();
        SafetyLimits.SeedDefaults(vault, T1, "Engineer");

        Arbiter(vault).Execute(new SensorReading(30.0, 5.0, "", false));

        Assert.Empty(_recorder.Events.OfType<IslandTriggered>());
        Assert.Equal(ArbiterDecision.AiApproved, Assert.Single(_decisions.Decisions).Record.Decision);
    }

    [Fact]
    public void Answer_RaisesIslandTriggeredOnAStaticVaultHit()
    {
        var vault = new StaticVault();
        var limit = vault.Register("max pressure", 120.0, T1, "Engineer");

        Arbiter(vault).Answer("max pressure");

        var e = Assert.Single(_recorder.Events.OfType<IslandTriggered>());
        Assert.Equal(limit.VectorId, e.VectorId);
        Assert.Equal(T2, e.OccurredAt);
    }

    [Fact]
    public void EscalatedCycle_StillPublishesItsControlDecision()
    {
        var vault = new StaticVault();
        SafetyLimits.SeedDefaults(vault, T1, "Engineer");

        Assert.Throws<DeterminismViolationException>(() =>
            Arbiter(vault).Execute(new SensorReading(30.0, 5.0, "deterministic island", false)));

        Assert.Equal(ArbiterDecision.HumanEscalation, Assert.Single(_decisions.Decisions).Record.Decision);
    }

    [Fact]
    public void AuditLogListener_WritesEachPublishedDecision()
    {
        var audit = new InMemoryAuditLog();
        var vault = new StaticVault();
        SafetyLimits.SeedDefaults(vault, T1, "Engineer");
        var bus = new DomainEventBus().Subscribe(new AuditLogListener(audit));

        new CompleteSystemArbiter(new MiniNeuralNetwork(), vault, bus, () => T2).Execute(new SensorReading(95.0, 8.5, "", false));

        Assert.Equal(ArbiterDecision.IslandOverride, Assert.Single(audit.Records).Decision);
    }
}
