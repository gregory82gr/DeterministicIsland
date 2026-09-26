using DeterministicIsland.Governance;
using System.Reflection;

namespace DeterministicIsland.Tests;

public class NeuralConstitutionTests
{
    public static IEnumerable<object[]> Rules() => NeuralConstitution.Rules.Select(r => new object[] { r.Name });

    [Fact]
    public void Constitution_ContainsTheFiveRulesOfTheBook()
    {
        Assert.Equal(5, NeuralConstitution.Rules.Count(r => r.Source == RuleSource.Book));
    }

    [Theory]
    [MemberData(nameof(Rules))]
    public void EveryRule_IsEnforcedAndVerifiedByExistingTests(string ruleName)
    {
        var rule = NeuralConstitution.Rules.Single(r => r.Name == ruleName);

        Assert.False(string.IsNullOrWhiteSpace(rule.EnforcedBy));
        Assert.NotEmpty(rule.VerifiedBy);

        var assembly = typeof(NeuralConstitutionTests).Assembly;
        foreach (var reference in rule.VerifiedBy)
        {
            var parts = reference.Split('.');
            Assert.Equal(2, parts.Length);

            var type = assembly.GetType($"{typeof(NeuralConstitutionTests).Namespace}.{parts[0]}");
            Assert.True(type is not null, $"Rule '{rule.Name}' cites unknown test class '{parts[0]}'.");

            var method = type!.GetMethod(parts[1], BindingFlags.Public | BindingFlags.Instance);
            Assert.True(method?.GetCustomAttribute<FactAttribute>() is not null,
                $"Rule '{rule.Name}' cites '{reference}', which is not an existing test.");
        }
    }
}
