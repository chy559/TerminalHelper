# Terminal Helper Design

## Goal

Build a native macOS helper that accepts folders dropped into its window or onto its Dock icon, then opens each folder in a new Terminal.app window with that folder as the working directory.

## Scope

- Target macOS 13 Ventura or later.
- Support only Apple's built-in Terminal.app.
- Accept one or more folders from the app window and the Dock application icon.
- Open one new Terminal window per unique valid folder, preserving input order.
- Do not add terminal selection, history, menu-bar residency, custom shortcuts, or automatic updates in the first release.

## User Experience

The main SwiftUI window contains a large, obvious folder drop area. The area highlights while a supported drag is over it. Dropping folders starts processing immediately. After processing, the window reports how many folders opened and how many failed.

Dragging folders onto the Dock application icon performs the same operation through the same coordinator. Terminal.app is brought forward after the new windows are created.

## Architecture

### DropView

`DropView` owns presentation and window-level drag-and-drop behavior. It converts dropped file representations into file URLs and forwards them to the coordinator. It does not validate paths or launch Terminal itself.

### FolderOpenCoordinator

`FolderOpenCoordinator` is the single entry point for both window and Dock drops. It:

1. preserves input order;
2. removes duplicate standardized paths;
3. checks that each URL exists, is accessible, and is a directory;
4. asks `TerminalLauncher` to open each valid folder;
5. returns an aggregate result that drives user-facing status text.

Valid folders continue to open if another item fails.

### TerminalLauncher

`TerminalLauncher` converts each directory path into a safely shell-quoted `cd -- <path>` command and uses Terminal.app's AppleScript interface to create a new window. Omitting an existing Terminal window target ensures every folder gets its own window. The launcher activates Terminal after issuing the command.

The shell quoting algorithm wraps the path in single quotes and replaces every embedded single quote with the POSIX-safe sequence `'"'"'`. This protects spaces, Chinese characters, quotes, and shell metacharacters from reinterpretation.

### Application Entry Points

The SwiftUI app delegate receives file URLs opened through Finder or the Dock icon and forwards them to `FolderOpenCoordinator`. The app's document type declaration accepts `public.folder`, making folders valid application-drop input.

## Data Flow

1. A drag/drop entry point supplies file URLs.
2. `FolderOpenCoordinator` normalizes, de-duplicates, and validates them.
3. Valid URLs are sent one at a time to `TerminalLauncher`.
4. The coordinator records successes and item-specific failures.
5. Shared observable app state publishes a concise summary to `DropView`.

## Error Handling

- Regular files are rejected with a request to choose a folder.
- Missing or inaccessible folders are skipped and counted as failures.
- A partial batch still opens all valid folders and reports both counts.
- AppleScript failures are converted into readable application errors.
- If macOS denies Automation permission, the app explains that Terminal control must be enabled under System Settings → Privacy & Security → Automation.
- An empty or unsupported drag does not launch Terminal.

## Testing

Automated tests cover:

- preservation of folder order;
- duplicate path removal;
- rejection of regular files and missing paths;
- partial-batch result aggregation;
- shell quoting for spaces, Chinese characters, single quotes, and shell metacharacters;
- AppleScript source construction and error mapping;
- shared processing behavior for both application entry points through the coordinator interface.

The launcher uses an injected script-execution boundary so tests exercise real command and AppleScript construction without opening Terminal.app.

Manual acceptance testing verifies:

- window-level folder drops;
- Dock-icon folder drops;
- one new Terminal window per folder;
- correct working directories;
- first-run Automation permission behavior;
- partial failures and visible status feedback.

## Deliverable

The repository contains an Xcode project for a native SwiftUI macOS application, its unit tests, and brief build/run instructions. The first release uses system styling and a generic application icon.
