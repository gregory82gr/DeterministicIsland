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

    public record DynamicIsland(string Name, IslandPriority Priority, Func<SensorReading, bool> Condition, double EnforcedOutput, IReadOnlyList<string> Sources);
}
