namespace TerminalHelper.WindowsPlatform.Discovery;

public sealed class SystemWindowsFileSystem : IWindowsFileSystem
{
    public string Combine(params string[] paths)
    {
        return Path.Combine(paths);
    }

    public IEnumerable<string> EnumerateFiles(
        string path,
        string searchPattern,
        SearchOption searchOption)
    {
        return Directory.EnumerateFiles(path, searchPattern, searchOption);
    }

    public bool FileExists(string path)
    {
        return File.Exists(path);
    }

    public string? GetDirectoryName(string path)
    {
        return Path.GetDirectoryName(path);
    }

    public string ReadAllText(string path)
    {
        return File.ReadAllText(path);
    }
}
