using System.Collections.Immutable;

namespace TerminalHelper.Core.Folders;

public sealed record FolderBatchPlan(
    ImmutableArray<string> ValidFolders,
    ImmutableArray<FolderInputFailure> Failures);
