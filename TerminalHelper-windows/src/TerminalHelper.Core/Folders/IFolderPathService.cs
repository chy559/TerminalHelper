namespace TerminalHelper.Core.Folders;

public interface IFolderPathService
{
    string GetFullPath(string path);

    bool DirectoryExists(string path);
}
