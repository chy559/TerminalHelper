namespace TerminalHelper.Core.Folders;

public sealed class SystemFolderPathService : IFolderPathService
{
    public string GetFullPath(string path)
    {
        return Path.GetFullPath(path);
    }

    public bool DirectoryExists(string path)
    {
        return Directory.Exists(path);
    }
}
