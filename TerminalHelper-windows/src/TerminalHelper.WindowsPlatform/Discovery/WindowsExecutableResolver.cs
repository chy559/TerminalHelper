using System.Collections.Concurrent;
using System.Security;
using System.Text.Json;
using System.Text.RegularExpressions;
using TerminalHelper.Core.Launching;

namespace TerminalHelper.WindowsPlatform.Discovery;

public sealed partial class WindowsExecutableResolver : ITargetExecutableResolver
{
    private static readonly TargetExecutable MissingExecutable = new((WorkspaceTarget)(-1), string.Empty);
    private static readonly Version MissingVersion = new(0, 0);

    private readonly IWindowsEnvironment _environment;
    private readonly IWindowsFileSystem _files;
    private readonly IRegistryReader _registry;
    private readonly ConcurrentDictionary<WorkspaceTarget, TargetExecutable?> _cache = new();

    public WindowsExecutableResolver(
        IWindowsEnvironment environment,
        IWindowsFileSystem files,
        IRegistryReader registry)
    {
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        _files = files ?? throw new ArgumentNullException(nameof(files));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    public TargetExecutable? Resolve(WorkspaceTarget target)
    {
        var executable = _cache.GetOrAdd(target, ResolveAndRepresentMissing);
        return ReferenceEquals(executable, MissingExecutable) ? null : executable;
    }

    public void Refresh()
    {
        _cache.Clear();
    }

    private TargetExecutable ResolveAndRepresentMissing(WorkspaceTarget target)
    {
        return ResolveUncached(target) ?? MissingExecutable;
    }

    private TargetExecutable? ResolveUncached(WorkspaceTarget target)
    {
        var path = target switch
        {
            WorkspaceTarget.Terminal => ResolveTerminal(),
            WorkspaceTarget.VisualStudioCode => ResolveVisualStudioCode(),
            WorkspaceTarget.IntelliJIdea => ResolveIntelliJIdea(),
            _ => throw new ArgumentOutOfRangeException(nameof(target), target, null),
        };

        return path is null ? null : new(target, path);
    }

    private string? ResolveTerminal()
    {
        return FindOnPath(["wt.exe"])
            ?? FindExisting(CombineOptional(_environment.LocalAppData, "Microsoft", "WindowsApps", "wt.exe"));
    }

    private string? ResolveVisualStudioCode()
    {
        var pathCandidate = FindVisualStudioCodeOnPath();
        if (pathCandidate is not null)
        {
            return pathCandidate;
        }

        var fixedCandidate = FirstExistingVisualStudioCodeExecutable(
        [
            CombineOptional(_environment.LocalAppData, "Programs", "Microsoft VS Code", "Code.exe"),
            CombineOptional(_environment.ProgramFiles, "Microsoft VS Code", "Code.exe"),
            CombineOptional(_environment.ProgramFilesX86, "Microsoft VS Code", "Code.exe"),
        ]);
        if (fixedCandidate is not null)
        {
            return fixedCandidate;
        }

        foreach (var appPath in GetAppPathsSafely("Code.exe"))
        {
            var candidate = FindExistingVisualStudioCodeExecutable(
                CleanRegistryExecutablePath(appPath));
            if (candidate is not null)
            {
                return candidate;
            }
        }

        foreach (var application in GetInstalledApplicationsSafely()
                     .Where(application => application.DisplayName.Contains(
                         "Visual Studio Code",
                         StringComparison.OrdinalIgnoreCase))
                     .OrderBy(application => application.DisplayName, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(application => application.InstallLocation, StringComparer.OrdinalIgnoreCase))
        {
            var candidate = FirstExistingVisualStudioCodeExecutable(
            [
                CombineOptional(application.InstallLocation, "Code.exe"),
                CleanRegistryExecutablePath(application.DisplayIcon),
            ]);
            if (candidate is not null)
            {
                return candidate;
            }
        }

        return null;
    }

    private string? ResolveIntelliJIdea()
    {
        var candidates = new List<IdeaCandidate>();
        AddPathIdeaCandidates(candidates, sourceRank: 0);
        AddProgramFilesIdeaCandidates(candidates, _environment.ProgramFiles, sourceRank: 1);
        AddProgramFilesIdeaCandidates(candidates, _environment.ProgramFilesX86, sourceRank: 1);
        AddRegistryIdeaCandidates(candidates, sourceRank: 2);
        AddToolboxIdeaCandidates(candidates, sourceRank: 3);

        return candidates
            .Where(candidate => SafeFileExists(candidate.Path))
            .OrderBy(candidate => candidate.Edition)
            .ThenByDescending(candidate => candidate.Version)
            .ThenBy(candidate => candidate.LauncherRank)
            .ThenBy(candidate => candidate.SourceRank)
            .ThenBy(candidate => candidate.Path, StringComparer.OrdinalIgnoreCase)
            .Select(candidate => candidate.Path)
            .FirstOrDefault();
    }

    private string? FindVisualStudioCodeOnPath()
    {
        foreach (var rawEntry in _environment.PathEntries)
        {
            var entry = CleanPathEntry(rawEntry);
            if (entry is null)
            {
                continue;
            }

            var directExecutable = FirstExistingVisualStudioCodeExecutable(
            [
                _files.Combine(entry, "code.exe"),
                _files.Combine(entry, "Code.exe"),
            ]);
            if (directExecutable is not null)
            {
                return directExecutable;
            }

            var commandPath = _files.Combine(entry, "code.cmd");
            var normalizedEntry = TrimEndingDirectorySeparators(entry);
            if (!SafeFileExists(commandPath)
                || !GetLastPathComponent(normalizedEntry).Equals(
                    "bin",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var installDirectory = _files.GetDirectoryName(normalizedEntry);
            var adjacentExecutable = FindExistingVisualStudioCodeExecutable(
                CombineOptional(installDirectory, "Code.exe"));
            if (adjacentExecutable is not null)
            {
                return adjacentExecutable;
            }
        }

        return null;
    }

    private void AddPathIdeaCandidates(ICollection<IdeaCandidate> candidates, int sourceRank)
    {
        foreach (var rawEntry in _environment.PathEntries)
        {
            var entry = CleanPathEntry(rawEntry);
            if (entry is null)
            {
                continue;
            }

            foreach (var executableName in new[] { "idea64.exe", "idea.exe" })
            {
                var path = _files.Combine(entry, executableName);
                candidates.Add(CreateIdeaCandidate(
                    path,
                    GetPathIdeaIdentity(path),
                    executableName,
                    sourceRank));
            }
        }
    }

    private string GetPathIdeaIdentity(string executablePath)
    {
        var launcherDirectory = _files.GetDirectoryName(executablePath);
        var productDirectory = launcherDirectory is null
            ? null
            : _files.GetDirectoryName(launcherDirectory);
        var metadataPath = CombineOptional(productDirectory, "product-info.json");
        if (metadataPath is null || !SafeFileExists(metadataPath))
        {
            return executablePath;
        }

        try
        {
            using var document = JsonDocument.Parse(_files.ReadAllText(metadataPath));
            var product = document.RootElement;
            var identity = GetIdeaProductIdentity(product);
            if (identity is null || !IsIdeaProduct(product, identity))
            {
                return executablePath;
            }

            return $"{identity} {GetJsonString(product, "version")}";
        }
        catch (Exception error) when (IsOptionalFileOrMetadataFailure(error))
        {
            return executablePath;
        }
    }

    private string? FindOnPath(IReadOnlyList<string> executableNames)
    {
        foreach (var rawEntry in _environment.PathEntries)
        {
            var entry = CleanPathEntry(rawEntry);
            if (entry is null)
            {
                continue;
            }

            foreach (var executableName in executableNames)
            {
                var candidate = FindExisting(_files.Combine(entry, executableName));
                if (candidate is not null)
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private void AddProgramFilesIdeaCandidates(
        ICollection<IdeaCandidate> candidates,
        string? programFiles,
        int sourceRank)
    {
        var root = CombineOptional(programFiles, "JetBrains");
        if (root is null)
        {
            return;
        }

        AddEnumeratedIdeaCandidates(candidates, root, "idea64.exe", sourceRank);
        AddEnumeratedIdeaCandidates(candidates, root, "idea.exe", sourceRank);
    }

    private void AddEnumeratedIdeaCandidates(
        ICollection<IdeaCandidate> candidates,
        string root,
        string executableName,
        int sourceRank)
    {
        foreach (var path in EnumerateFilesSafely(root, executableName))
        {
            candidates.Add(CreateIdeaCandidate(
                path,
                path,
                executableName,
                sourceRank));
        }
    }

    private void AddRegistryIdeaCandidates(ICollection<IdeaCandidate> candidates, int sourceRank)
    {
        foreach (var application in GetInstalledApplicationsSafely()
                     .Where(application => application.DisplayName.Contains(
                         "IntelliJ IDEA",
                         StringComparison.OrdinalIgnoreCase)))
        {
            AddIdeaCandidateIfPresent(
                candidates,
                CombineOptional(application.InstallLocation, "bin", "idea64.exe"),
                application.DisplayName,
                "idea64.exe",
                sourceRank);
            AddIdeaCandidateIfPresent(
                candidates,
                CombineOptional(application.InstallLocation, "bin", "idea.exe"),
                application.DisplayName,
                "idea.exe",
                sourceRank);

            var displayIcon = CleanRegistryExecutablePath(application.DisplayIcon);
            if (displayIcon is not null && IsIdeaExecutable(displayIcon))
            {
                candidates.Add(CreateIdeaCandidate(
                    displayIcon,
                    $"{application.DisplayName} {application.InstallLocation}",
                    GetLastPathComponent(displayIcon),
                    sourceRank));
            }
        }

        foreach (var executableName in new[] { "idea64.exe", "idea.exe" })
        {
            foreach (var appPath in GetAppPathsSafely(executableName))
            {
                var path = CleanRegistryExecutablePath(appPath);
                AddIdeaCandidateIfPresent(
                    candidates,
                    path,
                    path,
                    executableName,
                    sourceRank);
            }
        }
    }

    private void AddToolboxIdeaCandidates(ICollection<IdeaCandidate> candidates, int sourceRank)
    {
        var root = CombineOptional(_environment.LocalAppData, "JetBrains", "Toolbox", "apps");
        if (root is null)
        {
            return;
        }

        foreach (var metadataPath in EnumerateFilesSafely(root, "product-info.json"))
        {
            try
            {
                using var document = JsonDocument.Parse(_files.ReadAllText(metadataPath));
                var product = document.RootElement;
                if (product.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var identity = GetIdeaProductIdentity(product);
                if (identity is null || !IsIdeaProduct(product, identity))
                {
                    continue;
                }

                var version = GetJsonString(product, "version") ?? string.Empty;
                if (!product.TryGetProperty("launch", out var launchers)
                    || launchers.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                var productDirectory = _files.GetDirectoryName(metadataPath);
                if (productDirectory is null)
                {
                    continue;
                }

                foreach (var launcher in launchers.EnumerateArray())
                {
                    if (launcher.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    var os = GetJsonString(launcher, "os");
                    if (os is not null && !os.Equals("Windows", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var relativePath = GetJsonString(launcher, "launcherPath");
                    if (relativePath is null || !IsIdeaExecutable(relativePath))
                    {
                        continue;
                    }

                    var path = _files.Combine(productDirectory, relativePath);
                    candidates.Add(CreateIdeaCandidate(
                        path,
                        $"{identity} {version}",
                        GetLastPathComponent(relativePath),
                        sourceRank));
                }
            }
            catch (Exception error) when (IsOptionalFileOrMetadataFailure(error))
            {
                // Ignore malformed or inaccessible Toolbox metadata.
            }
        }
    }

    private void AddIdeaCandidateIfPresent(
        ICollection<IdeaCandidate> candidates,
        string? path,
        string? identity,
        string executableName,
        int sourceRank)
    {
        if (path is not null)
        {
            candidates.Add(CreateIdeaCandidate(
                path,
                identity ?? path,
                executableName,
                sourceRank));
        }
    }

    private static IdeaCandidate CreateIdeaCandidate(
        string path,
        string identity,
        string executableName,
        int sourceRank)
    {
        return new(
            path,
            GetIdeaEdition(identity),
            GetVersion(identity),
            executableName.Equals("idea64.exe", StringComparison.OrdinalIgnoreCase) ? 0 : 1,
            sourceRank);
    }

    private string? FirstExisting(IEnumerable<string?> candidates)
    {
        foreach (var candidate in candidates)
        {
            var existing = FindExisting(candidate);
            if (existing is not null)
            {
                return existing;
            }
        }

        return null;
    }

    private string? FirstExistingVisualStudioCodeExecutable(IEnumerable<string?> candidates)
    {
        foreach (var candidate in candidates)
        {
            var existing = FindExistingVisualStudioCodeExecutable(candidate);
            if (existing is not null)
            {
                return existing;
            }
        }

        return null;
    }

    private string? FindExistingVisualStudioCodeExecutable(string? path)
    {
        return path is not null
            && GetLastPathComponent(path).Equals("Code.exe", StringComparison.OrdinalIgnoreCase)
                ? FindExisting(path)
                : null;
    }

    private string? FindExisting(string? path)
    {
        return path is not null && SafeFileExists(path) ? path : null;
    }

    private bool SafeFileExists(string path)
    {
        try
        {
            return _files.FileExists(path);
        }
        catch (Exception error) when (IsOptionalFileFailure(error))
        {
            return false;
        }
    }

    private IReadOnlyList<string> EnumerateFilesSafely(string root, string searchPattern)
    {
        var results = new List<string>();
        var pendingDirectories = new SortedSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            root,
        };
        var visitedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (pendingDirectories.Count > 0)
        {
            var directory = pendingDirectories.Min!;
            pendingDirectories.Remove(directory);
            if (!visitedDirectories.Add(directory))
            {
                continue;
            }

            try
            {
                results.AddRange(_files
                    .EnumerateFiles(directory, searchPattern)
                    .Order(StringComparer.OrdinalIgnoreCase));
            }
            catch (Exception error) when (IsOptionalFileFailure(error))
            {
                // Continue traversing accessible descendants when file listing fails.
            }

            try
            {
                foreach (var child in _files
                             .EnumerateDirectories(directory)
                             .Order(StringComparer.OrdinalIgnoreCase))
                {
                    if (!visitedDirectories.Contains(child))
                    {
                        pendingDirectories.Add(child);
                    }
                }
            }
            catch (Exception error) when (IsOptionalFileFailure(error))
            {
                // This directory is optional; accessible siblings remain queued.
            }
        }

        return results
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private IReadOnlyList<string> GetAppPathsSafely(string executableName)
    {
        try
        {
            return _registry.GetAppPaths(executableName);
        }
        catch (Exception error) when (IsOptionalRegistryFailure(error))
        {
            return [];
        }
    }

    private IReadOnlyList<InstalledApplication> GetInstalledApplicationsSafely()
    {
        try
        {
            return _registry.GetInstalledApplications();
        }
        catch (Exception error) when (IsOptionalRegistryFailure(error))
        {
            return [];
        }
    }

    private string? CombineOptional(string? root, params string[] remainingPaths)
    {
        return string.IsNullOrWhiteSpace(root)
            ? null
            : _files.Combine([root, .. remainingPaths]);
    }

    private static string? CleanPathEntry(string? rawEntry)
    {
        if (string.IsNullOrWhiteSpace(rawEntry))
        {
            return null;
        }

        var entry = rawEntry.Trim();
        if (entry.Length >= 2 && entry[0] == '"' && entry[^1] == '"')
        {
            entry = entry[1..^1].Trim();
        }

        return entry.Length == 0 ? null : entry;
    }

    private static string TrimEndingDirectorySeparators(string path)
    {
        var minimumLength = path.Length >= 3
            && char.IsAsciiLetter(path[0])
            && path[1] == ':'
            && path[2] is '\\' or '/'
                ? 3
                : path.Length > 0 && path[0] is '\\' or '/'
                    ? 1
                    : 0;
        var length = path.Length;
        while (length > minimumLength && path[length - 1] is '\\' or '/')
        {
            length--;
        }

        return length == path.Length ? path : path[..length];
    }

    private static string? CleanRegistryExecutablePath(string? rawPath)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            return null;
        }

        var path = rawPath.Trim();
        if (path.StartsWith('"'))
        {
            var closingQuote = path.IndexOf('"', 1);
            return closingQuote > 1 ? path[1..closingQuote] : null;
        }

        var iconIndex = path.LastIndexOf(',');
        if (iconIndex > 0 && int.TryParse(path[(iconIndex + 1)..], out _))
        {
            path = path[..iconIndex].Trim();
        }

        return path.Length == 0 ? null : path;
    }

    private static bool IsIdeaProduct(JsonElement product, string name)
    {
        if (name.Contains("IntelliJ IDEA", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var productCode = GetJsonString(product, "productCode");
        return productCode is not null
            && (productCode.Equals("IU", StringComparison.OrdinalIgnoreCase)
                || productCode.Equals("IC", StringComparison.OrdinalIgnoreCase));
    }

    private static string? GetJsonString(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.String
                ? property.GetString()
                : null;
    }

    private static string? GetIdeaProductIdentity(JsonElement product)
    {
        var productCode = GetJsonString(product, "productCode");
        var name = GetJsonString(product, "name");
        return (productCode, name) switch
        {
            ({ Length: > 0 }, { Length: > 0 }) => $"{productCode} {name}",
            ({ Length: > 0 }, _) => productCode,
            (_, { Length: > 0 }) => name,
            _ => null,
        };
    }

    private static bool IsIdeaExecutable(string path)
    {
        var name = GetLastPathComponent(path);
        return name.Equals("idea64.exe", StringComparison.OrdinalIgnoreCase)
            || name.Equals("idea.exe", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetLastPathComponent(string path)
    {
        var separator = Math.Max(path.LastIndexOf('\\'), path.LastIndexOf('/'));
        return separator < 0 ? path : path[(separator + 1)..];
    }

    private static IdeaEdition GetIdeaEdition(string identity)
    {
        if (identity.Contains("Community", StringComparison.OrdinalIgnoreCase)
            || identity.Contains("IDEA-C", StringComparison.OrdinalIgnoreCase)
            || identity.Contains("IdeaIC", StringComparison.OrdinalIgnoreCase)
            || identity.Equals("IC", StringComparison.OrdinalIgnoreCase)
            || identity.StartsWith("IC ", StringComparison.OrdinalIgnoreCase))
        {
            return IdeaEdition.Community;
        }

        return identity.Contains("Ultimate", StringComparison.OrdinalIgnoreCase)
            || identity.Contains("IDEA-U", StringComparison.OrdinalIgnoreCase)
            || identity.Contains("IdeaIU", StringComparison.OrdinalIgnoreCase)
            || identity.Equals("IU", StringComparison.OrdinalIgnoreCase)
            || identity.StartsWith("IU ", StringComparison.OrdinalIgnoreCase)
            || identity.Contains("IntelliJ IDEA", StringComparison.OrdinalIgnoreCase)
                ? IdeaEdition.Ultimate
                : IdeaEdition.Unknown;
    }

    private static Version GetVersion(string identity)
    {
        var matches = VersionPattern().Matches(identity);
        for (var index = matches.Count - 1; index >= 0; index--)
        {
            if (Version.TryParse(matches[index].Value, out var version))
            {
                return version;
            }
        }

        return MissingVersion;
    }

    private static bool IsOptionalFileOrMetadataFailure(Exception error)
    {
        return IsOptionalFileFailure(error)
            || error is JsonException
            or KeyNotFoundException;
    }

    private static bool IsOptionalFileFailure(Exception error)
    {
        return error is UnauthorizedAccessException
            or SecurityException
            or IOException
            or ArgumentException
            or NotSupportedException;
    }

    private static bool IsOptionalRegistryFailure(Exception error)
    {
        return error is UnauthorizedAccessException
            or SecurityException
            or IOException
            or PlatformNotSupportedException;
    }

    [GeneratedRegex(@"\d+(?:\.\d+){1,3}", RegexOptions.CultureInvariant)]
    private static partial Regex VersionPattern();

    private enum IdeaEdition
    {
        Ultimate,
        Community,
        Unknown,
    }

    private sealed record IdeaCandidate(
        string Path,
        IdeaEdition Edition,
        Version Version,
        int LauncherRank,
        int SourceRank);
}
