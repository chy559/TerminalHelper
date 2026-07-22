namespace TerminalHelper.WindowsPlatform.Discovery;

public sealed class SystemWindowsEnvironment : IWindowsEnvironment
{
    public IReadOnlyList<string> PathEntries
    {
        get
        {
            var path = Environment.GetEnvironmentVariable("PATH");
            return path is null ? [] : path.Split(';');
        }
    }

    public string? LocalAppData => Environment.GetEnvironmentVariable("LOCALAPPDATA");

    public string? ProgramFiles => Environment.GetEnvironmentVariable("ProgramFiles");

    public string? ProgramFilesX86 => Environment.GetEnvironmentVariable("ProgramFiles(x86)");
}
