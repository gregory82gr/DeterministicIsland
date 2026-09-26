using DeterministicIsland.Audit;
using DeterministicIsland.domain;
using DeterministicIsland.Events;
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
        // Number of MC Dropout forward passes per control cycle (§13.6).
        public const int McDropoutPasses = 200;

        private readonly MiniNeuralNetwork _aiCore;
        private readonly MiniNeuralNetwork? _frozenSnapshot;
        private readonly StaticVault _vault;
        private readonly Shield _shield;
        private readonly DomainEventBus _events;
        private readonly Func<DateTime> _clock;

        // The Arbiter publishes domain events (§22.7); auditing and any other concern are
        // listeners on the bus, which the Arbiter does not know about (§23.4).
        public CompleteSystemArbiter(MiniNeuralNetwork aiCore, StaticVault vault, DomainEventBus events,
            Func<DateTime>? clock = null, MiniNeuralNetwork? frozenSnapshot = null)
        {
            if (frozenSnapshot is { IsFrozen: false })
                throw new ArgumentException("The frozen snapshot core must be created with a FrozenSnapshotConfig.", nameof(frozenSnapshot));

            _aiCore = aiCore;
            _frozenSnapshot = frozenSnapshot;
            _vault = vault;
            _shield = new Shield(vault);
            _events = events;
            _clock = clock ?? (() => DateTime.UtcNow);
        }

        // Convenience: a private bus whose only listener writes to the given audit log.
        public CompleteSystemArbiter(MiniNeuralNetwork aiCore, StaticVault vault, IAuditLog auditLog,
            Func<DateTime>? clock = null, MiniNeuralNetwork? frozenSnapshot = null)
            : this(aiCore, vault, new DomainEventBus().Subscribe(new AuditLogListener(auditLog)), clock, frozenSnapshot)
        {
        }

        private void RaiseIslandTriggered(VaultFact fact, DateTime occurredAt) =>
            _events.Publish(new IslandTriggered(
                StaticVault.HashQuery(StaticVault.Normalize(fact.Query)), fact.Query,
                fact.Vector.VectorId, fact.Vector.Determinism?.Version ?? "", occurredAt));

        // §12.4.2: answers a factual query in order of guarantee strength. Static Vault first
        // (following any pointer chain), then a caller-supplied candidate (e.g. from RAG
        // retrieval), and only then the stochastic generator, signalled by returning null.
        // With requireDeterminism the Causality Lock is engaged: no silent stochastic fallback.
        public ILMVector? Answer(string query, ILMVector? candidate = null, bool requireDeterminism = false)
        {
            DateTime now = _clock();
            var causalityLock = new CausalityLock();
            if (requireDeterminism)
                causalityLock.Engage();

            var vaultHit = _vault.Lookup(query, now);
            if (vaultHit is not null)
            {
                var resolved = GuardedResolver.ResolveUnderLock(vaultHit, _vault, causalityLock, now);
                RaiseIslandTriggered(new VaultFact(query, resolved), now);
                return resolved;
            }

            if (candidate is not null)
                return GuardedResolver.ResolveUnderLock(candidate, _vault, causalityLock, now);

            if (causalityLock.IsEngaged)
                throw new DeterminismViolationException("No deterministic mechanism resolved this query.");

            return null;
        }

        public ControlCommand Execute(SensorReading reading)
        {
            DateTime now = _clock();
            var causalityLock = new CausalityLock();
            if (CausalityLock.IsRequestedBy(reading.OperatorNotes))
                causalityLock.Engage();

            // Με ενεργό Causality Lock, μόνο το Frozen Snapshot είναι αποδεκτός πυρήνας (§12.4.3).
            MiniNeuralNetwork core = causalityLock.IsEngaged && _frozenSnapshot is not null ? _frozenSnapshot : _aiCore;

            // Ο στοχαστικός πυρήνας δίνει μέσο όρο και διάστημα αβεβαιότητας με MC Dropout (§13.6).
            // Το Frozen Snapshot είναι εξ ορισμού ντετερμινιστικό, οπότε αρκεί μία εκτέλεση.
            UncertainPrediction? estimate = core.IsFrozen ? null : core.PredictWithUncertainty(reading, McDropoutPasses);
            ControlCommand aiProposal = estimate is null
                ? core.Predict(reading)
                : new ControlCommand(estimate.Mean, $"{estimate.Origin} (MC Dropout mean of {estimate.Passes})");

            // Οι νησίδες αξιολογούνται σε κάθε κύκλο, όχι μόνο όταν το ζητήσει ο χειριστής (§17.6).
            List<DynamicIsland> activeIslands = IslandCatalog.Triggered(reading, _vault, now);
            foreach (var fact in activeIslands.SelectMany(i => i.Facts))
                RaiseIslandTriggered(fact, now);

            AuditRecord Audit(ArbiterDecision decision, ControlCommand? command, bool requiresHumanReview, string? reason = null) => new()
            {
                Timestamp = now,
                Reading = reading,
                CausalityLockEngaged = causalityLock.IsEngaged,
                AiProposal = aiProposal.ValveOpeningTarget,
                AiOrigin = aiProposal.Origin,
                AiUncertainty95 = estimate?.HalfWidth95,
                TriggeredIslands = activeIslands
                    .Select(i => new IslandSnapshot(i.Name, i.Priority, i.EnforcedOutput, i.Sources))
                    .ToList(),
                Decision = decision,
                FinalValveOpening = command?.ValveOpeningTarget,
                Origin = command?.Origin ?? "Human-in-the-Loop",
                RequiresHumanReview = requiresHumanReview,
                Reason = reason
            };

            if (!activeIslands.Any())
            {
                // Χωρίς νησίδα και χωρίς Frozen Snapshot, το Causality Lock δεν επιτρέπει στοχαστική απάντηση:
                // κλιμάκωση σε άνθρωπο, ποτέ σιωπηλή επιστροφή στο AI (§12.3.3).
                if (causalityLock.IsEngaged && !core.IsFrozen)
                {
                    const string reason = "No deterministic mechanism resolved this reading, but a Causality Lock is engaged.";
                    _events.Publish(new ControlDecisionMade(Audit(ArbiterDecision.HumanEscalation, null, requiresHumanReview: true, reason)));

                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.WriteLine("[HUMAN-IN-THE-LOOP] Causality Lock engaged, but no deterministic mechanism resolved this reading.");
                    Console.ResetColor();
                    throw new DeterminismViolationException(reason);
                }

                // Αν το AI είναι πολύ αβέβαιο, η εντολή του δεν εφαρμόζεται: κλιμάκωση σε άνθρωπο.
                // Το ανεκτό εύρος είναι γεγονός του Static Vault, όχι επιλογή του μοντέλου (§16.9).
                double maxUncertainty = SafetyLimits.Require(_vault, SafetyLimits.MaxAiUncertainty95, now).Value;
                if (estimate is not null && estimate.HalfWidth95 > maxUncertainty)
                {
                    string reason = $"AI prediction {estimate.Mean:P1} ± {estimate.HalfWidth95:P1} (95%) exceeds the permitted ± {maxUncertainty:P1}.";
                    _events.Publish(new ControlDecisionMade(Audit(ArbiterDecision.HumanEscalation, null, requiresHumanReview: true, reason)));

                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.WriteLine($"[HUMAN-IN-THE-LOOP] {reason}");
                    Console.ResetColor();
                    throw new UncertaintyEscalationException(reason);
                }

                // Ακόμη και χωρίς νησίδα, η πρόταση του AI περνά από το Shield (§17.4).
                ControlCommand shielded = _shield.Enforce(aiProposal, now);
                bool wasShielded = shielded != aiProposal;
                _events.Publish(new ControlDecisionMade(Audit(wasShielded ? ArbiterDecision.AiShielded : ArbiterDecision.AiApproved, shielded, requiresHumanReview: false)));

                Console.ForegroundColor = wasShielded ? ConsoleColor.Yellow : ConsoleColor.Green;
                string interval = estimate is null ? "" : $" (95% interval {estimate.Lower95:P1} to {estimate.Upper95:P1})";
                Console.WriteLine($"[INFO] Safe Zone: No islands triggered. Executing: {shielded.Origin} -> {shielded.ValveOpeningTarget:P1}{interval}");
                Console.ResetColor();
                return shielded;
            }

            // Ταξινομούμε με βάση την προτεραιότητα
            var sortedIslands = activeIslands.OrderByDescending(i => i.Priority).ToList();
            IslandPriority highestPriority = sortedIslands.First().Priority;

            // Βρίσκουμε όλες τις νησίδες που μοιράζονται την ίδια (μέγιστη) προτεραιότητα
            var topTierIslands = sortedIslands.Where(i => i.Priority == highestPriority).ToList();

            // Έλεγχος για αδιέξοδο (Deadlock): Ίδια προτεραιότητα, αλλά διαφορετική απαίτηση εξόδου
            bool hasConflictingDemands = topTierIslands.Select(i => i.EnforcedOutput).Distinct().Count() > 1;

            if (topTierIslands.Count > 1 && hasConflictingDemands)
            {
                // Στρατηγική Fail-Safe / SCRAM: επέκταση του POC, όχι κανόνας του βιβλίου.
                // Το βιβλίο (§12.4.3, σελ. 94) ζητά η διαφωνία να καταγράφεται για ανθρώπινο έλεγχο,
                // οπότε η εγγραφή ελέγχου σημειώνεται RequiresHumanReview.
                var scram = new ControlCommand(0.0, "FAIL-SAFE EMERGENCY SCRAM", IsScrammed: true);
                _events.Publish(new ControlDecisionMade(Audit(ArbiterDecision.Scram, scram, requiresHumanReview: true,
                    $"Conflicting demands at the same priority level ({highestPriority}): " +
                    string.Join(", ", topTierIslands.Select(i => $"{i.Name} -> {i.EnforcedOutput:P1}")))));

                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine($"\n[FATAL DEADLOCK] Multiple islands triggered at the SAME priority level ({highestPriority}) with conflicting demands!");
                foreach (var island in topTierIslands)
                {
                    Console.WriteLine($"  !! Island: '{island.Name}' demands Valve Opening -> {island.EnforcedOutput:P1}");
                }
                Console.WriteLine($"[FAIL-SAFE] Arbiter cannot resolve conflict logically. Triggering immediate SCRAM shutdown procedure...");
                Console.WriteLine("[AUDIT] Conflict logged for human review (§12.4.3).");
                Console.ResetColor();

                return scram;
            }

            // Αν δεν υπάρχει αδιέξοδο, κερδίζει η κορυφαία νησίδα
            DynamicIsland winningIsland = topTierIslands.First();
            var command = new ControlCommand(winningIsland.EnforcedOutput, $"Deterministic Island: {winningIsland.Name}");
            _events.Publish(new ControlDecisionMade(Audit(ArbiterDecision.IslandOverride, command, requiresHumanReview: false)));

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[AUDIT OVERRIDE] Island '{winningIsland.Name}' ({winningIsland.Priority}) took control.");
            foreach (var source in winningIsland.Sources)
                Console.WriteLine($"  -> Static Vault: {source}");
            Console.ResetColor();

            return command;
        }
    }

}
