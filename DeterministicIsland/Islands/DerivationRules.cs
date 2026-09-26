using System.Collections.Concurrent;

namespace DeterministicIsland.Islands
{
    // The pure functions derived islands may use, by name (§24.4.1). A derived island is
    // persisted with the rule's name, so reloading it needs the same rule registered here;
    // an unknown name fails loudly instead of being guessed.
    public static class DerivationRules
    {
        private static readonly ConcurrentDictionary<string, Func<IReadOnlyList<double>, double>> Rules = new(
            new Dictionary<string, Func<IReadOnlyList<double>, double>>
            {
                ["minimum"] = values => values.Min(),
                ["maximum"] = values => values.Max()
            });

        public static void Register(string name, Func<IReadOnlyList<double>, double> compute)
        {
            if (!Rules.TryAdd(name, compute) && Rules[name] != compute)
                throw new InvalidOperationException($"Derivation rule '{name}' is already registered with a different function.");
        }

        public static Func<IReadOnlyList<double>, double> Get(string name) =>
            Rules.TryGetValue(name, out var compute)
                ? compute
                : throw new InvalidOperationException($"Unknown derivation rule '{name}'.");
    }
}
