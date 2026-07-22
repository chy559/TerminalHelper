namespace TerminalHelper.WindowsPlatform.Discovery;

public interface IWindowsFileSystem
{
    string Combine(params string[] paths);

    IEnumerable<string> EnumerateDirectories(string path);

    IEnumerable<string> EnumerateFiles(string path, string searchPattern);

    bool FileExists(string path);

    string? GetDirectoryName(string path);

    string ReadAllText(string path);
}
