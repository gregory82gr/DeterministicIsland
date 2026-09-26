using DeterministicIsland.Api;
using DeterministicIsland.domain;
using DeterministicIsland.Governance;
using DeterministicIsland.Islands;
using System.Text.Json.Serialization;

// =======================================================================
// PRESENTATION LAYER (§23.5): a thin HTTP surface over the application layer.
// Every answer still goes through the Arbiter, so no endpoint can bypass the
// Static Vault, the Shield, the islands or the Causality Lock.
// =======================================================================
var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSingleton<NexusRuntime>();

var app = builder.Build();
app.Services.GetRequiredService<NexusRuntime>();

app.MapPost("/control", (ControlRequest request, NexusRuntime runtime) =>
{
    var reading = new SensorReading(request.TemperatureCelsius, request.PressureBar,
        request.OperatorNotes ?? "", request.RadiationLeakDetected);
    var operatorOverride = request.OperatorOverride is { } o
        ? new OperatorOverride(o.OperatorId, o.ValveOpening, o.Reason)
        : null;

    try
    {
        var record = runtime.Control(reading, operatorOverride);
        return Results.Ok(new ControlResponse(
            Applied: record.FinalValveOpening is not null,
            ValveOpening: record.FinalValveOpening,
            Origin: record.Origin,
            Decision: record.Decision.ToString(),
            RequiresHumanReview: record.RequiresHumanReview,
            Reason: record.Reason,
            AiProposal: record.AiProposal,
            AiUncertainty95: record.AiUncertainty95,
            TriggeredIslands: record.TriggeredIslands.Select(i => i.Name).ToList()));
    }
    catch (ArgumentException ex)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["operatorOverride"] = new[] { ex.Message } });
    }
});

app.MapPost("/answer", (AnswerRequest request, NexusRuntime runtime) =>
{
    try
    {
        var vector = runtime.Answer(request.Query, request.RequireDeterminism);
        return vector is null
            ? Results.Ok(new AnswerResponse(IsDeterministic: false, Value: null, VectorId: null, Version: null, ApprovedBy: null,
                Note: "No Static Vault entry; the query falls through to the stochastic generator (§12.4.2)."))
            : Results.Ok(new AnswerResponse(
                IsDeterministic: vector.Determinism is { IsDeterministic: true },
                Value: vector.Embedding.Length > 0 ? vector.Value : null,
                VectorId: vector.VectorId,
                Version: vector.Determinism?.Version,
                ApprovedBy: vector.Determinism?.ApprovedBy,
                Note: null));
    }
    catch (DeterminismViolationException ex)
    {
        return Results.Problem(ex.Message, statusCode: StatusCodes.Status409Conflict, title: "Human-in-the-Loop: no deterministic answer");
    }
    catch (InvalidOperationException ex)
    {
        return Results.Problem(ex.Message, statusCode: StatusCodes.Status409Conflict, title: "The vault cannot resolve this query");
    }
});

app.MapGet("/vault/history", (string query, NexusRuntime runtime) =>
{
    var history = runtime.History(query);
    return history.Count == 0
        ? Results.NotFound()
        : Results.Ok(history.Select(v => new VaultVersion(
            v.VectorId,
            v.Determinism!.Version,
            v.Embedding.Length > 0 ? v.Value : null,
            v.Determinism.ValidFrom,
            v.Determinism.ValidTo,
            v.Determinism.ApprovedBy,
            v.Determinism.PointerTo,
            v.Determinism.DerivedBy,
            v.Determinism.DerivedFrom)));
});

app.MapGet("/vault/queries", (NexusRuntime runtime) => Results.Ok(runtime.Vault.Queries.OrderBy(q => q)));

app.MapGet("/constitution", () => Results.Ok(NeuralConstitution.Rules));

app.Run();

public sealed record OperatorOverrideRequest(string OperatorId, double ValveOpening, string Reason);

public sealed record ControlRequest(
    double TemperatureCelsius,
    double PressureBar,
    string? OperatorNotes,
    bool RadiationLeakDetected,
    OperatorOverrideRequest? OperatorOverride);

public sealed record ControlResponse(
    bool Applied,
    double? ValveOpening,
    string Origin,
    string Decision,
    bool RequiresHumanReview,
    string? Reason,
    double AiProposal,
    double? AiUncertainty95,
    IReadOnlyList<string> TriggeredIslands);

public sealed record AnswerRequest(string Query, bool RequireDeterminism);

public sealed record AnswerResponse(
    bool IsDeterministic,
    double? Value,
    Guid? VectorId,
    string? Version,
    string? ApprovedBy,
    string? Note);

public sealed record VaultVersion(
    Guid VectorId,
    string Version,
    double? Value,
    DateTime ValidFrom,
    DateTime? ValidTo,
    string ApprovedBy,
    Guid? PointerTo,
    string? DerivedBy,
    IReadOnlyList<Guid>? DerivedFrom);

// Makes the entry point visible to WebApplicationFactory in the tests.
public partial class Program;
