namespace TerminalHelper.Windows.Input;

public static class StartupPathReader
{
    public static IReadOnlyList<string> Read(string[] commandLineArguments)
    {
        ArgumentNullException.ThrowIfNull(commandLineArguments);

        return commandLineArguments.Length <= 1 ? [] : commandLineArguments[1..];
    }
}
