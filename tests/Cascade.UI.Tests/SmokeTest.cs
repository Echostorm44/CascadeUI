namespace Cascade.UI.Tests;

/// <summary>
/// Smoke test to verify the test infrastructure works.
/// Replaced by real tests as work packages are completed.
/// </summary>
public class SmokeTest
{
    [TUnit.Core.Test]
    public async Task TestInfrastructureInitializes()
    {
        int result = 1 + 1;
        await TUnit.Assertions.Assert.That(result).IsEqualTo(2);
    }
}
