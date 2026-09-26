using DeterministicIsland;
using DeterministicIsland.Audit;
using DeterministicIsland.domain;
using DeterministicIsland.Islands;
using DeterministicIsland.ProbabilisticCore;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== NEXUS-1 COMPLETE POC: ALL 6 ARCHITECTURAL SCENARIOS ===");
        var vault = new StaticVault();
        SafetyLimits.SeedDefaults(vault, validFrom: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), approvedBy: "Shift Safety Engineer");
        var auditLog = new JsonLinesAuditLog(Path.Combine(AppContext.BaseDirectory, "nexus1-audit.jsonl"));
        var ai = new MiniNeuralNetwork();
        var arbiter = new CompleteSystemArbiter(ai, vault, auditLog);

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

        // -------------------------------------------------------------------
        // Σενάριο 5: Causality Lock χωρίς ντετερμινιστική απάντηση -> Human-in-the-Loop
        // -------------------------------------------------------------------
        Console.WriteLine("\n--- Scenario 5: Causality Lock Without a Deterministic Answer (Human-in-the-Loop) ---");
        var r5 = new SensorReading(TemperatureCelsius: 60.0, PressureBar: 5.0, "Set the valve within a deterministic island.", RadiationLeakDetected: false);
        try
        {
            arbiter.Execute(r5);
        }
        catch (DeterminismViolationException ex)
        {
            Console.WriteLine($"[ESCALATED] {ex.Message} The AI command was NOT applied; awaiting operator decision.");
        }

        // -------------------------------------------------------------------
        // Σενάριο 6: Causality Lock με Frozen Snapshot -> ίδια απάντηση κάθε φορά
        // -------------------------------------------------------------------
        Console.WriteLine("\n--- Scenario 6: Causality Lock Resolved by a Frozen Snapshot (Bitwise Reproducible) ---");
        var frozenCore = new MiniNeuralNetwork(MiniNeuralNetwork.CreateSnapshot(randomSeed: 42));
        var lockedArbiter = new CompleteSystemArbiter(ai, vault, auditLog, frozenSnapshot: frozenCore);
        var first = lockedArbiter.Execute(r5);
        var second = lockedArbiter.Execute(r5);
        Console.WriteLine($"[REPRODUCIBILITY] Identical output on repeat: {first.ValveOpeningTarget.Equals(second.ValveOpeningTarget)}");

        Console.WriteLine($"\n[AUDIT TRAIL] Every decision above was appended to {auditLog.Path}");
        Console.WriteLine("=================================================================");
    }
}