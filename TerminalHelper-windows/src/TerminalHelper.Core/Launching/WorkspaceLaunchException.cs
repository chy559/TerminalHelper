namespace TerminalHelper.Core.Launching;

public sealed class WorkspaceLaunchException : Exception
{
    public WorkspaceLaunchException(string message)
        : base(message)
    {
    }

    public WorkspaceLaunchException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
