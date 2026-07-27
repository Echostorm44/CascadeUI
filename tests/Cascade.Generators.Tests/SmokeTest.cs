namespace Cascade.Generators.Tests;

/// <summary>
/// Smoke test to verify the generator test infrastructure works.
/// </summary>
public class SmokeTest
{
    [TUnit.Core.Test]
    public async Task GeneratorTestInfrastructureInitializes()
    {
        int result = 1 + 1;
        await TUnit.Assertions.Assert.That(result).IsEqualTo(2);
    }
}
