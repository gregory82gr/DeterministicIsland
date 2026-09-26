using DeterministicIsland;
using DeterministicIsland.Audit;
using DeterministicIsland.domain;
using DeterministicIsland.Events;
using DeterministicIsland.Governance;
using DeterministicIsland.Islands;
using DeterministicIsland.ProbabilisticCore;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== NEXUS-1 COMPLETE POC: ALL 11 ARCHITECTURAL SCENARIOS ===");

        // Neural Constitution (§26.4): οι εγγυήσεις που το σύστημα δεν παραβιάζει ποτέ.
        Console.WriteLine("\n[NEURAL CONSTITUTION]");
        for (int i = 0; i < NeuralConstitution.Rules.Count; i++)
        {
            var rule = NeuralConstitution.Rules[i];
            string source = rule.Source == RuleSource.Book ? "book" : "POC extension";
            Console.WriteLine($"  {i + 1}. {rule.Name} ({source}; {rule.VerifiedBy.Count} tests)");
        }
        // Domain Events (§22.7): ο Arbiter και το Vault δημοσιεύουν γεγονότα· το audit trail και
        // το ημερολόγιο γεγονότων είναι listeners που αυτοί δεν γνωρίζουν (§23.4).
        var auditLog = new JsonLinesAuditLog(Path.Combine(AppContext.BaseDirectory, "nexus1-audit.jsonl"));
        var eventLog = new JsonLinesEventLog(Path.Combine(AppContext.BaseDirectory, "nexus1-events.jsonl"));
        var recorder = new InMemoryEventRecorder();
        var events = new DomainEventBus()
            .Subscribe(new AuditLogListener(auditLog))
            .Subscribe<IslandAdded>(eventLog).Subscribe<IslandTriggered>(eventLog)
            .Subscribe<IslandAdded>(recorder).Subscribe<IslandTriggered>(recorder);

        // Μόνιμη αποθήκευση (§23.2): κάθε νέα έκδοση γράφεται σε αρχείο JSON Lines.
        // Το demo ξεκινά από καθαρό αρχείο ώστε τα σενάρια να δίνουν πάντα το ίδιο αποτέλεσμα.
        string vaultPath = Path.Combine(AppContext.BaseDirectory, "nexus1-vault.jsonl");
        File.Delete(vaultPath);
        var vault = new StaticVault(events, new JsonLinesIslandRepository(vaultPath));
        SafetyLimits.SeedDefaults(vault, validFrom: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), approvedBy: "Shift Safety Engineer");
        var ai = new MiniNeuralNetwork();
        var arbiter = new CompleteSystemArbiter(ai, vault, events);

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
        var lockedArbiter = new CompleteSystemArbiter(ai, vault, events, frozenSnapshot: frozenCore);
        var first = lockedArbiter.Execute(r5);
        var second = lockedArbiter.Execute(r5);
        Console.WriteLine($"[REPRODUCIBILITY] Identical output on repeat: {first.ValveOpeningTarget.Equals(second.ValveOpeningTarget)}");

        // -------------------------------------------------------------------
        // Σενάριο 7: Intra-Vector Routing -> η γραμμή Β παραπέμπει στο όριο της γραμμής Α
        // -------------------------------------------------------------------
        Console.WriteLine("\n--- Scenario 7: Intra-Vector Routing (Backup Line Defers to the Primary Setpoint) ---");
        const string backupLineSetpoint = "high pressure relief setpoint for backup line b in bar";
        var primarySetpoint = vault.Lookup(SafetyLimits.PressureReliefSetpointBar, DateTime.UtcNow)!;
        vault.RegisterPointer(backupLineSetpoint, primarySetpoint.VectorId,
            validFrom: new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc), approvedBy: "Shift Safety Engineer");

        var routed = arbiter.Answer(backupLineSetpoint, requireDeterminism: true)!;
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"[ROUTED] '{backupLineSetpoint}' -> vector {routed.VectorId} = {routed.Value} bar ({routed.Determinism!.Version}, deterministic: {routed.Determinism.IsDeterministic})");
        Console.ResetColor();

        // Ένα ordinary vector από RAG, χωρίς Determinism block, δεν γίνεται δεκτό υπό Causality Lock.
        var ragCandidate = new ILMVector { VectorId = Guid.NewGuid(), Embedding = new[] { 7.2 } };
        try
        {
            arbiter.Answer("relief setpoint quoted in an old maintenance manual", ragCandidate, requireDeterminism: true);
        }
        catch (DeterminismViolationException ex)
        {
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine($"[HUMAN-IN-THE-LOOP] {ex.Message}");
            Console.ResetColor();
        }

        // -------------------------------------------------------------------
        // Σενάριο 8: Παράγωγη νησίδα -> το όριο εκτόνωσης = min(PT-1, PT-2, PT-3)
        // -------------------------------------------------------------------
        Console.WriteLine("\n--- Scenario 8: Derived Island (Relief Setpoint = Minimum of Three Transmitters) ---");
        var r8 = new SensorReading(TemperatureCelsius: 60.0, PressureBar: 7.8, "Routine check.", RadiationLeakDetected: false);
        Console.WriteLine($"[DERIVED] {SafetyLimits.Describe(SafetyLimits.PressureReliefSetpointBar, vault.Resolve(SafetyLimits.PressureReliefSetpointBar, DateTime.UtcNow))}");

        // Η PT-2 επαναβαθμονομείται στα 7.6 bar: η παράγωγη νησίδα υπολογίζεται ξανά αυτόματα.
        vault.Register(SafetyLimits.PressureTransmitterLimitsBar[1], 7.6, DateTime.UtcNow, approvedBy: "Instrumentation Engineer");
        Console.WriteLine($"[DERIVED] {SafetyLimits.Describe(SafetyLimits.PressureReliefSetpointBar, vault.Resolve(SafetyLimits.PressureReliefSetpointBar, DateTime.UtcNow))}");
        arbiter.Execute(r8);

        // -------------------------------------------------------------------
        // Σενάριο 9: Το AI είναι πολύ αβέβαιο (MC Dropout) -> Human-in-the-Loop
        // -------------------------------------------------------------------
        Console.WriteLine("\n--- Scenario 9: Uncertain AI Prediction (MC Dropout Interval Too Wide -> Human-in-the-Loop) ---");
        var r9 = new SensorReading(TemperatureCelsius: 85.0, PressureBar: 7.0, "Routine check.", RadiationLeakDetected: false);
        try
        {
            arbiter.Execute(r9);
        }
        catch (UncertaintyEscalationException)
        {
            Console.WriteLine("[ESCALATED] The AI command was NOT applied; awaiting operator decision.");
        }

        // -------------------------------------------------------------------
        // Σενάριο 10: Το Vault ξαναφορτώνεται από το αρχείο με ίδιο ιστορικό
        // -------------------------------------------------------------------
        Console.WriteLine("\n--- Scenario 10: Persistence (The Vault Is Reloaded From Its File) ---");
        var reloaded = new StaticVault(repository: new JsonLinesIslandRepository(vaultPath));
        var liveSetpoint = vault.Resolve(SafetyLimits.PressureReliefSetpointBar, DateTime.UtcNow);
        var reloadedSetpoint = reloaded.Resolve(SafetyLimits.PressureReliefSetpointBar, DateTime.UtcNow);
        bool identical = reloaded.VersionCount == vault.VersionCount
            && vault.Queries.All(q => vault.History(q).Select(v => v.VectorId).SequenceEqual(reloaded.History(q).Select(v => v.VectorId)));
        Console.ForegroundColor = identical && reloadedSetpoint.VectorId == liveSetpoint.VectorId ? ConsoleColor.Cyan : ConsoleColor.Red;
        Console.WriteLine($"[PERSISTENCE] Reloaded {reloaded.VersionCount} versions of {reloaded.Queries.Count} facts from {vaultPath}");
        Console.WriteLine($"[PERSISTENCE] Same history as the live vault: {identical}; relief setpoint = {reloadedSetpoint.Value} bar ({reloadedSetpoint.Determinism!.Version})");
        Console.WriteLine($"[PERSISTENCE] Hash chain verified; head hash {new JsonLinesIslandRepository(vaultPath).HeadHash}");
        Console.ResetColor();

        // -------------------------------------------------------------------
        // Σενάριο 11: Ο χειριστής απαντά στην κλιμάκωση του Σεναρίου 9,
        // αλλά δεν μπορεί να παρακάμψει το SCRAM του Σεναρίου 4
        // -------------------------------------------------------------------
        Console.WriteLine("\n--- Scenario 11: Operator Override (Answers an Escalation, Never Overrides a Safety Island) ---");
        arbiter.Execute(r9, new OperatorOverride("Shift Operator A", 0.55, "Answering the uncertainty escalation after checking local gauges."));
        arbiter.Execute(r4, new OperatorOverride("Shift Operator A", 1.0, "Trying to relieve pressure manually."));

        Console.WriteLine($"\n[AUDIT TRAIL] Every control decision above was appended to {auditLog.Path}");
        Console.WriteLine($"[DOMAIN EVENTS] {recorder.Events.OfType<IslandAdded>().Count()} IslandAdded, " +
                          $"{recorder.Events.OfType<IslandTriggered>().Count()} IslandTriggered -> {eventLog.Path}");
        Console.WriteLine("=================================================================");
    }
}