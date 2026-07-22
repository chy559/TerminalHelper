using TerminalHelper.Core.Launching;
using TerminalHelper.WindowsPlatform.Discovery;
using TerminalHelper.WindowsPlatform.Launching;

namespace TerminalHelper.WindowsPlatform.Tests.Launching;

[TestClass]
public sealed class WindowsWorkspaceLauncherTests
{
    private FakeTargetExecutableResolver resolver = null!;
    private FakeProcessRunner runner = null!;
    private WindowsWorkspaceLauncher launcher = null!;

    [TestInitialize]
    public void SetUp()
    {
        resolver = new();
        runner = new();
        launcher = new(resolver, new WindowsLaunchRequestFactory(), runner);
    }

    [TestMethod]
    public async Task LaunchAsync_UnavailableTargetDoesNotRunAnyProcess()
    {
        var exception = await Assert.ThrowsAsync<WorkspaceLaunchException>(() =>
            launcher.LaunchAsync([@"C:\\Folder"], WorkspaceTarget.Terminal, CancellationToken.None));

        Assert.Contains(WorkspaceTarget.Terminal.GetDisplayName(), exception.Message);
        Assert.IsEmpty(runner.Requests);
    }

    [TestMethod]
    public async Task LaunchAsync_RunsGeneratedRequestsInOrder()
    {
        resolver.Executables[WorkspaceTarget.Terminal] =
            new(WorkspaceTarget.Terminal, @"C:\\Tools\\wt.exe");

        await launcher.LaunchAsync(
            [@"C:\\First", @"D:\\Second"],
            WorkspaceTarget.Terminal,
            CancellationToken.None);

        Assert.AreEqual(2, runner.Requests.Count);
        CollectionAssert.AreEqual(
            new[] { "-w", "new", "-d", @"C:\\First" },
            runner.Requests[0].Arguments.ToArray());
        CollectionAssert.AreEqual(
            new[] { "-w", "new", "-d", @"D:\\Second" },
            runner.Requests[1].Arguments.ToArray());
    }

    [TestMethod]
    public async Task LaunchAsync_StopsAfterFirstRunnerFailure()
    {
        resolver.Executables[WorkspaceTarget.IntelliJIdea] =
            new(WorkspaceTarget.IntelliJIdea, @"C:\\JetBrains\\idea64.exe");
        runner.FailureAtRequest = 2;

        await Assert.ThrowsAsync<WorkspaceLaunchException>(() => launcher.LaunchAsync(
            [@"C:\\First", @"C:\\Second", @"C:\\Third"],
            WorkspaceTarget.IntelliJIdea,
            CancellationToken.None));

        Assert.AreEqual(1, runner.Requests.Count);
    }

    [TestMethod]
    public async Task LaunchAsync_ConvertsRunnerFailureToTargetSpecificErrorWithoutCommandText()
    {
        resolver.Executables[WorkspaceTarget.VisualStudioCode] =
            new(WorkspaceTarget.VisualStudioCode, @"C:\\VS Code\\Code.exe");
        runner.Failure = new InvalidOperationException(
            "C:\\VS Code\\Code.exe --new-window C:\\Private Folder");

        var exception = await Assert.ThrowsAsync<WorkspaceLaunchException>(() => launcher.LaunchAsync(
            [@"C:\\Private Folder"],
            WorkspaceTarget.VisualStudioCode,
            CancellationToken.None));

        Assert.Contains(WorkspaceTarget.VisualStudioCode.GetDisplayName(), exception.Message);
        Assert.DoesNotContain("Code.exe", exception.Message);
        Assert.DoesNotContain("Private Folder", exception.Message);
    }

    [TestMethod]
    public void IsAvailable_UsesTheResolverOnDemand()
    {
        Assert.IsFalse(launcher.IsAvailable(WorkspaceTarget.Terminal));

        resolver.Executables[WorkspaceTarget.Terminal] =
            new(WorkspaceTarget.Terminal, @"C:\\Tools\\wt.exe");

        Assert.IsTrue(launcher.IsAvailable(WorkspaceTarget.Terminal));
        Assert.AreEqual(2, resolver.ResolveCalls);
    }

    private sealed class FakeTargetExecutableResolver : ITargetExecutableResolver
    {
        public Dictionary<WorkspaceTarget, TargetExecutable> Executables { get; } = [];

        public int ResolveCalls { get; private set; }

        public TargetExecutable? Resolve(WorkspaceTarget target)
        {
            ResolveCalls++;
            return Executables.GetValueOrDefault(target);
        }
    }

    private sealed class FakeProcessRunner : IProcessRunner
    {
        public List<ProcessLaunchRequest> Requests { get; } = [];

        public Exception? Failure { get; set; }

        public int? FailureAtRequest { get; set; }

        public void Start(ProcessLaunchRequest request)
        {
            if (Failure is not null || FailureAtRequest == Requests.Count + 1)
            {
                throw Failure ?? new InvalidOperationException("Runner failed.");
            }

            Requests.Add(request);
        }
    }
}
