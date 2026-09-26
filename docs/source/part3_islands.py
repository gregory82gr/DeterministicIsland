"""Part III — Deterministic Islands: vault, time, routing, derived islands, protection, Arbiter,
Causality Lock, operator override."""

import hashlib


def sha(q):
    return hashlib.sha256(q.encode()).hexdigest()


def build(s, F):
    s.part("III", "Deterministic Islands",
           "The facts the AI is never asked for, how they are stored and versioned, how they are combined, "
           "and how one component — the Arbiter — decides who is in charge on every cycle.")

    # ---------------------------------------------------------------- 10
    s.chapter("10", "The Static Vault",
              "The strongest guarantee is the simplest mechanism: answer = Vault[SHA-256(normalised query)]. "
              "(Book, §12.3.1.)")
    s.p(
        "For a known, finite set of safety-critical facts the book's advice is to bypass the neural network "
        "entirely. The <b>Static Vault</b> maps a question directly to a stored, pre-verified answer, exactly "
        "the way a hash table maps a key to a value. In the POC every limit the shell needs — setpoints, "
        "valve positions, the Shield's cap, the uncertainty threshold — lives here. The AI never produces any "
        "of them.",
    )
    s.figure(F["vault_pipeline"], "Figure 10.1 — From a question to a fact: normalise, hash, find the "
             "version valid at time t, return the vector.")
    s.section("Step 1 — normalise")
    s.p(
        "Two questions that mean the same thing must produce the same key. The book is precise about this: "
        "normalisation must be fixed and documented, because a single differing character produces a "
        "completely unrelated hash. The POC's rule is deliberately minimal: <b>trim surrounding whitespace "
        "and convert to lower case</b> (invariant culture). <i>“  High Pressure Relief Setpoint in bar ”</i> "
        "and <i>“high pressure relief setpoint in bar”</i> are therefore the same question. Anything richer "
        "(collapsing inner spaces, synonyms, units) would be a design decision to document and version like "
        "any other fact.",
    )
    s.section("Step 2 — hash")
    s.p(
        "The normalised text is encoded as UTF-8 and hashed with <b>SHA-256</b>, giving a 64-character "
        "hexadecimal key. SHA-256 is deterministic: the same bytes always give the same 256 bits, on any "
        "machine, in any year. The test suite checks the book's own worked example:",
    )
    s.code(f"""
"what is the maximum allowed discharge pressure for pump p-101?"
   -> {sha('what is the maximum allowed discharge pressure for pump p-101?')}

"high pressure relief setpoint in bar"
   -> {sha('high pressure relief setpoint in bar')}
""")
    s.p(
        "How safe is it to treat different hashes as different questions? The book's §24.2.1 answers with "
        "the birthday bound: for a 256-bit hash about 4 × 10<super>38</super> distinct inputs are needed "
        "before a collision becomes as likely as not. At a physically absurd 10<super>18</super> hashes per "
        "second that is roughly 10<super>13</super> years — some 920 times the age of the universe. The "
        "vault's correctness rests on that number, not on a hope that “hashes are usually fine”.",
    )
    s.section("Step 3 — look up the version valid now")
    s.p(
        "A key does not point to one value but to a <b>history</b>: v1, v2, … Each version carries a "
        "Determinism block with <font face='Mono'>ValidFrom</font> (inclusive) and "
        "<font face='Mono'>ValidTo</font> (exclusive, empty while current). The lookup takes a moment in "
        "time and returns the version valid at that moment. The next chapter is about why this matters.",
    )
    s.section("What a fact looks like")
    s.code("""
ILMVector
  VectorId      9f72e7a1-…            unique, never reused
  Embedding     [ 8.0 ]               the value (one number in this POC)
  Determinism
    IsDeterministic  true
    Version          "v1"
    ValidFrom        2026-01-01T00:00:00Z
    ValidTo          null               (open: this is the current truth)
    ApprovedBy       "Shift Safety Engineer"
    PointerTo        null               (Chapter 12)
    DerivedFrom      [ … ]              (Chapter 13)
    DerivedBy        "minimum"
""")
    s.p(
        "The shape is the book's <font face='Mono'>ILMVector</font> from Chapter 10, kept on purpose. In an "
        "Industry Language Model the embedding is a long vector; here it is one number. Everything else — "
        "identity, determinism, validity, approval — is identical, which is why the mechanisms in this POC "
        "transfer directly to the language-model setting the book describes.",
    )
    s.section("The eleven facts of the demo")
    s.table([
        ["Question (normalised)", "Value", "Used by"],
        ["maximum safe valve opening for ai-proposed commands", "0.75", "Shield"],
        ["maximum ai prediction uncertainty as 95% half-width", "0.20", "Uncertainty escalation"],
        ["maximum safe reading of pressure transmitter pt-1 in bar", "8.4", "Derived setpoint"],
        ["maximum safe reading of pressure transmitter pt-2 in bar", "8.0 (7.6 from v2)", "Derived setpoint"],
        ["maximum safe reading of pressure transmitter pt-3 in bar", "8.2", "Derived setpoint"],
        ["high pressure relief setpoint in bar", "min of the three", "Pressure island"],
        ["valve opening for high pressure relief", "1.0", "Pressure island"],
        ["maximum coolant temperature in celsius", "90", "Structural island"],
        ["valve opening for structural cooling", "0.6", "Structural island"],
        ["valve opening for radiation containment", "0.0", "Radiation island"],
        ["high pressure relief setpoint for backup line b in bar", "→ pointer", "Scenario 7"],
    ], [0.6, 0.18, 0.22])
    s.callout("key", "Why a hash and not just the text?", [
        "A hash gives a fixed-size, collision-resistant identifier that is identical everywhere. It can be "
        "logged, compared and signed without carrying the text, and it makes the formal guarantee of "
        "§24.2.3 trivial: the vault is the composition A∘H of a hash function H and a finite lookup table A, "
        "and equal hashes give equal answers because A is a function. The engineering effort goes into "
        "making the hypotheses true — H pure, A unambiguous at query time — not into the proof.",
    ])

    # ---------------------------------------------------------------- 11
    s.chapter("11", "Time: versions, validity and point-in-time answers",
              "A safety limit is never edited. It is superseded. (Book, §12.4.4 and §24.4.1.)")
    s.p(
        "What happens when an engineer changes a setpoint? The tempting answer — overwrite the value — "
        "destroys the one thing an incident investigation needs: what the system believed <i>at the time</i>. "
        "The book therefore mandates an <b>append-only</b> discipline. To update a fact:",
    )
    s.bullets([
        "set the current version's <font face='Mono'>ValidTo</font> to the effective date of the change;",
        "register a new vector with a new <font face='Mono'>VectorId</font>, an incremented "
        "<font face='Mono'>Version</font> and <font face='Mono'>ValidFrom</font> equal to that same date;",
        "record who approved it. In the POC an update without an approver is rejected outright.",
    ], numbered=True)
    s.figure(F["version_timeline"], "Figure 11.1 — Validity intervals in the demo. Recalibrating PT-2 closes "
             "its v1 and opens v2; the derived relief setpoint follows automatically (Chapter 13); the backup-line "
             "pointer, which pointed at the old setpoint version, becomes stale (Chapter 12).")
    s.p(
        "Formally (§24.4.1) the validity intervals of all versions of one question must <b>partition time</b> "
        "without gaps or overlaps, so that “what was the answer on this date?” always has exactly one answer. "
        "The POC enforces the necessary ordering: a new version must start strictly after the current one. "
        "Lookups take an explicit instant, so the Arbiter asks for the facts valid at the moment of the "
        "control cycle, and a test can ask what the answer was last February.",
    )
    s.callout("ours", "Rejecting retroactive updates", [
        "We added one rule the book implies but does not spell out: an update is rejected if it would "
        "start before a derived island that depends on it (Chapter 13). Such an update would make that island "
        "stale for its whole validity period. It is cheaper to refuse the update and ask for a correct "
        "effective date than to discover the inconsistency during an incident.",
    ])
    s.callout("try", "Ask the past", [
        "In the tests, <font face='Mono'>Register_UpdateIsAppendOnlyAndKeepsHistoryQueryable</font> registers "
        "a SCRAM setpoint of 320 on 1 January and 315 on 1 June, then asks for the value one day before "
        "1 June (320) and on 1 June (315). Change the dates and watch which assertions fail.",
    ])

    # ---------------------------------------------------------------- 12
    s.chapter("12", "Intra-Vector Routing: facts that point to other facts",
              "The third deterministic mechanism of §12.3.3: a vector may defer to another vector instead of "
              "holding a value.")
    s.p(
        "Real plants are full of facts that are, by specification, the same as some other fact: a backup "
        "line designed to the primary line's rating, a spare pump with the duty pump's limit. Copying the "
        "value is dangerous — the copies drift apart. The book's answer is a <b>pointer</b>: a vector whose "
        "Determinism block says <font face='Mono'>IsDeterministic = false</font> and "
        "<font face='Mono'>PointerTo = &lt;another VectorId&gt;</font>. Resolution follows the pointer and "
        "defers to the target's determinism.",
    )
    s.figure(F["pointer_chain"], "Figure 12.1 — Scenario 7's pointer and the four ways a chain can fail. Every "
             "failure is loud; none falls back to a stochastic answer.")
    s.section("The resolver")
    s.code("""
resolve(start):
    current = start; visited = { start.id }
    while current is not deterministic and current points to next:
        if next already visited      -> fail "cycle"
        if visited count > 8         -> fail "too many hops"
        current = store.get(next)    or fail "dangling pointer"
    return current                    # deterministic, or an ordinary vector

resolveUnderLock(start, lock, t):
    r = resolve(start)
    if lock engaged and r is not deterministic   -> DeterminismViolation (Human-in-the-Loop)
    if r is deterministic and not valid at t     -> fail "stale pointer"
    if r is derived and any input not valid at t -> fail "stale derived island"
    return r
""")
    s.p(
        "The first function is the book's <font face='Mono'>VectorResolver</font>; the second its "
        "<font face='Mono'>GuardedResolver</font>, extended by us with the two time checks. The book is "
        "explicit that the cycle and dangling-pointer guards are “not optional hardening”: a cycle would "
        "loop forever, and a dangling pointer must fail loudly rather than silently fall back to an "
        "ungrounded answer. Every safety limit the POC reads goes through <font face='Mono'>resolveUnderLock</font> "
        "with the lock engaged, so a pointer chain that does not end in a valid deterministic fact can "
        "never reach an island.",
    )
    s.callout("ours", "Stale pointers", [
        "A pointer names one specific <i>version</i> of its target, by VectorId. When the target gets a new "
        "version, should the pointer silently follow? We decided no. The engineer who approved “backup line "
        "B uses the primary setpoint” approved it against a particular value; if that value changes, the "
        "pointer is marked stale and resolution fails until a human re-registers it. This is conservative, "
        "and it is exactly the §12.4.4 principle — no safety fact changes without human sign-off — applied "
        "to routing. Scenario 7 followed by scenario 8 shows it happening.",
    ])

    # ---------------------------------------------------------------- 13
    s.chapter("13", "Derived islands: facts computed from facts",
              "Three redundant transmitters, three certified limits, one safe operating limit: the minimum. "
              "(Book, §24.4.1 and §24.5.)")
    s.p(
        "The book's deep-dive chapter introduces a new kind of island: one whose answer is a <b>pure "
        "function</b> of other islands' answers. Its example is three redundant pressure sensors with "
        "certified maxima of 45.0, 42.5 and 44.0 bar; the system's safe limit is their minimum, 42.5 bar. "
        "Because min is an ordinary mathematical function, a function of deterministic inputs is itself "
        "deterministic — the one-line argument of §24.2.3 again.",
    )
    s.figure(F["derived_dag"], "Figure 13.1 — The POC's relief setpoint is a derived island over three "
             "transmitter limits, and the pressure-relief island reads it like any other fact.")
    s.p(
        "In the POC the relief setpoint is exactly such an island: min(8.4, 8.0, 8.2) = 8.0 bar. The derived "
        "vector records <font face='Mono'>DerivedBy = \"minimum\"</font> and "
        "<font face='Mono'>DerivedFrom</font> = the VectorIds of the three inputs it was computed from. That "
        "second field is what makes the value auditable: you can always tell which input versions produced it.",
    )
    s.section("Staying current: automatic recomputation")
    s.p(
        "The book names the failure mode plainly in its Honest Boundary: a derived island is only as current "
        "as the last time it was recomputed, and nothing in its sample code detects that an input has changed "
        "(§24.6). It also says what should happen: the §12.4.4 update discipline, <i>applied automatically "
        "rather than manually</i>. The POC implements both halves:",
    )
    s.bullets([
        "<b>Automatic recomputation.</b> When any input question receives a new version, every derived island "
        "that lists it is re-registered from the same effective date, and so on down the chain (a derived "
        "island of a derived island is recomputed in cascade). The new version's approver reads, for example, "
        "<i>“Instrumentation Engineer (auto-recomputed after ‘…pt-2…’ changed)”</i>, so the audit trail shows "
        "both who changed the input and that the derivation followed automatically.",
        "<b>Stale detection.</b> If an input changes by a route that does not trigger recomputation — "
        "typically because the derived island depends on a <i>pointer</i> whose target changed — the resolver "
        "checks every <font face='Mono'>DerivedFrom</font> id against the query time and fails loudly with "
        "“stale derived island”.",
    ])
    s.p(
        "Scenario 8 shows the happy path: PT-2 is recalibrated from 8.0 to 7.6 bar, the setpoint becomes "
        "min(8.4, 7.6, 8.2) = 7.6 bar as version v2, and a reading of 7.8 bar — harmless a moment earlier — "
        "now triggers the pressure-relief island.",
    )
    s.callout("ours", "Rules by name, not by code", [
        "A derived island stores the <i>name</i> of its rule (“minimum”, “maximum”, or one registered by the "
        "application), not a piece of code. That keeps the rule persistable and reviewable: the vault file "
        "(Chapter 19) says <i>what</i> function produced a value, and reloading a vault with an unknown rule "
        "fails rather than guessing.",
    ])
    s.callout("warn", "Island sprawl", [
        "The book's second warning in §24.6 applies fully: as the number of facts and derivations grows, the "
        "human review burden grows with it, and a registry too large for any one reviewer to understand is a "
        "governance failure however sound each entry is. Automatic recomputation reduces clerical work; it "
        "does not reduce the need for someone to understand the graph.",
    ])

    # ---------------------------------------------------------------- 14
    s.chapter("14", "Protection islands and the Shield",
              "Facts become behaviour: interlocks that take control of the valve, and a cap on everything the "
              "AI proposes.")
    s.p(
        "So far the vault holds numbers. A <b>protection island</b> turns numbers into a rule: a condition on "
        "the sensor reading, an enforced valve position, a priority ring, and the list of vault facts it was "
        "built from. The island catalogue is rebuilt on every cycle from the facts valid at that instant, so "
        "an approved change to a setpoint changes the island without touching any code.",
    )
    s.table([
        ["Island", "Ring", "Condition", "Enforced opening", "Facts it reads"],
        ["High Pressure Emergency Loop", "CriticalSafety", "P ≥ relief setpoint", "relief opening (100%)", "relief setpoint (derived), relief opening"],
        ["Radiation Leak Containment Boundary", "CriticalSafety", "leak detected", "containment (0%)", "containment opening"],
        ["Structural Thermal Protection", "Structural", "T ≥ coolant limit", "cooling (60%)", "coolant limit, cooling opening"],
    ], [0.25, 0.14, 0.18, 0.18, 0.25])
    s.callout("ours", "Islands are always on", [
        "In the POC's first version, islands were created only when the operator notes contained certain "
        "keywords; at 8.5 bar without a keyword the AI ran unchecked. The book is clear that hard temperature "
        "and pressure limits are non-negotiable and enforced regardless of what any model recommends (§17.6), "
        "so the islands are now evaluated on every single cycle. Keywords now only control the Causality Lock "
        "(Chapter 16), which is a request for a <i>stronger</i> guarantee, never a precondition for safety.",
    ])
    s.section("The Shield")
    s.p(
        "When no island is triggered the AI's proposal may be used, but never as is. The <b>Shield</b> of "
        "§17.4 wraps it in a non-learned layer: <i>command = min(proposal, maxSafe)</i>, where maxSafe is the "
        "vault fact “maximum safe valve opening for ai-proposed commands” (0.75). The book describes the "
        "effect precisely: the model is free to propose 85% again on the next step, and even to learn over "
        "time that such proposals are never applied, but the plant is never exposed to 85% while it learns. "
        "The audit record distinguishes <font face='Mono'>AiApproved</font> (the proposal was within bounds) "
        "from <font face='Mono'>AiShielded</font> (it was capped), and keeps the original proposal.",
    )
    s.callout("key", "Shielding is Deterministic Islands applied to actions", [
        "The book's own observation (§17.4): the shield's safe range is exactly the kind of static, "
        "auditable, non-stochastic constraint the Arbiter enforces for facts. The same vault, the same "
        "versions, the same approvals — applied to a command instead of an answer.",
    ])

    # ---------------------------------------------------------------- 15
    s.chapter("15", "The Arbiter: one control cycle, step by step",
              "Every guarantee in this guide meets in one place. This chapter walks through it in the order "
              "the code executes it.")
    s.figure(F["control_cycle"], "Figure 15.1 — One call to Execute(reading, override). Exactly one "
             "ControlDecisionMade event is published on every path.", width=0.92)
    s.section("Before the branch")
    s.bullets([
        "<b>Validate the override</b>, if any: a named operator, a reason, an opening between 0 and 1. An "
        "invalid override is rejected before anything else happens, and no decision is recorded.",
        "<b>Engage the Causality Lock</b> if the operator notes contain “deterministic island”.",
        "<b>Compute the AI proposal.</b> Normally the MC Dropout mean of 200 passes with its 95% band; under "
        "the lock, if a Frozen Snapshot is configured, one reproducible pass instead.",
        "<b>Build and evaluate the islands</b> from the vault at the cycle's timestamp, and publish an "
        "IslandTriggered event for every fact behind every triggered island.",
    ], numbered=True)
    s.section("Branch A — no island triggered")
    s.p(
        "The AI is a candidate, and four questions are asked in order. Is there an operator override? Then "
        "it is applied (Chapter 17). Is the lock engaged without a Frozen Snapshot? Then the system refuses "
        "to answer stochastically and escalates. Is the 95% band wider than the vault's limit? Then it "
        "escalates for uncertainty. Otherwise the Shield caps the proposal and it is applied.",
    )
    s.section("Branch B — at least one island triggered")
    s.p(
        "The AI's proposal is recorded for the audit trail but plays no further role. Triggered islands are "
        "sorted by priority ring. If the top ring contains islands with <i>different</i> demands — scenario 4, "
        "where high pressure wants the valve fully open and a radiation leak wants it fully closed — the "
        "Arbiter does not pick one. It commands a <b>fail-safe SCRAM</b> (valve to 0%, marked as scrammed) "
        "and flags the record for human review. Otherwise the top island wins.",
    )
    s.table([
        ["Decision", "When", "Valve", "Human review flag"],
        ["AiApproved", "No island; AI confident and within the Shield", "AI mean", "no"],
        ["AiShielded", "No island; AI confident but above 75%", "75%", "no"],
        ["IslandOverride", "An island (or several agreeing ones) triggered", "island's value", "no"],
        ["Scram", "Top-ring islands disagree", "0% (scrammed)", "<b>yes</b>"],
        ["HumanEscalation", "Lock without deterministic answer, or AI too uncertain", "not changed", "<b>yes</b>"],
        ["OperatorOverride", "Named operator command and no island triggered", "operator's value", "no"],
    ], [0.2, 0.44, 0.17, 0.19])
    s.callout("ours", "Priority rings and SCRAM are an extension", [
        "The book's precedence rule (§12.4.3) is between <i>mechanisms</i> — Static Vault, then routed vector, "
        "then Frozen Snapshot — and it treats any disagreement between two deterministic sources as a data "
        "maintenance error for human review, never something the Arbiter quietly adjudicates. The POC keeps "
        "that spirit and adds a control-specific layer: when two safety interlocks of equal rank demand "
        "opposite actions, the only defensible automatic action is the fail-safe one, and the record is still "
        "flagged for a human. Both the README and the code comments say that this layer is ours.",
    ])

    # ---------------------------------------------------------------- 16
    s.chapter("16", "The Causality Lock and Human-in-the-Loop",
              "When someone asks for certainty, the system either provides it or says it cannot. It never "
              "pretends. (Book, §12.3.3.)")
    s.p(
        "The <b>Causality Lock</b> is the book's name for an explicit request for determinism. Once engaged, "
        "every part of the answer must resolve to a deterministic source; there is no silent fallback to the "
        "stochastic generator anywhere in the path. When that is impossible, the system escalates to "
        "<b>Human-in-the-Loop</b>: it reports that it cannot answer deterministically, rather than quietly "
        "relaxing the guarantee it was asked for.",
    )
    s.table([
        ["Situation (lock engaged)", "Result"],
        ["An island decides the cycle", "The island's command — islands are deterministic."],
        ["No island, Frozen Snapshot configured", "One reproducible pass of the frozen core, then the Shield (scenario 6)."],
        ["No island, no Frozen Snapshot", "DeterminismViolationException; decision HumanEscalation; AI not applied (scenario 5)."],
        ["Query path, vault hit", "The resolved fact (following pointers)."],
        ["Query path, only a non-deterministic candidate", "DeterminismViolationException (scenario 7, second half)."],
    ], [0.42, 0.58])
    s.p(
        "In the POC the lock is engaged when the operator notes contain the phrase “deterministic island”. "
        "Our first version also reacted to “retrieve”, which the book never uses for this purpose; it was "
        "removed when the POC was aligned with the text. The exact trigger is less important than the "
        "principle: engaging the lock can only make the system <i>more</i> conservative.",
    )
    s.section("Two kinds of escalation")
    s.p(
        "Escalation is represented as an exception with a common base type, "
        "<font face='Mono'>HumanEscalationException</font>, and two concrete kinds: "
        "<font face='Mono'>DeterminismViolationException</font> (the lock could not be honoured) and "
        "<font face='Mono'>UncertaintyEscalationException</font> (the AI's 95% band is too wide, Chapter 8). "
        "Both are published as a HumanEscalation decision with no valve value and the human-review flag set "
        "<i>before</i> the exception is thrown, so an escalation is never lost even if the caller ignores it.",
    )
    s.callout("key", "An escalation is a decision, not an error", [
        "It is tempting to treat “I cannot answer” as a failure. In a safety architecture it is the correct "
        "output for a well-defined class of inputs, and it is tested, audited and counted like any other "
        "outcome. The Web API reflects this: an escalation returns HTTP 200 with <i>applied: false</i>, not "
        "an error code.",
    ])

    # ---------------------------------------------------------------- 17
    s.chapter("17", "Operator override and the limits of human authority",
              "Rule 5 of the Neural Constitution: no fully autonomous action without a human retaining "
              "override authority. But authority over what?")
    s.p(
        "The book's Neural Constitution (§26.4) includes the rule that a human always retains override "
        "authority. The POC implements this as an <b>operator override</b>: a command given by a named "
        "operator, with a stated reason, carried into the control cycle. It replaces the AI's proposal, and "
        "it is the natural answer to an escalation: the system says “I cannot decide this safely”, an "
        "operator checks the local gauges and decides.",
        "The harder design question is what the override may <i>not</i> do. The book is equally clear that in "
        "reactor control the hard limits are enforced by certified safety systems that keep final physical "
        "authority regardless of what any model recommends (§17.6). We read that as applying to people "
        "too, in this narrow sense: a triggered safety island or a SCRAM is itself the certified safety "
        "function, and an operator command does not bypass it in software.",
    )
    s.figure(F["authority"], "Figure 17.1 — The authority ladder used by the Arbiter. Higher rows override "
             "lower rows; lower rows are more flexible.", width=0.9)
    s.table([
        ["Situation", "Override applied?", "What is recorded"],
        ["No island; AI would be approved", "Yes", "OperatorOverride with operator, reason and the AI's proposal"],
        ["No island; lock or uncertainty escalation", "Yes — this is how an escalation is answered", "OperatorOverride"],
        ["Island triggered", "No", "IslandOverride + “Operator override (…) not applied: safety island … retains authority”"],
        ["SCRAM", "No", "Scram + “… not applied: the fail-safe SCRAM retains authority”"],
        ["Invalid (no name, no reason, outside 0–1)", "Rejected", "Nothing — the request is refused before a decision"],
    ], [0.32, 0.25, 0.43])
    s.callout("ours", "Why the operator is not bound by the Shield", [
        "The Shield's limit is, by its own name in the vault, the maximum safe opening for <i>AI-proposed</i> "
        "commands. It exists because the AI can be wrong in ways nobody reviewed. A named operator acting on "
        "local information is a different kind of authority, so the POC lets the override use the full "
        "0–100% range — while triggered islands still take precedence. A real plant would add procedures, "
        "permissions and perhaps a second pair of eyes; the audit trail records who did what and why.",
    ])
