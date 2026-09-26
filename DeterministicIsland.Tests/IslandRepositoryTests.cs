using DeterministicIsland.domain;
using DeterministicIsland.Events;
using DeterministicIsland.Islands;
using System.Text.Json;

namespace DeterministicIsland.Tests;

public sealed class IslandRepositoryTests : IDisposable
{
    private static readonly DateTime T1 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime T2 = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"nexus1-vault-{Guid.NewGuid():N}.jsonl");

    public void Dispose() => File.Delete(_path);

    private StaticVault OpenVault(DomainEventBus? events = null) => new(events, new JsonLinesIslandRepository(_path));

    private sealed class FailingRepository : IIslandRepository
    {
        public void Save(IslandRecord record) => throw new IOException("disk full");
        public IEnumerable<IslandRecord> GetAll() => Array.Empty<IslandRecord>();
    }

    [Fact]
    public void MissingFile_OpensAnEmptyVault()
    {
        var vault = OpenVault();

        Assert.Equal(0, vault.VersionCount);
    }

    [Fact]
    public void ReopenedVault_HasTheSameVersionsPointersAndDerivedIslands()
    {
        var original = OpenVault();
        SafetyLimits.SeedDefaults(original, T1, "Engineer");
        var setpoint = original.Lookup(SafetyLimits.PressureReliefSetpointBar, T1)!;
        original.RegisterPointer("backup line setpoint", setpoint.VectorId, T1.AddDays(1), "Engineer");
        original.Register(SafetyLimits.PressureTransmitterLimitsBar[1], 7.6, T2, "Instrumentation Engineer");

        var reopened = OpenVault();

        Assert.Equal(original.VersionCount, reopened.VersionCount);
        Assert.Equal(original.Queries.OrderBy(q => q), reopened.Queries.OrderBy(q => q));
        foreach (var query in original.Queries)
            Assert.Equal(original.History(query), reopened.History(query), new VectorComparer());

        Assert.Equal(8.0, reopened.Resolve(SafetyLimits.PressureReliefSetpointBar, T2.AddDays(-1)).Value);
        Assert.Equal(7.6, reopened.Resolve(SafetyLimits.PressureReliefSetpointBar, T2).Value);
        Assert.Equal(setpoint.VectorId, reopened.Resolve("backup line setpoint", T2.AddDays(-1)).VectorId);
    }

    [Fact]
    public void ReopenedVault_KeepsRecomputingDerivedIslands()
    {
        SafetyLimits.SeedDefaults(OpenVault(), T1, "Engineer");

        var reopened = OpenVault();
        reopened.Register(SafetyLimits.PressureTransmitterLimitsBar[0], 7.0, T2, "Engineer");

        Assert.Equal(7.0, reopened.Resolve(SafetyLimits.PressureReliefSetpointBar, T2).Value);
        Assert.Equal(7.0, OpenVault().Resolve(SafetyLimits.PressureReliefSetpointBar, T2).Value);
    }

    [Fact]
    public void Reload_RaisesNoIslandAddedEvents()
    {
        SafetyLimits.SeedDefaults(OpenVault(), T1, "Engineer");
        var recorder = new InMemoryEventRecorder();

        OpenVault(new DomainEventBus().Subscribe<IslandAdded>(recorder));

        Assert.Empty(recorder.Events);
    }

    [Fact]
    public void Values_RoundTripBitForBit()
    {
        double value = 0.1 + 0.2;
        OpenVault().Register("odd value", value, T1, "Engineer");

        Assert.Equal(BitConverter.DoubleToInt64Bits(value),
            BitConverter.DoubleToInt64Bits(OpenVault().Resolve("odd value", T2).Value));
    }

    [Fact]
    public void FailedSave_LeavesTheVaultUnchanged()
    {
        var vault = new StaticVault(repository: new FailingRepository());

        Assert.Throws<IOException>(() => vault.Register("limit", 1.0, T1, "Engineer"));
        Assert.Null(vault.Lookup("limit", T2));
    }

    [Fact]
    public void CorruptLine_IsReportedWithItsLineNumber()
    {
        OpenVault().Register("limit", 1.0, T1, "Engineer");
        File.AppendAllText(_path, "{ not json" + Environment.NewLine);

        var ex = Assert.Throws<InvalidDataException>(() => OpenVault());
        Assert.Contains("line 2", ex.Message);
    }

    [Fact]
    public void RecordWithoutApprover_IsRejected() =>
        AssertTamperedFileIsRejected(r => r with { Vector = r.Vector with { Determinism = r.Vector.Determinism! with { ApprovedBy = "" } } });

    [Fact]
    public void RecordWithASkippedVersion_IsRejected() =>
        AssertTamperedFileIsRejected(r => r with { Vector = r.Vector with { Determinism = r.Vector.Determinism! with { Version = "v7" } } });

    [Fact]
    public void RecordWithADanglingPointer_IsRejected() =>
        AssertTamperedFileIsRejected(r => r with { Vector = r.Vector with { Determinism = r.Vector.Determinism! with { PointerTo = Guid.NewGuid() } } });

    [Fact]
    public void RecordWithAnUnknownDerivationRule_IsRejected() =>
        AssertTamperedFileIsRejected(r => r with { DerivationRule = "average", DerivationInputs = new[] { "x" } });

    // Writes an inconsistent record with a *valid* hash chain, so that the vault's own
    // consistency checks, not the chain, are what must reject it.
    private void AssertTamperedFileIsRejected(Func<IslandRecord, IslandRecord> tamper)
    {
        OpenVault().Register("limit", 1.0, T1, "Engineer");
        var record = new JsonLinesIslandRepository(_path).GetAll().Single();
        File.Delete(_path);
        new JsonLinesIslandRepository(_path).Save(tamper(record));

        var ex = Assert.Throws<InvalidDataException>(() => OpenVault());
        Assert.Contains("cannot be restored", ex.Message);
    }

    [Fact]
    public void EditedValueInTheFile_BreaksTheHashChain()
    {
        OpenVault().Register("limit", 8.0, T1, "Engineer");
        File.WriteAllText(_path, File.ReadAllText(_path).Replace("[8]", "[9]"));

        var ex = Assert.Throws<InvalidDataException>(() => OpenVault());
        Assert.Contains("line 1: hash mismatch", ex.Message);
    }

    [Fact]
    public void RemovedLine_BreaksTheHashChain()
    {
        var vault = OpenVault();
        vault.Register("a", 1.0, T1, "Engineer");
        vault.Register("b", 2.0, T1, "Engineer");
        vault.Register("c", 3.0, T1, "Engineer");
        var lines = File.ReadAllLines(_path);
        File.WriteAllLines(_path, new[] { lines[0], lines[2] });

        var ex = Assert.Throws<InvalidDataException>(() => OpenVault());
        Assert.Contains("line 2: hash chain broken", ex.Message);
    }

    [Fact]
    public void ReorderedLines_BreakTheHashChain()
    {
        var vault = OpenVault();
        vault.Register("a", 1.0, T1, "Engineer");
        vault.Register("b", 2.0, T1, "Engineer");
        var lines = File.ReadAllLines(_path);
        File.WriteAllLines(_path, new[] { lines[1], lines[0] });

        var ex = Assert.Throws<InvalidDataException>(() => OpenVault());
        Assert.Contains("line 1: hash chain broken", ex.Message);
    }

    [Fact]
    public void ChainContinuesAcrossReopenedRepositories()
    {
        OpenVault().Register("a", 1.0, T1, "Engineer");
        OpenVault().Register("b", 2.0, T1, "Engineer");

        var lines = File.ReadAllLines(_path).Select(l => JsonDocument.Parse(l).RootElement).ToList();
        Assert.Equal(JsonLinesIslandRepository.GenesisHash, lines[0].GetProperty("PreviousHash").GetString());
        Assert.Equal(lines[0].GetProperty("Hash").GetString(), lines[1].GetProperty("PreviousHash").GetString());
        Assert.Equal(2, OpenVault().VersionCount);
    }

    private sealed class VectorComparer : IEqualityComparer<ILMVector>
    {
        public bool Equals(ILMVector? x, ILMVector? y) =>
            x is not null && y is not null &&
            x.VectorId == y.VectorId &&
            x.Embedding.SequenceEqual(y.Embedding) &&
            x.Determinism!.Version == y.Determinism!.Version &&
            x.Determinism.ValidFrom == y.Determinism.ValidFrom &&
            x.Determinism.ValidTo == y.Determinism.ValidTo &&
            x.Determinism.ApprovedBy == y.Determinism.ApprovedBy &&
            x.Determinism.PointerTo == y.Determinism.PointerTo &&
            x.Determinism.DerivedBy == y.Determinism.DerivedBy &&
            (x.Determinism.DerivedFrom ?? Array.Empty<Guid>()).SequenceEqual(y.Determinism.DerivedFrom ?? Array.Empty<Guid>());

        public int GetHashCode(ILMVector obj) => obj.VectorId.GetHashCode();
    }
}
