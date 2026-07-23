using TerminalHelper.Core.Folders;

namespace TerminalHelper.Core.Tests.Folders;

[TestClass]
public sealed class FolderBatchPlannerTests
{
    [TestMethod]
    public void MakePlan_PreservesValidFolderOrderAndRemovesWindowsCaseDuplicates()
    {
        var paths = new FakeFolderPathService(
            ("first", @"C:\\Work\\First"),
            ("duplicate", @"c:\\work\\first"),
            ("second", @"D:\\项目\\Second"));
        var plan = new FolderBatchPlanner(paths).MakePlan(["first", "duplicate", "second"]);

        CollectionAssert.AreEqual(
            new[] { @"C:\\Work\\First", @"D:\\项目\\Second" },
            plan.ValidFolders.ToArray());
        Assert.IsEmpty(plan.Failures);
    }

    [TestMethod]
    public void MakePlan_ReportsEmptyInvalidAndMissingInputsWithoutThrowing()
    {
        var paths = new FakeFolderPathService(("missing", @"C:\\Missing", false));
        paths.ThrowFor("invalid");
        var plan = new FolderBatchPlanner(paths).MakePlan(["", "invalid", "missing"]);

        CollectionAssert.AreEqual(
            new[]
            {
                FolderInputFailureReason.Empty,
                FolderInputFailureReason.InvalidPath,
                FolderInputFailureReason.MissingOrNotDirectory,
            },
            plan.Failures.Select(failure => failure.Reason).ToArray());
    }

    [TestMethod]
    [DataRow(@"\\server\share")]
    [DataRow("//server/share")]
    [DataRow(@"\\?\C:\Work")]
    [DataRow(@"\\.\C:\Work")]
    public void MakePlan_RejectsNetworkAndDevicePathsBeforePathServiceCalls(string rawPath)
    {
        var paths = new FakeFolderPathService(Array.Empty<(string Input, string FullPath)>());

        var plan = new FolderBatchPlanner(paths).MakePlan([rawPath]);

        Assert.IsEmpty(plan.ValidFolders);
        Assert.AreEqual(1, plan.Failures.Length);
        Assert.AreEqual(
            FolderInputFailureReason.NetworkOrDevicePathNotAllowed,
            plan.Failures[0].Reason);
        Assert.AreEqual(0, paths.GetFullPathCalls);
        Assert.AreEqual(0, paths.DirectoryExistsCalls);
    }

    [TestMethod]
    public void MakePlan_RejectsNetworkPathProducedByNormalizationBeforeExistenceProbe()
    {
        var paths = new FakeFolderPathService(("relative", @"\\server\share"));

        var plan = new FolderBatchPlanner(paths).MakePlan(["relative"]);

        Assert.IsEmpty(plan.ValidFolders);
        Assert.AreEqual(
            FolderInputFailureReason.NetworkOrDevicePathNotAllowed,
            plan.Failures.Single().Reason);
        Assert.AreEqual(1, paths.GetFullPathCalls);
        Assert.AreEqual(0, paths.DirectoryExistsCalls);
    }

    [TestMethod]
    public void MakePlan_AcceptsDriveLetterPathsIncludingMappedDrives()
    {
        var paths = new FakeFolderPathService(("mapped", @"Z:\Work"));

        var plan = new FolderBatchPlanner(paths).MakePlan(["mapped"]);

        CollectionAssert.AreEqual(new[] { @"Z:\Work" }, plan.ValidFolders.ToArray());
        Assert.IsEmpty(plan.Failures);
        Assert.AreEqual(1, paths.DirectoryExistsCalls);
    }

    [TestMethod]
    public void MakePlan_NormalizesTrailingSeparatorsBeforeDeduplication()
    {
        var paths = new FakeFolderPathService(
            ("plain", @"C:\Work"),
            ("trailing", @"C:\Work\"));

        var plan = new FolderBatchPlanner(paths).MakePlan(["plain", "trailing"]);

        CollectionAssert.AreEqual(new[] { @"C:\Work" }, plan.ValidFolders.ToArray());
        Assert.IsEmpty(plan.Failures);
        Assert.AreEqual(1, paths.DirectoryExistsCalls);
    }

    [TestMethod]
    public void MakePlan_DoesNotCorruptDriveOrPosixRootsWhenTrimmingSeparators()
    {
        var paths = new FakeFolderPathService(
            ("drive-root", @"C:\"),
            ("posix-root", "/"));

        var plan = new FolderBatchPlanner(paths).MakePlan(["drive-root", "posix-root"]);

        CollectionAssert.AreEqual(new[] { @"C:\", "/" }, plan.ValidFolders.ToArray());
        Assert.IsEmpty(plan.Failures);
    }

    private sealed class FakeFolderPathService : IFolderPathService
    {
        private readonly Dictionary<string, (string FullPath, bool Exists)> paths =
            new(StringComparer.Ordinal);
        private readonly HashSet<string> invalidInputs = new(StringComparer.Ordinal);

        public int GetFullPathCalls { get; private set; }

        public int DirectoryExistsCalls { get; private set; }

        public FakeFolderPathService(params (string Input, string FullPath)[] entries)
        {
            foreach (var (input, fullPath) in entries)
            {
                paths.Add(input, (fullPath, true));
            }
        }

        public FakeFolderPathService(params (string Input, string FullPath, bool Exists)[] entries)
        {
            foreach (var (input, fullPath, exists) in entries)
            {
                paths.Add(input, (fullPath, exists));
            }
        }

        public string GetFullPath(string path)
        {
            GetFullPathCalls++;
            if (invalidInputs.Contains(path))
            {
                throw new ArgumentException("The path is invalid.", nameof(path));
            }

            return paths[path].FullPath;
        }

        public bool DirectoryExists(string path)
        {
            DirectoryExistsCalls++;
            return paths.Values.First(entry => entry.FullPath == path).Exists;
        }

        public void ThrowFor(string input)
        {
            invalidInputs.Add(input);
        }
    }
}
