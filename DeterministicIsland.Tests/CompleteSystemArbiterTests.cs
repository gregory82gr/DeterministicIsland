using DeterministicIsland.Audit;
using DeterministicIsland.domain;
using DeterministicIsland.Islands;
using DeterministicIsland.ProbabilisticCore;

namespace DeterministicIsland.Tests;

public class CompleteSystemArbiterTests
{
    private static readonly DateTime SeededAt = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Now = new(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly StaticVault _vault = new();
    private readonly InMemoryAuditLog _audit = new();

    public CompleteSystemArbiterTests() => SafetyLimits.SeedDefaults(_vault, SeededAt, "Engineer");

    private CompleteSystemArbiter Arbiter(MiniNeuralNetwork? frozen = null, DateTime? now = null) =>
        new(new MiniNeuralNetwork(), _vault, _audit, () => now ?? Now, frozen);

    [Fact]
    public void NormalOperation_AiCommandNeverExceedsTheShieldLimit()
    {
        var arbiter = Arbiter();

        for (int i = 0; i < 200; i++)
        {
            var command = arbiter.Execute(new SensorReading(60.0, 7.5, "Routine check.", false));
            Assert.InRange(command.ValveOpeningTarget, 0.0, 0.75);
        }

        Assert.All(_audit.Records, r => Assert.Contains(r.Decision, new[] { ArbiterDecision.AiApproved, ArbiterDecision.AiShielded }));
        Assert.All(_audit.Records.Where(r => r.Decision == ArbiterDecision.AiShielded), r => Assert.True(r.AiProposal > 0.75));
    }

    [Fact]
    public void HighTemperature_StructuralIslandTakesControl()
    {
        var command = Arbiter().Execute(new SensorReading(95.0, 4.0, "", false));

        Assert.Equal(0.6, command.ValveOpeningTarget);
        Assert.Contains("Structural Thermal Protection", command.Origin);
        Assert.Equal(ArbiterDecision.IslandOverride, _audit.Records.Single().Decision);
    }

    [Fact]
    public void CriticalSafety_BeatsStructural()
    {
        var command = Arbiter().Execute(new SensorReading(95.0, 8.5, "", false));

        Assert.Equal(1.0, command.ValveOpeningTarget);
        Assert.Contains("High Pressure Emergency Loop", command.Origin);
        Assert.Equal(2, _audit.Records.Single().TriggeredIslands.Count);
    }

    [Fact]
    public void SamePriorityConflict_ScramsAndFlagsForHumanReview()
    {
        var command = Arbiter().Execute(new SensorReading(40.0, 8.5, "", true));

        Assert.True(command.IsScrammed);
        Assert.Equal(0.0, command.ValveOpeningTarget);
        var record = _audit.Records.Single();
        Assert.Equal(ArbiterDecision.Scram, record.Decision);
        Assert.True(record.RequiresHumanReview);
    }

    [Fact]
    public void CausalityLock_WithoutDeterministicAnswer_EscalatesToHuman()
    {
        var reading = new SensorReading(60.0, 5.0, "Run inside a Deterministic Island please.", false);

        Assert.Throws<DeterminismViolationException>(() => Arbiter().Execute(reading));

        var record = _audit.Records.Single();
        Assert.Equal(ArbiterDecision.HumanEscalation, record.Decision);
        Assert.True(record.CausalityLockEngaged);
        Assert.True(record.RequiresHumanReview);
        Assert.Null(record.FinalValveOpening);
    }

    [Fact]
    public void CausalityLock_WithFrozenSnapshot_IsReproducible()
    {
        var frozen = new MiniNeuralNetwork(MiniNeuralNetwork.CreateSnapshot(randomSeed: 42));
        var arbiter = Arbiter(frozen);
        var reading = new SensorReading(60.0, 5.0, "Deterministic island requested.", false);

        var first = arbiter.Execute(reading);
        var second = arbiter.Execute(reading);

        Assert.Equal(first.ValveOpeningTarget, second.ValveOpeningTarget);
        Assert.Contains("Frozen Snapshot", first.Origin);
    }

    [Fact]
    public void CausalityLock_IsNotEngagedByRetrieve()
    {
        var command = Arbiter().Execute(new SensorReading(60.0, 5.0, "Retrieve the latest telemetry.", false));

        Assert.False(_audit.Records.Single().CausalityLockEngaged);
        Assert.False(command.IsScrammed);
    }

    [Fact]
    public void ApprovedSetpointUpdate_ChangesIslandBehaviourFromItsEffectiveDate()
    {
        var effective = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        _vault.Register(SafetyLimits.PressureReliefSetpointBar, 9.0, effective, "Engineer");
        var reading = new SensorReading(60.0, 8.5, "", false);

        var before = Arbiter(now: effective.AddDays(-1)).Execute(reading);
        var after = Arbiter(now: effective).Execute(reading);

        Assert.Equal(1.0, before.ValveOpeningTarget);
        Assert.DoesNotContain("High Pressure", after.Origin);
    }

    [Fact]
    public void MissingSafetyLimit_FailsLoudly()
    {
        var arbiter = new CompleteSystemArbiter(new MiniNeuralNetwork(), new StaticVault(), _audit, () => Now);

        Assert.Throws<InvalidOperationException>(() => arbiter.Execute(new SensorReading(60.0, 5.0, "", false)));
    }
}
