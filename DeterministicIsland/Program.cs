using DeterministicIsland;
using DeterministicIsland.domain;
using DeterministicIsland.ProbabilisticCore;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== NEXUS-1 COMPLETE POC: ALL 4 ARCHITECTURAL SCENARIOS ===");
        var ai = new MiniNeuralNetwork();
        var arbiter = new CompleteSystemArbiter(ai);

        // -------------------------------------------------------------------
        // Σενάριο 1: Κανονική λειτουργία (Χωρίς λέξεις-κλειδιά, η AI τρέχει ελεύθερα)
        // -------------------------------------------------------------------
        Console.WriteLine("\n--- Scenario 1: Normal Operation (No Keywords, AI Approved) ---");
        var r1 = new SensorReading(TemperatureCelsius: 95.0, PressureBar: 8.5, "Standard daily log check.", RadiationLeakDetected: false);
        arbiter.Execute(r1);

        // -------------------------------------------------------------------
        // Σενάριο 2: Δυναμική ενεργοποίηση μίας μόνο νησίδας μέσω της λέξης "retrieve"
        // -------------------------------------------------------------------
        Console.WriteLine("\n--- Scenario 2: Dynamic Activation via 'retrieve' (Structural Island) ---");
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