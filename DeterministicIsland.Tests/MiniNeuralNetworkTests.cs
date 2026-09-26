using DeterministicIsland.domain;
using DeterministicIsland.ProbabilisticCore;

namespace DeterministicIsland.Tests;

public class MiniNeuralNetworkTests
{
    private static readonly SensorReading Reading = new(60.0, 5.0, "", false);

    [Fact]
    public void StochasticCore_GivesDifferentOutputsForIdenticalInput()
    {
        var core = new MiniNeuralNetwork();

        var outputs = Enumerable.Range(0, 100).Select(_ => core.Predict(Reading).ValveOpeningTarget).Distinct().Count();

        Assert.True(outputs > 1);
    }

    [Fact]
    public void FrozenSnapshot_IsBitwiseReproducible()
    {
        var core = new MiniNeuralNetwork(MiniNeuralNetwork.CreateSnapshot(randomSeed: 7));
        long expected = BitConverter.DoubleToInt64Bits(core.Predict(Reading).ValveOpeningTarget);

        for (int i = 0; i < 100; i++)
            Assert.Equal(expected, BitConverter.DoubleToInt64Bits(core.Predict(Reading).ValveOpeningTarget));
    }

    [Fact]
    public void FrozenSnapshot_RejectsMismatchedWeights()
    {
        var config = MiniNeuralNetwork.CreateSnapshot(1) with { ModelWeightsHash = "00" };

        Assert.Throws<InvalidOperationException>(() => new MiniNeuralNetwork(config));
    }

    [Fact]
    public void FrozenSnapshot_RejectsNonCpuInference()
    {
        var config = MiniNeuralNetwork.CreateSnapshot(1) with { CpuOnlyInference = false };

        Assert.Throws<InvalidOperationException>(() => new MiniNeuralNetwork(config));
    }
}
