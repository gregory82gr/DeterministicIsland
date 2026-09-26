using DeterministicIsland.domain;

namespace DeterministicIsland.Islands
{
    // Builds the protection islands for one control cycle. Every threshold and every
    // enforced output is resolved from the Static Vault at the cycle's timestamp, so an
    // approved update of a setpoint changes the island without touching this code.
    public static class IslandCatalog
    {
        public static List<DynamicIsland> Build(StaticVault vault, DateTime asOf)
        {
            var reliefSetpoint = SafetyLimits.RequireFact(vault, SafetyLimits.PressureReliefSetpointBar, asOf);
            var reliefOpening = SafetyLimits.RequireFact(vault, SafetyLimits.PressureReliefValveOpening, asOf);
            var tempLimit = SafetyLimits.RequireFact(vault, SafetyLimits.CoolantTemperatureLimitCelsius, asOf);
            var coolingOpening = SafetyLimits.RequireFact(vault, SafetyLimits.StructuralCoolingValveOpening, asOf);
            var containmentOpening = SafetyLimits.RequireFact(vault, SafetyLimits.RadiationContainmentValveOpening, asOf);

            return new List<DynamicIsland>
            {
                // Κρίσιμη Ασφάλεια Πίεσης: ζητά άνοιγμα βαλβίδας εκτόνωσης.
                new(Name: "High Pressure Emergency Loop",
                    Priority: IslandPriority.CriticalSafety,
                    Condition: r => r.PressureBar >= reliefSetpoint.Vector.Value,
                    EnforcedOutput: reliefOpening.Vector.Value,
                    Facts: new[] { reliefSetpoint, reliefOpening }),

                // Κρίσιμη Ασφάλεια Διαρροής: κλείνει τη βαλβίδα για να εγκλωβίσει τη ραδιενέργεια.
                new(Name: "Radiation Leak Containment Boundary",
                    Priority: IslandPriority.CriticalSafety,
                    Condition: r => r.RadiationLeakDetected,
                    EnforcedOutput: containmentOpening.Vector.Value,
                    Facts: new[] { containmentOpening }),

                // Δομική προστασία: ψύξη όταν η θερμοκρασία ξεπερνά το όριο.
                new(Name: "Structural Thermal Protection",
                    Priority: IslandPriority.Structural,
                    Condition: r => r.TemperatureCelsius >= tempLimit.Vector.Value,
                    EnforcedOutput: coolingOpening.Vector.Value,
                    Facts: new[] { tempLimit, coolingOpening })
            };
        }

        public static List<DynamicIsland> Triggered(SensorReading reading, StaticVault vault, DateTime asOf) =>
            Build(vault, asOf).Where(i => i.Condition(reading)).ToList();
    }
}
