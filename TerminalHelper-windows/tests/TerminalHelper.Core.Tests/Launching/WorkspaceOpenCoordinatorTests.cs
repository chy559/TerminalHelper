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

        launcher.Error = new WorkspaceLaunchException("进程启动失败，请重试");
        coordinator.Receive(["second"]);

        await coordinator.LaunchAsync(WorkspaceTarget.VisualStudioCode);

        Assert.AreEqual(1, coordinator.PendingFolders.Count);
        Assert.AreEqual(
            new WorkspaceStatus.Failed(WorkspaceTarget.VisualStudioCode, "进程启动失败，请重试"),
            coordinator.Status);
        Assert.AreEqual(
            "无法使用 Visual Studio Code 打开：进程启动失败，请重试",
            coordinator.StatusText);
    }

    [TestMethod]
    public void IsAvailable_ReturnsLauncherAvailabilityForEachTarget()
    {
        launcher.AvailableTargets.Remove(WorkspaceTarget.IntelliJIdea);

        Assert.IsTrue(coordinator.IsAvailable(WorkspaceTarget.Terminal));
        Assert.IsFalse(coordinator.IsAvailable(WorkspaceTarget.IntelliJIdea));
    }

    [TestMethod]
    public async Task LaunchAsync_UnavailableTargetFailsWithoutLaunchingAndRetainsSelection()
    {
        launcher.AvailableTargets.Remove(WorkspaceTarget.VisualStudioCode);
        coordinator.Receive(["first"]);

        await coordinator.LaunchAsync(WorkspaceTarget.VisualStudioCode);

        Assert.IsEmpty(launcher.Launches);
        CollectionAssert.AreEqual(new[] { @"C:\\First" }, coordinator.PendingFolders.ToArray());
        Assert.AreEqual(
            new WorkspaceStatus.Failed(
                WorkspaceTarget.VisualStudioCode,
                "未找到 Visual Studio Code，请先安装后重试"),
            coordinator.Status);
        Assert.AreEqual(
            "无法使用 Visual Studio Code 打开：未找到 Visual Studio Code，请先安装后重试",
            coordinator.StatusText);
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
        coordinator.Receive(["first"]);

        coordinator.Receive(["file", "invalid"]);

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

        await launcher.LaunchStarted.Task;

        Assert.AreEqual(1, launcher.Launches.Count);
        Assert.AreEqual(new WorkspaceStatus.Launching(WorkspaceTarget.Terminal), coordinator.Status);
        Assert.AreEqual("正在使用 Terminal 打开…", coordinator.StatusText);

        completion.SetResult();
        await Task.WhenAll(firstLaunch, secondLaunch);

        Assert.AreEqual(new WorkspaceStatus.Completed(WorkspaceTarget.Terminal, 1), coordinator.Status);
    }

    [TestMethod]
    public async Task LaunchAsync_PublishesLaunchingForOneUiTurnBeforeSynchronousLauncherRuns()
    {
        var context = new PumpSynchronizationContext();
        var previousContext = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(context);

        try
        {
            coordinator.Receive(["first"]);

            var launch = coordinator.LaunchAsync(WorkspaceTarget.Terminal);

            Assert.AreEqual(new WorkspaceStatus.Launching(WorkspaceTarget.Terminal), coordinator.Status);
            Assert.IsEmpty(launcher.Launches);
            Assert.IsFalse(launch.IsCompleted);

            Assert.IsTrue(context.RunOne());
            await launch;

            Assert.AreEqual(1, launcher.Launches.Count);
            Assert.AreEqual(new WorkspaceStatus.Completed(WorkspaceTarget.Terminal, 1), coordinator.Status);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previousContext);
        }
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

    [TestMethod]
    public async Task LaunchAsync_NewSelectionDoesNotClearLaunchActivityBeforeGateRelease()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        launcher.PendingLaunch = completion;
        coordinator.Receive(["first"]);
        var propertyChanges = new List<string?>();
        coordinator.PropertyChanged += (_, args) => propertyChanges.Add(args.PropertyName);

        var launch = coordinator.LaunchAsync(WorkspaceTarget.VisualStudioCode);
        await launcher.LaunchStarted.Task;
        coordinator.Receive(["second"]);

        Assert.AreEqual(new WorkspaceStatus.Ready(new WorkspaceSummary(1, 0)), coordinator.Status);
        Assert.IsTrue(coordinator.IsLaunchInProgress);
        Assert.AreEqual(WorkspaceTarget.VisualStudioCode, coordinator.ActiveLaunchTarget);

        completion.SetResult();
        await launch;

        Assert.IsFalse(coordinator.IsLaunchInProgress);
        Assert.IsNull(coordinator.ActiveLaunchTarget);
        CollectionAssert.Contains(propertyChanges, nameof(coordinator.IsLaunchInProgress));
        CollectionAssert.Contains(propertyChanges, nameof(coordinator.ActiveLaunchTarget));
        CollectionAssert.AreEqual(new[] { @"C:\\Second" }, coordinator.PendingFolders.ToArray());
        Assert.AreEqual(new WorkspaceStatus.Ready(new WorkspaceSummary(1, 0)), coordinator.Status);
    }

    [TestMethod]
    public async Task Receive_NewSelectionPlanningPreventsOldLaunchCompletionOrRelaunch()
    {
        var paths = new BlockingFolderPathService();
        coordinator = new WorkspaceOpenCoordinator(CreatePlanner(paths), launcher);
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        launcher.PendingLaunch = completion;
        coordinator.Receive(["first"]);

        var oldLaunch = coordinator.LaunchAsync(WorkspaceTarget.Terminal);
        var receivingNewSelection = Task.Run(() => coordinator.Receive(["delayed"]));
        Assert.IsTrue(paths.WaitForDelayedPlanning(TimeSpan.FromSeconds(5)));

        completion.SetResult();
        await oldLaunch;

        CollectionAssert.AreEqual(new[] { @"C:\\First" }, coordinator.PendingFolders.ToArray());
        Assert.IsNotInstanceOfType<WorkspaceStatus.Completed>(coordinator.Status);

        await coordinator.LaunchAsync(WorkspaceTarget.VisualStudioCode);

        Assert.AreEqual(1, launcher.Launches.Count);
        CollectionAssert.AreEqual(new[] { @"C:\\First" }, coordinator.PendingFolders.ToArray());

        paths.CompleteDelayedPlanning();
        await receivingNewSelection;

        CollectionAssert.AreEqual(new[] { @"C:\\Delayed" }, coordinator.PendingFolders.ToArray());
        Assert.AreEqual(new WorkspaceStatus.Ready(new WorkspaceSummary(1, 0)), coordinator.Status);
    }

    private static FolderBatchPlanner CreatePlanner(IFolderPathService? paths = null)
    {
        return new FolderBatchPlanner(paths ?? new FakeFolderPathService());
    }

    private sealed class FakeFolderPathService : IFolderPathService
    {
        private static readonly IReadOnlyDictionary<string, string> Paths = new Dictionary<string, string>
        {
            ["first"] = @"C:\\First",
            ["second"] = @"C:\\Second",
            ["missing"] = @"C:\\Missing",
            ["file"] = @"C:\\notes.txt",
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
            return path is not @"C:\\Missing" and not @"C:\\notes.txt";
        }
    }

    private sealed class FakeWorkspaceLauncher : IWorkspaceLauncher
    {
        public HashSet<WorkspaceTarget> AvailableTargets { get; } =
        [WorkspaceTarget.Terminal, WorkspaceTarget.VisualStudioCode, WorkspaceTarget.IntelliJIdea];

        public List<(IReadOnlyList<string> Folders, WorkspaceTarget Target)> Launches { get; } = [];

        public TaskCompletionSource LaunchStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

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
            LaunchStarted.TrySetResult();
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

    private sealed class BlockingFolderPathService : IFolderPathService
    {
        private readonly ManualResetEventSlim delayedPlanningStarted = new();
        private readonly ManualResetEventSlim completeDelayedPlanning = new();

        public string GetFullPath(string path)
        {
            if (path == "delayed")
            {
                delayedPlanningStarted.Set();
                completeDelayedPlanning.Wait();
                return @"C:\\Delayed";
            }

            return path switch
            {
                "first" => @"C:\\First",
                _ => throw new KeyNotFoundException(path),
            };
        }

        public bool DirectoryExists(string path)
        {
            return true;
        }

        public bool WaitForDelayedPlanning(TimeSpan timeout)
        {
            return delayedPlanningStarted.Wait(timeout);
        }

        public void CompleteDelayedPlanning()
        {
            completeDelayedPlanning.Set();
        }
    }

    private sealed class PumpSynchronizationContext : SynchronizationContext
    {
        private readonly Queue<(SendOrPostCallback Callback, object? State)> callbacks = new();

        public override void Post(SendOrPostCallback callback, object? state)
        {
            callbacks.Enqueue((callback, state));
        }

        public bool RunOne()
        {
            if (!callbacks.TryDequeue(out var callback))
            {
                return false;
            }

            callback.Callback(callback.State);
            return true;
        }
    }
}
