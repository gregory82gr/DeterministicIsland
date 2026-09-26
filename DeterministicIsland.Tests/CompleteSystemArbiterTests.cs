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

    // Runs one cycle and returns its audit record, whether the arbiter acted or escalated.
    private AuditRecord ExecuteAndAudit(CompleteSystemArbiter arbiter, SensorReading reading)
    {
        try { arbiter.Execute(reading); }
        catch (HumanEscalationException) { }
        return _audit.Records[^1];
    }

    [Fact]
    public void NormalOperation_ConfidentAiCommandIsApprovedWithinTheShieldLimit()
    {
        var arbiter = Arbiter();

        for (int i = 0; i < 50; i++)
        {
            var command = arbiter.Execute(new SensorReading(30.0, 5.0, "Routine check.", false));
            Assert.InRange(command.ValveOpeningTarget, 0.0, 0.75);
        }

        Assert.All(_audit.Records, r => Assert.Equal(ArbiterDecision.AiApproved, r.Decision));
        Assert.All(_audit.Records, r => Assert.InRange(r.AiUncertainty95!.Value, 0.0, 0.20));
    }

    [Fact]
    public void Shield_CapsAProposalAboveTheVaultLimit()
    {
        var shielded = new Shield(_vault).Enforce(new ControlCommand(0.9, "AI"), Now);

        Assert.Equal(0.75, shielded.ValveOpeningTarget);
        Assert.Contains("shielded", shielded.Origin);
    }

    [Fact]
    public void UncertainAiCommand_IsNotAppliedAndEscalatesToHuman()
    {
        // §13.6: at 85 °C / 7 bar the MC Dropout interval is about ±29%, above the vault's ±20%.
        Assert.Throws<UncertaintyEscalationException>(() => Arbiter().Execute(new SensorReading(85.0, 7.0, "", false)));

        var record = _audit.Records.Single();
        Assert.Equal(ArbiterDecision.HumanEscalation, record.Decision);
        Assert.True(record.RequiresHumanReview);
        Assert.Null(record.FinalValveOpening);
        Assert.True(record.AiUncertainty95 > 0.20);
    }

    [Fact]
    public void UncertaintyLimit_IsReadFromTheVault()
    {
        _vault.Register(SafetyLimits.MaxAiUncertainty95, 0.40, Now, "Engineer");

        var command = Arbiter().Execute(new SensorReading(85.0, 7.0, "", false));

        Assert.False(command.IsScrammed);
        Assert.Equal(ArbiterDecision.AiApproved, _audit.Records.Single().Decision);
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
        var command = Arbiter().Execute(new SensorReading(30.0, 5.0, "Retrieve the latest telemetry.", false));

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
        var after = ExecuteAndAudit(Arbiter(now: effective), reading);

        Assert.Equal(1.0, before.ValveOpeningTarget);
        Assert.DoesNotContain(after.TriggeredIslands, i => i.Name == "High Pressure Emergency Loop");
    }

    [Fact]
    public void MissingSafetyLimit_FailsLoudly()
    {
        var arbiter = new CompleteSystemArbiter(new MiniNeuralNetwork(), new StaticVault(), _audit, () => Now);

        Assert.Throws<InvalidOperationException>(() => arbiter.Execute(new SensorReading(60.0, 5.0, "", false)));
    }
}
