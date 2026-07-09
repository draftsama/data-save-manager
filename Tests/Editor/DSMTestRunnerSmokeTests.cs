#nullable enable

using NUnit.Framework;

[TestFixture]
public class DSMTestRunnerSmokeTests
{
    [Test]
    public void TestRunner_IsWired_Passes()
    {
        Assert.Pass();
    }

    [Test]
    public void DSMTestConfig_Create_AppliesAutoSaveFalse()
    {
        var config = DSMTestConfig.Create();
        Assert.That(config.AutoSave, Is.False);
    }
}
