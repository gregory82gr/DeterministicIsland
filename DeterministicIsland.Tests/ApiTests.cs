using DeterministicIsland.Islands;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace DeterministicIsland.Tests;

public sealed class ApiTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"nexus1-api-{Guid.NewGuid():N}");
    private readonly List<WebApplicationFactory<Program>> _factories = new();

    public ApiTests() => Directory.CreateDirectory(_directory);

    public void Dispose()
    {
        foreach (var factory in _factories)
            factory.Dispose();
        Directory.Delete(_directory, recursive: true);
    }

    // Each factory is a fresh API process that shares the same vault, audit and event files.
    private HttpClient Client()
    {
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Nexus:VaultPath", Path.Combine(_directory, "vault.jsonl"));
            builder.UseSetting("Nexus:AuditPath", Path.Combine(_directory, "audit.jsonl"));
            builder.UseSetting("Nexus:EventsPath", Path.Combine(_directory, "events.jsonl"));
        });
        _factories.Add(factory);
        return factory.CreateClient();
    }

    private static object Reading(double temperature, double pressure, string notes = "", bool leak = false, object? operatorOverride = null) => new
    {
        temperatureCelsius = temperature,
        pressureBar = pressure,
        operatorNotes = notes,
        radiationLeakDetected = leak,
        operatorOverride
    };

    private static async Task<JsonElement> Json(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    [Fact]
    public async Task Control_IslandTakesControl()
    {
        var response = await Client().PostAsJsonAsync("/control", Reading(95.0, 4.0));

        response.EnsureSuccessStatusCode();
        var body = await Json(response);
        Assert.True(body.GetProperty("applied").GetBoolean());
        Assert.Equal(0.6, body.GetProperty("valveOpening").GetDouble());
        Assert.Equal("IslandOverride", body.GetProperty("decision").GetString());
    }

    [Fact]
    public async Task Control_EscalationIsReportedAsNotApplied()
    {
        var response = await Client().PostAsJsonAsync("/control", Reading(30.0, 5.0, "deterministic island"));

        response.EnsureSuccessStatusCode();
        var body = await Json(response);
        Assert.False(body.GetProperty("applied").GetBoolean());
        Assert.Equal("HumanEscalation", body.GetProperty("decision").GetString());
        Assert.True(body.GetProperty("requiresHumanReview").GetBoolean());
    }

    [Fact]
    public async Task Control_OperatorOverrideIsApplied()
    {
        var response = await Client().PostAsJsonAsync("/control",
            Reading(30.0, 5.0, operatorOverride: new { operatorId = "op-1", valveOpening = 0.4, reason = "test" }));

        var body = await Json(response);
        Assert.Equal("OperatorOverride", body.GetProperty("decision").GetString());
        Assert.Equal(0.4, body.GetProperty("valveOpening").GetDouble());
    }

    [Fact]
    public async Task Control_InvalidOverrideIsABadRequest()
    {
        var response = await Client().PostAsJsonAsync("/control",
            Reading(30.0, 5.0, operatorOverride: new { operatorId = "", valveOpening = 0.4, reason = "test" }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Answer_ReturnsTheDerivedSetpointDeterministically()
    {
        var response = await Client().PostAsJsonAsync("/answer",
            new { query = SafetyLimits.PressureReliefSetpointBar, requireDeterminism = true });

        var body = await Json(response);
        Assert.True(body.GetProperty("isDeterministic").GetBoolean());
        Assert.Equal(8.0, body.GetProperty("value").GetDouble());
    }

    [Fact]
    public async Task Answer_UnknownQueryUnderLockIsAConflict()
    {
        var response = await Client().PostAsJsonAsync("/answer", new { query = "what do you recommend?", requireDeterminism = true });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Answer_UnknownQueryWithoutLockFallsThrough()
    {
        var response = await Client().PostAsJsonAsync("/answer", new { query = "what do you recommend?", requireDeterminism = false });

        var body = await Json(response);
        Assert.False(body.GetProperty("isDeterministic").GetBoolean());
        Assert.Contains("stochastic generator", body.GetProperty("note").GetString());
    }

    [Fact]
    public async Task History_ShowsTheDerivedIslandsProvenance()
    {
        var response = await Client().GetAsync($"/vault/history?query={Uri.EscapeDataString(SafetyLimits.PressureReliefSetpointBar)}");

        var version = Assert.Single((await Json(response)).EnumerateArray());
        Assert.Equal("minimum", version.GetProperty("derivedBy").GetString());
        Assert.Equal(3, version.GetProperty("derivedFrom").GetArrayLength());
    }

    [Fact]
    public async Task History_UnknownQueryIsNotFound()
    {
        var response = await Client().GetAsync("/vault/history?query=unknown");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Constitution_ListsEveryRule()
    {
        var body = await Json(await Client().GetAsync("/constitution"));

        Assert.Equal(Governance.NeuralConstitution.Rules.Count, body.GetArrayLength());
    }

    [Fact]
    public async Task Restart_ReloadsTheVaultInsteadOfCommissioningItAgain()
    {
        var first = await Json(await Client().GetAsync($"/vault/history?query={Uri.EscapeDataString(SafetyLimits.MaxAiValveOpening)}"));
        var second = await Json(await Client().GetAsync($"/vault/history?query={Uri.EscapeDataString(SafetyLimits.MaxAiValveOpening)}"));

        Assert.Equal(1, second.GetArrayLength());
        Assert.Equal(first[0].GetProperty("vectorId").GetGuid(), second[0].GetProperty("vectorId").GetGuid());
    }

    [Fact]
    public async Task EveryControlCycle_IsWrittenToTheAuditFile()
    {
        var client = Client();
        await client.PostAsJsonAsync("/control", Reading(95.0, 4.0));
        await client.PostAsJsonAsync("/control", Reading(30.0, 5.0, "deterministic island"));

        Assert.Equal(2, File.ReadAllLines(Path.Combine(_directory, "audit.jsonl")).Length);
    }
}
