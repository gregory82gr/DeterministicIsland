using DeterministicIsland.domain;
using System.Text.Json;

namespace DeterministicIsland.Islands
{
    // One registered version of a vault fact, exactly as it was appended: the vector with
    // its Determinism block (ValidTo still open) and, for a derived island, how it is derived.
    public sealed record IslandRecord(
        string Query,
        ILMVector Vector,
        string? DerivationRule,
        IReadOnlyList<string>? DerivationInputs);

    // §23.2: the vault's storage behind a narrow interface, so persistence can change
    // (memory, file, database) without the vault or any domain service noticing.
    // The store is append-only, like the vault's own history (§12.4.4).
    public interface IIslandRepository
    {
        void Save(IslandRecord record);

        // Every record ever saved, in the order it was saved.
        IEnumerable<IslandRecord> GetAll();
    }

    public sealed class InMemoryIslandRepository : IIslandRepository
    {
        private readonly List<IslandRecord> _records = new();
        public void Save(IslandRecord record) => _records.Add(record);
        public IEnumerable<IslandRecord> GetAll() => _records.ToList();
    }

    // Infrastructure (§23.3.2): one JSON object per line, appended and never rewritten.
    public sealed class JsonLinesIslandRepository : IIslandRepository
    {
        public string Path { get; }

        public JsonLinesIslandRepository(string path) => Path = path;

        public void Save(IslandRecord record) =>
            File.AppendAllText(Path, JsonSerializer.Serialize(record) + Environment.NewLine);

        public IEnumerable<IslandRecord> GetAll()
        {
            if (!File.Exists(Path))
                yield break;

            int lineNumber = 0;
            foreach (var line in File.ReadLines(Path))
            {
                lineNumber++;
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                IslandRecord? record;
                try
                {
                    record = JsonSerializer.Deserialize<IslandRecord>(line);
                }
                catch (JsonException ex)
                {
                    throw new InvalidDataException($"{Path}, line {lineNumber}: not a valid island record.", ex);
                }

                yield return record ?? throw new InvalidDataException($"{Path}, line {lineNumber}: empty island record.");
            }
        }
    }
}
