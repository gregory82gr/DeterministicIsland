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
    public void McDropout_ReportsMeanAndA95PercentInterval()
    {
        var estimate = new MiniNeuralNetwork().PredictWithUncertainty(Reading, passes: 500);

        Assert.Equal(500, estimate.Passes);
        Assert.True(estimate.StdDev > 0);
        Assert.Equal(1.96 * estimate.StdDev, estimate.HalfWidth95, precision: 12);
        Assert.InRange(estimate.Mean, estimate.Lower95, estimate.Upper95);
    }

    [Fact]
    public void McDropout_IsMoreUncertainWhereTheModelIsLessConfident()
    {
        var core = new MiniNeuralNetwork();

        var confident = core.PredictWithUncertainty(new SensorReading(30.0, 5.0, "", false), passes: 500);
        var uncertain = core.PredictWithUncertainty(new SensorReading(85.0, 7.0, "", false), passes: 500);

        Assert.True(uncertain.HalfWidth95 > 2 * confident.HalfWidth95);
    }

    [Fact]
    public void McDropout_NeedsAtLeastTwoPasses()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MiniNeuralNetwork().PredictWithUncertainty(Reading, passes: 1));
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
