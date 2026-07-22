using TerminalHelper.Core.Folders;
using TerminalHelper.Core.Launching;

namespace TerminalHelper.Core.Tests.Launching;

[TestClass]
public sealed class WorkspaceOpenCoordinatorTests
{
    private FakeWorkspaceLauncher launcher = null!;
    private WorkspaceOpenCoordinator coordinator = null!;

    [TestInitialize]
    public void Initialize()
    {
        launcher = new FakeWorkspaceLauncher();
        coordinator = new WorkspaceOpenCoordinator(CreatePlanner(), launcher);
    }

    [TestMethod]
    public void Receive_ReplacesExistingSelectionAndReportsInvalidCount()
    {
        coordinator.Receive(["first"]);
        coordinator.Receive(["second", "missing"]);

        CollectionAssert.AreEqual(new[] { @"C:\\Second" }, coordinator.PendingFolders.ToArray());
        Assert.AreEqual(new WorkspaceStatus.Ready(new WorkspaceSummary(1, 1)), coordinator.Status);
        Assert.AreEqual("已选择 1 个文件夹，1 项无效", coordinator.StatusText);
    }

    [TestMethod]
    public async Task LaunchAsync_SuccessClearsSelectionButFailureRetainsIt()
    {
        coordinator.Receive(["first"]);

        await coordinator.LaunchAsync(WorkspaceTarget.Terminal);

        Assert.IsEmpty(coordinator.PendingFolders);
        Assert.AreEqual(new WorkspaceStatus.Completed(WorkspaceTarget.Terminal, 1), coordinator.Status);
        Assert.AreEqual("已在 Terminal 中打开 1 个文件夹", coordinator.StatusText);

        launcher.Error = new WorkspaceLaunchException("boom");
        coordinator.Receive(["second"]);

        await coordinator.LaunchAsync(WorkspaceTarget.VisualStudioCode);

        Assert.AreEqual(1, coordinator.PendingFolders.Count);
        Assert.AreEqual(
            new WorkspaceStatus.Failed(WorkspaceTarget.VisualStudioCode, "boom"),
            coordinator.Status);
        Assert.AreEqual("无法使用 Visual Studio Code 打开：boom", coordinator.StatusText);
    }

    [TestMethod]
    public void IsAvailable_ReturnsLauncherAvailabilityForEachTarget()
    {
        launcher.AvailableTargets.Remove(WorkspaceTarget.IntelliJIdea);

        Assert.IsTrue(coordinator.IsAvailable(WorkspaceTarget.Terminal));
        Assert.IsFalse(coordinator.IsAvailable(WorkspaceTarget.IntelliJIdea));
    }

    [TestMethod]
    public void Receive_EmptyInputLeavesTheExistingSelectionUnchanged()
    {
        coordinator.Receive(["first"]);

        coordinator.Receive([]);

        CollectionAssert.AreEqual(new[] { @"C:\\First" }, coordinator.PendingFolders.ToArray());
        Assert.AreEqual(new WorkspaceStatus.Ready(new WorkspaceSummary(1, 0)), coordinator.Status);
        Assert.AreEqual("已选择 1 个文件夹", coordinator.StatusText);
    }

    [TestMethod]
    public void Receive_AllInvalidInputReportsInvalidCountWithoutPendingFolders()
    {
        coordinator.Receive(["missing", "invalid"]);

        Assert.IsEmpty(coordinator.PendingFolders);
        Assert.AreEqual(new WorkspaceStatus.Ready(new WorkspaceSummary(0, 2)), coordinator.Status);
        Assert.AreEqual("未找到可打开的文件夹（2 项无效）", coordinator.StatusText);
    }

    [TestMethod]
    public void Reset_ClearsSelectionAndReturnsToIdle()
    {
        coordinator.Receive(["first"]);

        coordinator.Reset();

        Assert.IsEmpty(coordinator.PendingFolders);
        Assert.AreEqual(new WorkspaceStatus.Idle(), coordinator.Status);
        Assert.AreEqual("拖入文件夹，然后选择打开方式", coordinator.StatusText);
    }

    [TestMethod]
    public async Task LaunchAsync_SameBatchDoubleInvocationStartsOnlyOneLaunch()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        launcher.PendingLaunch = completion;
        coordinator.Receive(["first"]);

        var firstLaunch = coordinator.LaunchAsync(WorkspaceTarget.Terminal);
        var secondLaunch = coordinator.LaunchAsync(WorkspaceTarget.Terminal);

        Assert.AreEqual(1, launcher.Launches.Count);
        Assert.AreEqual(new WorkspaceStatus.Launching(WorkspaceTarget.Terminal), coordinator.Status);
        Assert.AreEqual("正在使用 Terminal 打开…", coordinator.StatusText);

        completion.SetResult();
        await Task.WhenAll(firstLaunch, secondLaunch);

        Assert.AreEqual(new WorkspaceStatus.Completed(WorkspaceTarget.Terminal, 1), coordinator.Status);
    }

    [TestMethod]
    public async Task LaunchAsync_StaleCompletionDoesNotOverwriteNewSelection()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        launcher.PendingLaunch = completion;
        coordinator.Receive(["first"]);

        var launch = coordinator.LaunchAsync(WorkspaceTarget.Terminal);
        coordinator.Receive(["second"]);
        completion.SetResult();

        await launch;

        CollectionAssert.AreEqual(new[] { @"C:\\Second" }, coordinator.PendingFolders.ToArray());
        Assert.AreEqual(new WorkspaceStatus.Ready(new WorkspaceSummary(1, 0)), coordinator.Status);
        Assert.AreEqual("已选择 1 个文件夹", coordinator.StatusText);
    }

    private static FolderBatchPlanner CreatePlanner()
    {
        return new FolderBatchPlanner(new FakeFolderPathService());
    }

    private sealed class FakeFolderPathService : IFolderPathService
    {
        private static readonly IReadOnlyDictionary<string, string> Paths = new Dictionary<string, string>
        {
            ["first"] = @"C:\\First",
            ["second"] = @"C:\\Second",
            ["missing"] = @"C:\\Missing",
        };

        public string GetFullPath(string path)
        {
            if (path == "invalid")
            {
                throw new ArgumentException("The path is invalid.", nameof(path));
            }

            return Paths[path];
        }

        public bool DirectoryExists(string path)
        {
            return path != @"C:\\Missing";
        }
    }

    private sealed class FakeWorkspaceLauncher : IWorkspaceLauncher
    {
        public HashSet<WorkspaceTarget> AvailableTargets { get; } =
        [WorkspaceTarget.Terminal, WorkspaceTarget.VisualStudioCode, WorkspaceTarget.IntelliJIdea];

        public List<(IReadOnlyList<string> Folders, WorkspaceTarget Target)> Launches { get; } = [];

        public Exception? Error { get; set; }

        public TaskCompletionSource? PendingLaunch { get; set; }

        public bool IsAvailable(WorkspaceTarget target)
        {
            return AvailableTargets.Contains(target);
        }

        public async Task LaunchAsync(
            IReadOnlyList<string> folders,
            WorkspaceTarget target,
            CancellationToken cancellationToken)
        {
            Launches.Add((folders, target));
            if (PendingLaunch is not null)
            {
                await PendingLaunch.Task.WaitAsync(cancellationToken);
            }

            if (Error is not null)
            {
                throw Error;
            }
        }
    }
}
