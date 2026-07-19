# Terminal Helper Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a native macOS app that opens every dropped folder in its own new Terminal.app window at that working directory.

**Architecture:** A SwiftUI executable receives window and Dock drops and sends both through one `FolderOpenCoordinator`. Pure folder planning and shell/AppleScript construction live behind small protocols so behavior is covered with Swift Testing without launching Terminal. A deterministic build script wraps the release executable in a macOS `.app` bundle.

**Tech Stack:** Swift 6, SwiftUI, AppKit, Foundation, Swift Package Manager, Swift Testing, AppleScript via `NSAppleScript`, macOS 13+

## Global Constraints

- Target macOS 13 Ventura or later.
- Support only Apple's built-in Terminal.app.
- Accept one or more folders from the app window and the Dock application icon.
- Open one new Terminal window per unique valid folder, preserving input order.
- Valid folders continue to open if another item fails.
- Paths with spaces, Chinese characters, quotes, and shell metacharacters must be shell-safe.
- Automation denial must point users to System Settings → Privacy & Security → Automation.
- Do not add terminal selection, history, menu-bar residency, custom shortcuts, or automatic updates.
- Follow strict RED → GREEN → REFACTOR: every production behavior starts with a test that is run and observed failing for the expected reason.
- The package must build without third-party dependencies.
- Use `./scripts/test.sh` as the project-standard test command; it must invoke Swift Testing directly under full Xcode and add the installed Command Line Tools framework/rpaths only when that fallback is detected.

---

## File Structure

- `Package.swift`: macOS executable and test target definition.
- `Sources/TerminalHelper/FolderBatchPlanner.swift`: URL normalization, de-duplication, and folder validation.
- `Sources/TerminalHelper/TerminalLauncher.swift`: shell quoting, AppleScript encoding/execution, and launch error mapping.
- `Sources/TerminalHelper/FolderOpenCoordinator.swift`: aggregate processing and user-facing status state.
- `Sources/TerminalHelper/DropView.swift`: SwiftUI drop target and status presentation.
- `Sources/TerminalHelper/AppDelegate.swift`: Dock/Finder URL delivery and startup buffering.
- `Sources/TerminalHelper/TerminalHelperApp.swift`: application composition root.
- `Tests/TerminalHelperTests/FolderBatchPlannerTests.swift`: planner behavior with real temporary files and folders.
- `Tests/TerminalHelperTests/TerminalLauncherTests.swift`: quoting, script construction, and error mapping.
- `Tests/TerminalHelperTests/FolderOpenCoordinatorTests.swift`: batch success, partial failure, and permission status.
- `Resources/Info.plist`: app identity, folder document type, minimum OS, and Apple Events usage text.
- `scripts/build-app.sh`: release build and `.app` assembly.
- `README.md`: build, run, permission, and acceptance instructions.

### Task 1: Folder Batch Planning

**Files:**
- Create: `Package.swift`
- Create: `scripts/test.sh`
- Create: `Sources/TerminalHelper/FolderBatchPlanner.swift`
- Create: `Tests/TerminalHelperTests/FolderBatchPlannerTests.swift`

**Interfaces:**
- Consumes: `[URL]` from either application entry point.
- Produces: `FolderBatchPlanning.makePlan(for:) -> FolderBatchPlan`, where `FolderBatchPlan` contains ordered `validFolders` and `failures`.

- [ ] **Step 1: Create the package manifest and write failing planner tests**

Create a macOS 13 package named `TerminalHelper`, with an executable target at `Sources/TerminalHelper` and a test target at `Tests/TerminalHelperTests`. Add tests using real temporary filesystem items:

Create `scripts/test.sh` as an executable wrapper around `swift test --enable-swift-testing --disable-xctest`. If `/Library/Developer/CommandLineTools/Library/Developer/Frameworks/Testing.framework` exists, pass that framework path with `-Xswiftc -F`, pass framework and `/Library/Developer/CommandLineTools/Library/Developer/usr/lib` rpaths with `-Xlinker`, and suppress the known CLT deployment-link warning with `-Xlinker -w`. Forward all caller arguments. Without that CLT framework, call Swift Testing directly with the forwarded arguments and no machine-specific flags.

```swift
import Foundation
import Testing
@testable import TerminalHelper

@Suite("Folder batch planner")
struct FolderBatchPlannerTests {
    @Test("preserves order while removing duplicate standardized paths")
    func preservesOrderAndRemovesDuplicates() throws {
        let root = FileManager.default.temporaryDirectory
            .appending(path: UUID().uuidString, directoryHint: .isDirectory)
        let first = root.appending(path: "第一 个", directoryHint: .isDirectory)
        let second = root.appending(path: "second", directoryHint: .isDirectory)
        try FileManager.default.createDirectory(at: first, withIntermediateDirectories: true)
        try FileManager.default.createDirectory(at: second, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: root) }

        let plan = FolderBatchPlanner().makePlan(for: [first, second, first])

        #expect(plan.validFolders == [first.standardizedFileURL, second.standardizedFileURL])
        #expect(plan.failures.isEmpty)
    }

    @Test("rejects regular files and missing paths")
    func rejectsFilesAndMissingPaths() throws {
        let root = FileManager.default.temporaryDirectory
            .appending(path: UUID().uuidString, directoryHint: .isDirectory)
        try FileManager.default.createDirectory(at: root, withIntermediateDirectories: true)
        let file = root.appending(path: "note.txt")
        let missing = root.appending(path: "missing", directoryHint: .isDirectory)
        try Data("x".utf8).write(to: file)
        defer { try? FileManager.default.removeItem(at: root) }

        let plan = FolderBatchPlanner().makePlan(for: [file, missing])

        #expect(plan.validFolders.isEmpty)
        #expect(plan.failures.map(\.reason) == [.notDirectory, .missing])
    }
}
```

- [ ] **Step 2: Run the tests and verify RED**

Run: `./scripts/test.sh --filter FolderBatchPlannerTests`

Expected: compilation fails because `FolderBatchPlanner`, `FolderBatchPlan`, and `FolderInputFailure` do not exist.

- [ ] **Step 3: Implement the minimum planner**

Implement these exact public-within-module interfaces:

```swift
protocol FolderBatchPlanning {
    func makePlan(for urls: [URL]) -> FolderBatchPlan
}

struct FolderBatchPlan: Equatable {
    let validFolders: [URL]
    let failures: [FolderInputFailure]
}

struct FolderInputFailure: Equatable {
    enum Reason: Equatable { case missing, notDirectory, unreadable }
    let url: URL
    let reason: Reason
}

struct FolderBatchPlanner: FolderBatchPlanning {
    let fileManager: FileManager
    init(fileManager: FileManager = .default) { self.fileManager = fileManager }

    func makePlan(for urls: [URL]) -> FolderBatchPlan {
        var seen = Set<String>()
        var valid: [URL] = []
        var failures: [FolderInputFailure] = []

        for original in urls {
            let url = original.standardizedFileURL
            guard seen.insert(url.path).inserted else { continue }
            var isDirectory: ObjCBool = false
            guard fileManager.fileExists(atPath: url.path, isDirectory: &isDirectory) else {
                failures.append(.init(url: url, reason: .missing))
                continue
            }
            guard isDirectory.boolValue else {
                failures.append(.init(url: url, reason: .notDirectory))
                continue
            }
            guard fileManager.isReadableFile(atPath: url.path) else {
                failures.append(.init(url: url, reason: .unreadable))
                continue
            }
            valid.append(url)
        }
        return FolderBatchPlan(validFolders: valid, failures: failures)
    }
}
```

- [ ] **Step 4: Run the focused and full test suite and verify GREEN**

Run: `./scripts/test.sh --filter FolderBatchPlannerTests`, then `./scripts/test.sh`.

Expected: both commands pass with no failures or warnings.

- [ ] **Step 5: Commit**

Run:

```bash
git add Package.swift scripts/test.sh Sources/TerminalHelper/FolderBatchPlanner.swift Tests/TerminalHelperTests/FolderBatchPlannerTests.swift
git commit -m "feat: plan dropped folder batches"
```

### Task 2: Safe Terminal Launching

**Files:**
- Create: `Sources/TerminalHelper/TerminalLauncher.swift`
- Create: `Tests/TerminalHelperTests/TerminalLauncherTests.swift`

**Interfaces:**
- Consumes: one validated directory `URL` from `FolderOpenCoordinator`.
- Produces: `TerminalLaunching.open(directory:) throws`; reports `TerminalLaunchError.automationPermissionDenied` separately from other script failures.

- [ ] **Step 1: Write failing tests for quoting, script construction, and permission mapping**

Use an injected executor that records source and can throw:

```swift
import Foundation
import Testing
@testable import TerminalHelper

@Suite("Terminal launcher")
struct TerminalLauncherTests {
    @Test("quotes paths using POSIX single-quote escaping")
    func quotesPath() {
        #expect(ShellQuoter.quote("/tmp/项目 O'Brien & Co") == "'/tmp/项目 O'\"'\"'Brien & Co'")
    }

    @Test("builds a new-window Terminal script and activates Terminal")
    func buildsScript() throws {
        let executor = RecordingScriptExecutor()
        let launcher = TerminalLauncher(executor: executor)

        try launcher.open(directory: URL(fileURLWithPath: "/tmp/O'Brien"))

        #expect(executor.sources == [
            "tell application \"Terminal\"\n  do script \"cd -- '/tmp/O'\\\"'\\\"'Brien'\"\n  activate\nend tell"
        ])
    }

    @Test("maps Apple event denial to automation permission error")
    func mapsPermissionDenial() {
        let executor = RecordingScriptExecutor(error: ScriptExecutionError(number: -1743, message: "Not authorized"))
        let launcher = TerminalLauncher(executor: executor)

        #expect(throws: TerminalLaunchError.automationPermissionDenied) {
            try launcher.open(directory: URL(fileURLWithPath: "/tmp/project"))
        }
    }
}

private final class RecordingScriptExecutor: ScriptExecuting {
    var sources: [String] = []
    let error: Error?
    init(error: Error? = nil) { self.error = error }
    func execute(source: String) throws {
        sources.append(source)
        if let error { throw error }
    }
}
```

- [ ] **Step 2: Run the tests and verify RED**

Run: `./scripts/test.sh --filter TerminalLauncherTests`

Expected: compilation fails because the launcher, quoter, executor protocol, and launch error types do not exist.

- [ ] **Step 3: Implement safe command and AppleScript construction**

Implement:

```swift
enum ShellQuoter {
    static func quote(_ value: String) -> String {
        "'" + value.replacingOccurrences(of: "'", with: "'\"'\"'") + "'"
    }
}

enum AppleScriptLiteral {
    static func encode(_ value: String) -> String {
        value
            .replacingOccurrences(of: "\\", with: "\\\\")
            .replacingOccurrences(of: "\"", with: "\\\"")
            .replacingOccurrences(of: "\r", with: "\\r")
            .replacingOccurrences(of: "\n", with: "\\n")
    }
}

protocol ScriptExecuting { func execute(source: String) throws }

struct ScriptExecutionError: Error, Equatable {
    let number: Int?
    let message: String
}

struct NSAppleScriptExecutor: ScriptExecuting {
    func execute(source: String) throws {
        guard let script = NSAppleScript(source: source) else {
            throw ScriptExecutionError(number: nil, message: "Unable to create AppleScript")
        }
        var info: NSDictionary?
        script.executeAndReturnError(&info)
        if let info {
            throw ScriptExecutionError(
                number: (info[NSAppleScript.errorNumber] as? NSNumber)?.intValue,
                message: (info[NSAppleScript.errorMessage] as? String) ?? "Terminal automation failed"
            )
        }
    }
}

protocol TerminalLaunching { func open(directory: URL) throws }

enum TerminalLaunchError: Error, Equatable {
    case automationPermissionDenied
    case scriptFailed(String)
}

struct TerminalLauncher: TerminalLaunching {
    let executor: any ScriptExecuting
    init(executor: any ScriptExecuting = NSAppleScriptExecutor()) { self.executor = executor }

    func open(directory: URL) throws {
        let command = "cd -- \(ShellQuoter.quote(directory.path))"
        let source = "tell application \"Terminal\"\n  do script \"\(AppleScriptLiteral.encode(command))\"\n  activate\nend tell"
        do { try executor.execute(source: source) }
        catch let error as ScriptExecutionError where error.number == -1743 {
            throw TerminalLaunchError.automationPermissionDenied
        }
        catch { throw TerminalLaunchError.scriptFailed(error.localizedDescription) }
    }
}
```

- [ ] **Step 4: Run focused tests, then the full suite, and verify GREEN**

Run: `./scripts/test.sh --filter TerminalLauncherTests`, then `./scripts/test.sh`.

Expected: both commands pass with no failures or warnings.

- [ ] **Step 5: Commit**

Run:

```bash
git add Sources/TerminalHelper/TerminalLauncher.swift Tests/TerminalHelperTests/TerminalLauncherTests.swift
git commit -m "feat: launch folders safely in Terminal"
```

### Task 3: Coordinated Batch Status

**Files:**
- Create: `Sources/TerminalHelper/FolderOpenCoordinator.swift`
- Create: `Tests/TerminalHelperTests/FolderOpenCoordinatorTests.swift`

**Interfaces:**
- Consumes: `FolderBatchPlanning` from Task 1 and `TerminalLaunching` from Task 2.
- Produces: `@MainActor FolderOpenCoordinator.open(_:)`, observable `status`, and localized `statusText` for the UI.

- [ ] **Step 1: Write failing tests for success, partial failure, and permission denial**

Create real temporary directories and a controllable launcher. The tests must assert:

```swift
@Test @MainActor
func opensEveryValidFolderAndReportsSuccess() throws {
    let folders = try TemporaryFolders(count: 2)
    defer { folders.remove() }
    let launcher = RecordingTerminalLauncher()
    let coordinator = FolderOpenCoordinator(planner: FolderBatchPlanner(), launcher: launcher)

    coordinator.open(folders.urls)

    #expect(launcher.opened == folders.urls.map(\.standardizedFileURL))
    #expect(coordinator.status == .completed(.init(succeeded: 2, failed: 0)))
    #expect(coordinator.statusText == "已在终端打开 2 个文件夹")
}

@Test @MainActor
func continuesAfterAnItemFails() throws {
    let folders = try TemporaryFolders(count: 2)
    defer { folders.remove() }
    let launcher = RecordingTerminalLauncher(failingPaths: [folders.urls[0].path])
    let coordinator = FolderOpenCoordinator(planner: FolderBatchPlanner(), launcher: launcher)

    coordinator.open(folders.urls)

    #expect(launcher.opened == folders.urls.map(\.standardizedFileURL))
    #expect(coordinator.status == .completed(.init(succeeded: 1, failed: 1)))
    #expect(coordinator.statusText == "已打开 1 个文件夹，1 项失败")
}

@Test @MainActor
func explainsAutomationPermissionDenial() throws {
    let folders = try TemporaryFolders(count: 1)
    defer { folders.remove() }
    let launcher = RecordingTerminalLauncher(error: TerminalLaunchError.automationPermissionDenied)
    let coordinator = FolderOpenCoordinator(planner: FolderBatchPlanner(), launcher: launcher)

    coordinator.open(folders.urls)

    #expect(coordinator.status == .automationPermissionDenied(.init(succeeded: 0, failed: 1)))
    #expect(coordinator.statusText.contains("系统设置 → 隐私与安全性 → 自动化"))
}
```

Add focused tests for an all-invalid batch and an empty input. Test helpers stay in the test file and remove their temporary directories in `defer` blocks.

- [ ] **Step 2: Run the tests and verify RED**

Run: `./scripts/test.sh --filter FolderOpenCoordinatorTests`

Expected: compilation fails because `FolderOpenCoordinator`, `OpenStatus`, and `OpenSummary` do not exist.

- [ ] **Step 3: Implement the minimum observable coordinator**

Implement these types and behavior:

```swift
struct OpenSummary: Equatable {
    let succeeded: Int
    let failed: Int
}

enum OpenStatus: Equatable {
    case idle
    case completed(OpenSummary)
    case automationPermissionDenied(OpenSummary)
}

@MainActor
final class FolderOpenCoordinator: ObservableObject {
    @Published private(set) var status: OpenStatus = .idle
    private let planner: any FolderBatchPlanning
    private let launcher: any TerminalLaunching

    init(planner: any FolderBatchPlanning, launcher: any TerminalLaunching) {
        self.planner = planner
        self.launcher = launcher
    }

    func open(_ urls: [URL]) {
        guard !urls.isEmpty else { return }
        let plan = planner.makePlan(for: urls)
        var succeeded = 0
        var failed = plan.failures.count

        for folder in plan.validFolders {
            do {
                try launcher.open(directory: folder)
                succeeded += 1
            } catch TerminalLaunchError.automationPermissionDenied {
                failed += 1
                status = .automationPermissionDenied(.init(succeeded: succeeded, failed: failed))
                return
            } catch {
                failed += 1
            }
        }
        status = .completed(.init(succeeded: succeeded, failed: failed))
    }

    var statusText: String {
        switch status {
        case .idle: return "拖入文件夹，在终端中打开"
        case let .completed(summary) where summary.failed == 0:
            return "已在终端打开 \(summary.succeeded) 个文件夹"
        case let .completed(summary) where summary.succeeded == 0:
            return "未打开文件夹，请选择文件夹（\(summary.failed) 项失败）"
        case let .completed(summary):
            return "已打开 \(summary.succeeded) 个文件夹，\(summary.failed) 项失败"
        case .automationPermissionDenied:
            return "请前往系统设置 → 隐私与安全性 → 自动化，允许 Terminal Helper 控制终端"
        }
    }
}
```

- [ ] **Step 4: Run focused tests and the full suite and verify GREEN**

Run: `./scripts/test.sh --filter FolderOpenCoordinatorTests`, then `./scripts/test.sh`.

Expected: all tests pass with no failures or warnings.

- [ ] **Step 5: Commit**

Run:

```bash
git add Sources/TerminalHelper/FolderOpenCoordinator.swift Tests/TerminalHelperTests/FolderOpenCoordinatorTests.swift
git commit -m "feat: coordinate terminal opening batches"
```

### Task 4: SwiftUI Drop App and Distributable Bundle

**Files:**
- Create: `Sources/TerminalHelper/DropView.swift`
- Create: `Sources/TerminalHelper/AppDelegate.swift`
- Create: `Sources/TerminalHelper/TerminalHelperApp.swift`
- Create: `Tests/TerminalHelperTests/AppDelegateTests.swift`
- Create: `Resources/Info.plist`
- Create: `scripts/build-app.sh`
- Create: `README.md`

**Interfaces:**
- Consumes: `FolderOpenCoordinator.open(_:)` and `statusText` from Task 3.
- Produces: a window drop target, tested Dock/Finder URL buffering and routing through `FolderOpening`, and `dist/Terminal Helper.app`.

- [ ] **Step 1: Write failing tests for Dock/Finder URL buffering and routing**

Define the wished-for `FolderOpening` seam through the tests:

```swift
import Foundation
import Testing
@testable import TerminalHelper

@Suite("Application URL routing")
struct AppDelegateTests {
    @Test @MainActor
    func buffersURLsUntilAnOpenerIsAttached() {
        let delegate = AppDelegate()
        let opener = RecordingFolderOpener()
        let urls = [URL(fileURLWithPath: "/tmp/first"), URL(fileURLWithPath: "/tmp/second")]

        delegate.route(urls)
        #expect(opener.received.isEmpty)
        delegate.folderOpener = opener

        #expect(opener.received == [urls])
    }

    @Test @MainActor
    func forwardsURLsImmediatelyAfterAttachment() {
        let delegate = AppDelegate()
        let opener = RecordingFolderOpener()
        delegate.folderOpener = opener
        let urls = [URL(fileURLWithPath: "/tmp/project")]

        delegate.route(urls)

        #expect(opener.received == [urls])
    }
}

@MainActor
private final class RecordingFolderOpener: FolderOpening {
    var received: [[URL]] = []
    func open(_ urls: [URL]) { received.append(urls) }
}
```

- [ ] **Step 2: Run the routing tests and verify RED**

Run: `./scripts/test.sh --filter AppDelegateTests`

Expected: compilation fails because `AppDelegate` and `FolderOpening` do not exist.

- [ ] **Step 3: Add the SwiftUI window and Dock entry points**

Implement a `DropView` with a rounded rectangle, `folder.badge.plus` symbol, the coordinator's `statusText`, drag highlighting, and:

```swift
.dropDestination(for: URL.self) { urls, _ in
    coordinator.open(urls)
    return !urls.isEmpty
} isTargeted: { isTargeted = $0 }
```

Implement `@MainActor protocol FolderOpening: AnyObject { func open(_ urls: [URL]) }` and conform `FolderOpenCoordinator` in an extension. Implement `AppDelegate` as `@MainActor final class AppDelegate: NSObject, NSApplicationDelegate`. Its `folderOpener: (any FolderOpening)?` flushes buffered URLs when assigned. Its tested `route(_:)` either buffers or forwards URLs. `application(_:open:)` calls `route(_:)`, so Dock/Finder drops reach the same `coordinator.open(_:)` used by the view.

Compose the app with `@main struct TerminalHelperApp: App`, `@NSApplicationDelegateAdaptor(AppDelegate.self)`, and a `@StateObject` coordinator initialized with `FolderBatchPlanner()` and `TerminalLauncher()`. Assign the coordinator to the delegate on appearance. Give the window a 480×320 default size.

- [ ] **Step 4: Run routing tests and verify GREEN**

Run: `./scripts/test.sh --filter AppDelegateTests`, then `./scripts/test.sh`.

Expected: routing tests and the full suite pass with no failures or warnings.

- [ ] **Step 5: Add bundle metadata and the build script**

Create `Resources/Info.plist` with:

- `CFBundleIdentifier` = `com.local.TerminalHelper`
- `CFBundleDisplayName` = `Terminal Helper`
- `CFBundleExecutable` = `TerminalHelper`
- `LSMinimumSystemVersion` = `13.0`
- `NSAppleEventsUsageDescription` explaining that the app opens dropped folders in Terminal
- one `CFBundleDocumentTypes` entry whose `LSItemContentTypes` contains `public.folder`, role is `Editor`, and rank is `Owner`

Create an executable `scripts/build-app.sh` that:

1. resolves the repository root from the script location;
2. runs `swift build -c release --package-path "$project_root"`;
3. uses `swift build -c release --show-bin-path` to locate `TerminalHelper`;
4. creates `dist/Terminal Helper.app/Contents/MacOS` and `Contents/Resources`;
5. copies the executable and `Info.plist` into the bundle;
6. ad-hoc signs the bundle with `codesign --force --deep --sign -`;
7. prints the absolute bundle path.

- [ ] **Step 6: Document build and acceptance use**

Document exact commands:

```bash
./scripts/test.sh
./scripts/build-app.sh
open "dist/Terminal Helper.app"
```

Explain the first-run Automation prompt, the System Settings recovery path, window drops, Dock icon drops, multi-folder behavior, and the macOS 13 minimum. State that `Package.swift` can be opened directly in Xcode.

- [ ] **Step 7: Verify compilation, tests, metadata, and bundle assembly**

Run:

```bash
./scripts/test.sh
swift build -c release
./scripts/build-app.sh
plutil -lint Resources/Info.plist
plutil -extract CFBundleDocumentTypes xml1 -o - "dist/Terminal Helper.app/Contents/Info.plist"
codesign --verify --deep --strict "dist/Terminal Helper.app"
```

Expected: tests and release build pass, both property lists validate, extracted metadata contains `public.folder`, and code-sign verification exits successfully.

- [ ] **Step 8: Perform non-interactive bundle smoke checks**

Run `test -x "dist/Terminal Helper.app/Contents/MacOS/TerminalHelper"` and `mdls "dist/Terminal Helper.app"`. Confirm the executable exists and Spotlight metadata identifies an application bundle. Do not trigger Terminal automation during automated verification.

- [ ] **Step 9: Commit**

Run:

```bash
git add Sources/TerminalHelper/DropView.swift Sources/TerminalHelper/AppDelegate.swift Sources/TerminalHelper/TerminalHelperApp.swift Tests/TerminalHelperTests/AppDelegateTests.swift Resources/Info.plist scripts/build-app.sh README.md
git commit -m "feat: add native folder drop application"
```

## Final Verification

After all task reviews are clean, run:

```bash
./scripts/test.sh
swift build -c release
./scripts/build-app.sh
plutil -lint Resources/Info.plist
codesign --verify --deep --strict "dist/Terminal Helper.app"
git status --short
```

Expected: all tests pass, the release build succeeds, the app bundle is valid and signed, and only ignored build artifacts are absent from Git status.

Manual product acceptance for the user:

1. Open `dist/Terminal Helper.app`.
2. Drop one folder into the window and confirm a new Terminal window starts there.
3. Drop two folders together and confirm two new Terminal windows.
4. Keep the app in the Dock, then drag a folder onto its Dock icon and confirm another new Terminal window.
5. Drop a file with a folder and confirm the valid folder opens while the app reports one failure.
