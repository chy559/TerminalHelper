namespace TerminalHelper.WindowsPlatform.Launching;

public sealed record ProcessLaunchRequest
{
    public ProcessLaunchRequest(string fileName, IReadOnlyList<string> arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(arguments);

        FileName = fileName;
        Arguments = Array.AsReadOnly(arguments.ToArray());
    }

    public string FileName { get; }

    public IReadOnlyList<string> Arguments { get; }
}
