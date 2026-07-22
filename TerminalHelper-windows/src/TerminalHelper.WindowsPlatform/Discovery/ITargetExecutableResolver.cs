using TerminalHelper.Core.Launching;

namespace TerminalHelper.WindowsPlatform.Discovery;

public interface ITargetExecutableResolver
{
    TargetExecutable? Resolve(WorkspaceTarget target);
}
