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

            if (IsNetworkOrDevicePath(rawPath))
            {
                failures.Add(new(rawPath, FolderInputFailureReason.NetworkOrDevicePathNotAllowed));
                continue;
            }

            string fullPath;
            try
            {
                fullPath = NormalizeTrailingSeparators(pathService.GetFullPath(rawPath));
            }
            catch (Exception error) when (IsInvalidPath(error))
            {
                failures.Add(new(rawPath, FolderInputFailureReason.InvalidPath));
                continue;
            }

            if (IsNetworkOrDevicePath(fullPath))
            {
                failures.Add(new(rawPath, FolderInputFailureReason.NetworkOrDevicePathNotAllowed));
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

    private static bool IsNetworkOrDevicePath(string path)
    {
        return path.Length >= 2
            && IsDirectorySeparator(path[0])
            && IsDirectorySeparator(path[1]);
    }

    private static string NormalizeTrailingSeparators(string path)
    {
        var minimumLength = path.Length >= 3
            && char.IsAsciiLetter(path[0])
            && path[1] == ':'
            && IsDirectorySeparator(path[2])
                ? 3
                : path.Length > 0 && IsDirectorySeparator(path[0])
                    ? 1
                    : 0;
        var length = path.Length;
        while (length > minimumLength && IsDirectorySeparator(path[length - 1]))
        {
            length--;
        }

        return length == path.Length ? path : path[..length];
    }

    private static bool IsDirectorySeparator(char value)
    {
        return value is '\\' or '/';
    }

    private static bool IsInvalidPath(Exception error)
    {
        return error is ArgumentException
            or NotSupportedException
            or PathTooLongException
            or SecurityException;
    }
}
