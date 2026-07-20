# Workspace Launcher Selection Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Change folder drops into a two-stage workflow where users select exactly one destination: Terminal, Visual Studio Code, or IntelliJ IDEA.

**Architecture:** `WorkspaceTarget` describes the three choices and editor bundle identifiers. `WorkspaceLauncher` isolates Terminal and `NSWorkspace` operations behind testable protocols. `WorkspaceOpenCoordinator` stores a validated pending batch, exposes availability/status, and launches only after an explicit target selection; both window and Dock drops call its `receive(_:)` entry point.

**Tech Stack:** Swift 6, SwiftUI, AppKit `NSWorkspace`, Swift Testing, macOS 13+

## Global Constraints

- Support exactly Terminal, Visual Studio Code, and IntelliJ IDEA.
- Each pending batch opens in exactly one selected destination; never launch multiple destinations from one action.
- Visual Studio Code bundle identifier is `com.microsoft.VSCode`.
- IntelliJ IDEA bundle identifiers are `com.jetbrains.intellij` and `com.jetbrains.intellij.ce`, in that order.
- Terminal retains one new Terminal window per valid folder.
- Multiple valid folders retain standardized input order and are all sent to the selected destination.
- Missing editors remain visible but disabled and do not invoke the system open API.
- Editor launch failure keeps pending folders available for retry.
- Both window and Dock/Finder inputs call the same `receive(_:)` coordinator method.
- Do not add simultaneous launch, defaults, saved project commands, history, or additional editors.
- Use `./scripts/test.sh` as the project-standard test command.
- Preserve the existing custom application icon and packaging pipeline.

---

### Task 1: Workspace Targets and Destination Launcher

**Files:**
- Create: `Sources/TerminalHelper/WorkspaceTarget.swift`
- Create: `Sources/TerminalHelper/WorkspaceLauncher.swift`
- Create: `Tests/TerminalHelperTests/WorkspaceLauncherTests.swift`

**Interfaces:**
- Consumes: validated `[URL]`, existing `TerminalLaunching`, and injected `ApplicationWorkspaceOpening`.
- Produces: `WorkspaceLaunching.isAvailable(_:)` and `WorkspaceLaunching.launch(folders:in:) async throws`.

- [ ] **Step 1: Write failing launcher tests**

Add Swift Testing cases that assert:

```swift
#expect(WorkspaceTarget.visualStudioCode.bundleIdentifiers == ["com.microsoft.VSCode"])
#expect(WorkspaceTarget.intelliJIDEA.bundleIdentifiers == [
    "com.jetbrains.intellij",
    "com.jetbrains.intellij.ce",
])
```

Use a recording `ApplicationWorkspaceOpening` fake and recording `TerminalLaunching` fake to prove:

```swift
try await launcher.launch(folders: folders, in: .visualStudioCode)
#expect(applicationOpener.opened == [.init(applicationURL: vscodeURL, folders: folders)])
#expect(terminalLauncher.opened.isEmpty)

try await launcher.launch(folders: folders, in: .terminal)
#expect(terminalLauncher.opened == folders)
#expect(applicationOpener.opened.isEmpty)
```

Add cases for IntelliJ Ultimate lookup falling back to Community, unavailable editor rejection without an open call, and propagation of editor open errors.

- [ ] **Step 2: Run focused tests and verify RED**

Run: `./scripts/test.sh --filter WorkspaceLauncherTests`

Expected: compilation fails because `WorkspaceTarget`, `WorkspaceLauncher`, and `ApplicationWorkspaceOpening` do not exist.

- [ ] **Step 3: Implement the target model and launcher**

Create:

```swift
enum WorkspaceTarget: String, CaseIterable, Equatable, Sendable {
    case terminal
    case visualStudioCode
    case intelliJIDEA

    var displayName: String {
        switch self {
        case .terminal: "终端"
        case .visualStudioCode: "Visual Studio Code"
        case .intelliJIDEA: "IntelliJ IDEA"
        }
    }

    var systemImageName: String {
        switch self {
        case .terminal: "terminal"
        case .visualStudioCode: "chevron.left.forwardslash.chevron.right"
        case .intelliJIDEA: "hammer"
        }
    }

    var bundleIdentifiers: [String] {
        switch self {
        case .terminal: []
        case .visualStudioCode: ["com.microsoft.VSCode"]
        case .intelliJIDEA: ["com.jetbrains.intellij", "com.jetbrains.intellij.ce"]
        }
    }
}

@MainActor
protocol ApplicationWorkspaceOpening {
    func applicationURL(forBundleIdentifier identifier: String) -> URL?
    func open(_ folders: [URL], withApplicationAt applicationURL: URL) async throws
}

@MainActor
protocol WorkspaceLaunching {
    func isAvailable(_ target: WorkspaceTarget) -> Bool
    func launch(folders: [URL], in target: WorkspaceTarget) async throws
}
```

The `NSWorkspaceApplicationOpener` adapter must use `urlForApplication(withBundleIdentifier:)` and wrap `open(_:withApplicationAt:configuration:completionHandler:)` in `withCheckedThrowingContinuation`.

`WorkspaceLauncher` returns true for Terminal availability. For editors, it resolves the first installed bundle identifier. Terminal launch loops through folders and calls the existing `TerminalLaunching.open(directory:)`; editor launch makes one system call with the entire ordered folder array. Throw `WorkspaceLaunchError.targetUnavailable(WorkspaceTarget)` without calling open when no editor application is found.

- [ ] **Step 4: Verify GREEN**

Run: `./scripts/test.sh --filter WorkspaceLauncherTests`, then `./scripts/test.sh`.

Expected: all launcher tests and the full existing suite pass with pristine output.

- [ ] **Step 5: Commit**

```bash
git add Sources/TerminalHelper/WorkspaceTarget.swift Sources/TerminalHelper/WorkspaceLauncher.swift Tests/TerminalHelperTests/WorkspaceLauncherTests.swift
git commit -m "feat: add selectable workspace destinations"
```

### Task 2: Two-Stage Workspace Coordinator

**Files:**
- Delete: `Sources/TerminalHelper/FolderOpenCoordinator.swift`
- Create: `Sources/TerminalHelper/WorkspaceOpenCoordinator.swift`
- Delete: `Tests/TerminalHelperTests/FolderOpenCoordinatorTests.swift`
- Create: `Tests/TerminalHelperTests/WorkspaceOpenCoordinatorTests.swift`
- Modify: `Sources/TerminalHelper/AppDelegate.swift`
- Modify: `Tests/TerminalHelperTests/AppDelegateTests.swift`

**Interfaces:**
- Consumes: `FolderBatchPlanning` and `WorkspaceLaunching`.
- Produces: observable pending folders, target availability, `receive(_:)`, `launch(in:) async`, and `reset()`.

- [ ] **Step 1: Write failing coordinator tests**

Add tests that demonstrate the wished-for two-stage behavior:

```swift
coordinator.receive(folders)
#expect(coordinator.pendingFolders == folders.map(\.standardizedFileURL))
#expect(launcher.launches.isEmpty)

await coordinator.launch(in: .visualStudioCode)
#expect(launcher.launches == [.init(target: .visualStudioCode, folders: expectedFolders)])
#expect(coordinator.pendingFolders.isEmpty)
```

Cover all-invalid input, mixed valid/invalid input, unavailable target rejection without a launch, launch failure retaining pending folders, Terminal Automation denial guidance, empty input, and `reset()` returning to idle. Update AppDelegate tests so their fake implements the shared `FolderReceiving` protocol and buffered Dock calls still flush once in order.

- [ ] **Step 2: Run focused tests and verify RED**

Run: `./scripts/test.sh --filter WorkspaceOpenCoordinatorTests`

Expected: compilation fails because `WorkspaceOpenCoordinator` and its pending-state API do not exist.

- [ ] **Step 3: Implement the state machine**

Create these interfaces:

```swift
struct WorkspaceSummary: Equatable {
    let valid: Int
    let invalid: Int
}

enum WorkspaceStatus: Equatable {
    case idle
    case ready(WorkspaceSummary)
    case launching(WorkspaceTarget)
    case completed(target: WorkspaceTarget, count: Int)
    case failed(target: WorkspaceTarget, message: String)
    case automationPermissionDenied
}

@MainActor
protocol FolderReceiving: AnyObject {
    func receive(_ urls: [URL])
}

@MainActor
final class WorkspaceOpenCoordinator: ObservableObject, FolderReceiving {
    @Published private(set) var pendingFolders: [URL] = []
    @Published private(set) var status: WorkspaceStatus = .idle

    func receive(_ urls: [URL])
    func isAvailable(_ target: WorkspaceTarget) -> Bool
    func launch(in target: WorkspaceTarget) async
    func reset()
    var statusText: String { get }
}
```

`receive(_:)` plans and stores folders but never launches. `launch(in:)` guards pending folders and availability, sets `.launching`, awaits one launcher call, then clears pending folders only on success. It maps `TerminalLaunchError.automationPermissionDenied` to the existing System Settings guidance and retains pending folders for every failure.

Replace AppDelegate’s `FolderOpening` property with `folderReceiver: (any FolderReceiving)?`; preserve buffer-before-attachment and exact-once delivery, but call `receive(_:)`.

- [ ] **Step 4: Verify GREEN**

Run: `./scripts/test.sh --filter WorkspaceOpenCoordinatorTests`, `./scripts/test.sh --filter AppDelegateTests`, then `./scripts/test.sh`.

Expected: coordinator and routing suites pass, followed by a clean full suite.

- [ ] **Step 5: Commit**

```bash
git add Sources/TerminalHelper/FolderOpenCoordinator.swift Sources/TerminalHelper/WorkspaceOpenCoordinator.swift Sources/TerminalHelper/AppDelegate.swift Tests/TerminalHelperTests/FolderOpenCoordinatorTests.swift Tests/TerminalHelperTests/WorkspaceOpenCoordinatorTests.swift Tests/TerminalHelperTests/AppDelegateTests.swift
git commit -m "feat: stage folders before workspace launch"
```

### Task 3: Destination Selection Interface

**Files:**
- Modify: `Sources/TerminalHelper/DropView.swift`
- Modify: `Sources/TerminalHelper/TerminalHelperApp.swift`
- Modify: `README.md`

**Interfaces:**
- Consumes: `WorkspaceOpenCoordinator.pendingFolders`, `statusText`, target availability, `launch(in:)`, and `reset()`.
- Produces: initial drop screen and post-drop three-choice selection screen.

- [ ] **Step 1: Compose the application with the new coordinator**

Initialize one `WorkspaceLauncher` from `TerminalLauncher()` and `NSWorkspaceApplicationOpener()`, inject it into `WorkspaceOpenCoordinator`, pass the same coordinator to `DropView`, and assign it to `AppDelegate.folderReceiver` on appearance.

- [ ] **Step 2: Implement the two-state native SwiftUI view**

When `pendingFolders.isEmpty`, retain the existing folder drop area and call `coordinator.receive(urls)`.

When pending folders exist, show:

- `已选择 N 个文件夹` plus invalid count when nonzero;
- three full-width buttons in this order: Terminal, Visual Studio Code, IntelliJ IDEA;
- a system icon and display name for each target;
- `未安装` label and disabled state for unavailable editors;
- progress disabling while `.launching`;
- a secondary `重新选择文件夹` reset button.

Each target button performs only:

```swift
Task { await coordinator.launch(in: target) }
```

The root drop destination remains active in both states so a new drop replaces the pending batch.

- [ ] **Step 3: Update documentation**

Document the new drop-then-select flow, the three supported destinations, VS Code/IDEA installation detection, multiple-folder behavior, and that only one selected destination launches per batch.

- [ ] **Step 4: Run complete verification**

```bash
./scripts/test.sh
swift build -c release
./scripts/build-app.sh
./scripts/verify-icon.sh
plutil -lint Resources/Info.plist
codesign --verify --deep --strict "dist/Terminal Helper.app"
git diff --check
```

Expected: all tests pass, release and app builds succeed, the custom icon remains valid, plist/signature checks pass, and no whitespace errors exist.

- [ ] **Step 5: Commit**

```bash
git add Sources/TerminalHelper/DropView.swift Sources/TerminalHelper/TerminalHelperApp.swift README.md
git commit -m "feat: add workspace destination chooser"
```
