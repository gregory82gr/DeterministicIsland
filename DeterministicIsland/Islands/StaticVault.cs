using DeterministicIsland.domain;
using DeterministicIsland.Events;
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
    public sealed class StaticVault : IVectorStore
    {
        // §24.4.1: how a derived island is computed from other islands. The rule is kept by
        // name (DerivationRules) so that it can be persisted and reloaded.
        private sealed record Derivation(string Query, IReadOnlyList<string> InputQueries, string Name)
        {
            public double Compute(IReadOnlyList<double> values) => DerivationRules.Get(Name)(values);
        }

        private readonly Dictionary<string, List<ILMVector>> _history = new();
        private readonly Dictionary<string, string> _queries = new();
        private readonly Dictionary<Guid, ILMVector> _byId = new();
        private readonly Dictionary<string, Derivation> _derivations = new();
        private readonly DomainEventBus _events;
        private readonly IIslandRepository _repository;

        // §23.2: with a repository, the vault first replays every saved version, then saves
        // each new version before applying it in memory.
        public StaticVault(DomainEventBus? events = null, IIslandRepository? repository = null)
        {
            _events = events ?? new DomainEventBus();
            _repository = repository ?? new InMemoryIslandRepository();

            int recordNumber = 0;
            foreach (var record in _repository.GetAll())
            {
                recordNumber++;
                try
                {
                    Restore(record);
                }
                catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
                {
                    throw new InvalidDataException($"Island record {recordNumber} ('{record.Query}') cannot be restored: {ex.Message}", ex);
                }
            }
        }

        public IReadOnlyCollection<string> Queries => _queries.Values;

        public int VersionCount => _byId.Count;

        // Normalisation must be fixed and documented (§12.3.1): lower-cased, whitespace-trimmed.
        public static string Normalize(string query) => query.Trim().ToLowerInvariant();

        public static string HashQuery(string normalizedQuery)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedQuery));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        private static string Key(string query) => HashQuery(Normalize(query));

        public ILMVector Register(string query, double value, DateTime validFrom, string approvedBy)
        {
            var vector = Append(query, validFrom, approvedBy, new[] { value }, isDeterministic: true, pointerTo: null,
                derivedFrom: null, derivation: null);
            RecomputeDependents(query, validFrom, approvedBy);
            return vector;
        }

        // §12.3.3: registers a query whose answer defers to another, already registered vector.
        // The pointer targets one specific version; when that version is superseded the
        // pointer becomes stale and resolution fails until a human re-registers it.
        public ILMVector RegisterPointer(string query, Guid targetVectorId, DateTime validFrom, string approvedBy)
        {
            if (!_byId.ContainsKey(targetVectorId))
                throw new InvalidOperationException($"Cannot register a dangling pointer: vector {targetVectorId} does not exist.");

            var vector = Append(query, validFrom, approvedBy, Array.Empty<double>(), isDeterministic: false,
                pointerTo: targetVectorId, derivedFrom: null, derivation: null);
            RecomputeDependents(query, validFrom, approvedBy);
            return vector;
        }

        // §24.4.1: a derived island is a pure function of other islands' answers, so it is
        // itself deterministic. It records the exact input vectors it was computed from and
        // is recomputed automatically whenever one of its input queries gets a new version.
        public ILMVector RegisterDerived(string query, IReadOnlyList<string> inputQueries, string derivationRule,
            DateTime validFrom, string approvedBy)
        {
            DerivationRules.Get(derivationRule);
            if (inputQueries.Count == 0)
                throw new ArgumentException("A derived island needs at least one input.", nameof(inputQueries));
            if (inputQueries.Any(i => Key(i) == Key(query)))
                throw new ArgumentException("A derived island cannot be one of its own inputs.", nameof(inputQueries));

            var derivation = new Derivation(query, inputQueries.ToList(), derivationRule);
            var vector = AppendDerived(derivation, validFrom, approvedBy);
            RecomputeDependents(query, validFrom, approvedBy);
            return vector;
        }

        public ILMVector? Get(Guid vectorId) => _byId.GetValueOrDefault(vectorId);

        public ILMVector? Lookup(string query, DateTime asOf) =>
            _history.TryGetValue(Key(query), out var versions)
                ? versions.LastOrDefault(v => v.Determinism!.IsValidAt(asOf))
                : null;

        // Resolves a query to a deterministic vector that is valid at asOf, following pointers
        // under an engaged Causality Lock. Anything else fails loudly.
        public ILMVector Resolve(string query, DateTime asOf)
        {
            var entry = Lookup(query, asOf)
                ?? throw new InvalidOperationException($"'{query}' is not registered in the Static Vault at {asOf:O}.");

            var causalityLock = new CausalityLock();
            causalityLock.Engage();
            return GuardedResolver.ResolveUnderLock(entry, this, causalityLock, asOf);
        }

        public IReadOnlyList<ILMVector> History(string query) =>
            _history.TryGetValue(Key(query), out var versions)
                ? versions.AsReadOnly()
                : Array.Empty<ILMVector>();

        private ILMVector AppendDerived(Derivation derivation, DateTime validFrom, string approvedBy)
        {
            var inputs = derivation.InputQueries.Select(q => Resolve(q, validFrom)).ToList();
            double value = derivation.Compute(inputs.Select(i => i.Value).ToList());

            return Append(derivation.Query, validFrom, approvedBy, new[] { value }, isDeterministic: true,
                pointerTo: null, derivedFrom: inputs.Select(i => i.VectorId).ToList(), derivation: derivation);
        }

        // The update discipline of §12.4.4, applied automatically to derived islands (§24.4.1).
        private void RecomputeDependents(string changedQuery, DateTime validFrom, string approvedBy)
        {
            var dependents = _derivations.Values
                .Where(d => d.InputQueries.Any(i => Key(i) == Key(changedQuery)))
                .ToList();

            foreach (var derivation in dependents)
            {
                AppendDerived(derivation, validFrom, $"{approvedBy} (auto-recomputed after '{changedQuery}' changed)");
                RecomputeDependents(derivation.Query, validFrom, approvedBy);
            }
        }

        private ILMVector Append(string query, DateTime validFrom, string approvedBy, double[] embedding,
            bool isDeterministic, Guid? pointerTo, IReadOnlyList<Guid>? derivedFrom, Derivation? derivation)
        {
            if (string.IsNullOrWhiteSpace(approvedBy))
                throw new ArgumentException("Every island update requires human sign-off (§12.4.4).", nameof(approvedBy));

            var key = Key(query);
            var versions = _history.GetValueOrDefault(key) ?? new List<ILMVector>();
            if (versions.Count > 0 && validFrom <= versions[^1].Determinism!.ValidFrom)
                throw new InvalidOperationException(
                    $"A new version of '{query}' must start after {versions[^1].Determinism!.ValidFrom:O}; history is append-only.");

            // Reject an update that a dependent derived island could not follow in time;
            // it would leave that island stale for its whole validity (§24.4.1).
            foreach (var dependent in _derivations.Values.Where(d => d.InputQueries.Any(i => Key(i) == key)))
            {
                var dependentCurrent = Lookup(dependent.Query, DateTime.MaxValue);
                if (dependentCurrent is not null && validFrom <= dependentCurrent.Determinism!.ValidFrom)
                    throw new InvalidOperationException(
                        $"Updating '{query}' from {validFrom:O} would retroactively invalidate derived island '{dependent.Query}'.");
            }

            var vector = new ILMVector
            {
                VectorId = Guid.NewGuid(),
                Embedding = embedding,
                Determinism = new DeterminismMetadata
                {
                    IsDeterministic = isDeterministic,
                    Version = $"v{versions.Count + 1}",
                    ValidFrom = validFrom,
                    ApprovedBy = approvedBy,
                    PointerTo = pointerTo,
                    DerivedFrom = derivedFrom,
                    DerivedBy = derivation?.Name
                }
            };

            // Persist first: if saving fails, the in-memory vault is left unchanged.
            _repository.Save(new IslandRecord(query, vector, derivation?.Name, derivation?.InputQueries));
            Apply(query, vector, derivation);

            _events.Publish(new IslandAdded(key, query, vector, derivation?.Name, derivation?.InputQueries, DateTime.UtcNow));
            return vector;
        }

        private void Apply(string query, ILMVector vector, Derivation? derivation)
        {
            var key = Key(query);
            if (!_history.TryGetValue(key, out var versions))
            {
                versions = new List<ILMVector>();
                _history[key] = versions;
            }

            if (versions.Count > 0)
            {
                // Close the previous version so that the validity intervals partition time (§24.4.1).
                var current = versions[^1];
                var closed = current with { Determinism = current.Determinism! with { ValidTo = vector.Determinism!.ValidFrom } };
                versions[^1] = closed;
                _byId[closed.VectorId] = closed;
            }

            versions.Add(vector);
            _byId[vector.VectorId] = vector;
            _queries[key] = query;

            // A value or pointer registered over a derived island replaces its derivation rule.
            if (derivation is null)
                _derivations.Remove(key);
            else
                _derivations[key] = derivation;
        }

        // Replays one saved version exactly (same VectorId, so pointers and DerivedFrom still
        // resolve), after checking it is consistent with what has been replayed so far.
        // No event is raised and nothing is recomputed: the file already holds every version.
        private void Restore(IslandRecord record)
        {
            var vector = record.Vector;
            var determinism = vector.Determinism
                ?? throw new InvalidOperationException("the vector has no Determinism block.");

            if (string.IsNullOrWhiteSpace(determinism.ApprovedBy))
                throw new InvalidOperationException("the version has no approver.");
            if (determinism.ValidTo is not null)
                throw new InvalidOperationException("a saved version must still be open; ValidTo is set only by its successor.");
            if (_byId.ContainsKey(vector.VectorId))
                throw new InvalidOperationException($"vector {vector.VectorId} appears twice.");

            var versions = _history.GetValueOrDefault(Key(record.Query)) ?? new List<ILMVector>();
            if (determinism.Version != $"v{versions.Count + 1}")
                throw new InvalidOperationException($"expected version v{versions.Count + 1}, found {determinism.Version}.");
            if (versions.Count > 0 && determinism.ValidFrom <= versions[^1].Determinism!.ValidFrom)
                throw new InvalidOperationException("versions are not in chronological order.");

            if (determinism.PointerTo is Guid target && !_byId.ContainsKey(target))
                throw new InvalidOperationException($"it points to vector {target}, which was not saved before it.");
            foreach (var input in determinism.DerivedFrom ?? Array.Empty<Guid>())
            {
                if (!_byId.ContainsKey(input))
                    throw new InvalidOperationException($"it is derived from vector {input}, which was not saved before it.");
            }

            Derivation? derivation = null;
            if (record.DerivationRule is { } rule)
            {
                DerivationRules.Get(rule);
                if (determinism.DerivedBy != rule || record.DerivationInputs is not { Count: > 0 } inputs)
                    throw new InvalidOperationException("the derivation rule and the vector's DerivedBy do not match.");
                derivation = new Derivation(record.Query, inputs.ToList(), rule);
            }

            Apply(record.Query, vector, derivation);
        }
    }
}
