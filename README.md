# NEXUS-1: Bounded AI & Deterministic Islands POC

[![Platform](https://shields.io)](https://microsoft.com)
[![Language](https://shields.io)](https://microsoft.com)
[![Architecture](https://shields.io)]()

A Proof of Concept (POC) demonstrating the **Deterministic Islands** and **System Arbiter** architecture described in the *NEXUS-1 Engineering Series* by **Grigorios Agathangelidis**. 

This repository implements a hybrid control system designed for critical industrial infrastructure, wrapping a stochastic (probabilistic) Neural Network inside a strictly predictable, zero-entropy (\(H=0\)) deterministic software shell.

---

## 🏗️ Architectural Overview

The system establishes a **"Probabilistic Core, Deterministic Shell"** topology using Domain-Driven Design (DDD). Section numbers refer to the book listed under *Bibliography*.

* **Stochastic Core** (`MiniNeuralNetwork`): a small 2-4-1 perceptron that predicts the valve opening from temperature and pressure. Dropout stays active at inference, so two identical readings can produce two different commands (§12.2).
* **Static Vault** (`StaticVault`, §12.3.1): every safety limit is a pre-verified, human-approved fact stored under `SHA-256(normalised query)`. The AI never produces these values. Updates are append-only (§12.4.4). An update closes the old version's `ValidTo` and registers a new version, so the question "what was the limit on date X?" always has exactly one answer.
* **Intra-Vector Routing** (`VectorResolver`, `GuardedResolver`, §12.3.3): a vault entry can be a pointer (`PointerTo`) to another vector, for example a backup line that uses the primary line's setpoint. Resolution follows the chain. Cycles, chains longer than 8 hops and dangling pointers fail loudly. A pointer to a superseded version is stale and fails until a human re-registers it.
* **Derived Islands** (`DerivedIslandFactory`, §24.4.1): an island computed as a pure function of other islands. The relief setpoint is the minimum of three redundant pressure transmitters' certified limits. It records the exact input versions it was computed from and is recomputed automatically when an input query gets a new version. If an input changes without a recomputation (e.g. through a pointer), the derived value is stale and fails loudly. An input update that would invalidate a derived island retroactively is rejected.
* **Frozen Snapshot** (`FrozenSnapshotConfig`, §12.3.2): locks the weights hash, runtime version, random seed and CPU-only inference, which makes the same network bitwise reproducible.
* **Shield** (`Shield`, §17.4): bounds every AI proposal with `min(proposed, maxSafe)` on every cycle, with `maxSafe` taken from the vault.
* **Protection Islands** (`IslandCatalog`): sensor-triggered interlocks (pressure relief, radiation containment, thermal protection) that are evaluated on **every** cycle. Their thresholds and outputs come from the vault.
* **Causality Lock** (`CausalityLock`, §12.3.3): engaged when the operator notes explicitly request a `"deterministic island"`. While it is engaged the command must come from an island or a Frozen Snapshot. Otherwise the Arbiter escalates to **Human-in-the-Loop** by throwing `DeterminismViolationException`. It never falls back silently to the stochastic core.
* **System Arbiter** (`CompleteSystemArbiter`): `Execute` runs a control cycle. It orchestrates the above, resolves conflicts between islands, and writes every decision to the audit trail. `Answer` resolves a factual query as in §12.4.2: Static Vault first, then a candidate vector (e.g. from RAG), and only then the stochastic generator (it returns `null`). Under a Causality Lock it escalates instead of falling through.

### 🛡️ Conflict Resolution Matrix

| Strategy | Condition | Action |
| :--- | :--- | :--- |
| **1. Priority Ring** | Islands of *different* priorities triggered | The island with the higher ring (`CriticalSafety > Structural > Operational`) takes control. |
| **2. Fail-Safe SCRAM** | Islands of the *same* priority with conflicting demands | Immediate **Emergency SCRAM** (valve to 0%). The audit record is flagged `RequiresHumanReview`. |
| **3. Human-in-the-Loop** | Causality Lock engaged, no deterministic answer | `DeterminismViolationException`: the AI command is not applied and the record is flagged `RequiresHumanReview`. |
| **4. Auditable Boundary** | Every control cycle | One JSON line per decision in `nexus1-audit.jsonl`: reading, lock state, AI proposal, triggered islands with the vault entries and versions they used, and the final command. |

> **Note on the book.** Rules 1 and 2 are an extension of this POC; they are not part of the book. The book (§12.4.3, p. 94) resolves conflicts with a fixed precedence between mechanisms (Static Vault → routed vector → Frozen Snapshot) and treats any disagreement between two deterministic sources as an error for human review. This is why the SCRAM record is flagged for review instead of being treated as settled.

---

## 💻 Code Structure

```
DeterministicIsland/
├── Program.cs                     # Six demonstration scenarios
├── CompleteSystemArbiter.cs       # The Arbiter
├── domain/                        # SensorReading, ControlCommand, DynamicIsland, ILMVector
├── ProbabilisticCore/             # MiniNeuralNetwork (stochastic / frozen)
├── Islands/                       # StaticVault, VectorResolver, DerivedIslandFactory, FrozenSnapshotConfig,
│                                  # Shield, SafetyLimits, IslandCatalog, CausalityLock
└── Audit/                         # AuditRecord, JSON Lines and in-memory audit logs
DeterministicIsland.Tests/         # xUnit tests
```

---

## 🚀 Running the POC

### Prerequisites
* [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or newer (the projects target `net8.0`, as in Appendix A.7 of the book).

### Execution Steps
```bash
cd DeterministicIsland
dotnet run          # runs the eight scenarios and writes nexus1-audit.jsonl next to the binary
cd ..
dotnet test         # runs the xUnit test suite
```

---

## 🔬 Demonstration Scenarios

1. **Normal Operation:** 60 °C, 5 bar. No island is triggered. The AI decides, but its command is capped at the vault's 75% limit by the Shield.
2. **High Temperature:** 95 °C, 4 bar. The `Structural` thermal island overrides the AI and sets the cooling opening to 60%.
3. **Priority Resolution:** 95 °C, 8.5 bar. The `Structural` and `CriticalSafety` islands both trigger. The pressure-relief island wins and opens the valve to 100%.
4. **Deadlock → SCRAM:** 8.5 bar and a radiation leak. Two `CriticalSafety` islands demand 100% and 0%. The Arbiter fires a **FAIL-SAFE EMERGENCY SCRAM** and flags the conflict for human review.
5. **Causality Lock → Human-in-the-Loop:** the operator requests a "deterministic island", but no island applies and no Frozen Snapshot is configured. The Arbiter refuses to answer stochastically and escalates to a human.
6. **Causality Lock → Frozen Snapshot:** the same request with a Frozen Snapshot core. The answer is bitwise identical on every repeat.
7. **Intra-Vector Routing:** the backup line's relief setpoint is registered as a pointer to the primary line's setpoint and resolves deterministically to 8 bar. An ordinary RAG vector without a Determinism block is then rejected under the Causality Lock and escalated to a human.
8. **Derived Island:** the relief setpoint is `min(PT-1, PT-2, PT-3) = min(8.4, 8.0, 8.2) = 8.0 bar`. PT-2 is recalibrated to 7.6 bar, the setpoint is recomputed automatically to 7.6 bar, and a reading of 7.8 bar now triggers the pressure-relief island.

---

## 📖 Bibliography & References
* Agathangelidis, G., *From Stochastic Chaos to Deterministic Certainty: AI for Critical Industrial Infrastructure*, NEXUS-1 Engineering Series, September 2026 — the book this POC is based on ([`From_Stochastic_Chaos_to_Deterministic_Certainty.pdf`](From_Stochastic_Chaos_to_Deterministic_Certainty.pdf)). Relevant chapters: 12 (Deterministic Islands), 17.4 (Safe RL: Shielding), 22 (Bounding the AI Context), 24 (Deterministic Islands — Deep Dive).
* Agathangelidis, G., *From Core to Quantum: Quantum Mechanics and the Nucleus, for the Engineer Who Will Model Them*, NEXUS-1 Series, First Edition, September 2026 — the preceding volume of the series.

---
*Disclaimer: This codebase is a theoretical architectural companion to the NEXUS-1 project. It is intended strictly for educational and modeling demonstrations. It should not be used to operate or make automated safety decisions in real nuclear or critical industrial facilities.*
