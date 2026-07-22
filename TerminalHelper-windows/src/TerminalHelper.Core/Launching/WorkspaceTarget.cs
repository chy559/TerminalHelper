namespace TerminalHelper.Core.Launching;

public enum WorkspaceTarget
{
    Terminal,
    VisualStudioCode,
    IntelliJIdea,
}

public static class WorkspaceTargetExtensions
{
    public static string GetDisplayName(this WorkspaceTarget target)
    {
        return target switch
        {
            WorkspaceTarget.Terminal => "Terminal",
            WorkspaceTarget.VisualStudioCode => "Visual Studio Code",
            WorkspaceTarget.IntelliJIdea => "IntelliJ IDEA",
            _ => throw new ArgumentOutOfRangeException(nameof(target), target, null),
        };
    }
}
