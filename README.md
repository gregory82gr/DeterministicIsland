# NEXUS-1: Bounded AI & Deterministic Islands POC

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![Language](https://img.shields.io/badge/language-C%23-239120)](https://learn.microsoft.com/dotnet/csharp/)
![Architecture](https://img.shields.io/badge/architecture-DDD-1d4e89)
[![Guide](https://img.shields.io/badge/guide-PDF%2C%2057%20pages-b83280)](docs/NEXUS-1_Deterministic_Islands_Engineer_Guide.pdf)

A Proof of Concept (POC) demonstrating the **Deterministic Islands** and **System Arbiter** architecture described in the *NEXUS-1 Engineering Series* by **Grigorios Agathangelidis**. 

This repository implements a hybrid control system designed for critical industrial infrastructure, wrapping a stochastic (probabilistic) Neural Network inside a strictly predictable, zero-entropy (\(H=0\)) deterministic software shell.

> 📘 **New to the field? Start with the guide.** [*Deterministic Islands in Practice — An Engineer-to-Engineer Guide*](docs/NEXUS-1_Deterministic_Islands_Engineer_Guide.pdf) (PDF, 57 pages) explains the POC step by step as architecture rather than C#. It covers the neural core computed by hand, dropout and MC Dropout, the Static Vault, versions, pointers, derived islands, the Arbiter, the Causality Lock, operator override and the trust infrastructure. It also sets out the approach this POC took beyond the book and ends with graded exercises. See [Documentation](#-documentation).

---

## 🏗️ Architectural Overview

The system establishes a **"Probabilistic Core, Deterministic Shell"** topology using Domain-Driven Design (DDD). Section numbers refer to the book listed under *Bibliography*. The [engineer guide](docs/NEXUS-1_Deterministic_Islands_Engineer_Guide.pdf) explains each item below in depth.

* **Stochastic Core** (`MiniNeuralNetwork`): a small 2-4-1 perceptron that predicts the valve opening from temperature and pressure. Dropout stays active at inference, so two identical readings can produce two different commands (§12.2).
* **Uncertainty Quantification** (`PredictWithUncertainty`, §13.6): MC Dropout runs each reading through the network 200 times and reports the mean with a 95% interval (mean ± 1.96·s). The mean becomes the AI proposal. If the interval is wider than the vault's limit (±20%), the command is not applied and the cycle escalates to a human (`UncertaintyEscalationException`). As §16.9 recommends, the threshold is a vault fact, not a modelling choice.
* **Static Vault** (`StaticVault`, §12.3.1): every safety limit is a pre-verified, human-approved fact stored under `SHA-256(normalised query)`. The AI never produces these values. Updates are append-only (§12.4.4). An update closes the old version's `ValidTo` and registers a new version, so the question "what was the limit on date X?" always has exactly one answer.
* **Intra-Vector Routing** (`VectorResolver`, `GuardedResolver`, §12.3.3): a vault entry can be a pointer (`PointerTo`) to another vector, for example a backup line that uses the primary line's setpoint. Resolution follows the chain. Cycles, chains longer than 8 hops and dangling pointers fail loudly. A pointer to a superseded version is stale and fails until a human re-registers it.
* **Derived Islands** (`DerivedIslandFactory`, §24.4.1): an island computed as a pure function of other islands. The relief setpoint is the minimum of three redundant pressure transmitters' certified limits. It records the exact input versions it was computed from and is recomputed automatically when an input query gets a new version. If an input changes without a recomputation (e.g. through a pointer), the derived value is stale and fails loudly. An input update that would invalidate a derived island retroactively is rejected.
* **Frozen Snapshot** (`FrozenSnapshotConfig`, §12.3.2): locks the weights hash, runtime version, random seed and CPU-only inference, which makes the same network bitwise reproducible.
* **Shield** (`Shield`, §17.4): bounds every AI proposal with `min(proposed, maxSafe)` on every cycle, with `maxSafe` taken from the vault.
* **Protection Islands** (`IslandCatalog`): sensor-triggered interlocks (pressure relief, radiation containment, thermal protection) that are evaluated on **every** cycle. Their thresholds and outputs come from the vault.
* **Causality Lock** (`CausalityLock`, §12.3.3): engaged when the operator notes explicitly request a `"deterministic island"`. While it is engaged the command must come from an island or a Frozen Snapshot. Otherwise the Arbiter escalates to **Human-in-the-Loop** by throwing `DeterminismViolationException`. It never falls back silently to the stochastic core.
* **Operator Override** (`OperatorOverride`, Constitution rule 5): `Execute(reading, override)` lets a named operator, with a stated reason, replace the AI's command. This also answers a Human-in-the-Loop escalation. The operator is not bound by the Shield, whose limit applies to AI proposals only. An override **never** beats a triggered safety island or a SCRAM, because certified safety functions keep final physical authority (§17.6). In that case it is recorded in the audit trail as "not applied".
* **System Arbiter** (`CompleteSystemArbiter`): `Execute` runs a control cycle. It orchestrates the above, resolves conflicts between islands, and publishes one `ControlDecisionMade` event per cycle. `Answer` resolves a factual query as in §12.4.2: Static Vault first, then a candidate vector (e.g. from RAG), and only then the stochastic generator (it returns `null`). Under a Causality Lock it escalates instead of falling through.
* **Persistence** (`IIslandRepository`, `JsonLinesIslandRepository`, §23.2): each new version is saved as one JSON line **before** it takes effect, so a failed save leaves the vault unchanged. The file is append-only, like the vault's own history. On start-up the vault replays the file exactly, with the same `VectorId`s so pointers and derived islands still resolve. It rejects a record that is inconsistent: missing approver, skipped version, out-of-order dates, a pointer or derivation input not saved before it, or an unknown derivation rule. Derivation rules are stored by name (`DerivationRules`: `minimum`, `maximum`, or registered by the application).
* **Tamper evidence** (hash chain): every line stores `PreviousHash` and `Hash = SHA-256(PreviousHash + Record)`, starting from 64 zeros. A record edited, removed, inserted or reordered in the file makes loading fail with the line number. *Limitation:* cutting lines off the end, or rewriting the whole file with a new chain, cannot be seen from the file alone. For that, `HeadHash` (printed by scenario 10) should be recorded outside the file and compared on the next start.
* **Domain Events** (`DomainEventBus`, §22.7, §23.4): the vault publishes `IslandAdded` for every new version, including pointers, derived islands and automatic recomputations. The Arbiter publishes `IslandTriggered` for every vault fact that governed a decision, and `ControlDecisionMade` for every cycle. Auditing is an Observer listener (`AuditLogListener`, `JsonLinesEventLog`), so neither the vault nor the Arbiter knows that auditing exists.

### 📜 Neural Constitution (§26.4)

`NeuralConstitution` lists every guarantee the system makes. For each one it names the mechanism that enforces it and the tests that demonstrate it. The program prints the list at start-up, and `NeuralConstitutionTests` fails if a rule cites a test that does not exist.

| # | Rule | Source |
| :-- | :--- | :--- |
| 1 | A resolved deterministic fact is never overridden by a generated one. | Book |
| 2 | No control action reaches an actuator outside its certified safe range. | Book |
| 3 | A Causality Lock, once engaged, never silently falls back to a stochastic answer. | Book |
| 4 | Every registered fact's provenance and approval are permanently recorded. | Book |
| 5 | No fully autonomous action is taken without a human retaining override authority. | Book |
| 6 | An AI command is never applied when its uncertainty exceeds the certified bound. | POC extension |
| 7 | A routed or derived fact is never used once the fact it rests on has changed. | POC extension |
| 8 | Conflicting safety demands at the same priority end in a fail-safe SCRAM, never a guess. | POC extension |

### 🛡️ Conflict Resolution Matrix

| Strategy | Condition | Action |
| :--- | :--- | :--- |
| **1. Priority Ring** | Islands of *different* priorities triggered | The island with the higher ring (`CriticalSafety > Structural > Operational`) takes control. |
| **2. Fail-Safe SCRAM** | Islands of the *same* priority with conflicting demands | Immediate **Emergency SCRAM** (valve to 0%). The audit record is flagged `RequiresHumanReview`. |
| **3. Human-in-the-Loop** | Causality Lock engaged with no deterministic answer, **or** the AI's MC Dropout interval exceeds the vault limit | `DeterminismViolationException` / `UncertaintyEscalationException` (both `HumanEscalationException`): the AI command is not applied and the record is flagged `RequiresHumanReview`. |
| **4. Auditable Boundary** | Every control cycle | One JSON line per decision in `nexus1-audit.jsonl`: reading, lock state, AI proposal, triggered islands with the vault entries and versions they used, and the final command. Every `IslandAdded` / `IslandTriggered` event goes to `nexus1-events.jsonl`. |

> **Note on the book.** Rules 1 and 2 are an extension of this POC; they are not part of the book. The book (§12.4.3, p. 94) resolves conflicts with a fixed precedence between mechanisms (Static Vault → routed vector → Frozen Snapshot) and treats any disagreement between two deterministic sources as an error for human review. This is why the SCRAM record is flagged for review instead of being treated as settled.

---

## 💻 Code Structure

```
DeterministicIsland/
├── Program.cs                     # Eleven demonstration scenarios
├── CompleteSystemArbiter.cs       # The Arbiter
├── domain/                        # SensorReading, ControlCommand, DynamicIsland, ILMVector
├── ProbabilisticCore/             # MiniNeuralNetwork (stochastic / frozen)
├── Islands/                       # StaticVault, IslandRepository, DerivationRules, VectorResolver,
│                                  # DerivedIslandFactory, FrozenSnapshotConfig,
│                                  # Shield, SafetyLimits, IslandCatalog, CausalityLock
├── Audit/                         # AuditRecord, JSON Lines and in-memory audit logs
├── Events/                        # Domain events, DomainEventBus, listeners
└── Governance/                    # NeuralConstitution
DeterministicIsland.Api/          # Web API (§23.5): NexusRuntime (application layer) + endpoints
DeterministicIsland.Tests/         # xUnit tests, including API tests with WebApplicationFactory
docs/                              # Engineer-to-engineer guide (PDF) and the source that generates it
```

---

## 🚀 Running the POC

### Prerequisites
* [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or newer (the projects target `net8.0`, as in Appendix A.7 of the book).

### Execution Steps
```bash
cd DeterministicIsland
dotnet run          # runs the eleven scenarios; writes nexus1-vault.jsonl (recreated on each run),
                    # nexus1-audit.jsonl and nexus1-events.jsonl next to the binary
cd ..
dotnet test         # runs the xUnit test suite
```

### Web API (§23.5)
```bash
cd DeterministicIsland.Api
dotnet run          # listens on the URL printed at start-up
```

The API is a thin layer over the same Arbiter. There is no way to reach an answer without going through the Static Vault, the Shield, the islands and the Causality Lock. On start-up it reloads `nexus1-vault.jsonl` (hash chain verified). If the file is empty, it commissions the default safety limits once. Paths are set in `appsettings.json` under `Nexus`. Requests are processed one at a time, because the domain model is not thread-safe.

| Endpoint | Purpose |
| :--- | :--- |
| `POST /control` | One control cycle. Body: `temperatureCelsius`, `pressureBar`, `operatorNotes`, `radiationLeakDetected`, optional `operatorOverride {operatorId, valveOpening, reason}`. The response gives `applied`, `valveOpening`, `decision`, `requiresHumanReview`, `reason`, the AI proposal and its uncertainty, and the triggered islands. An escalation is `200` with `applied: false`; an invalid override is `400`. |
| `POST /answer` | A factual query (§12.4.2). Body: `query`, `requireDeterminism`. A Static Vault hit returns the value with its version and approver. With no deterministic answer the query falls through (`isDeterministic: false`), or, under the lock, returns `409` (Human-in-the-Loop). |
| `GET /vault/history?query=…` | Every version of one fact: value, validity, approver, pointer, derivation. |
| `GET /vault/queries` | The facts registered in the vault. |
| `GET /constitution` | The Neural Constitution, with mechanisms and verifying tests. |

```bash
curl -X POST http://localhost:5000/control -H "Content-Type: application/json" \
     -d '{"temperatureCelsius":95,"pressureBar":4,"radiationLeakDetected":false}'
```

The API deliberately has **no endpoint that writes safety limits**. The book (§22.8) names authentication and authorisation on who may register islands as a prerequisite for that, and this POC does not provide them.

---

## 🔬 Demonstration Scenarios

1. **Normal Operation:** 60 °C, 5 bar. No island is triggered. The AI decides with its MC Dropout mean (95% interval about ±17%), and the Shield caps the command at the vault's 75% limit.
2. **High Temperature:** 95 °C, 4 bar. The `Structural` thermal island overrides the AI and sets the cooling opening to 60%.
3. **Priority Resolution:** 95 °C, 8.5 bar. The `Structural` and `CriticalSafety` islands both trigger. The pressure-relief island wins and opens the valve to 100%.
4. **Deadlock → SCRAM:** 8.5 bar and a radiation leak. Two `CriticalSafety` islands demand 100% and 0%. The Arbiter fires a **FAIL-SAFE EMERGENCY SCRAM** and flags the conflict for human review.
5. **Causality Lock → Human-in-the-Loop:** the operator requests a "deterministic island", but no island applies and no Frozen Snapshot is configured. The Arbiter refuses to answer stochastically and escalates to a human.
6. **Causality Lock → Frozen Snapshot:** the same request with a Frozen Snapshot core. The answer is bitwise identical on every repeat.
7. **Intra-Vector Routing:** the backup line's relief setpoint is registered as a pointer to the primary line's setpoint and resolves deterministically to 8 bar. An ordinary RAG vector without a Determinism block is then rejected under the Causality Lock and escalated to a human.
8. **Derived Island:** the relief setpoint is `min(PT-1, PT-2, PT-3) = min(8.4, 8.0, 8.2) = 8.0 bar`. PT-2 is recalibrated to 7.6 bar, the setpoint is recomputed automatically to 7.6 bar, and a reading of 7.8 bar now triggers the pressure-relief island.
9. **Uncertain AI → Human-in-the-Loop:** 85 °C, 7 bar. No island applies, but the AI's 95% interval is about ±27%, wider than the permitted ±20%. The command is not applied and the cycle escalates.
10. **Persistence:** the vault is reopened from `nexus1-vault.jsonl`. It has the same 13 versions of 11 facts, with the same vector ids, the hash chain verifies, and the relief setpoint still resolves to 7.6 bar (v2).
11. **Operator Override:** the operator answers scenario 9's escalation by setting the valve to 55%, which is applied. The same operator then tries to open the valve to 100% during scenario 4's SCRAM; the command is recorded but not applied.

---

## 📚 Documentation

| Document | What it is |
| :--- | :--- |
| [`docs/NEXUS-1_Deterministic_Islands_Engineer_Guide.pdf`](docs/NEXUS-1_Deterministic_Islands_Engineer_Guide.pdf) | **Deterministic Islands in Practice**: a 57-page engineer-to-engineer guide in six parts: orientation, the stochastic core, Deterministic Islands, trust infrastructure, our approach, hands-on. |
| [`docs/source/`](docs/source) | The Python source that generates the guide. Its network mirrors `MiniNeuralNetwork.cs` exactly, so every number and chart in the guide is computed, not typed. |

The guide suggests three reading paths:
- **Quick tour (1 hour):** Chapters 1–3 and Chapter 25, with the demo running.
- **Engineer's path (1–2 days):** Parts I–IV and the Level 1–2 exercises.
- **Reviewer's path:** Part V (what we took from the book, what we changed and why, honest boundaries), the Neural Constitution, and the test map.

To rebuild the guide after changing the code:

```bash
pip install -r docs/source/requirements.txt
python docs/source/build_guide.py          # add --run to embed a fresh demo run
```

---

## 📖 Bibliography & References
* Agathangelidis, G., *From Stochastic Chaos to Deterministic Certainty: AI for Critical Industrial Infrastructure*, NEXUS-1 Engineering Series, Volume III, September 2026. This is the book the POC is based on. Relevant chapters: 12 (Deterministic Islands), 13.6 (MC Dropout), 17.4 (Safe RL: Shielding), 19.5 (Auditing), 22–23 (DDD, events, repositories, API), 24 (Deterministic Islands — Deep Dive), 26.4 (Neural Constitution).
* Agathangelidis, G., *From Core to Quantum: Quantum Mechanics and the Nucleus, for the Engineer Who Will Model Them*, NEXUS-1 Series, Volume II, First Edition, September 2026.
* Agathangelidis, G., *From Grid to Core*, NEXUS-1 Series, Volume I.
* The complete list of the author's 22 books is under [The author's books](#%EF%B8%8F-the-authors-books).

### ✍️ The author's books
The NEXUS-1 series by **Grigorios Agathangelidis** now counts **22 books**. It began with a single experiment, an interactive console for an educational digital twin of a nuclear plant, and grew one open question at a time. The whole series keeps one standing rule: *nothing is stated as certain that has not been shown to hold in practice*. The books are available on Leanpub:

👉 **[leanpub.com/u/grigorios-kyriakos-agathangelidis](https://leanpub.com/u/grigorios-kyriakos-agathangelidis)**

| Theme | Book | In one line |
| :--- | :--- | :--- |
| Physics and engineering of the plant | *From Grid to Core* | The foundation of the series: from the 400 kV substation to the reactor core, with neutron kinetics and SCRAM. |
|  | *From Queue to Core* | Stochastic queueing theory as a rigorous mathematical foundation for reactor kinetics, with C# implementations. |
|  | *From Core to Quantum* | From the quantum crisis to the structure of the nucleus, and a complete quantum-circuit simulator in modelled C#. |
| Interpretable and controlled AI | *From Flood to Cause* | When 200 alarms fire at once: a deterministic causal graph finds the root cause; the LLM only explains it. |
|  | *From Trial to Policy* | Reinforcement learning from scratch with a fully interpretable Q-learning agent: 175 numbers in an auditable table. |
|  | *From Stochastic Chaos to Deterministic Certainty* | An industrial language model wrapped in proven deterministic boundaries, mapped to the EU AI Act. **This POC's book.** |
| Data architecture | *From Schema to System* | The complete schema atlas: 17 domains, 654 tables, with ER diagrams and verification queries behind the twin. |
|  | *From Table to Twin* | The same schema built twice (Database First and Code First with EF Core) and a chapter reconciling them. |
|  | *From Entity to Context* | Clean EF Core mapping of the schema: one configuration per entity instead of a giant OnModelCreating. |
| Domain-Driven Design | *From Domain to Twin* | DDD from scratch in plain language, applied to NEXUS-1: from entities and aggregates to the SQL schema. |
|  | *From Context to Flow* | Advanced DDD patterns (context maps, sagas, outbox) through one flow that crosses nine bounded contexts. |
| Backend trilogy (.NET) | *From Blueprint to Core* | Domain and application layers without a database or web server: 17 contexts, 50 green tests in about 600 ms. |
|  | *From Core to Contract* | The core gets a database (EF Core, 654 tables) and a public API, with infrastructure strictly below the seam. |
|  | *From Contract to Container* | Integration tests on a real SQL Server via Testcontainers, an outbox that survives a killed process, a CI gate. |
| Microservices | *From Flow to Services* | More than 70 chapters: microservices as a consequence of mature boundaries, not a starting point, and when distribution is not worth it. |
|  | *From Services to Runtime* | Three owning services on a real .NET runtime blueprint: inbox/outbox, JWT, OpenTelemetry, Kubernetes. |
| Formal methods | *From Flow to Proof* | Architectural promises become proofs: state machines, TLA+, Petri nets, category theory; model versus implementation. |
| Systems trilogy (below .NET) | *From Runtime to Distribution — Volume I* | From C# to the hardware: CPU, kernel mode, threads, inside the CLR (JIT, Native AOT), memory and GC. |
|  | *From Runtime to Distribution — Volume II* | The process boundary: concurrency, async/await as a state machine, IPC, TCP/TLS/gRPC and the “Boundary Ledger”. |
|  | *From Runtime to Distribution — Volume III* | Production: containers, Kubernetes, SLIs/SLOs, canary deployments and practical observability. |
| Frontend (Angular) | *From File to Framework* | A 5,900-line single-file console becomes a real Angular application, and every screen is checked for “honesty”. |
| Retrospective | *From Certainty to Calibration* | The author revisits six of his own decisions: then → mechanism → lesson → now → what the correction risks. |

Books that connect directly to this POC: *From Flood to Cause* and *From Trial to Policy* (the same idea of a deterministic core with an explaining or learning component around it), *From Domain to Twin* and *From Blueprint to Core* (the DDD and layering used here), *From Flow to Proof* (turning the Neural Constitution's promises into proofs), and *From Certainty to Calibration* (the same habit as this POC's Part V: revisiting one's own decisions).


---
*Disclaimer: This codebase is a theoretical architectural companion to the NEXUS-1 project. It is intended strictly for educational and modeling demonstrations. It should not be used to operate or make automated safety decisions in real nuclear or critical industrial facilities.*
