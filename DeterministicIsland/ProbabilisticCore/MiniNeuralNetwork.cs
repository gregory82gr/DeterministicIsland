using DeterministicIsland.domain;
using DeterministicIsland.Islands;
using System.Security.Cryptography;

namespace DeterministicIsland.ProbabilisticCore
{
    // Mean and sample standard deviation of MC Dropout passes; the 95% interval is
    // mean ± 1.96·s, exactly as in the book's worked example (§13.6).
    public sealed record UncertainPrediction(double Mean, double StdDev, int Passes, string Origin)
    {
        public double HalfWidth95 => 1.96 * StdDev;
        public double Lower95 => Math.Max(0.0, Mean - HalfWidth95);
        public double Upper95 => Math.Min(1.0, Mean + HalfWidth95);
    }

    // =======================================================================
    // STOCHASTIC CORE: a 2-4-1 multilayer perceptron (Ch. 4-7) that predicts the
    // valve opening from temperature and pressure. Dropout stays active at inference,
    // so two identical readings can produce two different commands (§12.2.2).
    // With a FrozenSnapshotConfig the same network becomes bitwise reproducible (§12.3.2).
    // =======================================================================
    public class MiniNeuralNetwork
    {
        private const double DropoutRate = 0.2;

        // Fixed, "trained" weights. Inputs are scaled to roughly [0, 1].
        private static readonly double[,] HiddenWeights =
        {
            { 1.8, 0.6 },
            { -0.7, 1.9 },
            { 1.1, 1.3 },
            { 0.4, -1.2 }
        };
        private static readonly double[] HiddenBiases = { -0.9, -0.8, -1.0, 0.3 };
        private static readonly double[] OutputWeights = { 1.4, 1.6, 0.9, -1.1 };
        private const double OutputBias = -1.2;

        private readonly FrozenSnapshotConfig? _snapshot;

        public MiniNeuralNetwork(FrozenSnapshotConfig? snapshot = null)
        {
            if (snapshot is not null)
            {
                if (snapshot.ModelWeightsHash != WeightsHash())
                    throw new InvalidOperationException("Frozen Snapshot rejected: model weights do not match the locked hash.");
                if (snapshot.RuntimeVersion != CurrentRuntimeVersion)
                    throw new InvalidOperationException(
                        $"Frozen Snapshot rejected: locked runtime {snapshot.RuntimeVersion}, running {CurrentRuntimeVersion}.");
                if (!snapshot.CpuOnlyInference)
                    throw new InvalidOperationException("Frozen Snapshot rejected: only single-threaded CPU inference is reproducible.");
            }
            _snapshot = snapshot;
        }

        public static string CurrentRuntimeVersion => Environment.Version.ToString();

        public bool IsFrozen => _snapshot is not null;

        public static FrozenSnapshotConfig CreateSnapshot(int randomSeed) => new()
        {
            ModelWeightsHash = WeightsHash(),
            RuntimeVersion = CurrentRuntimeVersion,
            RandomSeed = randomSeed,
            CpuOnlyInference = true
        };

        public ControlCommand Predict(SensorReading reading)
        {
            // A frozen snapshot seeds the sampling identically on every call;
            // otherwise the dropout masks differ from call to call.
            var random = _snapshot is null ? Random.Shared : new Random(_snapshot.RandomSeed);
            return new ControlCommand(Forward(reading, random), Origin);
        }

        // §13.6 MC Dropout: run the identical input through the network many times with
        // dropout left on, and treat the spread of the outputs as an uncertainty estimate.
        public UncertainPrediction PredictWithUncertainty(SensorReading reading, int passes)
        {
            if (passes < 2)
                throw new ArgumentOutOfRangeException(nameof(passes), "MC Dropout needs at least two passes.");

            var random = _snapshot is null ? Random.Shared : new Random(_snapshot.RandomSeed);
            var outputs = new double[passes];
            for (int i = 0; i < passes; i++)
                outputs[i] = Forward(reading, random);

            double mean = outputs.Average();
            double variance = outputs.Sum(o => (o - mean) * (o - mean)) / (passes - 1);
            return new UncertainPrediction(mean, Math.Sqrt(variance), passes, Origin);
        }

        private string Origin => IsFrozen ? "AI Frozen Snapshot" : "AI Stochastic Core";

        private static double Forward(SensorReading reading, Random random)
        {
            double[] inputs = { reading.TemperatureCelsius / 100.0, reading.PressureBar / 10.0 };
            double output = OutputBias;

            // Single-threaded, fixed summation order (§24.2.2).
            for (int h = 0; h < HiddenBiases.Length; h++)
            {
                double sum = HiddenBiases[h];
                for (int i = 0; i < inputs.Length; i++)
                    sum += HiddenWeights[h, i] * inputs[i];

                double activation = Math.Tanh(sum);
                bool dropped = random.NextDouble() < DropoutRate;
                output += dropped ? 0.0 : OutputWeights[h] * activation / (1.0 - DropoutRate);
            }

            return 1.0 / (1.0 + Math.Exp(-output));
        }

        public static string WeightsHash()
        {
            var values = HiddenWeights.Cast<double>()
                .Concat(HiddenBiases)
                .Concat(OutputWeights)
                .Append(OutputBias);
            var bytes = values.SelectMany(BitConverter.GetBytes).ToArray();
            return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        }
    }
}
