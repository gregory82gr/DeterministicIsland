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

    // Human-in-the-Loop escalation: the system reports that it cannot answer
    // deterministically instead of quietly relaxing the guarantee it was asked for.
    public sealed class DeterminismViolationException : Exception
    {
        public DeterminismViolationException(string message) : base(message) { }
    }
}
