namespace DeterministicIsland.Islands
{
    // §12.3.3: engaged whenever the operator explicitly requests a deterministic island.
    // Once engaged, the answer must come from a deterministic mechanism; there is no
    // silent fallback to the stochastic core.
    public sealed class CausalityLock
    {
        public const string RequestPhrase = "deterministic island";

        public bool IsEngaged { get; private set; }
        public void Engage() => IsEngaged = true;
        public void Release() => IsEngaged = false;

        public static bool IsRequestedBy(string operatorNotes) =>
            operatorNotes.Contains(RequestPhrase, StringComparison.OrdinalIgnoreCase);
    }

    // Human-in-the-Loop escalation: the system does not act on its own answer and hands
    // the decision to an operator. The AI command of that cycle is never applied.
    public abstract class HumanEscalationException : Exception
    {
        protected HumanEscalationException(string message) : base(message) { }
    }

    // The system reports that it cannot answer deterministically instead of quietly
    // relaxing the guarantee it was asked for (§12.3.3).
    public sealed class DeterminismViolationException : HumanEscalationException
    {
        public DeterminismViolationException(string message) : base(message) { }
    }

    // The AI core's MC Dropout interval is wider than the vault permits (§13.6).
    public sealed class UncertaintyEscalationException : HumanEscalationException
    {
        public UncertaintyEscalationException(string message) : base(message) { }
    }
}
