using DeterministicIsland.Audit;
using DeterministicIsland.domain;
using DeterministicIsland.Islands;
using DeterministicIsland.ProbabilisticCore;

namespace DeterministicIsland.Tests;

public class IntraVectorRoutingTests
{
    private static readonly DateTime T1 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime T2 = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

    private sealed class InMemoryStore : IVectorStore
    {
        private readonly Dictionary<Guid, ILMVector> _vectors = new();
        public void Add(ILMVector vector) => _vectors[vector.VectorId] = vector;
        public ILMVector? Get(Guid vectorId) => _vectors.GetValueOrDefault(vectorId);
    }

    private static ILMVector Deterministic(double value) => new()
    {
        VectorId = Guid.NewGuid(),
        Embedding = new[] { value },
        Determinism = new DeterminismMetadata { IsDeterministic = true, Version = "v1", ValidFrom = T1, ApprovedBy = "Engineer" }
    };

    private static ILMVector Pointer(Guid target, Guid? id = null) => new()
    {
        VectorId = id ?? Guid.NewGuid(),
        Embedding = Array.Empty<double>(),
        Determinism = new DeterminismMetadata { IsDeterministic = false, Version = "v1", ValidFrom = T1, ApprovedBy = "Engineer", PointerTo = target }
    };

    private static ILMVector Ordinary() => new() { VectorId = Guid.NewGuid(), Embedding = new[] { 1.0 } };

    private static CausalityLock EngagedLock()
    {
        var causalityLock = new CausalityLock();
        causalityLock.Engage();
        return causalityLock;
    }

    [Fact]
    public void Resolve_FollowsTheChainToTheDeterministicTarget()
    {
        var store = new InMemoryStore();
        var target = Deterministic(120.0);
        var middle = Pointer(target.VectorId);
        store.Add(target);
        store.Add(middle);

        var resolved = VectorResolver.Resolve(Pointer(middle.VectorId), store);

        Assert.Equal(target.VectorId, resolved.VectorId);
    }

    [Fact]
    public void Resolve_ReturnsAnOrdinaryVectorAsIs()
    {
        var ordinary = Ordinary();

        Assert.Same(ordinary, VectorResolver.Resolve(ordinary, new InMemoryStore()));
    }

    [Fact]
    public void Resolve_DetectsACycle()
    {
        var store = new InMemoryStore();
        Guid a = Guid.NewGuid(), b = Guid.NewGuid();
        store.Add(Pointer(b, id: a));
        store.Add(Pointer(a, id: b));

        var ex = Assert.Throws<InvalidOperationException>(() => VectorResolver.Resolve(store.Get(a)!, store));
        Assert.Contains("Cycle", ex.Message);
    }

    [Fact]
    public void Resolve_FailsLoudlyOnADanglingPointer()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => VectorResolver.Resolve(Pointer(Guid.NewGuid()), new InMemoryStore()));
        Assert.Contains("Dangling", ex.Message);
    }

    [Fact]
    public void Resolve_RejectsAChainLongerThanMaxHops()
    {
        var store = new InMemoryStore();
        var current = Deterministic(1.0);
        store.Add(current);
        for (int i = 0; i < 10; i++)
        {
            current = Pointer(current.VectorId);
            store.Add(current);
        }

        var ex = Assert.Throws<InvalidOperationException>(() => VectorResolver.Resolve(current, store, maxHops: 8));
        Assert.Contains("maximum permitted hops", ex.Message);
    }

    [Fact]
    public void GuardedResolver_RejectsANonDeterministicAnswerOnlyUnderLock()
    {
        var ordinary = Ordinary();

        Assert.Same(ordinary, GuardedResolver.ResolveUnderLock(ordinary, new InMemoryStore(), new CausalityLock()));
        Assert.Throws<DeterminismViolationException>(() => GuardedResolver.ResolveUnderLock(ordinary, new InMemoryStore(), EngagedLock()));
    }

    [Fact]
    public void Vault_RefusesToRegisterADanglingPointer()
    {
        Assert.Throws<InvalidOperationException>(() => new StaticVault().RegisterPointer("alias", Guid.NewGuid(), T1, "Engineer"));
    }

    [Fact]
    public void SafetyLimit_RegisteredAsPointer_ResolvesToTheTargetValue()
    {
        var vault = new StaticVault();
        var primary = vault.Register("primary setpoint", 8.0, T1, "Engineer");
        vault.RegisterPointer("backup setpoint", primary.VectorId, T1.AddDays(1), "Engineer");

        var resolved = SafetyLimits.Require(vault, "backup setpoint", T2);

        Assert.Equal(primary.VectorId, resolved.VectorId);
        Assert.Equal(8.0, resolved.Value);
    }

    [Fact]
    public void Pointer_ToASupersededVersion_IsStaleAndFailsLoudly()
    {
        var vault = new StaticVault();
        var v1 = vault.Register("primary setpoint", 8.0, T1, "Engineer");
        vault.RegisterPointer("backup setpoint", v1.VectorId, T1.AddDays(1), "Engineer");
        vault.Register("primary setpoint", 9.0, T2, "Engineer");

        Assert.Equal(8.0, SafetyLimits.Require(vault, "backup setpoint", T2.AddDays(-1)).Value);
        var ex = Assert.Throws<InvalidOperationException>(() => SafetyLimits.Require(vault, "backup setpoint", T2));
        Assert.Contains("Stale pointer", ex.Message);
    }

    [Fact]
    public void Island_UsesALimitThatIsRoutedThroughAPointer()
    {
        var vault = new StaticVault();
        SafetyLimits.SeedDefaults(vault, T1, "Engineer");
        var shared = vault.Register("shared relief setpoint", 7.0, T1, "Engineer");
        vault.RegisterPointer(SafetyLimits.PressureReliefSetpointBar, shared.VectorId, T1.AddDays(1), "Engineer");
        var arbiter = new CompleteSystemArbiter(new MiniNeuralNetwork(), vault, new InMemoryAuditLog(), () => T2);

        var command = arbiter.Execute(new SensorReading(60.0, 7.5, "", false));

        Assert.Contains("High Pressure Emergency Loop", command.Origin);
    }

    [Fact]
    public void Answer_PrefersTheStaticVaultOverACandidate()
    {
        var vault = new StaticVault();
        vault.Register("max pressure", 120.0, T1, "Engineer");
        var arbiter = new CompleteSystemArbiter(new MiniNeuralNetwork(), vault, new InMemoryAuditLog(), () => T2);

        var answer = arbiter.Answer("max pressure", Deterministic(99.0));

        Assert.Equal(120.0, answer!.Value);
    }

    [Fact]
    public void Answer_WithoutAnyDeterministicPath_FallsThroughOrEscalates()
    {
        var arbiter = new CompleteSystemArbiter(new MiniNeuralNetwork(), new StaticVault(), new InMemoryAuditLog(), () => T2);

        Assert.Null(arbiter.Answer("what do you recommend?"));
        Assert.Throws<DeterminismViolationException>(() => arbiter.Answer("what do you recommend?", requireDeterminism: true));
    }
}
