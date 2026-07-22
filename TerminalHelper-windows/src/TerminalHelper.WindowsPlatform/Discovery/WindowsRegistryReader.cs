using System.Security;
using Microsoft.Win32;

namespace TerminalHelper.WindowsPlatform.Discovery;

public sealed class WindowsRegistryReader : IRegistryReader
{
    private const string AppPathsKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths";
    private const string UninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

    private static readonly RegistryHive[] Hives =
    [
        RegistryHive.CurrentUser,
        RegistryHive.LocalMachine,
    ];

    private static readonly RegistryView[] Views =
    [
        RegistryView.Registry64,
        RegistryView.Registry32,
    ];

    public IReadOnlyList<string> GetAppPaths(string executableName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executableName);
        var results = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var hive in Hives)
        {
            foreach (var view in Views)
            {
                try
                {
                    using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                    using var appKey = baseKey.OpenSubKey($@"{AppPathsKey}\{executableName}");
                    if (appKey?.GetValue(null) is string path && !string.IsNullOrWhiteSpace(path))
                    {
                        results.Add(path);
                    }
                }
                catch (Exception error) when (IsOptionalRegistryFailure(error))
                {
                    // An unavailable registry view is an optional discovery source.
                }
            }
        }

        return results.Order(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public IReadOnlyList<InstalledApplication> GetInstalledApplications()
    {
        var results = new HashSet<InstalledApplication>();

        foreach (var hive in Hives)
        {
            foreach (var view in Views)
            {
                ReadInstalledApplications(hive, view, results);
            }
        }

        return results
            .OrderBy(application => application.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(application => application.InstallLocation, StringComparer.OrdinalIgnoreCase)
            .ThenBy(application => application.DisplayIcon, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void ReadInstalledApplications(
        RegistryHive hive,
        RegistryView view,
        ISet<InstalledApplication> results)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var uninstallKey = baseKey.OpenSubKey(UninstallKey);
            if (uninstallKey is null)
            {
                return;
            }

            foreach (var subKeyName in uninstallKey.GetSubKeyNames())
            {
                try
                {
                    using var applicationKey = uninstallKey.OpenSubKey(subKeyName);
                    if (applicationKey?.GetValue("DisplayName") is not string displayName
                        || string.IsNullOrWhiteSpace(displayName))
                    {
                        continue;
                    }

                    results.Add(new(
                        displayName,
                        applicationKey.GetValue("InstallLocation") as string,
                        applicationKey.GetValue("DisplayIcon") as string));
                }
                catch (Exception error) when (IsOptionalRegistryFailure(error))
                {
                    // Continue with the remaining read-only uninstall entries.
                }
            }
        }
        catch (Exception error) when (IsOptionalRegistryFailure(error))
        {
            // An unavailable registry hive or view is an optional discovery source.
        }
    }

    private static bool IsOptionalRegistryFailure(Exception error)
    {
        return error is UnauthorizedAccessException
            or SecurityException
            or IOException
            or PlatformNotSupportedException;
    }
}
