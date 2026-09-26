"""Front matter and Part I — Orientation."""

from guide_style import PageBreak, Spacer, cm


def build(s, F):
    s.chapter("", "How to read this guide")
    s.p(
        "This guide is written by an engineer for engineers. It assumes you can read a block diagram, "
        "that you have met a probability distribution before, and that you are comfortable with the "
        "idea that a program is a model of something real. It does <i>not</i> assume that you have "
        "trained a neural network, written a hash function or designed a safety system. Everything "
        "needed is built up from the ground, one step at a time, using a single small example: the "
        "<b>DeterministicIsland</b> proof of concept (POC) in this repository.",
        "The POC is a direct companion to <i>From Stochastic Chaos to Deterministic Certainty</i> by "
        "Grigorios Agathangelidis, Volume III of the NEXUS-1 series. The book develops the theory; the "
        "POC is a working, tested, deliberately small machine that puts that theory under load. This guide "
        "sits between the two. It explains the POC as <b>architecture</b> — what each part is for, what it "
        "guarantees, and why it is shaped the way it is — rather than as C# source. Where a precise "
        "statement needs code, the guide uses short, language-neutral pseudo-code or the actual data "
        "formats written to disk.",
    )
    s.section("Three ways through")
    s.bullets([
        "<b>The quick tour (1 hour).</b> Read Chapters 1–3, look at Figures 3.1 and 15.1, then read "
        "Chapter 25 while running the demo. You will know what every part does and why.",
        "<b>The engineer's path (1–2 days).</b> Read Parts I–IV in order and do the exercises marked "
        "<i>Level 1</i> and <i>Level 2</i> in Chapter 26. This is the intended route for someone "
        "starting out in trustworthy AI for critical systems.",
        "<b>The reviewer's path.</b> Start with Part V (what we took from the book, what we changed and "
        "why, and the honest boundaries), then the Neural Constitution in Chapter 20, then the test map "
        "in Appendix B. That is the evidence a reviewer or regulator would ask for.",
    ])
    s.section("Conventions")
    s.table([
        ["Marker", "Meaning"],
        ["<b>KEY IDEA</b>", "A concept you should be able to explain to a colleague after reading the chapter."],
        ["<b>OUR APPROACH</b>", "A place where this POC made a decision of its own, beyond or different from the book."],
        ["<b>HONEST BOUNDARY</b>", "What a mechanism does <i>not</i> guarantee. The book ends every chapter with one; so do we."],
        ["<b>TRY IT</b>", "A small experiment you can run against the code in a few minutes."],
        ["§12.3.1", "A section of the book <i>From Stochastic Chaos to Deterministic Certainty</i>."],
        ["<font face='Mono'>StaticVault</font>", "A name in the code base. You do not need to read the code, but the name tells you where to look."],
    ], [0.24, 0.76])
    s.p(
        "All numbers in this guide — network outputs, uncertainty bands, hashes, versions — are computed "
        "from the same weights and the same rules as the code, by the script that produced this PDF "
        "(<font face='Mono'>docs/source/</font>). If the code changes, re-running the script regenerates "
        "the guide with the new numbers."
    )
    s.append(PageBreak())
    s.toc()

    # ------------------------------------------------------------------ Part I
    s.part("I", "Orientation",
           "Why a neural network cannot be trusted on its own in a control loop, what the book proposes "
           "instead, and the whole POC on one page.")

    s.chapter("1", "Why this guide, and why this example",
              "A valve, two sensors, one small neural network and a set of rules it is not allowed to break. "
              "That is enough to learn the core ideas of trustworthy AI for critical infrastructure.")
    s.p(
        "Every plant engineer knows the difference between a <i>measurement</i> and a <i>guess</i>. A "
        "measurement is reproducible: same conditions, same instrument, same number. A guess may be "
        "excellent on average and still wrong on the one occasion that matters. Modern AI is, at its core, "
        "an extremely good guesser. It is trained on data, it generalises, and it is flexible in ways no "
        "hand-written rule ever will be. It is also — by construction — stochastic: random weight "
        "initialisation, dropout, and sampling at inference time are not bugs but features that make the "
        "model learn and generalise (book, §12.2.1).",
        "The book's central claim is that in critical systems this flexibility is only acceptable inside "
        "boundaries that are <b>not</b> learned: reproducible, auditable, certifiable reference points it "
        "calls <b>Deterministic Islands</b>. The AI may reason freely in the open sea; the hard facts it must "
        "respect — a relief setpoint, a maximum valve opening, a SCRAM condition — live on islands where "
        "identical questions always receive identical answers.",
        "The POC turns that claim into a small control system. A <b>stochastic core</b> (a tiny neural "
        "network) proposes how far to open a valve based on temperature and pressure. A <b>deterministic "
        "shell</b> decides whether that proposal may reach the actuator, replaces it when a safety rule "
        "applies, and hands the decision to a human when it cannot be sure. Everything it does is recorded "
        "so that, afterwards, anyone can answer the question <i>which rule, which version, approved by "
        "whom, produced this command?</i>",
    )
    s.section("Why such a small example?")
    s.p(
        "Because small systems can be understood completely. The network has two inputs, four hidden "
        "neurons and one output: 17 numbers in total. You can compute its output by hand (we do so in "
        "Chapter 6). Its randomness comes from exactly one place — dropout on four neurons — so there are "
        "only 16 possible internal configurations, and we can enumerate all of them (Chapter 7). The demo vault "
        "holds eleven facts. The arbiter has six possible decisions. There are eleven scenarios and one "
        "hundred automated tests.",
        "Nothing in this example is realistic in scale, and that is the point. Every mechanism in it scales "
        "to a real plant or a real language model — hashing, versioning, pointer resolution, precedence, "
        "uncertainty estimation, escalation, audit — but here each one fits on a page. Once you understand "
        "why each is there, adding scale is an engineering exercise, not a conceptual leap.",
    )
    s.callout("key", "What you should be able to do after this guide", [
        "Explain why a correctly working neural network can give two different answers to the same question, "
        "and why that matters in a control loop.",
        "Design a deterministic boundary around a stochastic component: which facts it holds, how they are "
        "versioned, how the system decides who is in charge, and when it must stop and ask a human.",
        "Read the POC's audit trail and reconstruct exactly why each command was issued.",
    ])
    s.section("What the POC is not")
    s.p(
        "It is not a reactor controller, a certified safety system, or a model of any real plant. The "
        "numbers (8 bar, 90 °C, 75%) are illustrative. The network's weights were chosen by hand to produce "
        "a plausible, smooth response surface, not trained on data. The disclaimer at the end of the README "
        "applies throughout: this code must not be used to operate or make safety decisions in a real "
        "facility. What it offers instead is a faithful, testable architecture that you can study, break "
        "and extend.",
    )

    s.chapter("2", "The problem: a correct network that disagrees with itself",
              "The book's §12.2 in one sentence: nothing has malfunctioned, and yet the same input produces "
              "different outputs.")
    s.p(
        "Run the POC's network three times on the same reading — 60 °C and 5 bar — and you may get 37.2%, "
        "29.7% and 50.0% valve opening. Nothing is broken. The network uses <b>dropout</b>: on each call, "
        "each of its four hidden neurons is switched off with probability 0.2. Which neurons survive is a "
        "random draw, and each draw is a slightly different network. The book traces three separate sources "
        "of this behaviour in real systems — random initialisation, dropout, and temperature sampling in "
        "language models — and makes the same observation about all of them: the randomness is deliberate "
        "and useful everywhere except for a specific class of questions (§12.2.1).",
    )
    s.section("Why a plant cannot live with this")
    s.p(
        "Consider two operators on two shifts who both ask the system for the maximum permitted discharge "
        "pressure of a pump and receive two different numbers. The book is blunt about it: they have not "
        "encountered a minor inconsistency; they have encountered a system that cannot be trusted to state a "
        "fact (§12.2.3). In a control loop the problem is sharper still. If the command sent to a valve "
        "depends on a coin flip, then the plant's behaviour depends on a coin flip, and after an incident no "
        "one can reproduce what happened.",
        "There are really three distinct requirements hiding here, and the POC addresses each with a "
        "different mechanism:",
    )
    s.table([
        ["Requirement", "Question it answers", "Mechanism in the POC"],
        ["<b>Reproducibility</b>", "Will the same question get the same answer tomorrow, on another server?",
         "Static Vault (hash-addressed facts); Frozen Snapshot (locked inference)"],
        ["<b>Boundedness</b>", "Can the AI ever command something outside the certified range?",
         "Shield; protection islands; priority rings; SCRAM"],
        ["<b>Accountability</b>", "Afterwards, can we say exactly which rule and which approval produced a command?",
         "Versioned facts with approvers; domain events; audit trail; hash-chained storage"],
    ], [0.2, 0.42, 0.38])
    s.section("Two kinds of question")
    s.p(
        "The book's worked example in §12.5 separates two questions an operator might ask. <i>“What is the "
        "maximum allowed pressure?”</i> has one correct answer that a human engineer has verified; it "
        "should never be generated. <i>“What do you recommend for the current situation?”</i> has no fixed "
        "answer and genuinely needs the model's flexible reasoning — but if that recommendation mentions a "
        "limit, the limit itself must come from the verified source, not from the model.",
        "The POC's control loop has exactly the same structure. <i>How far should the valve open right "
        "now?</i> is a recommendation, and the network is allowed to make it. <i>What is the relief "
        "setpoint, the maximum valve opening, the containment position?</i> are facts, and the network is "
        "never asked for them. The whole architecture is the machinery that keeps these two kinds of answer "
        "apart and decides, cycle by cycle, which one governs the actuator.",
    )
    s.callout("key", "Randomness is not the enemy", [
        "The goal is not to remove stochasticity from the system; that would throw away the reason for "
        "using AI at all. The goal is to put it in its place: free where flexibility helps, excluded where a "
        "guarantee is required, and <i>measured</i> everywhere in between. The POC even uses the network's "
        "randomness as a signal: the spread of its answers tells us how much to trust it (Chapter 8).",
    ])

    s.chapter("3", "The big picture: probabilistic core, deterministic shell",
              "One figure, one control cycle, one query path. Everything later in the guide is a zoom into "
              "a box on this page.")
    s.figure(F["architecture"], "Figure 3.1 — The POC's architecture. The stochastic core proposes; the "
             "deterministic shell holds the facts and the rules; the Arbiter decides; humans can intervene; "
             "everything is recorded.")
    s.p(
        "A <b>sensor reading</b> enters the system: temperature, pressure, a radiation-leak flag and free-text "
        "operator notes. Two things happen in parallel. The <b>stochastic core</b> computes a proposed valve "
        "opening together with an estimate of how uncertain that proposal is. The <b>deterministic shell</b> "
        "looks up, in the Static Vault, every safety fact valid at this moment and builds the protection "
        "islands from them.",
        "The <b>System Arbiter</b> then decides. If a safety island is triggered, the island's command wins, "
        "and if two equally important islands disagree, the system fails safe with a SCRAM. If no island is "
        "triggered, the AI's proposal may be used — but only if the operator has not asked for a "
        "deterministic answer, only if the AI is confident enough, and only after the Shield caps it to the "
        "certified maximum. When none of these conditions allows a safe automatic answer, the Arbiter does "
        "not guess: it escalates to a human.",
        "Every decision publishes a <b>domain event</b>. Listeners write an audit trail and an event log; "
        "every new version of a safety fact is appended to a hash-chained file. Nothing that influenced a "
        "command is lost.",
    )
    s.section("Two paths through the shell")
    s.table([
        ["", "Control path — Execute(reading)", "Query path — Answer(query)"],
        ["Input", "Sensor reading, optional operator override", "A question in words, optional candidate answer"],
        ["Output", "A valve command, a SCRAM, or an escalation", "A deterministic fact, a fall-through, or an escalation"],
        ["Book section", "§17.4 Shielding, §12.4.3 precedence", "§12.4.2 the Arbiter, §12.5 worked example"],
        ["Example", "95 °C, 4 bar → Structural island → 60%", "“high pressure relief setpoint in bar” → 8.0 (v1)"],
    ], [0.14, 0.43, 0.43])
    s.p(
        "Most of this guide follows the control path, because that is where the interesting interactions "
        "are. The query path reuses the same vault and resolver and is covered in Chapters 10–13 and 21.",
    )
    s.section("The layers")
    s.figure(F["layers"], "Figure 3.2 — The same system seen as layers (book, Chapters 20–23). The domain "
             "layer holds all the rules; the outer layers only wire, store and expose them.", width=0.95)
    s.p(
        "The layering follows the book's Domain-Driven Design chapters. The <b>domain</b> layer contains "
        "every rule that matters for safety and has no knowledge of files, HTTP or consoles. The "
        "<b>infrastructure</b> layer stores and logs. The <b>application</b> layer wires the pieces "
        "together once. The <b>presentation</b> layer — a console demo and a small Web API — is thin by "
        "design: as the book puts it, the endpoint has no way to bypass the Arbiter, because the Arbiter is "
        "the only path to an answer (§23.5).",
    )

    s.chapter("4", "Vocabulary",
              "The book calls this the Ubiquitous Language (§20.4.1): words that mean the same thing to the "
              "plant engineer, the software engineer and the reviewer.")
    s.table([
        ["Term", "Meaning in this POC"],
        ["Stochastic core", "The neural network that proposes valve openings. Allowed to be flexible; never trusted with facts."],
        ["Deterministic Island", "A safety fact (or rule built from facts) whose answer is identical for identical questions, pre-verified and approved by a human."],
        ["Static Vault", "The store of deterministic facts, addressed by SHA-256 of the normalised question; append-only and versioned."],
        ["ILMVector", "The book's unit of knowledge: an id, an embedding (here: one number) and an optional Determinism block."],
        ["Determinism block", "Metadata that makes a vector part of an island: IsDeterministic, Version, ValidFrom/ValidTo, ApprovedBy, PointerTo, DerivedFrom."],
        ["Version", "One entry in the append-only history of a fact (v1, v2, …). Old versions are closed, never edited."],
        ["Pointer (Intra-Vector Routing)", "A vault entry that defers to another vector instead of holding a value."],
        ["Derived island", "A fact computed as a pure function (e.g. minimum) of other facts, recomputed when they change."],
        ["Protection island", "A sensor-triggered interlock (pressure relief, radiation containment, thermal protection) built from vault facts."],
        ["Priority ring", "The rank of a protection island: CriticalSafety &gt; Structural &gt; Operational."],
        ["Shield", "The bound applied to every AI proposal: min(proposal, maximum safe opening)."],
        ["Frozen Snapshot", "A configuration that makes the network bitwise reproducible: locked weights, runtime, seed, CPU-only."],
        ["MC Dropout", "Running the network many times with dropout on and using the spread as an uncertainty estimate."],
        ["Causality Lock", "Engaged when the operator explicitly requests a deterministic answer; forbids a stochastic one."],
        ["Human-in-the-Loop", "Escalation: the system refuses to act on its own answer and hands the decision to a person."],
        ["Operator override", "A command from a named operator with a reason. Beats the AI, never a safety island."],
        ["SCRAM", "Fail-safe shutdown (valve to 0%) when equally ranked safety islands disagree."],
        ["System Arbiter", "The component that combines all of the above into one decision per cycle."],
        ["Domain event", "A record of something that happened (IslandAdded, IslandTriggered, ControlDecisionMade)."],
        ["Audit record", "One line per control cycle describing every input to the decision and the decision itself."],
        ["Neural Constitution", "The list of rules the system never violates, each linked to its mechanism and its tests."],
    ], [0.27, 0.73])
