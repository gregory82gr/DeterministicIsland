using DeterministicIsland.domain;
using DeterministicIsland.ProbabilisticCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeterministicIsland
{
    // =======================================================================
    // 3. THE COMPLETE ARBITER WITH FAIL-SAFE (SCRAM) ENGINE
    // =
    public class CompleteSystemArbiter
    {
        private readonly MiniNeuralNetwork _aiCore;

        public CompleteSystemArbiter(MiniNeuralNetwork aiCore)
        {
            _aiCore = aiCore;
        }

        public ControlCommand Execute(SensorReading reading)
        {
            ControlCommand currentCommand = _aiCore.Predict(reading);
            List<DynamicIsland> activeIslands = GenerateIslandsOnDemand(reading);

            if (!activeIslands.Any())
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[INFO] Safe Zone: No islands triggered. Executing: {currentCommand.Origin} -> {currentCommand.ValveOpeningTarget:P1}");
                Console.ResetColor();
                return currentCommand;
            }

            // Ταξινομούμε με βάση την προτεραιότητα
            var sortedIslands = activeIslands.OrderByDescending(i => i.Priority).ToList();
            double highestPriorityValue = (double)sortedIslands.First().Priority;

            // Βρίσκουμε όλες τις νησίδες που μοιράζονται την ίδια (μέγιστη) προτεραιότητα
            var topTierIslands = sortedIslands.Where(i => (double)i.Priority == highestPriorityValue).ToList();

            // Έλεγχος για αδιέξοδο (Deadlock): Ίδια προτεραιότητα, αλλά διαφορετική απαίτηση εξόδου
            bool hasConflictingDemands = topTierIslands.Select(i => i.EnforcedOutput).Distinct().Count() > 1;

            if (topTierIslands.Count > 1 && hasConflictingDemands)
            {
                // Στρατηγική Fail-Safe / SCRAM (Page 94)
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine($"\n[FATAL DEADLOCK] Multiple islands triggered at the SAME priority level ({topTierIslands.First().Priority}) with conflicting demands!");
                foreach (var island in topTierIslands)
                {
                    Console.WriteLine($"  !! Island: '{island.Name}' demands Valve Opening -> {island.EnforcedOutput:P1}");
                }
                Console.WriteLine($"[FAIL-SAFE] Arbiter cannot resolve conflict logically. Triggering immediate SCRAM shutdown procedure...");
                Console.ResetColor();

                return new ControlCommand(0.0, "FAIL-SAFE EMERGENCY SCRAM", IsScrammed: true);
            }

            // Αν δεν υπάρχει αδιέξοδο, κερδίζει η κορυφαία νησίδα
            DynamicIsland winningIsland = topTierIslands.First();

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[AUDIT OVERRIDE] Island '{winningIsland.Name}' ({winningIsland.Priority}) took control.");
            Console.ResetColor();

            return new ControlCommand(winningIsland.EnforcedOutput, $"Deterministic Island: {winningIsland.Name}");
        }

        private List<DynamicIsland> GenerateIslandsOnDemand(SensorReading reading)
        {
            var triggeredIslands = new List<DynamicIsland>();
            string notes = reading.OperatorNotes.ToLower();

            if (notes.Contains("deterministic island") || notes.Contains("retrieve"))
            {
                // Νησίδα 1: Κρίσιμη Ασφάλεια Πίεσης (Priority: CriticalSafety)
                var pressureIsland = new DynamicIsland(
                    Name: "High Pressure Emergency Loop",
                    Priority: IslandPriority.CriticalSafety,
                    Condition: (r) => r.PressureBar >= 8.0,
                    EnforcedOutput: 1.0 // Ζητάει 100% άνοιγμα βαλβίδας
                );

                // Νησίδα 2: Κρίσιμη Ασφάλεια Διαρροής Ραδιενέργειας (Priority: CriticalSafety)
                var radiationIsland = new DynamicIsland(
                    Name: "Radiation Leak Containment Boundary",
                    Priority: IslandPriority.CriticalSafety,
                    Condition: (r) => r.RadiationLeakDetected == true,
                    EnforcedOutput: 0.0 // Ζητάει 0% (κλείσιμο βαλβίδας) για να εγκλωβίσει τη ραδιενέργεια
                );

                if (pressureIsland.Condition(reading)) triggeredIslands.Add(pressureIsland);
                if (radiationIsland.Condition(reading)) triggeredIslands.Add(radiationIsland);
            }

            return triggeredIslands;
        }
    }

}
