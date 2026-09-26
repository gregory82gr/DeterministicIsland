# NEXUS-1: Bounded AI & Deterministic Islands POC

[![Platform](https://shields.io)](https://microsoft.com)
[![Language](https://shields.io)](https://microsoft.com)
[![Architecture](https://shields.io)]()

A Proof of Concept (POC) demonstrating the **Deterministic Islands** and **System Arbiter** architecture described in the *NEXUS-1 Engineering Series* by **Grigorios Agathangelidis**. 

This repository implements a hybrid control system designed for critical industrial infrastructure, wrapping a stochastic (probabilistic) Neural Network inside a strictly predictable, zero-entropy (\(H=0\)) deterministic software shell.

---

## 🏗️ Architectural Overview

The system establishes a **"Probabilistic Core, Deterministic Shell"** topology using Domain-Driven Design (DDD):

* **Stochastic Core Layer:** A lightweight, pure C# neural network (`MiniNeuralNetwork`) that predicts the required valve opening based on input telemetry. It represents the flexible but inherently unpredictable AI component.
* **Dynamic Deterministic Islands:** Safety boundaries that are instantiated **on-demand (runtime context isolation)** when specific keywords (`"deterministic island"`, `"retrieve"`) are detected in the operator notes.
* **System Arbiter:** The central orchestrator that intercepts AI predictions, maps active deterministic constraints, and handles **Conflict Resolution** based on strict priority rings.

### 🛡️ Conflict Resolution Matrix

When multiple deterministic boundaries trigger simultaneously, the Arbiter handles the conflict through the following hierarchy:

| Strategy | Condition | Action |
| :--- | :--- | :--- |
| **1. Priority Ring** | Disagreement between islands of *different* priorities | The island with the higher priority ring (`CriticalSafety > Structural > Operational`) takes absolute control. |
| **2. Fail-Safe SCRAM** | Disagreement between islands of the *same* priority | The Arbiter declares a structural deadlock and triggers an immediate **Emergency SCRAM procedure** (Valve to 0%, system shutdown). |
| **3. Auditable Boundary** | Any override or SCRAM event | The decision and the islands involved are written to the console output. |

> **Note on the book.** The priority rings and the SCRAM-on-deadlock rule are an extension of this POC; they are not part of the book. The book (§12.4.3, p. 94) resolves conflicts with a fixed precedence between mechanisms (Static Vault → routed vector → Frozen Snapshot) and treats any disagreement between two deterministic sources as an error that must be logged for human review — never adjudicated silently by the Arbiter.

---

## 💻 Code Structure

* `SensorReading`: Strongly-typed input domain model capturing telemetry (Temperature, Pressure, Radiation leak status, and Operator text notes).
* `ControlCommand`: Immutable Value Object enforcing data boundaries (clamping the valve opening target between `0.0` and `1.0`).
* `DynamicIsland`: Runtime-isolated boundary carrying a strict `IslandPriority` ring and an enforced mathematical output.
* `CompleteSystemArbiter`: The central evaluation engine enforcing the structural boundary rules.

---

## 🚀 Running the POC

### Prerequisites
* [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or newer installed on your machine (the project targets `net8.0`, as in Appendix A.7 of the book).

### Execution Steps
1. Clone this repository or copy the source code into a local directory.
2. Open your terminal and navigate to the project directory:
   ```bash
   cd DeterministicIsland
   ```
3. Run the console application:
   ```bash
   dotnet run
   ```

---

## 🔬 Test Suite Scenarios

The codebase includes an automated suite of four real-world telemetry scenarios:

1. **Scenario 1: Normal Operation (No Keywords)**
   * *Conditions:* High temperature and pressure, but no context-isolation keywords used.
   * *Result:* The AI Core runs freely without restrictions; its command is approved by the Arbiter.
2. **Scenario 2: Keyword Without a Triggered Island**
   * *Conditions:* High temperature, normal pressure, notes include `"retrieve"`.
   * *Result:* No island condition is met (there is no temperature island yet), so the AI command is executed.
3. **Scenario 3: Single Critical Island Override**
   * *Conditions:* High temperature AND high pressure, notes include `"deterministic island"`.
   * *Result:* Only the `CriticalSafety` pressure island is triggered; the Arbiter overrides the AI and opens the relief valve to 100%.
4. **Scenario 4: The Ultimate Deadlock (SCRAM)**
   * *Conditions:* High pressure (demands 100% opening) AND a Radiation Leak occurs (demands 0% closing), notes include `"retrieve"`.
   * *Result:* Both loops share an identical `CriticalSafety` priority but carry contradicting demands. The Arbiter fires an immediate **FAIL-SAFE EMERGENCY SCRAM** to halt the process safely.

---

## 📖 Bibliography & References
* Agathangelidis, G., *From Stochastic Chaos to Deterministic Certainty: AI for Critical Industrial Infrastructure*, NEXUS-1 Engineering Series, September 2026 — the book this POC is based on ([`From_Stochastic_Chaos_to_Deterministic_Certainty.pdf`](From_Stochastic_Chaos_to_Deterministic_Certainty.pdf)). Relevant chapters: 12 (Deterministic Islands), 17.4 (Safe RL: Shielding), 22 (Bounding the AI Context), 24 (Deterministic Islands — Deep Dive).
* Agathangelidis, G., *From Core to Quantum: Quantum Mechanics and the Nucleus, for the Engineer Who Will Model Them*, NEXUS-1 Series, First Edition, September 2026 — the preceding volume of the series.
* Agathangelidis, G., *From Stochastic Chaos to Deterministic Certainty: AI for Critical Industrial Infrastructure*, NEXUS-1 Series, 2026.

---
*Disclaimer: This codebase is a theoretical architectural companion to the NEXUS-1 project. It is intended strictly for educational and modeling demonstrations. It should not be used to operate or make automated safety decisions in real nuclear or critical industrial facilities.*
