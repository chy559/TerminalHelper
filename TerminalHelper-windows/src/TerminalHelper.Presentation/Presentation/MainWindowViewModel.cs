using TerminalHelper.Core.Launching;

namespace TerminalHelper.Windows.Presentation;

public sealed class MainWindowViewModel
{
    private static readonly WorkspaceTarget[] OrderedTargets =
    [
        WorkspaceTarget.Terminal,
        WorkspaceTarget.VisualStudioCode,
        WorkspaceTarget.IntelliJIdea,
    ];

    private readonly WorkspaceOpenCoordinator coordinator;

    public MainWindowViewModel(WorkspaceOpenCoordinator coordinator)
    {
        this.coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        TargetOptions = OrderedTargets
            .Select(target => new TargetOptionViewModel(this, target, coordinator.IsAvailable(target)))
            .ToArray();
        coordinator.PropertyChanged += (_, _) => StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? StateChanged;

    public bool HasSelection => coordinator.PendingFolders.Count > 0;

    public string StatusText => coordinator.StatusText;

    public bool IsLaunching => coordinator.Status is WorkspaceStatus.Launching;

    public WorkspaceTarget? LaunchingTarget =>
        coordinator.Status is WorkspaceStatus.Launching launching ? launching.Target : null;

    public IReadOnlyList<TargetOptionViewModel> TargetOptions { get; }

    public void Receive(IEnumerable<string> rawPaths)
    {
        coordinator.Receive(rawPaths);
    }

    public void Reset()
    {
        coordinator.Reset();
    }

    public Task LaunchAsync(
        WorkspaceTarget target,
        CancellationToken cancellationToken = default)
    {
        return coordinator.LaunchAsync(target, cancellationToken);
    }
}
