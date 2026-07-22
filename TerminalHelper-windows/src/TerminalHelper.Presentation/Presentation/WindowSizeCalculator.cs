namespace TerminalHelper.Windows.Presentation;

public readonly record struct WindowPixelSize(int Width, int Height);

public static class WindowSizeCalculator
{
    private const int LogicalWidth = 500;
    private const int LogicalHeight = 440;

    public static WindowPixelSize ForRasterizationScale(double rasterizationScale)
    {
        if (!double.IsFinite(rasterizationScale) || rasterizationScale <= 0)
        {
            rasterizationScale = 1;
        }

        var physicalWidth = LogicalWidth * rasterizationScale;
        var physicalHeight = LogicalHeight * rasterizationScale;
        if (physicalWidth > int.MaxValue || physicalHeight > int.MaxValue)
        {
            return new WindowPixelSize(LogicalWidth, LogicalHeight);
        }

        return new WindowPixelSize(
            Math.Max(1, (int)Math.Round(physicalWidth, MidpointRounding.AwayFromZero)),
            Math.Max(1, (int)Math.Round(physicalHeight, MidpointRounding.AwayFromZero)));
    }
}
