using System.Diagnostics;

namespace TerminalHelper.WindowsPlatform.Launching;

public sealed class SystemProcessRunner : IProcessRunner
{
    public void Start(ProcessLaunchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var startInfo = new ProcessStartInfo(request.FileName)
        {
            UseShellExecute = false,
            CreateNoWindow = false,
        };
        foreach (var argument in request.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        if (Process.Start(startInfo) is null)
        {
            throw new InvalidOperationException("进程未能启动");
        }
    }
}
