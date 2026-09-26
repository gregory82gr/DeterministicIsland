using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeterministicIsland.domain
{
    public enum IslandPriority { Operational = 1, Structural = 2, CriticalSafety = 3 }
    public record SensorReading(double TemperatureCelsius, double PressureBar, string OperatorNotes, bool RadiationLeakDetected);
    public record ControlCommand(double ValveOpeningTarget, string Origin, bool IsScrammed = false)
    {
        public double ValveOpeningTarget { get; init; } = IsScrammed ? 0.0 : Math.Clamp(ValveOpeningTarget, 0.0, 1.0);
    }

    // A command given by a named operator (Constitution rule 5, human override authority).
    // It replaces the AI's proposal, and answers a Human-in-the-Loop escalation, but it never
    // overrides a triggered safety island: certified safety functions keep final physical
    // authority (§17.6).
    public sealed record OperatorOverride(string OperatorId, double ValveOpening, string Reason)
    {
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(OperatorId))
                throw new ArgumentException("An operator override must name the operator.", nameof(OperatorId));
            if (string.IsNullOrWhiteSpace(Reason))
                throw new ArgumentException("An operator override must state a reason.", nameof(Reason));
            if (double.IsNaN(ValveOpening) || ValveOpening < 0.0 || ValveOpening > 1.0)
                throw new ArgumentOutOfRangeException(nameof(ValveOpening), "The valve opening must be between 0 and 1.");
        }
    }

    public record DynamicIsland(string Name, IslandPriority Priority, Func<SensorReading, bool> Condition, double EnforcedOutput, IReadOnlyList<VaultFact> Facts)
    {
        public IReadOnlyList<string> Sources => Facts.Select(f => f.Describe()).ToList();
    }
}
