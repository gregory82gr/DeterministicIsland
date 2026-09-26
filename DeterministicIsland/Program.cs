using DeterministicIsland;
using DeterministicIsland.domain;
using DeterministicIsland.Islands;
using DeterministicIsland.ProbabilisticCore;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== NEXUS-1 COMPLETE POC: ALL 4 ARCHITECTURAL SCENARIOS ===");
        var vault = new StaticVault();
        SafetyLimits.SeedDefaults(vault, validFrom: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), approvedBy: "Shift Safety Engineer");
        var ai = new MiniNeuralNetwork();
        var arbiter = new CompleteSystemArbiter(ai, vault);

        // -------------------------------------------------------------------
        // Σενάριο 1: Κανονική λειτουργία (η AI αποφασίζει, πάντα μέσα στο όριο του Shield)
        // -------------------------------------------------------------------
        Console.WriteLine("\n--- Scenario 1: Normal Operation (AI Decides, Bounded by the Shield) ---");
        var r1 = new SensorReading(TemperatureCelsius: 60.0, PressureBar: 5.0, "Standard daily log check.", RadiationLeakDetected: false);
        arbiter.Execute(r1);

        // -------------------------------------------------------------------
        // Σενάριο 2: Υψηλή θερμοκρασία ενεργοποιεί τη νησίδα Structural
        // -------------------------------------------------------------------
        Console.WriteLine("\n--- Scenario 2: High Temperature (Structural Island) ---");
        var r2 = new SensorReading(TemperatureCelsius: 95.0, PressureBar: 4.0, "Execute command and retrieve latest sensor telemetry.", RadiationLeakDetected: false);
        arbiter.Execute(r2);

        // -------------------------------------------------------------------
        // Σενάριο 3: Σύγκρουση Νησίδων διαφορετικής προτεραιότητας (Κερδίζει η ανώτερη)
        // -------------------------------------------------------------------
        Console.WriteLine("\n--- Scenario 3: Standard Priority Resolution (Critical beats Structural) ---");
        var r3 = new SensorReading(TemperatureCelsius: 95.0, PressureBar: 8.5, "Execute within deterministic island.", RadiationLeakDetected: false);
        arbiter.Execute(r3);

        // -------------------------------------------------------------------
        // Σενάριο 4: Το Απόλυτο Αδιέξοδο (Δύο νησίδες CriticalSafety συγκρούονται)
        // -------------------------------------------------------------------
        Console.WriteLine("\n--- Scenario 4: The Ultimate Deadlock (Same Priority Conflict -> SCRAM) ---");
        var r4 = new SensorReading(TemperatureCelsius: 40.0, PressureBar: 8.5, "Critical failure. Retrieve backup protocols immediately.", RadiationLeakDetected: true);
        ControlCommand finalCommand = arbiter.Execute(r4);

        Console.WriteLine($"\n[FINAL SYSTEM STATE] Is Scrammed: {finalCommand.IsScrammed} | Action Driven By: {finalCommand.Origin}");
        Console.WriteLine("=================================================================");
    }
}