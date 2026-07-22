namespace TerminalHelper.WindowsPlatform.Discovery;

public interface IWindowsEnvironment
{
    IReadOnlyList<string> PathEntries { get; }

    string? LocalAppData { get; }

    string? ProgramFiles { get; }

    string? ProgramFilesX86 { get; }
}
