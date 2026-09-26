using DeterministicIsland.domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeterministicIsland.ProbabilisticCore
{
    public class MiniNeuralNetwork
    {
        public ControlCommand Predict(SensorReading reading)
        {
            return new ControlCommand(0.15, "AI Stochastic Core");
        }
    }
}
