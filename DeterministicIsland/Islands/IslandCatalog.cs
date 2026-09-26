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
            var reliefSetpoint = SafetyLimits.Require(vault, SafetyLimits.PressureReliefSetpointBar, asOf);
            var reliefOpening = SafetyLimits.Require(vault, SafetyLimits.PressureReliefValveOpening, asOf);
            var tempLimit = SafetyLimits.Require(vault, SafetyLimits.CoolantTemperatureLimitCelsius, asOf);
            var coolingOpening = SafetyLimits.Require(vault, SafetyLimits.StructuralCoolingValveOpening, asOf);
            var containmentOpening = SafetyLimits.Require(vault, SafetyLimits.RadiationContainmentValveOpening, asOf);

            return new List<DynamicIsland>
            {
                // Κρίσιμη Ασφάλεια Πίεσης: ζητά άνοιγμα βαλβίδας εκτόνωσης.
                new(Name: "High Pressure Emergency Loop",
                    Priority: IslandPriority.CriticalSafety,
                    Condition: r => r.PressureBar >= reliefSetpoint.Value,
                    EnforcedOutput: reliefOpening.Value,
                    Sources: new[]
                    {
                        SafetyLimits.Describe(SafetyLimits.PressureReliefSetpointBar, reliefSetpoint),
                        SafetyLimits.Describe(SafetyLimits.PressureReliefValveOpening, reliefOpening)
                    }),

                // Κρίσιμη Ασφάλεια Διαρροής: κλείνει τη βαλβίδα για να εγκλωβίσει τη ραδιενέργεια.
                new(Name: "Radiation Leak Containment Boundary",
                    Priority: IslandPriority.CriticalSafety,
                    Condition: r => r.RadiationLeakDetected,
                    EnforcedOutput: containmentOpening.Value,
                    Sources: new[]
                    {
                        SafetyLimits.Describe(SafetyLimits.RadiationContainmentValveOpening, containmentOpening)
                    }),

                // Δομική προστασία: ψύξη όταν η θερμοκρασία ξεπερνά το όριο.
                new(Name: "Structural Thermal Protection",
                    Priority: IslandPriority.Structural,
                    Condition: r => r.TemperatureCelsius >= tempLimit.Value,
                    EnforcedOutput: coolingOpening.Value,
                    Sources: new[]
                    {
                        SafetyLimits.Describe(SafetyLimits.CoolantTemperatureLimitCelsius, tempLimit),
                        SafetyLimits.Describe(SafetyLimits.StructuralCoolingValveOpening, coolingOpening)
                    })
            };
        }

        public static List<DynamicIsland> Triggered(SensorReading reading, StaticVault vault, DateTime asOf) =>
            Build(vault, asOf).Where(i => i.Condition(reading)).ToList();
    }
}
