using TerminalHelper.Core.Launching;
using TerminalHelper.WindowsPlatform.Discovery;

namespace TerminalHelper.WindowsPlatform.Launching;

public sealed class WindowsLaunchRequestFactory
{
    public IReadOnlyList<ProcessLaunchRequest> Create(
        TargetExecutable executable,
        IReadOnlyList<string> folders)
    {
        ArgumentNullException.ThrowIfNull(executable);
        ArgumentNullException.ThrowIfNull(folders);

        if (folders.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("文件夹路径不能为空。", nameof(folders));
        }

        return executable.Target switch
        {
            WorkspaceTarget.Terminal => folders
                .Select(folder => new ProcessLaunchRequest(executable.Path, ["-w", "new", "-d", folder]))
                .ToArray(),
            WorkspaceTarget.VisualStudioCode =>
            [new ProcessLaunchRequest(executable.Path, ["--new-window", .. folders])],
            WorkspaceTarget.IntelliJIdea => folders
                .Select(folder => new ProcessLaunchRequest(executable.Path, [folder]))
                .ToArray(),
            _ => throw new ArgumentOutOfRangeException(nameof(executable), executable.Target, null),
        };
    }
}
