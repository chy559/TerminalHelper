using TerminalHelper.Windows.Presentation;

namespace TerminalHelper.WindowsPlatform.Tests.Presentation;

[TestClass]
public sealed class WindowSizeCalculatorTests
{
    [TestMethod]
    public void ForRasterizationScale_ConvertsLogicalSizeToPhysicalPixels()
    {
        Assert.AreEqual(new WindowPixelSize(500, 440), WindowSizeCalculator.ForRasterizationScale(1.0));
        Assert.AreEqual(new WindowPixelSize(750, 660), WindowSizeCalculator.ForRasterizationScale(1.5));
    }

    [TestMethod]
    [DataRow(0.0)]
    [DataRow(-1.0)]
    [DataRow(double.NaN)]
    [DataRow(double.PositiveInfinity)]
    public void ForRasterizationScale_InvalidScaleFallsBackToOne(double scale)
    {
        Assert.AreEqual(new WindowPixelSize(500, 440), WindowSizeCalculator.ForRasterizationScale(scale));
    }
}
