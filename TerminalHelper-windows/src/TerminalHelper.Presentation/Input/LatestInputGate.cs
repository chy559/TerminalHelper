namespace TerminalHelper.Windows.Input;

public sealed class LatestInputGate
{
    private readonly object gate = new();
    private long latestGeneration;

    public long BeginInput()
    {
        lock (gate)
        {
            return ++latestGeneration;
        }
    }

    public bool TryApply(
        long generation,
        IEnumerable<string?> rawPaths,
        Action<IReadOnlyList<string>> apply)
    {
        ArgumentNullException.ThrowIfNull(rawPaths);
        ArgumentNullException.ThrowIfNull(apply);

        var usablePaths = rawPaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path!)
            .ToArray();

        lock (gate)
        {
            if (generation != latestGeneration)
            {
                return false;
            }

            apply(usablePaths);
            return true;
        }
    }
}
