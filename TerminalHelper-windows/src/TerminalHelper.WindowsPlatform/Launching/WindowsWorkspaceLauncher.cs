using TerminalHelper.Core.Launching;
using TerminalHelper.WindowsPlatform.Discovery;

namespace TerminalHelper.WindowsPlatform.Launching;

public sealed class WindowsWorkspaceLauncher : IWorkspaceLauncher
{
    private readonly ITargetExecutableResolver _resolver;
    private readonly WindowsLaunchRequestFactory _requestFactory;
    private readonly IProcessRunner _processRunner;

    public WindowsWorkspaceLauncher(
        ITargetExecutableResolver resolver,
        WindowsLaunchRequestFactory requestFactory,
        IProcessRunner processRunner)
    {
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        _requestFactory = requestFactory ?? throw new ArgumentNullException(nameof(requestFactory));
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
    }

    public bool IsAvailable(WorkspaceTarget target)
    {
        return _resolver.Resolve(target) is not null;
    }

    public Task LaunchAsync(
        IReadOnlyList<string> folders,
        WorkspaceTarget target,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var executable = _resolver.Resolve(target);
        if (executable is null)
        {
            return Task.FromException(new WorkspaceLaunchException(
                $"未找到 {target.GetDisplayName()}，请先安装后重试"));
        }

        try
        {
            foreach (var request in _requestFactory.Create(executable, folders))
            {
                cancellationToken.ThrowIfCancellationRequested();
                _processRunner.Start(request);
            }

            return Task.CompletedTask;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled(cancellationToken);
        }
        catch (Exception exception)
        {
            return Task.FromException(new WorkspaceLaunchException(
                $"无法使用 {target.GetDisplayName()} 打开。",
                exception));
        }
    }
}
