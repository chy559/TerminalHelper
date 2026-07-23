using TerminalHelper.Core.Folders;
using TerminalHelper.Core.Launching;
using TerminalHelper.Windows.Input;
using TerminalHelper.Windows.Presentation;

namespace TerminalHelper.WindowsPlatform.Tests.Presentation;

[TestClass]
public sealed class MainWindowViewModelTests
{
    private FakeWorkspaceLauncher launcher = null!;
    private WorkspaceOpenCoordinator coordinator = null!;
    private MainWindowViewModel viewModel = null!;

    [TestInitialize]
    public void Initialize()
    {
        launcher = new FakeWorkspaceLauncher();
        launcher.AvailableTargets.Remove(WorkspaceTarget.IntelliJIdea);
        coordinator = new WorkspaceOpenCoordinator(
            new FolderBatchPlanner(new FakeFolderPathService()),
            launcher);
        viewModel = new MainWindowViewModel(coordinator);
    }

    [TestMethod]
    public void TargetOptions_AreMutuallyExclusiveOrderedActions()
    {
        CollectionAssert.AreEqual(
            new[]
            {
                WorkspaceTarget.Terminal,
                WorkspaceTarget.VisualStudioCode,
                WorkspaceTarget.IntelliJIdea,
            },
            viewModel.TargetOptions.Select(option => option.Target).ToArray());
    }

    [TestMethod]
    public void TargetOptions_ProjectAvailabilityAndMissingText()
    {
        var terminal = viewModel.TargetOptions.Single(option => option.Target == WorkspaceTarget.Terminal);
        var idea = viewModel.TargetOptions.Single(option => option.Target == WorkspaceTarget.IntelliJIdea);

        Assert.IsTrue(terminal.IsAvailable);
        Assert.AreEqual(string.Empty, terminal.AvailabilityText);
        Assert.IsFalse(idea.IsAvailable);
        Assert.AreEqual("未安装", idea.AvailabilityText);
    }

    [TestMethod]
    public async Task LaunchAsync_ProjectsLaunchingTargetProgressAndDisablesActions()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        launcher.PendingLaunch = completion;
        viewModel.Receive(["first"]);

        var launch = viewModel.LaunchAsync(WorkspaceTarget.VisualStudioCode);

        Assert.IsTrue(viewModel.IsLaunching);
        Assert.IsTrue(viewModel.TargetOptions.Single(
            option => option.Target == WorkspaceTarget.VisualStudioCode).IsLaunching);
        Assert.IsFalse(viewModel.TargetOptions.Any(option => option.CanLaunch));

        completion.SetResult();
        await launch;

        Assert.IsFalse(viewModel.IsLaunching);
    }

    [TestMethod]
    public async Task LaunchAsync_ReplacementSelectionStaysDisabledUntilOldLaunchReleasesGate()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        launcher.PendingLaunch = completion;
        viewModel.Receive(["first"]);
        var stateChanges = 0;
        viewModel.StateChanged += (_, _) => stateChanges++;

        var launch = viewModel.LaunchAsync(WorkspaceTarget.VisualStudioCode);
        await launcher.LaunchStarted.Task;
        viewModel.Receive(["second"]);

        Assert.AreEqual("已选择 1 个文件夹", viewModel.StatusText);
        Assert.IsTrue(viewModel.IsLaunching);
        Assert.AreEqual(WorkspaceTarget.VisualStudioCode, viewModel.LaunchingTarget);
        Assert.IsTrue(viewModel.TargetOptions.Single(
            option => option.Target == WorkspaceTarget.VisualStudioCode).IsLaunching);
        Assert.IsFalse(viewModel.TargetOptions.Any(option => option.CanLaunch));

        var changesBeforeCompletion = stateChanges;
        completion.SetResult();
        await launch;

        Assert.IsFalse(viewModel.IsLaunching);
        Assert.IsNull(viewModel.LaunchingTarget);
        Assert.IsFalse(viewModel.TargetOptions.Any(option => option.IsLaunching));
        Assert.IsTrue(viewModel.TargetOptions.Single(
            option => option.Target == WorkspaceTarget.Terminal).CanLaunch);
        Assert.IsTrue(viewModel.TargetOptions.Single(
            option => option.Target == WorkspaceTarget.VisualStudioCode).CanLaunch);
        Assert.IsGreaterThan(changesBeforeCompletion, stateChanges);
    }

    [TestMethod]
    public void Reset_ReturnsTheEmptyState()
    {
        viewModel.Receive(["first"]);

        viewModel.Reset();

        Assert.IsFalse(viewModel.HasSelection);
        Assert.AreEqual("拖入文件夹，然后选择打开方式", viewModel.StatusText);
    }

    [TestMethod]
    public void Receive_StartupPathsUseTheSameCoordinatorInputPath()
    {
        viewModel.Receive(["second"]);
        var directSelection = coordinator.PendingFolders.ToArray();
        viewModel.Reset();

        viewModel.Receive(StartupPathReader.Read(["TerminalHelper.Windows.exe", "second"]));

        CollectionAssert.AreEqual(directSelection, coordinator.PendingFolders.ToArray());
        Assert.IsTrue(viewModel.HasSelection);
    }

    [TestMethod]
    public void CoordinatorChanges_RaiseStateChangedForBindingRefresh()
    {
        var eventCount = 0;
        viewModel.StateChanged += (_, _) => eventCount++;

        viewModel.Receive(["first"]);

        Assert.IsGreaterThan(0, eventCount);
    }

    [TestMethod]
    public void Selection_ProjectsFolderCountForTheWindowHeader()
    {
        viewModel.Receive(["first", "second"]);

        Assert.AreEqual(2, viewModel.SelectedFolderCount);
    }

    [TestMethod]
    public async Task FailedLaunch_ProjectsErrorStatusForTheWindowBrush()
    {
        viewModel.Receive(["first"]);

        await viewModel.LaunchAsync(WorkspaceTarget.IntelliJIdea);

        Assert.IsTrue(viewModel.HasError);
    }

    private sealed class FakeFolderPathService : IFolderPathService
    {
        public string GetFullPath(string path)
        {
            return path switch
            {
                "first" => @"C:\First",
                "second" => @"C:\Second",
                _ => throw new ArgumentException("Unknown test path.", nameof(path)),
            };
        }

        public bool DirectoryExists(string path)
        {
            return true;
        }
    }

    private sealed class FakeWorkspaceLauncher : IWorkspaceLauncher
    {
        public HashSet<WorkspaceTarget> AvailableTargets { get; } =
        [
            WorkspaceTarget.Terminal,
            WorkspaceTarget.VisualStudioCode,
            WorkspaceTarget.IntelliJIdea,
        ];

        public TaskCompletionSource? PendingLaunch { get; set; }

        public TaskCompletionSource LaunchStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool IsAvailable(WorkspaceTarget target)
        {
            return AvailableTargets.Contains(target);
        }

        public async Task LaunchAsync(
            IReadOnlyList<string> folders,
            WorkspaceTarget target,
            CancellationToken cancellationToken)
        {
            LaunchStarted.TrySetResult();
            if (PendingLaunch is not null)
            {
                await PendingLaunch.Task.WaitAsync(cancellationToken);
            }
        }
    }
}
