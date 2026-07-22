namespace TerminalHelper.Core.Launching;

public sealed record WorkspaceSummary(int FolderCount, int InvalidCount);

public abstract record WorkspaceStatus
{
    public sealed record Idle : WorkspaceStatus;

    public sealed record Ready(WorkspaceSummary Summary) : WorkspaceStatus;

    public sealed record Launching(WorkspaceTarget Target) : WorkspaceStatus;

    public sealed record Completed(WorkspaceTarget Target, int Count) : WorkspaceStatus;

    public sealed record Failed(WorkspaceTarget Target, string Message) : WorkspaceStatus;
}
