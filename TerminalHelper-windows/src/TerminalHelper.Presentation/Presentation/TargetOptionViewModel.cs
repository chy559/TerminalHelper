using TerminalHelper.Core.Launching;

namespace TerminalHelper.Windows.Presentation;

public sealed class TargetOptionViewModel
{
    private readonly MainWindowViewModel owner;

    internal TargetOptionViewModel(
        MainWindowViewModel owner,
        WorkspaceTarget target,
        bool isAvailable)
    {
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
        Target = target;
        IsAvailable = isAvailable;
    }

    public WorkspaceTarget Target { get; }

    public string DisplayName => Target.GetDisplayName();

    public bool IsAvailable { get; }

    public string AvailabilityText => IsAvailable ? string.Empty : "未安装";

    public bool IsLaunching => owner.LaunchingTarget == Target;

    public bool CanLaunch => IsAvailable && owner.HasSelection && !owner.IsLaunching;
}
