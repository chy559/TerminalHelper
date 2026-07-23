namespace TerminalHelper.Core.Folders;

public enum FolderInputFailureReason
{
    Empty,
    InvalidPath,
    NetworkOrDevicePathNotAllowed,
    MissingOrNotDirectory,
}

public sealed record FolderInputFailure(string Input, FolderInputFailureReason Reason);
