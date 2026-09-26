using DeterministicIsland.domain;
using DeterministicIsland.Islands;
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
        private readonly StaticVault _vault;
        private readonly Shield _shield;
        private readonly Func<DateTime> _clock;

        public CompleteSystemArbiter(MiniNeuralNetwork aiCore, StaticVault vault, Func<DateTime>? clock = null)
        {
            _aiCore = aiCore;
            _vault = vault;
            _shield = new Shield(vault);
            _clock = clock ?? (() => DateTime.UtcNow);
        }

        public ControlCommand Execute(SensorReading reading)
        {
            DateTime now = _clock();
            ControlCommand aiProposal = _aiCore.Predict(reading);

            // Οι νησίδες αξιολογούνται σε κάθε κύκλο, όχι μόνο όταν το ζητήσει ο χειριστής (§17.6).
            List<DynamicIsland> activeIslands = IslandCatalog.Triggered(reading, _vault, now);

            if (!activeIslands.Any())
            {
                // Ακόμη και χωρίς νησίδα, η πρόταση του AI περνά από το Shield (§17.4).
                ControlCommand shielded = _shield.Enforce(aiProposal, now);
                Console.ForegroundColor = shielded == aiProposal ? ConsoleColor.Green : ConsoleColor.Yellow;
                Console.WriteLine($"[INFO] Safe Zone: No islands triggered. Executing: {shielded.Origin} -> {shielded.ValveOpeningTarget:P1}");
                Console.ResetColor();
                return shielded;
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
                // Στρατηγική Fail-Safe / SCRAM: επέκταση του POC, όχι κανόνας του βιβλίου.
                // Το βιβλίο (§12.4.3, σελ. 94) ζητά η διαφωνία να καταγράφεται για ανθρώπινο έλεγχο.
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
            foreach (var source in winningIsland.Sources)
                Console.WriteLine($"  -> Static Vault: {source}");
            Console.ResetColor();

            return new ControlCommand(winningIsland.EnforcedOutput, $"Deterministic Island: {winningIsland.Name}");
        }
    }

}
