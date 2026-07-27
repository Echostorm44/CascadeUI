namespace Cascade.UI.Integration;

/// <summary>
/// Smoke test to verify the integration test infrastructure works.
/// </summary>
public class SmokeTest
{
    [TUnit.Core.Test]
    public async Task IntegrationTestInfrastructureInitializes()
    {
        int result = 1 + 1;
        await TUnit.Assertions.Assert.That(result).IsEqualTo(2);
    }
}
