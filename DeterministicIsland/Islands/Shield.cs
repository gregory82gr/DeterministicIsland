using DeterministicIsland.domain;

namespace DeterministicIsland.Islands
{
    // =======================================================================
    // SHIELD (§17.4): an independent, non-learned layer that bounds every command the
    // AI core proposes. The bound comes from the Static Vault, not from the model, and
    // it applies on every cycle, whether or not the operator asked for an island.
    // =======================================================================
    public sealed class Shield
    {
        private readonly StaticVault _vault;

        public Shield(StaticVault vault) => _vault = vault;

        public ControlCommand Enforce(ControlCommand proposal, DateTime asOf)
        {
            double maxSafe = SafetyLimits.Require(_vault, SafetyLimits.MaxAiValveOpening, asOf).Value;
            if (proposal.ValveOpeningTarget <= maxSafe)
                return proposal;

            return proposal with
            {
                ValveOpeningTarget = maxSafe,
                Origin = $"{proposal.Origin} (shielded from {proposal.ValveOpeningTarget:P1})"
            };
        }
    }
}
