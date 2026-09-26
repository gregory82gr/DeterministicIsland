using DeterministicIsland.Audit;
using DeterministicIsland.domain;
using DeterministicIsland.Islands;
using DeterministicIsland.ProbabilisticCore;

namespace DeterministicIsland.Tests;

public class DerivedIslandTests
{
    private static readonly DateTime T1 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime T2 = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

    private static StaticVault VaultWithThreeSensors()
    {
        var vault = new StaticVault();
        vault.Register("pt-1 limit", 45.0, T1, "Engineer");
        vault.Register("pt-2 limit", 42.5, T1, "Engineer");
        vault.Register("pt-3 limit", 44.0, T1, "Engineer");
        DerivedIslandFactory.DeriveMinimum(vault, "system limit", T1, "Engineer", "pt-1 limit", "pt-2 limit", "pt-3 limit");
        return vault;
    }

    [Fact]
    public void DeriveMinimum_ReproducesTheBooksWorkedExample()
    {
        // §24.4.1: min(45.0, 42.5, 44.0) = 42.5 bar.
        var derived = VaultWithThreeSensors().Resolve("system limit", T2);

        Assert.Equal(42.5, derived.Value);
        Assert.True(derived.Determinism!.IsDeterministic);
        Assert.Equal("minimum", derived.Determinism.DerivedBy);
        Assert.Equal(3, derived.Determinism.DerivedFrom!.Count);
    }

    [Fact]
    public void InputUpdate_RecomputesTheDerivedIslandAutomatically()
    {
        var vault = VaultWithThreeSensors();

        vault.Register("pt-3 limit", 40.0, T2, "Engineer B");

        var history = vault.History("system limit");
        Assert.Equal(2, history.Count);
        Assert.Contains("auto-recomputed", history[1].Determinism!.ApprovedBy);
        Assert.Equal(42.5, vault.Resolve("system limit", T2.AddDays(-1)).Value);
        Assert.Equal(40.0, vault.Resolve("system limit", T2).Value);
    }

    [Fact]
    public void DerivedOfDerived_IsRecomputedInCascade()
    {
        var vault = VaultWithThreeSensors();
        vault.Register("margin", 2.0, T1, "Engineer");
        vault.RegisterDerived("alarm limit", new[] { "system limit", "margin" }, "difference", v => v[0] - v[1], T1, "Engineer");

        vault.Register("pt-2 limit", 41.0, T2, "Engineer");

        Assert.Equal(40.5, vault.Resolve("alarm limit", T2.AddDays(-1)).Value);
        Assert.Equal(39.0, vault.Resolve("alarm limit", T2).Value);
    }

    [Fact]
    public void Derive_RequiresEveryInputToBeRegistered()
    {
        var vault = new StaticVault();
        vault.Register("pt-1 limit", 45.0, T1, "Engineer");

        Assert.Throws<InvalidOperationException>(() =>
            DerivedIslandFactory.DeriveMinimum(vault, "system limit", T1, "Engineer", "pt-1 limit", "missing limit"));
    }

    [Fact]
    public void InputUpdate_ThatWouldRetroactivelyInvalidateADerivedIsland_IsRejected()
    {
        var vault = new StaticVault();
        vault.Register("pt-1 limit", 45.0, T1, "Engineer");
        DerivedIslandFactory.DeriveMinimum(vault, "system limit", T2, "Engineer", "pt-1 limit");

        Assert.Throws<InvalidOperationException>(() => vault.Register("pt-1 limit", 40.0, T2.AddDays(-1), "Engineer"));
    }

    [Fact]
    public void DerivedIsland_OverAPointerWhoseTargetChanged_IsStaleAndFailsLoudly()
    {
        var vault = new StaticVault();
        var primary = vault.Register("primary limit", 45.0, T1, "Engineer");
        vault.RegisterPointer("backup limit", primary.VectorId, T1, "Engineer");
        vault.Register("other limit", 50.0, T1, "Engineer");
        DerivedIslandFactory.DeriveMinimum(vault, "system limit", T1, "Engineer", "backup limit", "other limit");

        // "primary limit" is not a direct input, so nothing is recomputed automatically;
        // the derived value still rests on the superseded primary version.
        vault.Register("primary limit", 40.0, T2, "Engineer");

        Assert.Equal(45.0, vault.Resolve("system limit", T2.AddDays(-1)).Value);
        var ex = Assert.Throws<InvalidOperationException>(() => vault.Resolve("system limit", T2));
        Assert.Contains("Stale derived island", ex.Message);
    }

    [Fact]
    public void RegisteringAPlainValue_ReplacesTheDerivationRule()
    {
        var vault = VaultWithThreeSensors();
        vault.Register("system limit", 30.0, T2, "Engineer");

        vault.Register("pt-1 limit", 20.0, T2.AddDays(1), "Engineer");

        Assert.Equal(30.0, vault.Resolve("system limit", T2.AddDays(2)).Value);
    }

    [Fact]
    public void Arbiter_UsesTheDerivedReliefSetpoint()
    {
        var vault = new StaticVault();
        SafetyLimits.SeedDefaults(vault, T1, "Engineer");
        vault.Register(SafetyLimits.PressureTransmitterLimitsBar[1], 7.5, T2, "Engineer");
        var reading = new SensorReading(30.0, 7.8, "", false);

        var audit = new InMemoryAuditLog();

        // Below the old 8.0 bar setpoint the AI decides (or, being uncertain, escalates):
        // either way no pressure island is triggered.
        try { new CompleteSystemArbiter(new MiniNeuralNetwork(), vault, audit, () => T2.AddDays(-1)).Execute(reading); }
        catch (HumanEscalationException) { }
        var after = new CompleteSystemArbiter(new MiniNeuralNetwork(), vault, audit, () => T2).Execute(reading);

        Assert.Empty(audit.Records[0].TriggeredIslands);
        Assert.Contains("High Pressure Emergency Loop", after.Origin);
    }
}
