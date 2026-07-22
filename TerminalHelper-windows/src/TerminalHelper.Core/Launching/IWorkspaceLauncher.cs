namespace TerminalHelper.Core.Launching;

public interface IWorkspaceLauncher
{
    bool IsAvailable(WorkspaceTarget target);

    Task LaunchAsync(
        IReadOnlyList<string> folders,
        WorkspaceTarget target,
        CancellationToken cancellationToken);
}
