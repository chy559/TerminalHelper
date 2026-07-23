using TerminalHelper.Core.Launching;
using TerminalHelper.WindowsPlatform.Discovery;

namespace TerminalHelper.WindowsPlatform.Tests.Discovery;

[TestClass]
public sealed class WindowsExecutableResolverTests
{
    private FakeWindowsEnvironment _environment = null!;
    private FakeWindowsFileSystem _files = null!;
    private FakeRegistryReader _registry = null!;
    private WindowsExecutableResolver _resolver = null!;

    [TestInitialize]
    public void SetUp()
    {
        _environment = new()
        {
            LocalAppData = @"C:\Users\Me\AppData\Local",
            ProgramFiles = @"C:\Program Files",
            ProgramFilesX86 = @"C:\Program Files (x86)",
        };
        _files = new();
        _registry = new();
        _resolver = new(_environment, _files, _registry);
    }

    [TestMethod]
    public void ResolveTerminal_PrefersPathThenUsesWindowsAppsAlias()
    {
        _environment.PathEntries = [@"C:\Tools"];
        _files.Add(@"C:\Tools\wt.exe");
        _files.Add(@"C:\Users\Me\AppData\Local\Microsoft\WindowsApps\wt.exe");

        Assert.AreEqual(@"C:\Tools\wt.exe", _resolver.Resolve(WorkspaceTarget.Terminal)?.Path);

        _files.Remove(@"C:\Tools\wt.exe");
        _resolver.Refresh();

        Assert.AreEqual(
            @"C:\Users\Me\AppData\Local\Microsoft\WindowsApps\wt.exe",
            _resolver.Resolve(WorkspaceTarget.Terminal)?.Path);
    }

    [TestMethod]
    public void ResolveTerminal_StripsWhitespaceAndSurroundingQuotesFromPathEntries()
    {
        _environment.PathEntries = ["  \"C:\\Quoted Tools\"  "];
        _files.Add(@"C:\Quoted Tools\wt.exe");

        Assert.AreEqual(
            @"C:\Quoted Tools\wt.exe",
            _resolver.Resolve(WorkspaceTarget.Terminal)?.Path);
    }

    [TestMethod]
    public void ResolveVisualStudioCode_PrefersPathAndExecutableNameOrder()
    {
        _environment.PathEntries = [@"C:\First", @"D:\Second"];
        _files.Add(@"C:\First\Code.exe");
        _files.Add(@"C:\First\code.cmd");
        _files.Add(@"D:\Second\code.exe");

        Assert.AreEqual(
            @"C:\First\code.exe",
            _resolver.Resolve(WorkspaceTarget.VisualStudioCode)?.Path);
    }

    [TestMethod]
    public void ResolveVisualStudioCode_DerivesAdjacentExecutableFromPathBinCommand()
    {
        _environment.PathEntries = [@"C:\Users\Me\AppData\Local\Programs\Microsoft VS Code\bin"];
        _files.Add(@"C:\Users\Me\AppData\Local\Programs\Microsoft VS Code\bin\code.cmd");
        var executable = @"C:\Users\Me\AppData\Local\Programs\Microsoft VS Code\Code.exe";
        _files.Add(executable);

        Assert.AreEqual(
            executable,
            _resolver.Resolve(WorkspaceTarget.VisualStudioCode)?.Path);
    }

    [TestMethod]
    public void ResolveVisualStudioCode_PathCommandWithoutAdjacentExecutableFallsBackToFixedExecutable()
    {
        _environment.PathEntries = [@"C:\Broken VS Code\bin"];
        _files.Add(@"C:\Broken VS Code\bin\code.cmd");
        var fixedExecutable =
            @"C:\Users\Me\AppData\Local\Programs\Microsoft VS Code\Code.exe";
        _files.Add(fixedExecutable);

        Assert.AreEqual(
            fixedExecutable,
            _resolver.Resolve(WorkspaceTarget.VisualStudioCode)?.Path);
    }

    [TestMethod]
    public void ResolveVisualStudioCode_NeverReturnsCommandFilesFromRegistryValues()
    {
        _registry.AppPaths["Code.exe"] = [@"C:\Registry\code.cmd"];
        _registry.Installations =
        [
            new("Microsoft Visual Studio Code", @"C:\Missing", @"C:\Uninstall\code.cmd"),
        ];
        _files.Add(@"C:\Registry\code.cmd");
        _files.Add(@"C:\Uninstall\code.cmd");

        Assert.IsNull(_resolver.Resolve(WorkspaceTarget.VisualStudioCode));
    }

    [TestMethod]
    public void ResolveVisualStudioCode_PrefersUserInstallThenSystemLocations()
    {
        var user = @"C:\Users\Me\AppData\Local\Programs\Microsoft VS Code\Code.exe";
        var system64 = @"C:\Program Files\Microsoft VS Code\Code.exe";
        var system32 = @"C:\Program Files (x86)\Microsoft VS Code\Code.exe";
        _files.Add(user);
        _files.Add(system64);
        _files.Add(system32);

        Assert.AreEqual(user, _resolver.Resolve(WorkspaceTarget.VisualStudioCode)?.Path);

        _files.Remove(user);
        _resolver.Refresh();
        Assert.AreEqual(system64, _resolver.Resolve(WorkspaceTarget.VisualStudioCode)?.Path);

        _files.Remove(system64);
        _resolver.Refresh();
        Assert.AreEqual(system32, _resolver.Resolve(WorkspaceTarget.VisualStudioCode)?.Path);
    }

    [TestMethod]
    public void ResolveVisualStudioCode_UsesAppPathsBeforeUninstallInformation()
    {
        var appPath = @"E:\Editors\VS Code\Code.exe";
        var uninstallPath = @"F:\Portable\VS Code";
        _registry.AppPaths["Code.exe"] = [appPath];
        _registry.Installations =
        [
            new("Microsoft Visual Studio Code", uninstallPath),
        ];
        _files.Add(appPath);
        _files.Add(@"F:\Portable\VS Code\Code.exe");

        Assert.AreEqual(appPath, _resolver.Resolve(WorkspaceTarget.VisualStudioCode)?.Path);
    }

    [TestMethod]
    public void ResolveVisualStudioCode_UsesVerifiedUninstallInstallLocationOrDisplayIcon()
    {
        _registry.Installations =
        [
            new("Microsoft Visual Studio Code", @"E:\Missing", "\"F:\\VS Code\\Code.exe\",0"),
        ];
        _files.Add(@"F:\VS Code\Code.exe");

        Assert.AreEqual(
            @"F:\VS Code\Code.exe",
            _resolver.Resolve(WorkspaceTarget.VisualStudioCode)?.Path);
    }

    [TestMethod]
    public void ResolveIdea_PrefersIdea64AnywhereOnPathBeforeIdea()
    {
        _environment.PathEntries = [@"C:\First", @"D:\Second"];
        _files.Add(@"C:\First\idea.exe");
        _files.Add(@"D:\Second\idea64.exe");

        Assert.AreEqual(
            @"D:\Second\idea64.exe",
            _resolver.Resolve(WorkspaceTarget.IntelliJIdea)?.Path);
    }

    [TestMethod]
    public void ResolveIdea_PathCommunityMetadataDoesNotOutrankRegistryUltimate()
    {
        var pathCommunity = @"C:\JetBrains\IdeaIC\bin\idea64.exe";
        var registryUltimate = @"D:\JetBrains\IdeaIU\bin\idea64.exe";
        _environment.PathEntries = [@"C:\JetBrains\IdeaIC\bin"];
        _files.Add(pathCommunity);
        _files.AddText(
            @"C:\JetBrains\IdeaIC\product-info.json",
            """
            {
              "name": "IntelliJ IDEA",
              "productCode": "IC",
              "version": "2027.1"
            }
            """);
        _registry.Installations =
        [
            new("IntelliJ IDEA Ultimate 2026.3", @"D:\JetBrains\IdeaIU"),
        ];
        _files.Add(registryUltimate);

        Assert.AreEqual(
            registryUltimate,
            _resolver.Resolve(WorkspaceTarget.IntelliJIdea)?.Path);
    }

    [TestMethod]
    public void ResolveIdea_PathCommunityMetadataDoesNotOutrankToolboxUltimate()
    {
        var pathCommunity = @"C:\JetBrains\IdeaIC\bin\idea64.exe";
        var toolboxUltimate =
            @"C:\Users\Me\AppData\Local\JetBrains\Toolbox\apps\IDEA-U\2026.2\bin\idea64.exe";
        _environment.PathEntries = [@"C:\JetBrains\IdeaIC\bin"];
        _files.Add(pathCommunity);
        _files.AddText(
            @"C:\JetBrains\IdeaIC\product-info.json",
            """
            {
              "name": "IntelliJ IDEA Community Edition",
              "version": "2027.1"
            }
            """);
        _files.AddText(
            @"C:\Users\Me\AppData\Local\JetBrains\Toolbox\apps\IDEA-U\2026.2\product-info.json",
            """
            {
              "productCode": "IU",
              "version": "2026.2",
              "launch": [{ "launcherPath": "bin\\idea64.exe" }]
            }
            """);
        _files.Add(toolboxUltimate);

        Assert.AreEqual(
            toolboxUltimate,
            _resolver.Resolve(WorkspaceTarget.IntelliJIdea)?.Path);
    }

    [TestMethod]
    public void ResolveIdea_UnknownPathCandidateDoesNotOutrankKnownUltimate()
    {
        var unknownPath = @"C:\Tools\idea64.exe";
        var registryUltimate = @"D:\JetBrains\IdeaIU\bin\idea64.exe";
        _environment.PathEntries = [@"C:\Tools"];
        _files.Add(unknownPath);
        _registry.Installations =
        [
            new("IntelliJ IDEA Ultimate 2026.3", @"D:\JetBrains\IdeaIU"),
        ];
        _files.Add(registryUltimate);

        Assert.AreEqual(
            registryUltimate,
            _resolver.Resolve(WorkspaceTarget.IntelliJIdea)?.Path);
    }

    [TestMethod]
    public void ResolveIdea_PrefersUltimateOverCommunityAcrossRegistryCandidates()
    {
        _registry.Installations =
        [
            new("IntelliJ IDEA Community Edition 2027.1", @"C:\JetBrains\IdeaIC"),
            new("IntelliJ IDEA Ultimate 2026.3", @"D:\JetBrains\IdeaIU"),
        ];
        _files.Add(@"C:\JetBrains\IdeaIC\bin\idea64.exe");
        _files.Add(@"D:\JetBrains\IdeaIU\bin\idea64.exe");

        Assert.AreEqual(
            @"D:\JetBrains\IdeaIU\bin\idea64.exe",
            _resolver.Resolve(WorkspaceTarget.IntelliJIdea)?.Path);
    }

    [TestMethod]
    public void ResolveIdea_UsesNewestVersionAndPrefersIdea64WithinIt()
    {
        _registry.Installations =
        [
            new("IntelliJ IDEA Ultimate 2025.3", @"C:\JetBrains\IU-2025.3"),
            new("IntelliJ IDEA Ultimate 2026.1", @"C:\JetBrains\IU-2026.1"),
            new("IntelliJ IDEA Ultimate 2026.1", @"D:\JetBrains\IU-2026.1"),
        ];
        _files.Add(@"C:\JetBrains\IU-2025.3\bin\idea64.exe");
        _files.Add(@"C:\JetBrains\IU-2026.1\bin\idea.exe");
        _files.Add(@"D:\JetBrains\IU-2026.1\bin\idea64.exe");

        Assert.AreEqual(
            @"D:\JetBrains\IU-2026.1\bin\idea64.exe",
            _resolver.Resolve(WorkspaceTarget.IntelliJIdea)?.Path);
    }

    [TestMethod]
    public void ResolveIdea_ReadsToolboxProductInfoAndIgnoresMalformedJson()
    {
        _files.AddText(
            @"C:\Users\Me\AppData\Local\JetBrains\Toolbox\apps\IDEA-U\broken\product-info.json",
            "{not json");
        _files.AddText(
            @"C:\Users\Me\AppData\Local\JetBrains\Toolbox\apps\IDEA-U\2026.2\product-info.json",
            """
            {
              "name": "IntelliJ IDEA Ultimate",
              "version": "2026.2",
              "launch": [
                { "os": "Windows", "launcherPath": "bin\\idea64.exe" }
              ]
            }
            """);
        _files.Add(@"C:\Users\Me\AppData\Local\JetBrains\Toolbox\apps\IDEA-U\2026.2\bin\idea64.exe");

        Assert.AreEqual(
            @"C:\Users\Me\AppData\Local\JetBrains\Toolbox\apps\IDEA-U\2026.2\bin\idea64.exe",
            _resolver.Resolve(WorkspaceTarget.IntelliJIdea)?.Path);
    }

    [TestMethod]
    [DataRow("[]")]
    [DataRow("null")]
    [DataRow("\"metadata\"")]
    [DataRow("42")]
    public void ResolveIdea_IgnoresWrongShapedToolboxJsonAndContinues(string wrongShapedJson)
    {
        _files.AddText(
            @"C:\Users\Me\AppData\Local\JetBrains\Toolbox\apps\A-Wrong\product-info.json",
            wrongShapedJson);
        _files.AddText(
            @"C:\Users\Me\AppData\Local\JetBrains\Toolbox\apps\B-Valid\product-info.json",
            """
            {
              "productCode": "IU",
              "version": "2026.2",
              "launch": [{ "launcherPath": "bin\\idea64.exe" }]
            }
            """);
        var expected =
            @"C:\Users\Me\AppData\Local\JetBrains\Toolbox\apps\B-Valid\bin\idea64.exe";
        _files.Add(expected);

        Assert.AreEqual(expected, _resolver.Resolve(WorkspaceTarget.IntelliJIdea)?.Path);
    }

    [TestMethod]
    public void ResolveIdea_UsesToolboxProductCodeToPreferUltimateOverNewerCommunity()
    {
        _files.AddText(
            @"C:\Users\Me\AppData\Local\JetBrains\Toolbox\apps\IDEA-C\2027.1\product-info.json",
            """
            {
              "productCode": "IC",
              "version": "2027.1",
              "launch": [{ "launcherPath": "bin\\idea64.exe" }]
            }
            """);
        _files.AddText(
            @"C:\Users\Me\AppData\Local\JetBrains\Toolbox\apps\IDEA-U\2026.2\product-info.json",
            """
            {
              "productCode": "IU",
              "version": "2026.2",
              "launch": [{ "launcherPath": "bin\\idea64.exe" }]
            }
            """);
        _files.Add(@"C:\Users\Me\AppData\Local\JetBrains\Toolbox\apps\IDEA-C\2027.1\bin\idea64.exe");
        _files.Add(@"C:\Users\Me\AppData\Local\JetBrains\Toolbox\apps\IDEA-U\2026.2\bin\idea64.exe");

        Assert.AreEqual(
            @"C:\Users\Me\AppData\Local\JetBrains\Toolbox\apps\IDEA-U\2026.2\bin\idea64.exe",
            _resolver.Resolve(WorkspaceTarget.IntelliJIdea)?.Path);
    }

    [TestMethod]
    public void ResolveIdea_RanksProgramFilesInstallationsDeterministically()
    {
        _files.Add(@"C:\Program Files\JetBrains\IntelliJ IDEA Community Edition 2027.1\bin\idea64.exe");
        _files.Add(@"C:\Program Files\JetBrains\IntelliJ IDEA 2025.3\bin\idea64.exe");
        _files.Add(@"C:\Program Files\JetBrains\IntelliJ IDEA 2026.2\bin\idea64.exe");

        Assert.AreEqual(
            @"C:\Program Files\JetBrains\IntelliJ IDEA 2026.2\bin\idea64.exe",
            _resolver.Resolve(WorkspaceTarget.IntelliJIdea)?.Path);
    }

    [TestMethod]
    public void ResolveIdea_ContinuesProgramFilesScanAfterInaccessibleDescendant()
    {
        var blocked = @"C:\Program Files\JetBrains\A-Blocked";
        var expected = @"C:\Program Files\JetBrains\B-Available\bin\idea64.exe";
        _files.Add($@"{blocked}\bin\idea64.exe");
        _files.Add(expected);
        _files.InaccessibleRoots.Add(blocked);

        Assert.AreEqual(expected, _resolver.Resolve(WorkspaceTarget.IntelliJIdea)?.Path);
    }

    [TestMethod]
    public void ResolveIdea_ContinuesToolboxScanAfterInaccessibleDescendant()
    {
        var blocked =
            @"C:\Users\Me\AppData\Local\JetBrains\Toolbox\apps\A-Blocked";
        _files.AddText($@"{blocked}\product-info.json", "{}");
        _files.AddText(
            @"C:\Users\Me\AppData\Local\JetBrains\Toolbox\apps\B-Available\product-info.json",
            """
            {
              "name": "IntelliJ IDEA Ultimate",
              "version": "2026.2",
              "launch": [{ "launcherPath": "bin\\idea64.exe" }]
            }
            """);
        var expected =
            @"C:\Users\Me\AppData\Local\JetBrains\Toolbox\apps\B-Available\bin\idea64.exe";
        _files.Add(expected);
        _files.InaccessibleRoots.Add(blocked);

        Assert.AreEqual(expected, _resolver.Resolve(WorkspaceTarget.IntelliJIdea)?.Path);
    }

    [TestMethod]
    public void Resolve_CachesPresentAndMissingResultsUntilRefresh()
    {
        Assert.IsNull(_resolver.Resolve(WorkspaceTarget.Terminal));

        var alias = @"C:\Users\Me\AppData\Local\Microsoft\WindowsApps\wt.exe";
        _files.Add(alias);
        Assert.IsNull(_resolver.Resolve(WorkspaceTarget.Terminal));

        _resolver.Refresh();
        Assert.AreEqual(alias, _resolver.Resolve(WorkspaceTarget.Terminal)?.Path);

        _files.Remove(alias);
        Assert.AreEqual(alias, _resolver.Resolve(WorkspaceTarget.Terminal)?.Path);

        _resolver.Refresh();
        Assert.IsNull(_resolver.Resolve(WorkspaceTarget.Terminal));
    }

    [TestMethod]
    public void Resolve_IgnoresInaccessibleOptionalSourcesAndNeverReturnsMissingFiles()
    {
        _registry.ThrowOnRead = true;
        _files.InaccessibleRoots.Add(@"C:\Users\Me\AppData\Local\JetBrains\Toolbox\apps");
        _files.InaccessibleRoots.Add(@"C:\Program Files\JetBrains");

        Assert.IsNull(_resolver.Resolve(WorkspaceTarget.IntelliJIdea));
        Assert.IsNull(_resolver.Resolve(WorkspaceTarget.VisualStudioCode));
    }

    private sealed class FakeWindowsEnvironment : IWindowsEnvironment
    {
        public IReadOnlyList<string> PathEntries { get; set; } = [];

        public string? LocalAppData { get; set; }

        public string? ProgramFiles { get; set; }

        public string? ProgramFilesX86 { get; set; }
    }

    private sealed class FakeWindowsFileSystem : IWindowsFileSystem
    {
        private readonly HashSet<string> _files = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _contents = new(StringComparer.OrdinalIgnoreCase);

        public HashSet<string> InaccessibleRoots { get; } = new(StringComparer.OrdinalIgnoreCase);

        public void Add(string path)
        {
            _files.Add(path);
        }

        public void AddText(string path, string contents)
        {
            Add(path);
            _contents[path] = contents;
        }

        public void Remove(string path)
        {
            _files.Remove(path);
            _contents.Remove(path);
        }

        public string Combine(params string[] paths)
        {
            var nonEmpty = paths.Where(path => !string.IsNullOrWhiteSpace(path)).ToArray();
            if (nonEmpty.Length == 0)
            {
                return string.Empty;
            }

            return string.Join(
                '\\',
                nonEmpty.Select((path, index) => index == 0
                    ? path.TrimEnd('\\', '/')
                    : path.Trim('\\', '/')));
        }

        public IEnumerable<string> EnumerateDirectories(string path)
        {
            ThrowIfInaccessible(path);
            var prefix = path.TrimEnd('\\', '/') + "\\";
            return _files
                .Where(file => file.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .Select(file => GetRelativePath(prefix, file))
                .Where(relativePath => relativePath.Contains('\\'))
                .Select(relativePath =>
                    prefix + relativePath[..relativePath.IndexOf('\\')])
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public IEnumerable<string> EnumerateFiles(string path, string searchPattern)
        {
            ThrowIfInaccessible(path);
            var prefix = path.TrimEnd('\\', '/') + "\\";
            return _files
                .Where(file => file.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .Where(file => !GetRelativePath(prefix, file).Contains('\\'))
                .Where(file => string.Equals(
                    GetFileName(file),
                    searchPattern,
                    StringComparison.OrdinalIgnoreCase))
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public bool FileExists(string path)
        {
            ThrowIfInaccessible(path);
            return _files.Contains(path);
        }

        public string? GetDirectoryName(string path)
        {
            var separator = Math.Max(path.LastIndexOf('\\'), path.LastIndexOf('/'));
            return separator < 0 ? null : path[..separator];
        }

        public string ReadAllText(string path)
        {
            ThrowIfInaccessible(path);
            return _contents[path];
        }

        private static string GetFileName(string path)
        {
            var separator = Math.Max(path.LastIndexOf('\\'), path.LastIndexOf('/'));
            return separator < 0 ? path : path[(separator + 1)..];
        }

        private static string GetRelativePath(string prefix, string file)
        {
            return file[prefix.Length..];
        }

        private void ThrowIfInaccessible(string path)
        {
            if (InaccessibleRoots.Any(root => path.StartsWith(root, StringComparison.OrdinalIgnoreCase)))
            {
                throw new UnauthorizedAccessException(path);
            }
        }
    }

    private sealed class FakeRegistryReader : IRegistryReader
    {
        public Dictionary<string, IReadOnlyList<string>> AppPaths { get; } =
            new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<InstalledApplication> Installations { get; set; } = [];

        public bool ThrowOnRead { get; set; }

        public IReadOnlyList<string> GetAppPaths(string executableName)
        {
            ThrowIfRequested();
            return AppPaths.TryGetValue(executableName, out var paths) ? paths : [];
        }

        public IReadOnlyList<InstalledApplication> GetInstalledApplications()
        {
            ThrowIfRequested();
            return Installations;
        }

        private void ThrowIfRequested()
        {
            if (ThrowOnRead)
            {
                throw new UnauthorizedAccessException("Registry is inaccessible.");
            }
        }
    }
}
