namespace DeterministicIsland.Governance
{
    public enum RuleSource { Book, PocExtension }

    // One rule the system must never violate, the mechanism that enforces it, and the
    // tests (as "Class.Method" in DeterministicIsland.Tests) that demonstrate it.
    public sealed record ConstitutionalRule(
        string Name,
        string EnforcedBy,
        RuleSource Source,
        IReadOnlyList<string> VerifiedBy);

    // =======================================================================
    // NEURAL CONSTITUTION (§26.4): not new enforcement, but one legible list of every
    // guarantee the system makes, each traceable to the chapter that built it and to the
    // tests that prove it. Like any other fact, it changes only through reviewed,
    // human-approved updates (§26.5).
    // =======================================================================
    public static class NeuralConstitution
    {
        public static readonly IReadOnlyList<ConstitutionalRule> Rules = new[]
        {
            new ConstitutionalRule(
                "A resolved deterministic fact is never overridden by a generated one.",
                "Arbiter's fixed precedence order (§12.4.3): triggered islands always take control from the AI core, and Answer consults the Static Vault before any candidate.",
                RuleSource.Book,
                new[]
                {
                    "CompleteSystemArbiterTests.HighTemperature_StructuralIslandTakesControl",
                    "IntraVectorRoutingTests.Answer_PrefersTheStaticVaultOverACandidate"
                }),

            new ConstitutionalRule(
                "No control action reaches an actuator outside its certified safe range.",
                "Shield (§17.4): every AI proposal is bounded by the vault's maximum safe valve opening.",
                RuleSource.Book,
                new[]
                {
                    "CompleteSystemArbiterTests.Shield_CapsAProposalAboveTheVaultLimit",
                    "CompleteSystemArbiterTests.NormalOperation_ConfidentAiCommandIsApprovedWithinTheShieldLimit"
                }),

            new ConstitutionalRule(
                "A Causality Lock, once engaged, never silently falls back to a stochastic answer.",
                "GuardedResolver and the Causality Lock (§12.3.3): no deterministic answer means DeterminismViolationException.",
                RuleSource.Book,
                new[]
                {
                    "CompleteSystemArbiterTests.CausalityLock_WithoutDeterministicAnswer_EscalatesToHuman",
                    "IntraVectorRoutingTests.GuardedResolver_RejectsANonDeterministicAnswerOnlyUnderLock",
                    "IntraVectorRoutingTests.Answer_WithoutAnyDeterministicPath_FallsThroughOrEscalates"
                }),

            new ConstitutionalRule(
                "Every registered fact's provenance and approval are permanently recorded.",
                "Append-only update discipline (§12.4.4): versions are closed, never edited, and each names its approver; every version is saved to an append-only, hash-chained repository before it takes effect (§23.2).",
                RuleSource.Book,
                new[]
                {
                    "StaticVaultTests.Register_UpdateIsAppendOnlyAndKeepsHistoryQueryable",
                    "StaticVaultTests.Register_RequiresHumanSignOff",
                    "DerivedIslandTests.InputUpdate_RecomputesTheDerivedIslandAutomatically",
                    "IslandRepositoryTests.ReopenedVault_HasTheSameVersionsPointersAndDerivedIslands",
                    "IslandRepositoryTests.FailedSave_LeavesTheVaultUnchanged",
                    "IslandRepositoryTests.EditedValueInTheFile_BreaksTheHashChain"
                }),

            new ConstitutionalRule(
                "No fully autonomous action is taken without a human retaining override authority.",
                "OperatorOverride: a named operator can always replace the AI's command and answer any escalation, but never a triggered safety island or SCRAM, which keep final authority (§17.6). Plus Human-in-the-Loop escalation, RequiresHumanReview flags (§12.3.3, §12.4.3) and human sign-off on every vault update.",
                RuleSource.Book,
                new[]
                {
                    "OperatorOverrideTests.Override_ReplacesTheAiCommandAndIsAudited",
                    "OperatorOverrideTests.Override_AnswersAnUncertaintyEscalation",
                    "OperatorOverrideTests.Override_NeverOverridesATriggeredSafetyIsland",
                    "CompleteSystemArbiterTests.UncertainAiCommand_IsNotAppliedAndEscalatesToHuman",
                    "StaticVaultTests.Register_RequiresHumanSignOff"
                }),

            new ConstitutionalRule(
                "An AI command is never applied when its uncertainty exceeds the certified bound.",
                "MC Dropout (§13.6) checked against a Static Vault threshold (§16.9).",
                RuleSource.PocExtension,
                new[]
                {
                    "CompleteSystemArbiterTests.UncertainAiCommand_IsNotAppliedAndEscalatesToHuman",
                    "CompleteSystemArbiterTests.UncertaintyLimit_IsReadFromTheVault"
                }),

            new ConstitutionalRule(
                "A routed or derived fact is never used once the fact it rests on has changed.",
                "Stale-pointer and stale-derived-island checks in GuardedResolver (§12.4.4, §24.4.1).",
                RuleSource.PocExtension,
                new[]
                {
                    "IntraVectorRoutingTests.Pointer_ToASupersededVersion_IsStaleAndFailsLoudly",
                    "DerivedIslandTests.DerivedIsland_OverAPointerWhoseTargetChanged_IsStaleAndFailsLoudly"
                }),

            new ConstitutionalRule(
                "Conflicting safety demands at the same priority end in a fail-safe SCRAM, never a guess.",
                "Priority rings and SCRAM-on-deadlock in CompleteSystemArbiter.",
                RuleSource.PocExtension,
                new[]
                {
                    "CompleteSystemArbiterTests.SamePriorityConflict_ScramsAndFlagsForHumanReview",
                    "CompleteSystemArbiterTests.CriticalSafety_BeatsStructural"
                })
        };
    }
}
