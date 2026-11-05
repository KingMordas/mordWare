using ArchUnitNET.Fluent;

namespace MordWare.Tests.Architecture;

/// <summary>Fails the test with every violation, naming the offending types and dependencies.</summary>
public static class ArchRuleAssert
{
    public static void HasNoViolations(IArchRule rule)
    {
        var architecture = MordWareArchitecture.Instance;
        if (rule.HasNoViolations(architecture))
        {
            return;
        }

        var violations = rule.Evaluate(architecture)
            .Where(r => !r.Passed)
            .Select(r => "  - " + r.Description);
        Assert.Fail($"Architecture rule broken: {rule.Description}{Environment.NewLine}{string.Join(Environment.NewLine, violations)}");
    }
}
