"""Part IV — Trust infrastructure, and Part V — Our approach."""


def build(s, F):
    s.part("IV", "Trust infrastructure",
           "A decision nobody can reconstruct is a decision nobody can defend. Events, audit records, "
           "tamper-evident storage, a written constitution and a thin API.")

    # ---------------------------------------------------------------- 18
    s.chapter("18", "Domain events and the audit trail",
              "The Arbiter announces what happened; it does not know who is listening. (Book, §22.7, §23.4, §19.5.)")
    s.figure(F["events"], "Figure 18.1 — Publishers, the event bus and listeners. Adding a new consumer — a "
             "dashboard, an alarm, a compliance export — does not touch the Arbiter or the vault.", width=0.95)
    s.p(
        "The book's DDD chapters give auditing a precise place. The domain raises <b>events</b>; "
        "infrastructure <b>listens</b>. In its words, a subscriber can listen for one event type and build a "
        "complete, append-only audit trail of every deterministic answer, “without the Arbiter itself needing "
        "to know that auditing exists” (§22.7). The POC has three events:",
    )
    s.table([
        ["Event", "Published by", "When", "Carries"],
        ["IslandAdded", "Static Vault", "Every new version: value, pointer, derived island, automatic recomputation",
         "hash, question, the full vector, derivation rule and inputs"],
        ["IslandTriggered", "Arbiter", "A vault fact governed a decision (behind a triggered island, or a vault hit in Answer)",
         "hash, question, VectorId, version, time"],
        ["ControlDecisionMade", "Arbiter", "Exactly once per control cycle, on every path including escalations",
         "the complete audit record"],
    ], [0.2, 0.15, 0.35, 0.3])
    s.section("Anatomy of an audit record")
    s.code("""
{ "Timestamp": "2026-09-26T11:23:39Z",
  "Reading": { "TemperatureCelsius": 40, "PressureBar": 8.5,
               "OperatorNotes": "Critical failure. ...", "RadiationLeakDetected": true },
  "CausalityLockEngaged": false,
  "AiProposal": 0.649, "AiOrigin": "AI Stochastic Core (MC Dropout mean of 200)",
  "AiUncertainty95": 0.29,
  "TriggeredIslands": [
    { "Name": "High Pressure Emergency Loop", "Priority": "CriticalSafety", "EnforcedOutput": 1,
      "Sources": [ "high pressure relief setpoint in bar = 8 (v1, derived: minimum of 3 inputs, ...)",
                   "valve opening for high pressure relief = 1 (v1, approved by ...)" ] },
    { "Name": "Radiation Leak Containment Boundary", "Priority": "CriticalSafety", "EnforcedOutput": 0, ... } ],
  "Decision": "Scram", "FinalValveOpening": 0, "Origin": "FAIL-SAFE EMERGENCY SCRAM",
  "RequiresHumanReview": true,
  "Reason": "Conflicting demands at the same priority level (CriticalSafety): ...",
  "Operator": null }
""")
    s.p(
        "Read the record from top to bottom and you have the whole story: what the plant looked like, "
        "whether determinism was requested, what the AI wanted and how sure it was, which safety rules fired "
        "and on the basis of which fact versions and approvals, what was decided, and whether a human must "
        "look at it. This is §19.5's point made concrete: a complete audit trail is simply the discipline of "
        "logging the decisions the architecture is already making, alongside the version metadata of the "
        "facts it used.",
    )
    s.callout("key", "Log inputs, not just outputs", [
        "An audit trail that records only “valve set to 0%” cannot answer why. The expensive part — and the "
        "valuable part — is recording the inputs to the decision with their provenance: the AI's proposal "
        "even when it was overridden, and the exact versions of the facts that overrode it.",
    ])

    # ---------------------------------------------------------------- 19
    s.chapter("19", "Persistence and the hash chain",
              "A vault that forgets on restart is not a vault. A vault file anyone can edit silently is not "
              "evidence. (Book, §23.2, §19.5.)")
    s.p(
        "The Static Vault sits behind a narrow repository interface with two operations: <i>save one "
        "record</i> and <i>return every record in order</i>. That is the whole contract, and it matches the "
        "vault's own append-only discipline. The POC ships an in-memory implementation (tests) and a JSON "
        "Lines file (one JSON object per line, never rewritten).",
    )
    s.section("Save first, then apply")
    s.p(
        "Every new version is written to the repository <b>before</b> it takes effect in memory. If the "
        "write fails — a full disk, a permissions error — the in-memory vault is unchanged and the caller "
        "sees the error. The alternative order would allow a limit to be in force that exists nowhere on "
        "disk, which is precisely the kind of undocumented state the architecture exists to prevent.",
    )
    s.section("Replay on start-up")
    s.p(
        "When a vault is opened on an existing file it replays every record exactly: same VectorIds, same "
        "versions, validity intervals closed by their successors. Pointers and derived islands therefore "
        "resolve after a restart exactly as before, and automatic recomputation keeps working. Replay raises "
        "no events and recomputes nothing — the file already contains every version, including the automatic "
        "ones. Each record is checked against what has been replayed so far, and an inconsistent file is "
        "refused with the record number: missing approver, skipped version, dates out of order, a pointer or "
        "derivation input that was not saved earlier, or an unknown derivation rule.",
    )
    s.section("The hash chain")
    s.figure(F["hash_chain"], "Figure 19.1 — Each line stores the hash of the previous line. Changing any "
             "record changes its hash and breaks every link after it.")
    s.p(
        "Each line of <font face='Mono'>nexus1-vault.jsonl</font> has three fields: "
        "<font face='Mono'>PreviousHash</font>, <font face='Mono'>Hash</font> and "
        "<font face='Mono'>Record</font>, with <i>Hash = SHA-256(PreviousHash + Record)</i> and the first "
        "PreviousHash equal to 64 zeros. The record is embedded verbatim, so the bytes that were hashed are "
        "the bytes on disk. On load, the chain is verified line by line:",
    )
    s.table([
        ["Tampering", "Detected as"],
        ["A value edited in place (e.g. 8 → 9)", "line N: hash mismatch; this record was altered after it was saved"],
        ["A line removed from the middle", "line N: hash chain broken; a record before this line was removed, added or reordered"],
        ["Two lines swapped", "line 1: hash chain broken"],
        ["Malformed JSON", "line N: not a valid island record"],
    ], [0.38, 0.62])
    s.callout("warn", "What a chain cannot see", [
        "Lines cut off the <i>end</i> of the file leave a shorter but valid chain, and someone with write "
        "access can rewrite the whole file with a freshly computed chain. The POC exposes the last hash "
        "(<font face='Mono'>HeadHash</font>, printed by scenario 10) so it can be recorded somewhere else — "
        "a shift log, a separate system, a signed receipt — and compared on the next start. Real systems "
        "anchor the head hash externally or sign records with a key; the POC stops at making the need "
        "visible.",
    ])

    # ---------------------------------------------------------------- 20
    s.chapter("20", "The Neural Constitution",
              "Not new enforcement, but legibility: one list of everything the system promises, each item "
              "traceable to the code that enforces it and the test that proves it. (Book, §26.4.)")
    s.p(
        "Every island, every shield and every precedence rule is, on its own, a technical mechanism. The book "
        "proposes naming them together as a <b>Neural Constitution</b>: the complete, explicit set of rules "
        "the system must never violate, whatever its stochastic parts learn. The book's version has five "
        "rules, each with an “EnforcedBy” reference. The POC keeps those five, adds three of its own, and adds "
        "one thing the book's sketch does not have: a <b>VerifiedBy</b> list of test names for each rule.",
    )
    s.table([
        ["#", "Rule", "Enforced by", "Source"],
        ["1", "A resolved deterministic fact is never overridden by a generated one.", "Arbiter precedence; islands before AI; vault before candidates", "Book"],
        ["2", "No control action reaches an actuator outside its certified safe range.", "Shield (vault limit)", "Book"],
        ["3", "A Causality Lock, once engaged, never silently falls back to a stochastic answer.", "GuardedResolver, lock escalation", "Book"],
        ["4", "Every registered fact's provenance and approval are permanently recorded.", "Append-only versions; save-before-apply; hash chain", "Book"],
        ["5", "No fully autonomous action is taken without a human retaining override authority.", "Operator override; escalation; sign-off", "Book"],
        ["6", "An AI command is never applied when its uncertainty exceeds the certified bound.", "MC Dropout vs vault threshold", "POC"],
        ["7", "A routed or derived fact is never used once the fact it rests on has changed.", "Stale pointer / stale derived checks", "POC"],
        ["8", "Conflicting safety demands at the same priority end in a fail-safe SCRAM, never a guess.", "Priority rings, SCRAM", "POC"],
    ], [0.04, 0.46, 0.38, 0.12])
    s.section("A constitution that cannot drift")
    s.p(
        "A list of promises in a document goes out of date the day the code changes. The POC's constitution "
        "is code, and a test reads it: for every rule, every cited test must exist in the test assembly as a "
        "real test method. Rename or delete a test that a rule relies on and the build goes red. When we "
        "tried citing a non-existent test on purpose, the failure message named the rule and the missing "
        "test. The program prints the constitution at start-up, and the Web API serves it at "
        "<font face='Mono'>GET /constitution</font>.",
    )
    s.callout("ours", "From “EnforcedBy” to “VerifiedBy”", [
        "The book notes that the constitution itself should be governed like any other fact — reviewed on a "
        "cadence and changed only through an approved process (§26.5). Linking each rule to executable "
        "evidence is our contribution to that governance: a reviewer can go from a promise, to the mechanism, "
        "to a test that demonstrates it, to the green build that ran it.",
    ])

    # ---------------------------------------------------------------- 21
    s.chapter("21", "The Web API",
              "The thinnest layer of all: translate an HTTP request into a call on the application layer and "
              "translate the answer back. (Book, §23.3.3, §23.5.)")
    s.table([
        ["Endpoint", "What it does", "Notable responses"],
        ["POST /control", "One control cycle; optional operator override", "200 with applied=false on escalation; 400 for an invalid override"],
        ["POST /answer", "A factual query with the §12.4.2 precedence", "409 when determinism is required but impossible; isDeterministic=false on fall-through"],
        ["GET /vault/history?query=…", "Every version of one fact with validity, approver, pointer, derivation", "404 if the fact is unknown"],
        ["GET /vault/queries", "The facts currently registered", ""],
        ["GET /constitution", "The Neural Constitution with mechanisms and tests", ""],
    ], [0.25, 0.4, 0.35])
    s.code("""
POST /control
{ "temperatureCelsius": 95, "pressureBar": 4, "radiationLeakDetected": false }

200 OK
{ "applied": true, "valveOpening": 0.6, "decision": "IslandOverride",
  "origin": "Deterministic Island: Structural Thermal Protection",
  "requiresHumanReview": false, "aiProposal": 0.32, "aiUncertainty95": 0.30,
  "triggeredIslands": [ "Structural Thermal Protection" ] }
""")
    s.p(
        "Behind the endpoints sits one application-layer object that wires the event bus, reloads the "
        "hash-chained vault, commissions the default limits only if the file is empty, and processes requests "
        "one at a time — the domain model is not thread-safe, and the book's Honest Boundary in §22.8 names "
        "concurrency as one of the things a teaching model leaves out.",
    )
    s.callout("warn", "No endpoint writes a safety limit", [
        "It would take ten lines to add “POST /vault”. It is deliberately absent. The book lists "
        "authentication and authorisation on who may register an island among the prerequisites for any "
        "production use (§22.8), and an unauthenticated write endpoint for safety limits would undo every "
        "other guarantee in this guide. Limits change through code-reviewed commissioning, with named "
        "approvers, until proper identity and permissions exist.",
    ])

    # ================================================================== Part V
    s.part("V", "Our approach",
           "What we took from the book as written, what we deliberately changed, what we added — and why. "
           "This is the part to read if you need to judge the POC rather than learn from it.")

    s.chapter("22", "From book to code: a traceability map")
    s.table([
        ["Book", "Concept", "In the POC", "Status"],
        ["§12.3.1", "Static Vault, SHA-256 routing", "StaticVault, normalisation, point-in-time lookup", "As written"],
        ["§12.3.2", "Frozen Snapshot", "FrozenSnapshotConfig, per-call seeding, weights hash check", "As written"],
        ["§12.3.3", "Intra-Vector Routing, Arbiter, Causality Lock, HITL", "VectorResolver, GuardedResolver, CausalityLock", "As written + stale checks"],
        ["§12.4.2", "Arbiter.Answer precedence", "CompleteSystemArbiter.Answer", "As written"],
        ["§12.4.3", "Conflict resolution", "Precedence + priority rings + SCRAM + review flag", "Extended"],
        ["§12.4.4", "Append-only updates, human sign-off", "Versioning, ValidFrom/ValidTo, mandatory approver", "As written + retroactive check"],
        ["§13.6", "MC Dropout", "PredictWithUncertainty, 200 passes, 1.96·s", "Applied in the loop (ours)"],
        ["§16.9", "Thresholds as islands", "Uncertainty limit as a vault fact", "Applied"],
        ["§17.4", "Shield", "Shield: min(proposal, vault limit)", "As written"],
        ["§17.6", "Certified safety keeps final authority", "Islands/SCRAM beat operator override", "Interpreted"],
        ["§19.5", "Audit trail from Arbiter decisions", "AuditRecord per cycle, JSONL", "As written"],
        ["§22.7, §23.4", "Domain events, Observer", "IslandAdded, IslandTriggered, ControlDecisionMade", "As written + ControlDecisionMade"],
        ["§23.2", "Repositories", "IIslandRepository, JSON Lines, replay with checks", "As written + hash chain"],
        ["§23.5", "Minimal Web API", "DeterministicIsland.Api", "As written, read-only vault"],
        ["§24.2.2", "Floating-point order", "Single-threaded fixed-order loop", "As written"],
        ["§24.4.1, §24.5", "Derived islands", "RegisterDerived, DerivedIslandFactory, auto-recompute", "Extended"],
        ["§26.4", "Neural Constitution", "8 rules, VerifiedBy tests, checked by a test", "Extended"],
    ], [0.14, 0.27, 0.39, 0.2])

    s.chapter("23", "Design decisions and their rationale",
              "Every non-obvious choice, stated once, with the reason. If you disagree with one, this is the "
              "list to argue with.")
    decisions = [
        ("Safety islands are evaluated on every cycle.",
         "A safety limit that applies only when someone types the right word is not a safety limit (§17.6)."),
        ("“retrieve” no longer engages anything.",
         "The book engages the Causality Lock only on an explicit request for a deterministic island."),
        ("The AI's proposal is the MC Dropout mean, not a single pass.",
         "A single pass is one random draw; the mean is the model's best estimate and comes with a confidence measure."),
        ("Uncertainty above the vault limit escalates.",
         "A proposal the model itself disagrees about by more than ±20 points should not move a valve unattended."),
        ("The uncertainty threshold lives in the vault.",
         "It is an engineering decision with an owner and a history, not a hyper-parameter (§16.9)."),
        ("Pointers do not follow a superseded target.",
         "The approval was given against a specific value; a change requires a new approval (§12.4.4)."),
        ("Derived islands recompute automatically, but updates cannot be retroactive.",
         "Automatic where the book asks for it; refused where automation would create a stale period."),
        ("Priority rings and SCRAM on same-ring conflict.",
         "Two equally authoritative safety demands cannot be reconciled by software; fail safe and escalate."),
        ("Operator override beats the AI, never an island or SCRAM.",
         "Human authority over the model; certified safety functions keep physical authority (§17.6)."),
        ("The operator is not bound by the AI Shield.",
         "The Shield limits AI-proposed commands; a named, accountable person is a different authority."),
        ("Save before apply; replay without events.",
         "No limit may be in force that exists nowhere on disk; restarting must not rewrite history."),
        ("Hash-chain the vault file and expose the head hash.",
         "Make tampering evident, and make the remaining gap (truncation, full rewrite) explicit."),
        ("Escalation is a decision, not an error.",
         "It is recorded, counted and returned (HTTP 200) like any other outcome."),
        ("No write endpoint for limits in the API.",
         "Without authentication and authorisation, writing safety limits over HTTP would undo every guarantee."),
        ("The constitution cites tests and a test checks the citations.",
         "Promises that cannot drift silently away from the code."),
    ]
    s.table([["Decision", "Rationale"]] + [[f"<b>{d}</b>", r] for d, r in decisions], [0.42, 0.58])

    s.chapter("24", "Honest boundaries",
              "The book's standing rule: nothing in this book claims to be exact that is not. The same applies "
              "to this POC.")
    s.bullets([
        "<b>Islands guarantee reproducibility, not correctness.</b> A wrong value in the vault is reproduced "
        "faithfully forever — until a human corrects it (§12.5). Verification of the stored value is a human "
        "process, not a software property.",
        "<b>The network is hand-weighted and tiny.</b> Its uncertainty map illustrates the method; it says "
        "nothing about any real process.",
        "<b>MC Dropout measures self-disagreement.</b> A model can be confidently wrong, especially outside its "
        "training distribution.",
        "<b>200 passes are a sample.</b> The estimated half-width varies by about ±0.01–0.02 between cycles; "
        "readings near the ±0.20 limit may be approved on one cycle and escalated on the next.",
        "<b>The hash chain cannot detect truncation or a full rewrite</b> without an externally recorded head "
        "hash.",
        "<b>No authentication or authorisation.</b> Approver names are free text. Anyone who can run the code can "
        "“approve” a change.",
        "<b>No concurrency in the domain model.</b> The API serialises requests; a real service needs a proper "
        "concurrency design.",
        "<b>Island sprawl and review burden</b> grow with the number of facts and derivations (§24.6).",
        "<b>Not certified, not certifiable as is.</b> The disclaimer in the README applies without exception.",
    ])
