# Terminal Helper Windows Version Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a portable Windows 11 x64 WinUI 3 application that accepts one or more folders and opens them in exactly one selected target: Windows Terminal, Visual Studio Code, or IntelliJ IDEA.

**Architecture:** Keep all folder planning and launch-state behavior in a platform-neutral `TerminalHelper.Core` library. Put executable discovery and safe process invocation behind testable Windows adapters in `TerminalHelper.WindowsPlatform`; keep the WinUI project limited to presentation, drag/drop, and startup arguments. Publish the unpackaged app as a self-contained `win-x64` directory and zip it in Windows CI.

**Tech Stack:** C# 14, .NET SDK 10.0.302, .NET 10 LTS, WinUI 3, Windows App SDK 2.3.1, MSTest 4.3.2, GitHub Actions.

## Global Constraints

- Support Windows 11 x64 only; target `net10.0-windows10.0.22000.0` for Windows projects.
- Do not modify any file under `TerminalHelper-mac/`.
- Pin `Microsoft.WindowsAppSDK` to `2.3.1`, `MSTest` to `4.3.2`, and the SDK to `10.0.302` with latest-feature roll-forward.
- Keep `TerminalHelper.Core` at `net10.0` with no Windows-only dependency so its tests run on macOS.
- Use `ProcessStartInfo.ArgumentList`, `UseShellExecute = false`, and never invoke `cmd.exe` or concatenate a shell command.
- Preserve input order, de-duplicate paths with `StringComparer.OrdinalIgnoreCase`, clear after complete success, and retain after failure.
- New input replaces the current batch; stale asynchronous completion may not clear or overwrite newer input.
- Do not add third-party MVVM, DI, command-line, or logging libraries.
- Package as an unpackaged, self-contained, portable `win-x64` ZIP plus SHA-256 file.

---

### Task 1: Solution Scaffold and Folder Batch Planning

**Files:**
- Create: `TerminalHelper-windows/global.json`
- Create: `TerminalHelper-windows/Directory.Build.props`
- Create: `TerminalHelper-windows/Directory.Packages.props`
- Create: `TerminalHelper-windows/TerminalHelper.Windows.slnx`
- Create: `TerminalHelper-windows/src/TerminalHelper.Core/TerminalHelper.Core.csproj`
- Create: `TerminalHelper-windows/src/TerminalHelper.Core/Folders/IFolderPathService.cs`
- Create: `TerminalHelper-windows/src/TerminalHelper.Core/Folders/SystemFolderPathService.cs`
- Create: `TerminalHelper-windows/src/TerminalHelper.Core/Folders/FolderInputFailure.cs`
- Create: `TerminalHelper-windows/src/TerminalHelper.Core/Folders/FolderBatchPlan.cs`
- Create: `TerminalHelper-windows/src/TerminalHelper.Core/Folders/FolderBatchPlanner.cs`
- Create: `TerminalHelper-windows/tests/TerminalHelper.Core.Tests/TerminalHelper.Core.Tests.csproj`
- Create: `TerminalHelper-windows/tests/TerminalHelper.Core.Tests/Folders/FolderBatchPlannerTests.cs`

**Interfaces:**
- Produces: `IFolderPathService.GetFullPath(string)`, `IFolderPathService.DirectoryExists(string)`, and `FolderBatchPlanner.MakePlan(IEnumerable<string>) -> FolderBatchPlan`.
- Produces: `FolderBatchPlan.ValidFolders` in first-seen order and `FolderBatchPlan.Failures` with `Empty`, `InvalidPath`, or `MissingOrNotDirectory` reasons.

- [ ] **Step 1: Create pinned build metadata and minimal projects**

Use `global.json` with SDK `10.0.302` and `rollForward: latestFeature`. Set `ManagePackageVersionsCentrally`, nullable reference types, implicit usings, deterministic builds, and warnings-as-errors in `Directory.Build.props`. Pin `MSTest` `4.3.2` and `Microsoft.WindowsAppSDK` `2.3.1` in `Directory.Packages.props`. The test project references `MSTest` and `TerminalHelper.Core`; the solution contains the Core project and Core tests.

- [ ] **Step 2: Write failing planner tests**

Create tests with an in-memory `FakeFolderPathService` proving:

```csharp
[TestMethod]
public void MakePlan_PreservesValidFolderOrderAndRemovesWindowsCaseDuplicates()
{
    var paths = new FakeFolderPathService(
        ("first", @"C:\\Work\\First"),
        ("duplicate", @"c:\\work\\first"),
        ("second", @"D:\\项目\\Second"));
    var plan = new FolderBatchPlanner(paths).MakePlan(["first", "duplicate", "second"]);
    CollectionAssert.AreEqual(
        new[] { @"C:\\Work\\First", @"D:\\项目\\Second" },
        plan.ValidFolders.ToArray());
    Assert.IsEmpty(plan.Failures);
}

[TestMethod]
public void MakePlan_ReportsEmptyInvalidAndMissingInputsWithoutThrowing()
{
    var paths = new FakeFolderPathService(("missing", @"C:\\Missing", false));
    paths.ThrowFor("invalid");
    var plan = new FolderBatchPlanner(paths).MakePlan(["", "invalid", "missing"]);
    CollectionAssert.AreEqual(
        new[] { FolderInputFailureReason.Empty, FolderInputFailureReason.InvalidPath,
                FolderInputFailureReason.MissingOrNotDirectory },
        plan.Failures.Select(failure => failure.Reason).ToArray());
}
```

- [ ] **Step 3: Run the Core tests to verify RED**

Run: `dotnet test tests/TerminalHelper.Core.Tests/TerminalHelper.Core.Tests.csproj --no-restore`

Expected: compilation fails because folder planning types do not exist.

- [ ] **Step 4: Implement the minimal planner**

Implement `SystemFolderPathService` with `Path.GetFullPath` and `Directory.Exists`. In `FolderBatchPlanner`, ignore duplicate normalized paths, catch `ArgumentException`, `NotSupportedException`, `PathTooLongException`, and `SecurityException` as `InvalidPath`, and return immutable array snapshots:

```csharp
public FolderBatchPlan MakePlan(IEnumerable<string> rawPaths)
{
    ArgumentNullException.ThrowIfNull(rawPaths);
    var valid = new List<string>();
    var failures = new List<FolderInputFailure>();
    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach (var rawPath in rawPaths)
    {
        if (string.IsNullOrWhiteSpace(rawPath)) {
            failures.Add(new(rawPath ?? string.Empty, FolderInputFailureReason.Empty));
            continue;
        }
        string fullPath;
        try { fullPath = pathService.GetFullPath(rawPath); }
        catch (Exception error) when (IsInvalidPath(error)) {
            failures.Add(new(rawPath, FolderInputFailureReason.InvalidPath));
            continue;
        }
        if (!seen.Add(fullPath)) { continue; }
        if (!pathService.DirectoryExists(fullPath)) {
            failures.Add(new(fullPath, FolderInputFailureReason.MissingOrNotDirectory));
            continue;
        }
        valid.Add(fullPath);
    }
    return new(valid.ToArray(), failures.ToArray());
}
```

- [ ] **Step 5: Run tests and commit**

Run: `dotnet test tests/TerminalHelper.Core.Tests/TerminalHelper.Core.Tests.csproj`

Expected: all planner tests pass with zero warnings.

Commit: `feat(windows): add folder batch planning core`

---

### Task 2: Launch Coordinator State Machine

**Files:**
- Create: `TerminalHelper-windows/src/TerminalHelper.Core/Launching/WorkspaceTarget.cs`
- Create: `TerminalHelper-windows/src/TerminalHelper.Core/Launching/WorkspaceStatus.cs`
- Create: `TerminalHelper-windows/src/TerminalHelper.Core/Launching/WorkspaceLaunchException.cs`
- Create: `TerminalHelper-windows/src/TerminalHelper.Core/Launching/IWorkspaceLauncher.cs`
- Create: `TerminalHelper-windows/src/TerminalHelper.Core/Launching/WorkspaceOpenCoordinator.cs`
- Create: `TerminalHelper-windows/tests/TerminalHelper.Core.Tests/Launching/WorkspaceOpenCoordinatorTests.cs`

**Interfaces:**
- Consumes: `FolderBatchPlanner.MakePlan(IEnumerable<string>)` from Task 1.
- Produces: `IWorkspaceLauncher.IsAvailable(WorkspaceTarget)` and `LaunchAsync(IReadOnlyList<string>, WorkspaceTarget, CancellationToken)`.
- Produces: observable `WorkspaceOpenCoordinator.PendingFolders`, `Status`, `StatusText`, `Receive`, `Reset`, `IsAvailable`, and `LaunchAsync`.

- [ ] **Step 1: Write failing coordinator state tests**

Use a controllable fake launcher and test these exact transitions:

```csharp
[TestMethod]
public void Receive_ReplacesExistingSelectionAndReportsInvalidCount()
{
    coordinator.Receive(["first"]);
    coordinator.Receive(["second", "missing"]);
    CollectionAssert.AreEqual(new[] { @"C:\\Second" }, coordinator.PendingFolders.ToArray());
    Assert.AreEqual(new WorkspaceStatus.Ready(new WorkspaceSummary(1, 1)), coordinator.Status);
}

[TestMethod]
public async Task LaunchAsync_SuccessClearsSelectionButFailureRetainsIt()
{
    coordinator.Receive(["first"]);
    await coordinator.LaunchAsync(WorkspaceTarget.Terminal);
    Assert.IsEmpty(coordinator.PendingFolders);
    Assert.IsInstanceOfType<WorkspaceStatus.Completed>(coordinator.Status);

    launcher.Error = new WorkspaceLaunchException("boom");
    coordinator.Receive(["second"]);
    await coordinator.LaunchAsync(WorkspaceTarget.VisualStudioCode);
    Assert.AreEqual(1, coordinator.PendingFolders.Count);
    Assert.IsInstanceOfType<WorkspaceStatus.Failed>(coordinator.Status);
}
```

Also test unavailable targets, empty input, all-invalid input, reset, same-batch double invocation, and a new selection arriving while a `TaskCompletionSource` launch is pending.

- [ ] **Step 2: Run the focused tests to verify RED**

Run: `dotnet test tests/TerminalHelper.Core.Tests/TerminalHelper.Core.Tests.csproj --filter WorkspaceOpenCoordinatorTests`

Expected: compilation fails because coordinator types do not exist.

- [ ] **Step 3: Implement target and status models**

Use an enum with `Terminal`, `VisualStudioCode`, and `IntelliJIdea`. Model status as closed records:

```csharp
public abstract record WorkspaceStatus
{
    public sealed record Idle : WorkspaceStatus;
    public sealed record Ready(WorkspaceSummary Summary) : WorkspaceStatus;
    public sealed record Launching(WorkspaceTarget Target) : WorkspaceStatus;
    public sealed record Completed(WorkspaceTarget Target, int Count) : WorkspaceStatus;
    public sealed record Failed(WorkspaceTarget Target, string Message) : WorkspaceStatus;
}
```

Add extension methods returning the exact UI display names `Terminal`, `Visual Studio Code`, and `IntelliJ IDEA`.

- [ ] **Step 4: Implement the coordinator with version and launch guards**

Implement `INotifyPropertyChanged`. `Receive` increments `_selectionVersion`; empty input returns without a change. `LaunchAsync` snapshots folders/version, sets `Launching`, and catches exceptions into `Failed`. Use an interlocked or locked `_launchInProgress` flag so two calls cannot start the same batch. In `finally`, clear the flag; only mutate status or folders after await when the selection version still matches.

Status text must use:

```text
拖入文件夹，然后选择打开方式
未找到可打开的文件夹（N 项无效）
已选择 N 个文件夹
已选择 N 个文件夹，M 项无效
正在使用 TARGET 打开…
已在 TARGET 中打开 N 个文件夹
无法使用 TARGET 打开：MESSAGE
```

- [ ] **Step 5: Run all Core tests and commit**

Run: `dotnet test tests/TerminalHelper.Core.Tests/TerminalHelper.Core.Tests.csproj`

Expected: all Core tests pass, including stale-completion and duplicate-launch tests.

Commit: `feat(windows): add workspace launch coordinator`

---

### Task 3: Windows Executable Discovery

**Files:**
- Create: `TerminalHelper-windows/src/TerminalHelper.WindowsPlatform/TerminalHelper.WindowsPlatform.csproj`
- Create: `TerminalHelper-windows/src/TerminalHelper.WindowsPlatform/Discovery/IWindowsEnvironment.cs`
- Create: `TerminalHelper-windows/src/TerminalHelper.WindowsPlatform/Discovery/IWindowsFileSystem.cs`
- Create: `TerminalHelper-windows/src/TerminalHelper.WindowsPlatform/Discovery/IRegistryReader.cs`
- Create: `TerminalHelper-windows/src/TerminalHelper.WindowsPlatform/Discovery/SystemWindowsEnvironment.cs`
- Create: `TerminalHelper-windows/src/TerminalHelper.WindowsPlatform/Discovery/SystemWindowsFileSystem.cs`
- Create: `TerminalHelper-windows/src/TerminalHelper.WindowsPlatform/Discovery/WindowsRegistryReader.cs`
- Create: `TerminalHelper-windows/src/TerminalHelper.WindowsPlatform/Discovery/TargetExecutable.cs`
- Create: `TerminalHelper-windows/src/TerminalHelper.WindowsPlatform/Discovery/WindowsExecutableResolver.cs`
- Create: `TerminalHelper-windows/tests/TerminalHelper.WindowsPlatform.Tests/TerminalHelper.WindowsPlatform.Tests.csproj`
- Create: `TerminalHelper-windows/tests/TerminalHelper.WindowsPlatform.Tests/Discovery/WindowsExecutableResolverTests.cs`
- Modify: `TerminalHelper-windows/TerminalHelper.Windows.slnx`

**Interfaces:**
- Consumes: `WorkspaceTarget` from Task 2.
- Produces: `TargetExecutable(WorkspaceTarget Target, string Path)` and `WindowsExecutableResolver.Resolve(WorkspaceTarget) -> TargetExecutable?`.
- Produces: injectable environment, filesystem, and registry readers that return values rather than exposing static APIs.

- [ ] **Step 1: Write failing discovery tests**

Build fake environment/filesystem/registry adapters and cover:

```csharp
[TestMethod]
public void ResolveTerminal_PrefersPathThenUsesWindowsAppsAlias()
{
    environment.PathEntries = [@"C:\\Tools"];
    files.Add(@"C:\\Tools\\wt.exe");
    files.Add(@"C:\\Users\\Me\\AppData\\Local\\Microsoft\\WindowsApps\\wt.exe");
    Assert.AreEqual(@"C:\\Tools\\wt.exe", resolver.Resolve(WorkspaceTarget.Terminal)?.Path);
}

[TestMethod]
public void ResolveIdea_PrefersUltimateOverCommunityAcrossRegistryCandidates()
{
    registry.Installations = [
        new("IntelliJ IDEA Community Edition", @"C:\\JetBrains\\IdeaIC"),
        new("IntelliJ IDEA Ultimate", @"D:\\JetBrains\\IdeaIU")];
    files.Add(@"C:\\JetBrains\\IdeaIC\\bin\\idea64.exe");
    files.Add(@"D:\\JetBrains\\IdeaIU\\bin\\idea64.exe");
    Assert.AreEqual(@"D:\\JetBrains\\IdeaIU\\bin\\idea64.exe",
        resolver.Resolve(WorkspaceTarget.IntelliJIdea)?.Path);
}
```

Also cover VS Code user/system paths, App Paths, missing candidates, PATH values with quotes, Toolbox `product-info.json`, malformed JSON, and deterministic IDEA version ordering.

- [ ] **Step 2: Run WindowsPlatform tests to verify RED on Windows**

Run: `dotnet test tests/TerminalHelper.WindowsPlatform.Tests/TerminalHelper.WindowsPlatform.Tests.csproj`

Expected: compilation fails because discovery types do not exist. On macOS, record that this Windows-targeted command requires a Windows runner rather than changing the target framework.

- [ ] **Step 3: Implement candidate collection without process launches**

Implement PATH enumeration first, fixed environment-derived locations second, registry installations third, and JetBrains Toolbox scanning fourth. Strip outer quotes from PATH entries. Verify every returned candidate with `FileExists`. Parse Toolbox `product-info.json` using `System.Text.Json`; ignore inaccessible directories and malformed metadata.

Registry reads are read-only and cover current-user/local-machine, 32/64-bit App Paths and uninstall views. Return simple `InstalledApplication(DisplayName, InstallLocation, DisplayIcon)` records.

- [ ] **Step 4: Implement deterministic target-specific preference rules**

- Terminal: `wt.exe` from PATH, then WindowsApps alias.
- VS Code: `code.exe`, `Code.exe`, `code.cmd` from PATH; user install; 64-bit and 32-bit Program Files; App Paths/registry.
- IDEA: PATH; valid Ultimate candidates newest-first; valid Community candidates newest-first. Prefer `idea64.exe` over `idea.exe`.

Cache resolved values in `ConcurrentDictionary<WorkspaceTarget, TargetExecutable?>` and expose `Refresh()` to clear the cache.

- [ ] **Step 5: Run WindowsPlatform tests and commit**

Run on Windows: `dotnet test tests/TerminalHelper.WindowsPlatform.Tests/TerminalHelper.WindowsPlatform.Tests.csproj`

Expected: all discovery tests pass with no registry writes and zero warnings.

Commit: `feat(windows): discover supported workspace applications`

---

### Task 4: Safe Process Launching

**Files:**
- Create: `TerminalHelper-windows/src/TerminalHelper.WindowsPlatform/Launching/ProcessLaunchRequest.cs`
- Create: `TerminalHelper-windows/src/TerminalHelper.WindowsPlatform/Launching/IProcessRunner.cs`
- Create: `TerminalHelper-windows/src/TerminalHelper.WindowsPlatform/Launching/SystemProcessRunner.cs`
- Create: `TerminalHelper-windows/src/TerminalHelper.WindowsPlatform/Launching/WindowsLaunchRequestFactory.cs`
- Create: `TerminalHelper-windows/src/TerminalHelper.WindowsPlatform/Launching/WindowsWorkspaceLauncher.cs`
- Create: `TerminalHelper-windows/tests/TerminalHelper.WindowsPlatform.Tests/Launching/WindowsLaunchRequestFactoryTests.cs`
- Create: `TerminalHelper-windows/tests/TerminalHelper.WindowsPlatform.Tests/Launching/WindowsWorkspaceLauncherTests.cs`

**Interfaces:**
- Consumes: `IWorkspaceLauncher` and `WorkspaceTarget` from Task 2; `WindowsExecutableResolver` from Task 3.
- Produces: immutable `ProcessLaunchRequest(string FileName, IReadOnlyList<string> Arguments)`.
- Produces: `WindowsWorkspaceLauncher : IWorkspaceLauncher`.

- [ ] **Step 1: Write failing request-factory tests**

Assert exact request sequences:

```csharp
[TestMethod]
public void Create_TerminalCreatesOneNewWindowRequestPerFolder()
{
    var requests = factory.Create(
        new(WorkspaceTarget.Terminal, @"C:\\Tools\\wt.exe"),
        [@"C:\\A & B", @"D:\\项目 (2)"]);
    Assert.AreEqual(2, requests.Count);
    CollectionAssert.AreEqual(new[] { "-w", "new", "-d", @"C:\\A & B" },
        requests[0].Arguments.ToArray());
}

[TestMethod]
public void Create_CodeCreatesOneNewWindowRequestWithAllFolders()
{
    var requests = factory.Create(
        new(WorkspaceTarget.VisualStudioCode, @"C:\\VS Code\\Code.exe"),
        [@"C:\\A's Work", @"D:\\项目"]);
    Assert.AreEqual(1, requests.Count);
    var request = requests[0];
    CollectionAssert.AreEqual(new[] { "--new-window", @"C:\\A's Work", @"D:\\项目" },
        request.Arguments.ToArray());
}
```

Also assert one IDEA request per folder, input-order preservation, empty-folder rejection, and no embedded quoting added around any argument.

- [ ] **Step 2: Write failing launcher tests**

Verify unavailable targets do not call the runner, all requests are run in order, first runner failure stops later requests, and thrown errors become `WorkspaceLaunchException` containing the target display name without leaking a command string.

- [ ] **Step 3: Run focused tests to verify RED**

Run on Windows: `dotnet test tests/TerminalHelper.WindowsPlatform.Tests/TerminalHelper.WindowsPlatform.Tests.csproj --filter "WindowsLaunch"`

Expected: compilation fails because launch types do not exist.

- [ ] **Step 4: Implement request generation and process adapter**

`SystemProcessRunner.Start` must construct:

```csharp
var startInfo = new ProcessStartInfo(request.FileName)
{
    UseShellExecute = false,
    CreateNoWindow = false,
};
foreach (var argument in request.Arguments) {
    startInfo.ArgumentList.Add(argument);
}
if (Process.Start(startInfo) is null) {
    throw new InvalidOperationException("进程未能启动");
}
```

The launcher resolves availability on demand and runs requests sequentially. Do not wait for editor/terminal processes to exit.

- [ ] **Step 5: Run WindowsPlatform tests and commit**

Run on Windows: `dotnet test tests/TerminalHelper.WindowsPlatform.Tests/TerminalHelper.WindowsPlatform.Tests.csproj`

Expected: discovery and launching tests pass with zero warnings.

Commit: `feat(windows): launch folders with safe process arguments`

---

### Task 5: WinUI Presentation Model and Input Adapter

**Files:**
- Create: `TerminalHelper-windows/src/TerminalHelper.Windows/TerminalHelper.Windows.csproj`
- Create: `TerminalHelper-windows/src/TerminalHelper.Windows/app.manifest`
- Create: `TerminalHelper-windows/src/TerminalHelper.Windows/App.xaml`
- Create: `TerminalHelper-windows/src/TerminalHelper.Windows/App.xaml.cs`
- Create: `TerminalHelper-windows/src/TerminalHelper.Windows/Presentation/TargetOptionViewModel.cs`
- Create: `TerminalHelper-windows/src/TerminalHelper.Windows/Presentation/MainWindowViewModel.cs`
- Create: `TerminalHelper-windows/src/TerminalHelper.Windows/Input/StartupPathReader.cs`
- Create: `TerminalHelper-windows/tests/TerminalHelper.WindowsPlatform.Tests/Presentation/MainWindowViewModelTests.cs`
- Create: `TerminalHelper-windows/tests/TerminalHelper.WindowsPlatform.Tests/Input/StartupPathReaderTests.cs`
- Modify: `TerminalHelper-windows/TerminalHelper.Windows.slnx`

**Interfaces:**
- Consumes: `WorkspaceOpenCoordinator` and three target values from Task 2.
- Produces: UI-facing properties `HasSelection`, `StatusText`, `IsLaunching`, `TargetOptions`, `ResetCommand`, and `LaunchCommand` behavior.
- Produces: `StartupPathReader.Read(string[] commandLineArguments)` that removes only the executable at index zero and preserves every subsequent raw argument.

- [ ] **Step 1: Write failing presentation tests**

Verify the three targets remain in Terminal/VS Code/IDEA order, availability text is `未安装`, a launching target shows progress, reset returns the empty state, and command-line paths flow through the same coordinator path as drag/drop.

```csharp
[TestMethod]
public void TargetOptions_AreMutuallyExclusiveOrderedActions()
{
    CollectionAssert.AreEqual(
        new[] { WorkspaceTarget.Terminal, WorkspaceTarget.VisualStudioCode, WorkspaceTarget.IntelliJIdea },
        viewModel.TargetOptions.Select(option => option.Target).ToArray());
}
```

- [ ] **Step 2: Run the focused tests to verify RED on Windows**

Run: `dotnet test tests/TerminalHelper.WindowsPlatform.Tests/TerminalHelper.WindowsPlatform.Tests.csproj --filter "MainWindowViewModelTests|StartupPathReaderTests"`

Expected: compilation fails because presentation/input types do not exist.

- [ ] **Step 3: Implement the WinUI project and presentation model**

Set `<UseWinUI>true</UseWinUI>`, `<WindowsPackageType>None</WindowsPackageType>`, `<WindowsAppSDKSelfContained>true</WindowsAppSDKSelfContained>`, `<RuntimeIdentifier>win-x64</RuntimeIdentifier>`, and `<SelfContained>true</SelfContained>`. Reference Core, WindowsPlatform, and `Microsoft.WindowsAppSDK`.

The view model subscribes to coordinator `PropertyChanged`, projects target availability, and raises a single `StateChanged` event for `MainWindow` to refresh bindings. Keep commands as explicit async methods so exceptions remain owned by the coordinator.

- [ ] **Step 4: Wire application startup arguments**

`App.OnLaunched` creates the resolver, request factory, launcher, planner, coordinator, view model, and `MainWindow`. After activation, call `viewModel.Receive(StartupPathReader.Read(Environment.GetCommandLineArgs()))`. Empty arguments keep the idle state.

- [ ] **Step 5: Run Windows tests and commit**

Run on Windows: `dotnet test tests/TerminalHelper.WindowsPlatform.Tests/TerminalHelper.WindowsPlatform.Tests.csproj`

Expected: presentation and startup-input tests pass.

Commit: `feat(windows): wire WinUI application state`

---

### Task 6: WinUI Window, Drag/Drop, and Accessibility

**Files:**
- Create: `TerminalHelper-windows/src/TerminalHelper.Windows/MainWindow.xaml`
- Create: `TerminalHelper-windows/src/TerminalHelper.Windows/MainWindow.xaml.cs`
- Create: `TerminalHelper-windows/src/TerminalHelper.Windows/Presentation/WorkspaceTargetTemplateSelector.cs`
- Create: `TerminalHelper-windows/docs/manual-test-checklist.md`

**Interfaces:**
- Consumes: `MainWindowViewModel` from Task 5.
- Produces: a 500×440 main window with empty/selected states, root drop target, three target buttons, progress, status, and reset.

- [ ] **Step 1: Add the complete empty and selected visual states**

Create a root `Grid AllowDrop="True"` with 24px outer margin and an 18px rounded `Border`. Use a dashed border drawn by a transparent `Rectangle` with `StrokeDashArray="7,7"`. Put both states in the same panel and toggle their visibility from `HasSelection`.

The exact user-facing copy is:

```text
拖入文件夹
拖入文件夹，然后选择打开方式
已选择 N 个文件夹
选择一个打开方式
Terminal
Visual Studio Code
IntelliJ IDEA
未安装
重新选择文件夹
```

Target rows are 48px high, have a 12px corner radius, target icon, display name, right-side `ProgressRing`/`未安装`/chevron, and a complete-row hit target.

- [ ] **Step 2: Implement drop extraction**

On `DragOver`, accept only `StandardDataFormats.StorageItems`, set `AcceptedOperation = DataPackageOperation.Copy`, and update the targeted visual state. On `Drop`, call `GetStorageItemsAsync()`, select `StorageFolder` items, read `.Path`, and pass the paths to `viewModel.Receive`. Files are omitted by the adapter and therefore never reach the launcher.

- [ ] **Step 3: Implement interactions and accessibility**

Each target button calls `await viewModel.LaunchAsync(target)` and is disabled when unavailable or any launch is active. Set `AutomationProperties.Name` to the display name or `“DISPLAY，未安装”`. Preserve the target order as tab order. Reset is disabled during launch. Handle `PropertyChanged`/`StateChanged` on the UI dispatcher and render failed status in the error text brush.

- [ ] **Step 4: Add a manual UI acceptance checklist**

The checklist must enumerate Windows 11 light/dark theme, 100%/150% scaling, keyboard-only operation, Narrator labels, hover/drop highlight, installed/missing target states, and every path/launch concurrency case from the design.

- [ ] **Step 5: Build on Windows and commit**

Run: `dotnet build TerminalHelper.Windows.slnx -c Release -p:Platform=x64`

Expected: all three production projects and both test projects build with zero warnings.

Commit: `feat(windows): add native folder drop interface`

---

### Task 7: Windows Icon and Portable Packaging

**Files:**
- Create: `TerminalHelper-windows/assets/TerminalHelper-1024.png`
- Create: `TerminalHelper-windows/src/TerminalHelper.Windows/Assets/TerminalHelper.ico`
- Create: `TerminalHelper-windows/src/TerminalHelper.Windows/Assets/Square44x44Logo.png`
- Create: `TerminalHelper-windows/src/TerminalHelper.Windows/Assets/Square150x150Logo.png`
- Create: `TerminalHelper-windows/scripts/build-portable.ps1`
- Create: `TerminalHelper-windows/scripts/verify-portable.ps1`
- Modify: `TerminalHelper-windows/src/TerminalHelper.Windows/TerminalHelper.Windows.csproj`
- Modify: `TerminalHelper-windows/src/TerminalHelper.Windows/app.manifest`

**Interfaces:**
- Consumes: the existing macOS 1024px icon only as a source copied outside `TerminalHelper-mac/`.
- Produces: application icon assets and `artifacts/TerminalHelper-windows-win-x64.zip` plus `.sha256`.

- [ ] **Step 1: Copy and convert icon assets without altering the source**

Copy `TerminalHelper-mac/Resources/AppIcon/TerminalHelper-1024.png` to the Windows assets source. Generate an ICO containing 16, 24, 32, 48, 64, 128, and 256px frames and PNG assets at exact WinUI sizes. Visually inspect the output to confirm the white rounded base, blue folder, and `>_` mark remain sharp.

- [ ] **Step 2: Wire the icon into the executable and window**

Add `<ApplicationIcon>Assets\TerminalHelper.ico</ApplicationIcon>` and include the assets as content. Set the window AppWindow icon from the ICO after obtaining the native window handle. Keep the original 1024px copy as the reproducible source.

- [ ] **Step 3: Write the portable verification script first**

`verify-portable.ps1` accepts `-PublishDirectory`, fails unless `TerminalHelper.Windows.exe` exists, fails if the directory contains `.pdb`, and verifies the app executable plus required Windows App SDK/native runtime files are present. It returns non-zero with a Chinese/English readable error for every failure.

- [ ] **Step 4: Implement the build script**

`build-portable.ps1` removes only its explicit `artifacts/publish/win-x64` output, then runs:

```powershell
dotnet publish src/TerminalHelper.Windows/TerminalHelper.Windows.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:WindowsPackageType=None -p:WindowsAppSDKSelfContained=true `
  -p:DebugType=None -p:DebugSymbols=false `
  -o artifacts/publish/win-x64
```

It calls `verify-portable.ps1`, creates `artifacts/TerminalHelper-windows-win-x64.zip`, computes SHA-256 with `Get-FileHash`, and writes exactly `HASH  TerminalHelper-windows-win-x64.zip` to the `.sha256` file.

- [ ] **Step 5: Publish, verify, and commit on Windows**

Run: `pwsh -File scripts/build-portable.ps1`

Expected: script exits 0, the ZIP contains `TerminalHelper.Windows.exe`, and the checksum verifies against the ZIP.

Commit: `build(windows): package portable self-contained app`

---

### Task 8: Windows CI, Documentation, and Final Regression

**Files:**
- Create: `.github/workflows/windows.yml`
- Replace: `TerminalHelper-windows/README.md`
- Modify: `/Users/mac/Desktop/TerminalHelper/README.md`

**Interfaces:**
- Consumes: all build/test/package commands from Tasks 1–7.
- Produces: repeatable Windows validation and user/developer documentation.

- [ ] **Step 1: Add the Windows workflow**

Use `windows-latest`, `actions/checkout`, and `actions/setup-dotnet` with `dotnet-version: 10.0.302`. Run restore, Release build, both test projects with TRX output, and `scripts/build-portable.ps1`. Upload test results with `if: always()` and upload the ZIP plus `.sha256` only after success. Trigger on pushes and pull requests that touch `TerminalHelper-windows/**`, the workflow, or shared README.

- [ ] **Step 2: Replace the Windows placeholder README**

Document supported OS/architecture, drag-to-window and drag-to-EXE flows, three mutually exclusive targets, portable ZIP usage, target discovery behavior, developer prerequisites, local Core tests, Windows tests/build/package commands, CI artifact names, limitations, and the manual checklist path.

- [ ] **Step 3: Update only the root platform index**

Add Windows status and link to `TerminalHelper-windows/README.md` without changing any file under `TerminalHelper-mac/`.

- [ ] **Step 4: Run the full local and Windows verification matrix**

On macOS with .NET 10 installed:

```bash
dotnet test TerminalHelper-windows/tests/TerminalHelper.Core.Tests/TerminalHelper.Core.Tests.csproj -c Release
git diff --name-only 4e89785..HEAD -- TerminalHelper-mac
```

Expected: Core tests pass; the second command prints nothing.

On Windows 11 x64:

```powershell
dotnet build TerminalHelper-windows/TerminalHelper.Windows.slnx -c Release -p:Platform=x64
dotnet test TerminalHelper-windows/tests/TerminalHelper.Core.Tests/TerminalHelper.Core.Tests.csproj -c Release
dotnet test TerminalHelper-windows/tests/TerminalHelper.WindowsPlatform.Tests/TerminalHelper.WindowsPlatform.Tests.csproj -c Release
pwsh -File TerminalHelper-windows/scripts/build-portable.ps1
```

Expected: zero warnings, all tests pass, and portable artifacts are generated.

- [ ] **Step 5: Perform final diff and documentation checks**

Run:

```bash
rg -n "T[B]D|T[O]DO|implement[ ]later|fill[ ]in" TerminalHelper-windows .github/workflows/windows.yml
git diff --check
git status --short
```

Expected: placeholder scan is empty, diff check is empty, and status contains only intended Windows/root workflow/documentation changes.

- [ ] **Step 6: Commit the delivery configuration**

Commit: `ci(windows): verify and package Terminal Helper`

After the commit, run the `requesting-code-review` workflow, address approved findings through test-first changes, then run `verification-before-completion`. Do not call the Windows version release-ready until the Windows build/test/package job and the manual Windows 11 checklist have both passed.
