using System.Collections.Immutable;
using System.Security;

namespace TerminalHelper.Core.Folders;

public sealed class FolderBatchPlanner
{
    private readonly IFolderPathService pathService;

    public FolderBatchPlanner(IFolderPathService pathService)
    {
        this.pathService = pathService ?? throw new ArgumentNullException(nameof(pathService));
    }

    public FolderBatchPlan MakePlan(IEnumerable<string> rawPaths)
    {
        ArgumentNullException.ThrowIfNull(rawPaths);

        var valid = new List<string>();
        var failures = new List<FolderInputFailure>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawPath in rawPaths)
        {
            if (string.IsNullOrWhiteSpace(rawPath))
            {
                failures.Add(new(rawPath ?? string.Empty, FolderInputFailureReason.Empty));
                continue;
            }

            string fullPath;
            try
            {
                fullPath = pathService.GetFullPath(rawPath);
            }
            catch (Exception error) when (IsInvalidPath(error))
            {
                failures.Add(new(rawPath, FolderInputFailureReason.InvalidPath));
                continue;
            }

            if (!seen.Add(fullPath))
            {
                continue;
            }

            if (!pathService.DirectoryExists(fullPath))
            {
                failures.Add(new(fullPath, FolderInputFailureReason.MissingOrNotDirectory));
                continue;
            }

            valid.Add(fullPath);
        }

        return new(ImmutableArray.CreateRange(valid), ImmutableArray.CreateRange(failures));
    }

    private static bool IsInvalidPath(Exception error)
    {
        return error is ArgumentException
            or NotSupportedException
            or PathTooLongException
            or SecurityException;
    }
}
