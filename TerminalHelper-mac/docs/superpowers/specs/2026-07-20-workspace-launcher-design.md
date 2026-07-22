# Workspace Launcher Selection Design

## Goal

Extend Terminal Helper into a lightweight developer workspace launcher. After users drop one or more project folders, they choose exactly one destination: Terminal, Visual Studio Code, or IntelliJ IDEA.

## User Experience

The app keeps its existing drag-and-drop entry points:

- Drop folders into the main window.
- Drop folders onto the Dock application icon.

Dropping folders no longer launches Terminal immediately. Instead, the window shows the validated folder selection and three large actions:

1. Open in Terminal
2. Open in Visual Studio Code
3. Open in IntelliJ IDEA

Only one action can be chosen for each batch. The app never opens multiple tools from one click. A reset action clears the pending folders and returns to the drop screen.

For multiple folders, the chosen destination receives every unique valid folder in the original order. Invalid items remain excluded and are reflected in the status summary.

## Availability

Terminal is always presented as available because it is a built-in macOS application.

Visual Studio Code is detected through bundle identifier `com.microsoft.VSCode`.

IntelliJ IDEA supports both editions:

- Ultimate: `com.jetbrains.intellij`
- Community: `com.jetbrains.intellij.ce`

If an editor is not installed, its action remains visible but disabled and shows “Not installed.” This makes the supported destinations discoverable without producing a failed launch attempt.

## Architecture

### WorkspaceTarget

`WorkspaceTarget` represents the mutually exclusive destination choices: Terminal, Visual Studio Code, and IntelliJ IDEA. It owns stable display names and editor bundle-identifier candidates.

### WorkspaceLauncher

`WorkspaceLauncher` routes a validated folder batch to exactly one target:

- Terminal delegates each folder to the existing `TerminalLauncher`, preserving one new Terminal window per folder.
- Editor targets use an injected macOS workspace boundary to locate the application and open all selected folder URLs with it.

The application lookup and open operation are isolated behind a protocol so unit tests do not launch external apps.

### WorkspaceOpenCoordinator

The coordinator becomes a two-stage state machine:

1. `receive(_:)` validates, standardizes, de-duplicates, and stores pending folders.
2. `launch(in:)` opens the current pending batch using exactly one selected target.

It publishes pending folders, invalid-item count, target availability, and a concise status. Window drops and Dock/Finder delivery both call the same `receive(_:)` method.

### DropView

The initial screen retains the existing drop area. When pending folders exist, the view switches to a selection panel containing:

- a folder-count summary;
- three large destination buttons with system icons;
- disabled/not-installed editor states;
- a reset action.

The layout uses native SwiftUI controls and remains within the existing compact window.

## Data Flow

1. Window or Dock input supplies file URLs.
2. `FolderBatchPlanner` validates and de-duplicates the URLs.
3. `WorkspaceOpenCoordinator` stores valid folders without launching anything.
4. The view renders the three destination choices and their availability.
5. The user selects one target.
6. `WorkspaceLauncher` opens all pending folders only in that target.
7. The coordinator publishes success/failure status and clears the pending selection after a successful launch.

## Error Handling

- Empty input leaves the current state unchanged.
- All-invalid input shows a folder-selection error and never shows launch choices.
- Mixed input keeps valid folders and reports the invalid count.
- Selecting an unavailable editor is rejected without calling the system open API.
- Terminal Automation denial retains the existing System Settings guidance.
- Editor lookup or launch failures produce a readable failure status while keeping the pending folders available for retry with another target.

## Testing

Automated tests cover:

- dropping folders stores a pending batch without launching;
- duplicates and invalid items are handled by the existing planner contract;
- exactly one selected target receives all pending folders in order;
- Terminal still opens one window per folder;
- VS Code and both IntelliJ bundle identifiers are detected correctly;
- unavailable editors are disabled/rejected without a launch call;
- launch failure keeps the pending batch for retry;
- reset clears pending state;
- Dock and window inputs share the same coordinator entry point.

Live VS Code, IntelliJ IDEA, Terminal, and macOS permission UI are not invoked by automated tests.

## Scope

The first workspace-launcher release supports only Terminal, Visual Studio Code, and IntelliJ IDEA. It does not add simultaneous multi-tool launch, saved defaults, project-specific commands, editor preferences, history, or additional editors.
