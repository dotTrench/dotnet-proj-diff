namespace ProjectDiff.Tests;

public sealed class VerifyTests
{
    [Fact]
    public async Task VerifyIsSetupCorrectly()
    {
        await VerifyChecks.Run();
    }

}
