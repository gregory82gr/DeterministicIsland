using DeterministicIsland.domain;
using System.Security.Cryptography;
using System.Text;

namespace DeterministicIsland.Islands
{
    // =======================================================================
    // STATIC VAULT (§12.3.1): answer = Vault[SHA-256(normalised query)]
    // The stochastic core is never consulted on this path.
    // Updates are append-only (§12.4.4): the old vector's ValidTo is closed and a new
    // vector with a new VectorId and an incremented Version is registered.
    // =======================================================================
    public sealed class StaticVault
    {
        private readonly Dictionary<string, List<ILMVector>> _history = new();

        // Normalisation must be fixed and documented (§12.3.1): lower-cased, whitespace-trimmed.
        public static string Normalize(string query) => query.Trim().ToLowerInvariant();

        public static string HashQuery(string normalizedQuery)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedQuery));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        public ILMVector Register(string query, double value, DateTime validFrom, string approvedBy)
        {
            if (string.IsNullOrWhiteSpace(approvedBy))
                throw new ArgumentException("Every island update requires human sign-off (§12.4.4).", nameof(approvedBy));

            var key = HashQuery(Normalize(query));
            if (!_history.TryGetValue(key, out var versions))
            {
                versions = new List<ILMVector>();
                _history[key] = versions;
            }

            if (versions.Count > 0)
            {
                var current = versions[^1];
                if (validFrom <= current.Determinism!.ValidFrom)
                    throw new InvalidOperationException(
                        $"A new version must start after {current.Determinism.ValidFrom:O}; history is append-only.");

                // Close the previous version so that the validity intervals partition time (§24.4.1).
                versions[^1] = current with { Determinism = current.Determinism with { ValidTo = validFrom } };
            }

            var vector = new ILMVector
            {
                VectorId = Guid.NewGuid(),
                Embedding = new[] { value },
                Determinism = new DeterminismMetadata
                {
                    IsDeterministic = true,
                    Version = $"v{versions.Count + 1}",
                    ValidFrom = validFrom,
                    ApprovedBy = approvedBy
                }
            };
            versions.Add(vector);
            return vector;
        }

        public ILMVector? Lookup(string query, DateTime asOf)
        {
            var key = HashQuery(Normalize(query));
            return _history.TryGetValue(key, out var versions)
                ? versions.LastOrDefault(v => v.Determinism!.IsValidAt(asOf))
                : null;
        }

        public IReadOnlyList<ILMVector> History(string query) =>
            _history.TryGetValue(HashQuery(Normalize(query)), out var versions)
                ? versions.AsReadOnly()
                : Array.Empty<ILMVector>();
    }
}
