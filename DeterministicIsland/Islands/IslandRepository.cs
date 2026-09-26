using DeterministicIsland.domain;
using System.Security.Cryptography;
using System.Text;
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
    // Each line is chained to the one before it:
    //     Hash = SHA-256(PreviousHash + Record)
    // so a record that is edited, removed or moved breaks the chain from that line on, and
    // loading fails with the line number instead of silently using a tampered safety limit.
    public sealed class JsonLinesIslandRepository : IIslandRepository
    {
        public static readonly string GenesisHash = new('0', 64);

        private string? _lastHash;

        public string Path { get; }

        public JsonLinesIslandRepository(string path) => Path = path;

        // The hash of the last line. The chain cannot notice lines cut off the end of the file
        // (or a file rewritten with a freshly computed chain), so this value is meant to be
        // recorded somewhere else, e.g. in the shift log, and compared on the next start.
        public string HeadHash => _lastHash ??= ReadVerified().LastHash;

        public void Save(IslandRecord record)
        {
            string previousHash = _lastHash ?? ReadVerified().LastHash;
            string recordJson = JsonSerializer.Serialize(record);
            string hash = ChainHash(previousHash, recordJson);

            // The record is embedded verbatim, so the bytes that were hashed are the bytes on disk.
            string line = $"{{\"PreviousHash\":\"{previousHash}\",\"Hash\":\"{hash}\",\"Record\":{recordJson}}}";
            File.AppendAllText(Path, line + Environment.NewLine);
            _lastHash = hash;
        }

        public IEnumerable<IslandRecord> GetAll()
        {
            var (records, lastHash) = ReadVerified();
            _lastHash = lastHash;
            return records;
        }

        public static string ChainHash(string previousHash, string recordJson) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(previousHash + recordJson))).ToLowerInvariant();

        private (List<IslandRecord> Records, string LastHash) ReadVerified()
        {
            var records = new List<IslandRecord>();
            string expectedPrevious = GenesisHash;
            if (!File.Exists(Path))
                return (records, expectedPrevious);

            int lineNumber = 0;
            foreach (var line in File.ReadLines(Path))
            {
                lineNumber++;
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                string previousHash, hash, recordJson;
                try
                {
                    using var document = JsonDocument.Parse(line);
                    var root = document.RootElement;
                    previousHash = root.GetProperty("PreviousHash").GetString() ?? "";
                    hash = root.GetProperty("Hash").GetString() ?? "";
                    recordJson = root.GetProperty("Record").GetRawText();
                }
                catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
                {
                    throw new InvalidDataException($"{Path}, line {lineNumber}: not a valid island record.", ex);
                }

                if (previousHash != expectedPrevious)
                    throw new InvalidDataException(
                        $"{Path}, line {lineNumber}: hash chain broken; a record before this line was removed, added or reordered.");
                if (hash != ChainHash(previousHash, recordJson))
                    throw new InvalidDataException(
                        $"{Path}, line {lineNumber}: hash mismatch; this record was altered after it was saved.");

                var record = JsonSerializer.Deserialize<IslandRecord>(recordJson)
                    ?? throw new InvalidDataException($"{Path}, line {lineNumber}: empty island record.");
                records.Add(record);
                expectedPrevious = hash;
            }

            return (records, expectedPrevious);
        }
    }
}
