namespace TerminalHelper.Core.Folders;

public enum FolderInputFailureReason
{
    Empty,
    InvalidPath,
    MissingOrNotDirectory,
}

public sealed record FolderInputFailure(string Input, FolderInputFailureReason Reason);
