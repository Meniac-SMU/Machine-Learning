using MachineLearning.Soccer;
using NUnit.Framework;

public sealed class SoccerHudControllerTests
{
    [TestCase(300f, "05:00")]
    [TestCase(180f, "03:00")]
    [TestCase(178.9f, "02:59")]
    [TestCase(60f, "01:00")]
    [TestCase(0f, "00:00")]
    [TestCase(-3f, "00:00")]
    public void FormatTime_UsesMatchClockFormat(float seconds, string expected)
    {
        Assert.That(SoccerHudController.FormatTime(seconds), Is.EqualTo(expected));
    }
}
