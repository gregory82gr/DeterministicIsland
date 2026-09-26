namespace DeterministicIsland.Islands
{
    // §24.5: builds a new deterministic island as a pure function of already registered
    // islands. Because the function is pure, the result inherits IsDeterministic = true.
    public static class DerivedIslandFactory
    {
        public static void DeriveMinimum(StaticVault vault, string derivedQuery, DateTime validFrom, string approvedBy,
            params string[] inputQueries) =>
            vault.RegisterDerived(derivedQuery, inputQueries, "minimum", values => values.Min(), validFrom, approvedBy);
    }
}
