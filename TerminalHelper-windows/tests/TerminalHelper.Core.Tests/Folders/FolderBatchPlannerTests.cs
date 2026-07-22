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

    private sealed class FakeFolderPathService : IFolderPathService
    {
        private readonly Dictionary<string, (string FullPath, bool Exists)> paths =
            new(StringComparer.Ordinal);
        private readonly HashSet<string> invalidInputs = new(StringComparer.Ordinal);

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
            if (invalidInputs.Contains(path))
            {
                throw new ArgumentException("The path is invalid.", nameof(path));
            }

            return paths[path].FullPath;
        }

        public bool DirectoryExists(string path)
        {
            return paths.Values.Single(entry => entry.FullPath == path).Exists;
        }

        public void ThrowFor(string input)
        {
            invalidInputs.Add(input);
        }
    }
}
