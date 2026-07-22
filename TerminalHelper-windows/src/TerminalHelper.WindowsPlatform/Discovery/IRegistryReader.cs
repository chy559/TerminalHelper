namespace TerminalHelper.WindowsPlatform.Discovery;

public sealed record InstalledApplication(
    string DisplayName,
    string? InstallLocation,
    string? DisplayIcon = null);

public interface IRegistryReader
{
    IReadOnlyList<string> GetAppPaths(string executableName);

    IReadOnlyList<InstalledApplication> GetInstalledApplications();
}
