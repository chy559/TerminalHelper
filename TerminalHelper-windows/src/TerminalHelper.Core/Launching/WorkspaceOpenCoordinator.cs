using System.Collections.Immutable;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using TerminalHelper.Core.Folders;

namespace TerminalHelper.Core.Launching;

public sealed class WorkspaceOpenCoordinator : INotifyPropertyChanged
{
    private readonly object stateGate = new();
    private readonly FolderBatchPlanner folderBatchPlanner;
    private readonly IWorkspaceLauncher launcher;
    private ImmutableArray<string> pendingFolders = [];
    private WorkspaceStatus status = new WorkspaceStatus.Idle();
    private long selectionVersion;
    private long pendingFoldersVersion;
    private bool isLaunchInProgress;
    private WorkspaceTarget? activeLaunchTarget;

    public WorkspaceOpenCoordinator(FolderBatchPlanner folderBatchPlanner, IWorkspaceLauncher launcher)
    {
        this.folderBatchPlanner = folderBatchPlanner ?? throw new ArgumentNullException(nameof(folderBatchPlanner));
        this.launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<string> PendingFolders => pendingFolders;

    public WorkspaceStatus Status => status;

    public string StatusText => GetStatusText(status);

    public bool IsLaunchInProgress => isLaunchInProgress;

    public WorkspaceTarget? ActiveLaunchTarget => activeLaunchTarget;

    public void Receive(IEnumerable<string> rawPaths)
    {
        ArgumentNullException.ThrowIfNull(rawPaths);

        var paths = rawPaths.ToArray();
        if (paths.Length == 0)
        {
            return;
        }

        long receivedSelectionVersion;
        lock (stateGate)
        {
            receivedSelectionVersion = ++selectionVersion;
        }

        var plan = folderBatchPlanner.MakePlan(paths);
        lock (stateGate)
        {
            if (selectionVersion != receivedSelectionVersion)
            {
                return;
            }

            pendingFolders = plan.ValidFolders;
            pendingFoldersVersion = receivedSelectionVersion;
            OnPropertyChanged(nameof(PendingFolders));
            SetStatus(new WorkspaceStatus.Ready(new(pendingFolders.Length, plan.Failures.Length)));
        }
    }

    public void Reset()
    {
        lock (stateGate)
        {
            selectionVersion++;
            pendingFolders = [];
            pendingFoldersVersion = selectionVersion;
            OnPropertyChanged(nameof(PendingFolders));
            SetStatus(new WorkspaceStatus.Idle());
        }
    }

    public bool IsAvailable(WorkspaceTarget target)
    {
        return launcher.IsAvailable(target);
    }

    public async Task LaunchAsync(WorkspaceTarget target, CancellationToken cancellationToken = default)
    {
        lock (stateGate)
        {
            if (isLaunchInProgress)
            {
                return;
            }

            SetLaunchActivity(true, target);
        }

        try
        {
            IReadOnlyList<string> folders;
            long launchSelectionVersion;

            lock (stateGate)
            {
                if (pendingFolders.IsEmpty || pendingFoldersVersion != selectionVersion)
                {
                    return;
                }

                folders = pendingFolders;
                launchSelectionVersion = selectionVersion;

                if (!launcher.IsAvailable(target))
                {
                    SetStatus(new WorkspaceStatus.Failed(
                        target,
                        $"未找到 {target.GetDisplayName()}，请先安装后重试"));
                    return;
                }

                SetStatus(new WorkspaceStatus.Launching(target));
            }

            await Task.Yield();

            try
            {
                await launcher.LaunchAsync(folders, target, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception error)
            {
                lock (stateGate)
                {
                    if (selectionVersion == launchSelectionVersion)
                    {
                        SetStatus(new WorkspaceStatus.Failed(target, error.Message));
                    }
                }

                return;
            }

            lock (stateGate)
            {
                if (selectionVersion != launchSelectionVersion)
                {
                    return;
                }

                pendingFolders = [];
                pendingFoldersVersion = selectionVersion;
                OnPropertyChanged(nameof(PendingFolders));
                SetStatus(new WorkspaceStatus.Completed(target, folders.Count));
            }
        }
        finally
        {
            lock (stateGate)
            {
                SetLaunchActivity(false, null);
            }
        }
    }

    private void SetLaunchActivity(bool inProgress, WorkspaceTarget? target)
    {
        isLaunchInProgress = inProgress;
        activeLaunchTarget = target;
        OnPropertyChanged(nameof(IsLaunchInProgress));
        OnPropertyChanged(nameof(ActiveLaunchTarget));
    }

    private void SetStatus(WorkspaceStatus value)
    {
        status = value;
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(StatusText));
    }

    private static string GetStatusText(WorkspaceStatus currentStatus)
    {
        return currentStatus switch
        {
            WorkspaceStatus.Idle => "拖入文件夹，然后选择打开方式",
            WorkspaceStatus.Ready { Summary.FolderCount: 0 } ready =>
                $"未找到可打开的文件夹（{ready.Summary.InvalidCount} 项无效）",
            WorkspaceStatus.Ready ready when ready.Summary.InvalidCount == 0 =>
                $"已选择 {ready.Summary.FolderCount} 个文件夹",
            WorkspaceStatus.Ready ready =>
                $"已选择 {ready.Summary.FolderCount} 个文件夹，{ready.Summary.InvalidCount} 项无效",
            WorkspaceStatus.Launching launching =>
                $"正在使用 {launching.Target.GetDisplayName()} 打开…",
            WorkspaceStatus.Completed completed =>
                $"已在 {completed.Target.GetDisplayName()} 中打开 {completed.Count} 个文件夹",
            WorkspaceStatus.Failed failed =>
                $"无法使用 {failed.Target.GetDisplayName()} 打开：{failed.Message}",
            _ => throw new ArgumentOutOfRangeException(nameof(currentStatus), currentStatus, null),
        };
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
