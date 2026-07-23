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

    public async Task LaunchAsync(
        IReadOnlyList<string> folders,
        WorkspaceTarget target,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var executable = _resolver.Resolve(target);
        if (executable is null)
        {
            throw new WorkspaceLaunchException("未找到可执行文件，请先安装后重试");
        }

        try
        {
            var requests = _requestFactory.Create(executable, folders);
            await Task.Run(
                () =>
                {
                    foreach (var request in requests)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        _processRunner.Start(request);
                    }
                },
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            throw new WorkspaceLaunchException("进程启动失败，请重试");
        }
    }
}
