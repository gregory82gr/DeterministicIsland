using DeterministicIsland.Islands;

namespace DeterministicIsland.Tests;

public class StaticVaultTests
{
    private static readonly DateTime T1 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime T2 = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void HashQuery_MatchesTheBooksWorkedExample()
    {
        // §12.3.1, "Hashing a Real Query".
        var hash = StaticVault.HashQuery("what is the maximum allowed discharge pressure for pump p-101?");

        Assert.Equal("f20ab319ace115290fd196a1c1d5b574e60ed0b39692add217c4ff0bcf03e574", hash);
    }

    [Fact]
    public void Lookup_NormalisesCaseAndSurroundingWhitespace()
    {
        var vault = new StaticVault();
        vault.Register("Maximum Allowed Pressure", 120.0, T1, "Engineer");

        var hit = vault.Lookup("  maximum allowed pressure  ", T2);

        Assert.NotNull(hit);
        Assert.Equal(120.0, hit!.Value);
        Assert.True(hit.Determinism!.IsDeterministic);
    }

    [Fact]
    public void Lookup_ReturnsNullForAnUnregisteredQuery()
    {
        var vault = new StaticVault();

        Assert.Null(vault.Lookup("unknown", T1));
    }

    [Fact]
    public void Register_UpdateIsAppendOnlyAndKeepsHistoryQueryable()
    {
        var vault = new StaticVault();
        var v1 = vault.Register("scram setpoint", 320.0, T1, "Engineer A");
        var v2 = vault.Register("scram setpoint", 315.0, T2, "Engineer B");

        var history = vault.History("scram setpoint");
        Assert.Equal(2, history.Count);
        Assert.NotEqual(v1.VectorId, v2.VectorId);
        Assert.Equal("v1", history[0].Determinism!.Version);
        Assert.Equal(T2, history[0].Determinism!.ValidTo);
        Assert.Equal("v2", history[1].Determinism!.Version);
        Assert.Null(history[1].Determinism!.ValidTo);

        // Point-in-time answers (§24.6): last year's question gets last year's value.
        Assert.Equal(320.0, vault.Lookup("scram setpoint", T2.AddDays(-1))!.Value);
        Assert.Equal(315.0, vault.Lookup("scram setpoint", T2)!.Value);
        Assert.Null(vault.Lookup("scram setpoint", T1.AddDays(-1)));
    }

    [Fact]
    public void Register_RequiresHumanSignOff()
    {
        var vault = new StaticVault();

        Assert.Throws<ArgumentException>(() => vault.Register("limit", 1.0, T1, " "));
    }

    [Fact]
    public void Register_RejectsAVersionThatDoesNotStartAfterTheCurrentOne()
    {
        var vault = new StaticVault();
        vault.Register("limit", 1.0, T2, "Engineer");

        Assert.Throws<InvalidOperationException>(() => vault.Register("limit", 2.0, T1, "Engineer"));
    }
}
