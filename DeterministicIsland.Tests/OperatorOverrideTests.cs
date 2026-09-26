using DeterministicIsland.Audit;
using DeterministicIsland.domain;
using DeterministicIsland.Islands;
using DeterministicIsland.ProbabilisticCore;

namespace DeterministicIsland.Tests;

public class OperatorOverrideTests
{
    private static readonly DateTime SeededAt = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Now = new(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly StaticVault _vault = new();
    private readonly InMemoryAuditLog _audit = new();
    private readonly CompleteSystemArbiter _arbiter;

    public OperatorOverrideTests()
    {
        SafetyLimits.SeedDefaults(_vault, SeededAt, "Engineer");
        _arbiter = new CompleteSystemArbiter(new MiniNeuralNetwork(), _vault, _audit, () => Now);
    }

    private static OperatorOverride Override(double opening) => new("operator-7", opening, "Manual flow test");

    [Fact]
    public void Override_ReplacesTheAiCommandAndIsAudited()
    {
        var command = _arbiter.Execute(new SensorReading(30.0, 5.0, "", false), Override(0.4));

        Assert.Equal(0.4, command.ValveOpeningTarget);
        Assert.Contains("operator-7", command.Origin);
        var record = _audit.Records.Single();
        Assert.Equal(ArbiterDecision.OperatorOverride, record.Decision);
        Assert.Equal("operator-7", record.Operator!.OperatorId);
        Assert.Equal("Manual flow test", record.Reason);
        Assert.InRange(record.AiProposal, 0.0, 1.0);
    }

    [Fact]
    public void Override_AnswersACausalityLockEscalation()
    {
        var command = _arbiter.Execute(new SensorReading(30.0, 5.0, "deterministic island", false), Override(0.3));

        Assert.Equal(0.3, command.ValveOpeningTarget);
        Assert.Equal(ArbiterDecision.OperatorOverride, _audit.Records.Single().Decision);
    }

    [Fact]
    public void Override_AnswersAnUncertaintyEscalation()
    {
        var command = _arbiter.Execute(new SensorReading(85.0, 7.0, "", false), Override(0.5));

        Assert.Equal(0.5, command.ValveOpeningTarget);
    }

    [Fact]
    public void Override_IsNotBoundByTheAiShieldLimit()
    {
        var command = _arbiter.Execute(new SensorReading(30.0, 5.0, "", false), Override(0.9));

        Assert.Equal(0.9, command.ValveOpeningTarget);
    }

    [Fact]
    public void Override_NeverOverridesATriggeredSafetyIsland()
    {
        var command = _arbiter.Execute(new SensorReading(95.0, 4.0, "", false), Override(0.1));

        Assert.Equal(0.6, command.ValveOpeningTarget);
        var record = _audit.Records.Single();
        Assert.Equal(ArbiterDecision.IslandOverride, record.Decision);
        Assert.Equal("operator-7", record.Operator!.OperatorId);
        Assert.Contains("not applied", record.Reason);
    }

    [Fact]
    public void Override_NeverOverridesAFailSafeScram()
    {
        var command = _arbiter.Execute(new SensorReading(40.0, 8.5, "", true), Override(1.0));

        Assert.True(command.IsScrammed);
        Assert.Contains("not applied", _audit.Records.Single().Reason);
    }

    [Theory]
    [InlineData("", 0.5, "reason")]
    [InlineData("operator-7", 0.5, " ")]
    [InlineData("operator-7", 1.5, "reason")]
    [InlineData("operator-7", -0.1, "reason")]
    [InlineData("operator-7", double.NaN, "reason")]
    public void InvalidOverride_IsRejectedBeforeAnythingIsDecided(string operatorId, double opening, string reason)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            _arbiter.Execute(new SensorReading(30.0, 5.0, "", false), new OperatorOverride(operatorId, opening, reason)));

        Assert.Empty(_audit.Records);
    }
}
