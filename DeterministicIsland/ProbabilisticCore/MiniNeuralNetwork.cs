using DeterministicIsland.domain;
using DeterministicIsland.Islands;
using System.Security.Cryptography;

namespace DeterministicIsland.ProbabilisticCore
{
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

            double valveOpening = 1.0 / (1.0 + Math.Exp(-output));
            return new ControlCommand(valveOpening, IsFrozen ? "AI Frozen Snapshot" : "AI Stochastic Core");
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
