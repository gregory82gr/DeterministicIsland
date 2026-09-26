"""Part VI — Hands-on: scenarios, exercises, next steps; appendices."""


def build(s, F, run_output):
    s.part("VI", "Hands-on",
           "Run it, read what it says, then change it. The eleven scenarios, a graded set of exercises, and "
           "where to go after this guide.")

    # ---------------------------------------------------------------- 25
    s.chapter("25", "The eleven scenarios, explained",
              "Run the demo with “dotnet run” in the DeterministicIsland folder and read along. Each scenario "
              "isolates one mechanism.")
    s.code("""
git clone https://github.com/gregory82gr/DeterministicIsland
cd DeterministicIsland/DeterministicIsland
dotnet run          # .NET 8 SDK or later
""")
    scenarios = [
        ("1", "Normal operation", "60 °C, 5 bar, routine notes",
         "No island is triggered. The AI proposes the MC Dropout mean (about 35%) with a 95% band of about "
         "±17 points, inside the ±20 limit, and below the 75% Shield cap: AiApproved.",
         "Chapters 7, 8, 14"),
        ("2", "High temperature", "95 °C, 4 bar",
         "T ≥ 90 °C triggers Structural Thermal Protection; the valve goes to 60%. The output lists the two "
         "vault facts, with versions and approver, that the island used.", "Chapter 14"),
        ("3", "Priority resolution", "95 °C, 8.5 bar",
         "Both the Structural and the CriticalSafety pressure islands trigger. The higher ring wins; the valve "
         "opens to 100%. Note that the relief setpoint is shown as <i>derived: minimum of 3 inputs</i>.",
         "Chapters 13, 15"),
        ("4", "Deadlock → SCRAM", "40 °C, 8.5 bar, radiation leak",
         "Two CriticalSafety islands demand 100% and 0%. The Arbiter cannot reconcile them, commands a fail-safe "
         "SCRAM and flags the record for human review.", "Chapter 15"),
        ("5", "Causality Lock → Human-in-the-Loop", "60 °C, 5 bar, “…deterministic island.”",
         "Determinism is requested, no island applies and no Frozen Snapshot is configured. The system refuses "
         "to answer stochastically and escalates; the AI command is not applied.", "Chapter 16"),
        ("6", "Causality Lock → Frozen Snapshot", "same reading, frozen core (seed 42)",
         "The same request now has a deterministic mechanism: the frozen network. Two calls give 41.7% and "
         "41.7%, identical to the last bit.", "Chapter 9"),
        ("7", "Intra-Vector Routing", "query: backup line B setpoint",
         "The backup line's setpoint is registered as a pointer to the primary setpoint and resolves to 8 bar. "
         "Then an ordinary vector from a pretend RAG lookup, without a Determinism block, is rejected under the "
         "lock.", "Chapter 12"),
        ("8", "Derived island", "PT-2 recalibrated to 7.6 bar; reading 60 °C, 7.8 bar",
         "The relief setpoint is recomputed automatically from 8.0 to 7.6 bar (v2). A 7.8 bar reading, harmless "
         "before the change, now triggers the pressure-relief island.", "Chapter 13"),
        ("9", "Uncertain AI", "85 °C, 7 bar",
         "No island applies, but the network disagrees with itself by about ±29 points (95%). The command is "
         "not applied; the cycle escalates for uncertainty.", "Chapter 8"),
        ("10", "Persistence", "reopen the vault from its file",
         "A second vault is opened from nexus1-vault.jsonl. The hash chain verifies; it holds the same 13 "
         "versions of 11 facts with the same ids; the setpoint is still 7.6 bar (v2).", "Chapter 19"),
        ("11", "Operator override", "scenario 9's reading, then scenario 4's",
         "The operator answers the uncertainty escalation with 55%, which is applied. The same operator then "
         "tries 100% during the SCRAM; the command is recorded as <i>not applied</i>.", "Chapter 17"),
    ]
    s.table([["#", "Scenario", "Input", "What happens and why", "Read"]] +
            [[a, f"<b>{b}</b>", c, d, e] for a, b, c, d, e in scenarios],
            [0.05, 0.16, 0.19, 0.46, 0.14])
    s.section("What the console actually prints")
    s.p("An excerpt of one real run (paths shortened). Numbers from the stochastic core vary slightly from run "
        "to run; everything deterministic is identical every time.")
    lines = run_output.splitlines()
    keep = [l for l in lines if any(k in l for k in (
        "Scenario 1:", "Safe Zone", "Scenario 4:", "FATAL", "!! Island", "FAIL-SAFE] Arbiter", "Scenario 8:",
        "[DERIVED]", "Scenario 9:", "HUMAN-IN-THE-LOOP] AI", "Scenario 11:", "[OPERATOR"))]
    s.code("\n".join(keep[:24]), truncate=True)
    s.p(
        "After the run, three files sit next to the program: <font face='Mono'>nexus1-vault.jsonl</font> "
        "(recreated on every run, hash-chained), <font face='Mono'>nexus1-audit.jsonl</font> (one line per "
        "control decision) and <font face='Mono'>nexus1-events.jsonl</font> (every IslandAdded and "
        "IslandTriggered). Open them; they are the ground truth of what the system did.",
    )

    # ---------------------------------------------------------------- 26
    s.chapter("26", "Exercises: a graded path into the field",
              "Each exercise is small, has a clear success criterion, and teaches one idea you will meet "
              "again in real systems. Run “dotnet test” after each change.")
    levels = [
        ("Level 1 — Observe", [
            "Run the demo three times. Which numbers change and which never do? Explain each difference "
            "using Chapters 7–9.",
            "Open nexus1-audit.jsonl and, for scenario 4, reconstruct the decision using only the file.",
            "Compute by hand the network output for 30 °C, 5 bar with all neurons kept. Compare with Chapter 6's "
            "method and with the table in Chapter 8 (about 23%).",
            "Hash a question of your own with any SHA-256 tool after normalising it. Check that capitalisation "
            "and surrounding spaces do not change the key, but an inner double space does.",
        ]),
        ("Level 2 — Change a fact, not the code", [
            "Register a new version of the Shield limit (0.60) dated after 1 January. Which scenario's output "
            "changes? Is the old value still visible in the history?",
            "Raise the uncertainty limit to 0.30. Which scenario stops escalating? Why is this a decision for an "
            "engineer and not for the model?",
            "Recalibrate PT-3 to 7.0 bar. Predict the new relief setpoint before you run it, then check the "
            "IslandAdded events for the automatic recomputation.",
            "Try to register an update dated before the derived setpoint's current version. Read the error and "
            "explain what problem it prevents.",
        ]),
        ("Level 3 — Extend the architecture", [
            "Add an <i>Operational</i>-ring island, e.g. “close the valve to 20% below 2 bar”. Write a test where it "
            "competes with the Structural island and check the priority ring.",
            "Register a derived island using “maximum”. Where would a maximum, rather than a minimum, be the safe "
            "choice? (Hint: think about minimum cooling flow.)",
            "Add a fourth domain event, for example ShieldApplied, and a listener that counts how often the AI "
            "was capped. Do not change the Arbiter's decisions.",
            "Add a rule to the Neural Constitution with a test that verifies it. Then rename the test and watch "
            "the constitution test fail.",
        ]),
        ("Level 4 — Toward a real system", [
            "Replace the hand-set weights with weights trained on synthetic data (book, Chapter 7). What must be "
            "true of the new network before it may be used under a Frozen Snapshot?",
            "Record the vault's head hash in a separate file on every start and refuse to start if the file was "
            "truncated. What new failure mode have you created, and how would operators recover from it?",
            "Design (on paper) authentication for vault updates: who may approve which facts, and how would the "
            "approver's identity be bound to the record instead of free text?",
            "Sketch how the query path (Answer) would sit in front of a real language model, following the "
            "book's §12.5: the model's open-ended text stays stochastic, every numeric limit it mentions is "
            "resolved through the vault.",
        ]),
    ]
    for title, items in levels:
        s.section(title)
        s.bullets(items, numbered=True)
    s.callout("try", "A good habit", [
        "Before every change, write down what you expect the audit trail to show. After the change, compare. "
        "The gap between expectation and record is where understanding happens — and in a real plant, where "
        "incidents are prevented.",
    ])

    # ---------------------------------------------------------------- 27
    s.chapter("27", "Where to go next")
    s.p(
        "This guide covers the parts of the book that the POC implements. The book itself goes much further: "
        "information theory and the Shannon limit as the forerunner of learning (Part I), the full path from "
        "perceptron to Transformer (Part II), the Industry Language Model and its evaluation, ethics and "
        "governance (Part III), applications in predictive maintenance, anomaly detection, control and "
        "decision support (Part IV), and the formal treatment of Deterministic Islands (Part VI).",
    )
    s.table([
        ["If you want to…", "Read in the book"],
        ["understand why neural networks are stochastic in the first place", "Chapters 4–8"],
        ["see the three deterministic mechanisms in full", "Chapter 12"],
        ["measure uncertainty and evaluate models honestly", "Chapter 13"],
        ["apply islands to anomaly thresholds and control loops", "Chapters 16–17"],
        ["build a safety case and an audit trail regulators accept", "Chapter 19"],
        ["structure the software with DDD", "Chapters 20–23"],
        ["see the mathematics behind the guarantees", "Chapter 24"],
        ["think about the future: Neural Constitution, quantum networks", "Chapter 26"],
    ], [0.6, 0.4])
    s.section("The NEXUS-1 series")
    s.p(
        "<i>From Stochastic Chaos to Deterministic Certainty</i> is one of <b>22 books</b> in the NEXUS-1 "
        "series by Grigorios Agathangelidis. The series began with a single experiment, an interactive console "
        "for an educational digital twin of a nuclear plant, and grew one open question at a time into "
        "physics, AI, data, architecture, formal methods, systems and frontend. It keeps one standing rule: "
        "nothing is stated as certain that has not been shown to hold in practice. The books are available at "
        "<font color='#1d4e89'>leanpub.com/u/grigorios-kyriakos-agathangelidis</font>.",
    )
    s.table([['Theme', 'Book', 'In one line'], ['<b>Physics and engineering of the plant</b>', '<i>From Grid to Core</i>', 'The foundation of the series: from the 400 kV substation to the reactor core, with neutron kinetics and SCRAM.'], ['', '<i>From Queue to Core</i>', 'Stochastic queueing theory as a rigorous mathematical foundation for reactor kinetics, with C# implementations.'], ['', '<i>From Core to Quantum</i>', 'From the quantum crisis to the structure of the nucleus, and a complete quantum-circuit simulator in modelled C#.'], ['<b>Interpretable and controlled AI</b>', '<i>From Flood to Cause</i>', 'When 200 alarms fire at once: a deterministic causal graph finds the root cause; the LLM only explains it.'], ['', '<i>From Trial to Policy</i>', 'Reinforcement learning from scratch with a fully interpretable Q-learning agent: 175 numbers in an auditable table.'], ['', '<i>From Stochastic Chaos to Deterministic Certainty</i>', "An industrial language model wrapped in proven deterministic boundaries, mapped to the EU AI Act. <b>This POC's book.</b>"], ['<b>Data architecture</b>', '<i>From Schema to System</i>', 'The complete schema atlas: 17 domains, 654 tables, with ER diagrams and verification queries behind the twin.'], ['', '<i>From Table to Twin</i>', 'The same schema built twice (Database First and Code First with EF Core) and a chapter reconciling them.'], ['', '<i>From Entity to Context</i>', 'Clean EF Core mapping of the schema: one configuration per entity instead of a giant OnModelCreating.'], ['<b>Domain-Driven Design</b>', '<i>From Domain to Twin</i>', 'DDD from scratch in plain language, applied to NEXUS-1: from entities and aggregates to the SQL schema.'], ['', '<i>From Context to Flow</i>', 'Advanced DDD patterns (context maps, sagas, outbox) through one flow that crosses nine bounded contexts.'], ['<b>Backend trilogy (.NET)</b>', '<i>From Blueprint to Core</i>', 'Domain and application layers without a database or web server: 17 contexts, 50 green tests in about 600 ms.'], ['', '<i>From Core to Contract</i>', 'The core gets a database (EF Core, 654 tables) and a public API, with infrastructure strictly below the seam.'], ['', '<i>From Contract to Container</i>', 'Integration tests on a real SQL Server via Testcontainers, an outbox that survives a killed process, a CI gate.'], ['<b>Microservices</b>', '<i>From Flow to Services</i>', 'More than 70 chapters: microservices as a consequence of mature boundaries, not a starting point, and when distribution is not worth it.'], ['', '<i>From Services to Runtime</i>', 'Three owning services on a real .NET runtime blueprint: inbox/outbox, JWT, OpenTelemetry, Kubernetes.'], ['<b>Formal methods</b>', '<i>From Flow to Proof</i>', 'Architectural promises become proofs: state machines, TLA+, Petri nets, category theory; model versus implementation.'], ['<b>Systems trilogy (below .NET)</b>', '<i>From Runtime to Distribution — Volume I</i>', 'From C# to the hardware: CPU, kernel mode, threads, inside the CLR (JIT, Native AOT), memory and GC.'], ['', '<i>From Runtime to Distribution — Volume II</i>', 'The process boundary: concurrency, async/await as a state machine, IPC, TCP/TLS/gRPC and the “Boundary Ledger”.'], ['', '<i>From Runtime to Distribution — Volume III</i>', 'Production: containers, Kubernetes, SLIs/SLOs, canary deployments and practical observability.'], ['<b>Frontend (Angular)</b>', '<i>From File to Framework</i>', 'A 5,900-line single-file console becomes a real Angular application, and every screen is checked for “honesty”.'], ['<b>Retrospective</b>', '<i>From Certainty to Calibration</i>', 'The author revisits six of his own decisions: then → mechanism → lesson → now → what the correction risks.']], [0.2, 0.3, 0.5])
    s.p(
        "Natural next reads after this guide: <i>From Flood to Cause</i> and <i>From Trial to Policy</i> apply "
        "the same principle as this POC (a deterministic core, with the learning or explaining component kept "
        "at its edge). <i>From Domain to Twin</i> and <i>From Blueprint to Core</i> go deeper into the DDD and "
        "layering used here. <i>From Flow to Proof</i> shows how promises like the Neural Constitution's become "
        "proofs. <i>From Certainty to Calibration</i> does for the author's own decisions what Part V of this "
        "guide does for the POC's.",
    )

    # ---------------------------------------------------------------- Appendices
    s.part("", "Appendices", "A map of the code, a map of the tests, and how to rebuild this guide.")
    s.chapter("A", "Code map",
              "Where each concept of this guide lives. Paths are relative to the repository root.")
    s.table([
        ["Concept", "Where"],
        ["Sensor reading, command, islands, override", "DeterministicIsland/domain/SensorReading.cs"],
        ["ILMVector, Determinism block, VaultFact", "DeterministicIsland/domain/ILMVector.cs"],
        ["Neural network, MC Dropout, Frozen Snapshot check", "DeterministicIsland/ProbabilisticCore/MiniNeuralNetwork.cs"],
        ["Frozen Snapshot configuration", "DeterministicIsland/Islands/FrozenSnapshotConfig.cs"],
        ["Static Vault, versions, derived islands, replay", "DeterministicIsland/Islands/StaticVault.cs"],
        ["Repository, JSON Lines, hash chain", "DeterministicIsland/Islands/IslandRepository.cs"],
        ["Derivation rules, DerivedIslandFactory", "DeterministicIsland/Islands/DerivationRules.cs, DerivedIslandFactory.cs"],
        ["Pointer resolution, stale checks", "DeterministicIsland/Islands/VectorResolver.cs"],
        ["Causality Lock, escalation exceptions", "DeterministicIsland/Islands/CausalityLock.cs"],
        ["Safety facts and defaults", "DeterministicIsland/Islands/SafetyLimits.cs"],
        ["Protection islands", "DeterministicIsland/Islands/IslandCatalog.cs"],
        ["Shield", "DeterministicIsland/Islands/Shield.cs"],
        ["System Arbiter (control and query paths)", "DeterministicIsland/CompleteSystemArbiter.cs"],
        ["Audit record, audit logs", "DeterministicIsland/Audit/AuditLog.cs"],
        ["Domain events, bus, listeners", "DeterministicIsland/Events/"],
        ["Neural Constitution", "DeterministicIsland/Governance/NeuralConstitution.cs"],
        ["Demo scenarios", "DeterministicIsland/Program.cs"],
        ["Web API", "DeterministicIsland.Api/"],
        ["Tests", "DeterministicIsland.Tests/"],
        ["This guide's source", "docs/source/"],
    ], [0.42, 0.58])

    s.chapter("B", "Test map",
              "100 automated tests (89 test methods; theories run once per case). Each file tests one concept.")
    s.table([
        ["Test class", "Tests", "What it demonstrates"],
        ["StaticVaultTests", "6", "The book's SHA-256 example; normalisation; append-only versions; approver required"],
        ["MiniNeuralNetworkTests", "7", "Stochastic vs frozen; bitwise reproducibility; MC Dropout interval"],
        ["CompleteSystemArbiterTests", "12", "Shield, priority rings, SCRAM, Causality Lock, uncertainty escalation"],
        ["IntraVectorRoutingTests", "12", "Pointer chains, cycles, dangling and stale pointers, Answer precedence"],
        ["DerivedIslandTests", "8", "The book's min example; automatic and cascading recomputation; stale detection"],
        ["DomainEventTests", "8", "Which events are raised, when, and to whom"],
        ["IslandRepositoryTests", "15", "Replay, save-before-apply, consistency checks, hash chain tampering"],
        ["OperatorOverrideTests", "7 (11 cases)", "Override applies, answers escalations, never beats an island or SCRAM"],
        ["NeuralConstitutionTests", "2 (9 cases)", "Five book rules; every cited test exists"],
        ["ApiTests", "12", "Endpoints, status codes, restart without re-commissioning, audit per cycle"],
    ], [0.34, 0.12, 0.54])

    s.chapter("C", "Rebuilding this guide")
    s.p(
        "The guide is generated, not typed into a word processor, so that its numbers can never disagree with "
        "the code. The network in <font face='Mono'>docs/source/figures.py</font> mirrors "
        "<font face='Mono'>MiniNeuralNetwork.cs</font> exactly; the chapters are Python modules under "
        "<font face='Mono'>docs/source/</font>.",
    )
    s.code("""
pip install reportlab matplotlib

# writes docs/NEXUS-1_Deterministic_Islands_Engineer_Guide.pdf
python docs/source/build_guide.py

# runs the demo first and embeds its fresh console output
python docs/source/build_guide.py --run
""")
    s.p(
        "If you change the network's weights or the vault defaults, update the constants in "
        "<font face='Mono'>figures.py</font> and the fact table in Chapter 10, rebuild, and read the diff "
        "of the numbers. Treat the guide like the constitution: it is part of the evidence, and it should "
        "change in the same commit as the code it describes.",
    )
