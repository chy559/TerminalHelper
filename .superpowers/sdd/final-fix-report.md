# Terminal Helper Final Fix Report

## Scope and Starting State

- Reviewed head: `f5988cc057fe83742ff360b4240f9e954efd451c` (`feat: add native folder drop application`)
- Worktree: `/Users/mac/Desktop/TerminalHelper/.worktrees/terminal-helper-app`
- Baseline: `./scripts/test.sh` exited 0 with 12 tests in 4 suites.
- Safety: no app launch command was run; Terminal.app and Automation UI were not triggered.

## Findings Addressed

1. `FolderBatchPlanner` now rejects non-file URLs before `standardizedFileURL` and records the explicit `.notFileURL` reason.
2. Directory validation now requires both readable and executable/searchable access. The regression test uses POSIX mode `0400`, confirms the preconditions, and restores `0700` before cleanup.
3. `TerminalLauncher` now maps non-permission `ScriptExecutionError` values to `.scriptFailed(error.message)` while retaining the dedicated `-1743` Automation-permission mapping and the generic fallback for other error types.
4. `AppleScriptLiteral.encode` has direct coverage for double quotes, backslashes, carriage returns, and newlines in one exact input/output assertion.
5. `AppDelegate` coverage now proves that multiple pre-attachment calls flush once in input order and are not replayed after opener reassignment.
6. `README.md` now states that command-line builds require a Swift 6-compatible Apple Command Line Tools or Xcode toolchain.

## Strict TDD RED Evidence

All three behavior tests were added and run before any production change.

### Finding 1: non-file URL accepted as a local directory

Command: `./scripts/test.sh --filter rejectsNonFileURLWithExistingDirectoryPath`

Exit: 1

Exact output:

```text
Building for debugging...
[0/5] Write sources
[1/5] Write swift-version--1AB21518FC5DEDBE.txt
[3/6] Emitting module TerminalHelperTests
[4/6] Compiling TerminalHelperTests TerminalLauncherTests.swift
[5/6] Compiling TerminalHelperTests FolderBatchPlannerTests.swift
[5/7] Write Objects.LinkFileList
[6/7] Linking TerminalHelperPackageTests
Build complete! (0.71s)
◇ Test run started.
↳ Testing Library Version: 1902
↳ Target Platform: arm64e-apple-macos14.0
◇ Suite "Folder batch planner" started.
◇ Test "rejects non-file URLs before interpreting their paths" started.
✘ Test "rejects non-file URLs before interpreting their paths" recorded an issue at FolderBatchPlannerTests.swift:49:9: Expectation failed: (plan.validFolders → [https://example.invalid/var/folders/lj/ycnmvvkx6dx2nn1y9d5wpt1h0000gn/T/3294ADBC-8CC6-4F1D-936A-07072F23E3AB]).isEmpty → false
✘ Test "rejects non-file URLs before interpreting their paths" recorded an issue at FolderBatchPlannerTests.swift:50:9: Expectation failed: (plan.failures.count → 0) == 1
✘ Test "rejects non-file URLs before interpreting their paths" recorded an issue at FolderBatchPlannerTests.swift:51:9: Expectation failed: (plan.failures.first?.url → nil) == (httpURL → https://example.invalid/var/folders/lj/ycnmvvkx6dx2nn1y9d5wpt1h0000gn/T/3294ADBC-8CC6-4F1D-936A-07072F23E3AB)
✘ Test "rejects non-file URLs before interpreting their paths" recorded an issue at FolderBatchPlannerTests.swift:52:9: Expectation failed: (String(describing: plan.failures.first?.reason) → "nil") == "Optional(TerminalHelper.FolderInputFailure.Reason.notFileURL)"
✘ Test "rejects non-file URLs before interpreting their paths" failed after 0.007 seconds with 4 issues.
✘ Suite "Folder batch planner" failed after 0.007 seconds with 4 issues.
✘ Test run with 1 test in 1 suite failed after 0.007 seconds with 4 issues.
```

Reason: the HTTP URL's path matched an existing temporary directory, and the planner accepted it as valid because it standardized and inspected the path without first checking `isFileURL`.

### Finding 2: readable directory without search permission accepted

Command: `./scripts/test.sh --filter rejectsDirectoryWithoutSearchPermission`

Exit: 1

Exact output:

```text
Building for debugging...
[0/4] Write swift-version--1AB21518FC5DEDBE.txt
Build complete! (0.07s)
◇ Test run started.
↳ Testing Library Version: 1902
↳ Target Platform: arm64e-apple-macos14.0
◇ Suite "Folder batch planner" started.
◇ Test "rejects readable directories without search permission" started.
✘ Test "rejects readable directories without search permission" recorded an issue at FolderBatchPlannerTests.swift:71:9: Expectation failed: (plan.validFolders → [file:///var/folders/lj/ycnmvvkx6dx2nn1y9d5wpt1h0000gn/T/C3E85373-15C3-4931-835D-D8075AF1EAEF/readable-only/]).isEmpty → false
✘ Test "rejects readable directories without search permission" recorded an issue at FolderBatchPlannerTests.swift:72:9: Expectation failed: (plan.failures → []) == ([.init(url: directory.standardizedFileURL, reason: .unreadable)] → [TerminalHelper.FolderInputFailure(url: file:///var/folders/lj/ycnmvvkx6dx2nn1y9d5wpt1h0000gn/T/C3E85373-15C3-4931-835D-D8075AF1EAEF/readable-only/, reason: TerminalHelper.FolderInputFailure.Reason.unreadable)])
± removed [TerminalHelper.FolderInputFailure(url: file:///var/folders/lj/ycnmvvkx6dx2nn1y9d5wpt1h0000gn/T/C3E85373-15C3-4931-835D-D8075AF1EAEF/readable-only/, reason: TerminalHelper.FolderInputFailure.Reason.unreadable)]
✘ Test "rejects readable directories without search permission" failed after 0.001 seconds with 2 issues.
✘ Suite "Folder batch planner" failed after 0.002 seconds with 2 issues.
✘ Test run with 1 test in 1 suite failed after 0.002 seconds with 2 issues.
```

Reason: the mode-`0400` directory satisfied `isReadableFile` but not `isExecutableFile`; the planner checked only readability and incorrectly marked it valid.

### Finding 3: readable ScriptExecutionError message discarded

Command: `./scripts/test.sh --filter preservesScriptFailureMessage`

Exit: 1

Exact output:

```text
Building for debugging...
[0/4] Write swift-version--1AB21518FC5DEDBE.txt
Build complete! (0.07s)
◇ Test run started.
↳ Testing Library Version: 1902
↳ Target Platform: arm64e-apple-macos14.0
◇ Suite "Terminal launcher" started.
◇ Test "preserves readable messages for non-permission script failures" started.
✘ Test "preserves readable messages for non-permission script failures" recorded an issue at TerminalLauncherTests.swift:41:9: Expectation failed: expected error ".scriptFailed("Expected end of line")" of type TerminalLaunchError, but ".scriptFailed("The operation couldn’t be completed. (TerminalHelper.ScriptExecutionError error 1.)")" of type TerminalLaunchError was thrown instead
✘ Test "preserves readable messages for non-permission script failures" failed after 0.002 seconds with 1 issue.
✘ Suite "Terminal launcher" failed after 0.002 seconds with 1 issue.
✘ Test run with 1 test in 1 suite failed after 0.002 seconds with 1 issue.
```

Reason: the broad catch used `localizedDescription`, which synthesized a generic Foundation message for `ScriptExecutionError` instead of using its stored `message`.

## Focused GREEN Results

Individual regression reruns after the minimal production fixes:

- `./scripts/test.sh --filter rejectsNonFileURLWithExistingDirectoryPath` — exit 0; 1 test in 1 suite passed.
- `./scripts/test.sh --filter rejectsDirectoryWithoutSearchPermission` — exit 0; 1 test in 1 suite passed.
- `./scripts/test.sh --filter preservesScriptFailureMessage` — exit 0; 1 test in 1 suite passed.

Required component-focused suites after all test additions:

- `./scripts/test.sh --filter FolderBatchPlannerTests` — exit 0; 4 tests in 1 suite passed.
- `./scripts/test.sh --filter TerminalLauncherTests` — exit 0; 5 tests in 1 suite passed.
- `./scripts/test.sh --filter AppDelegateTests` — exit 0; 3 tests in 1 suite passed.

## Full GREEN and Bundle Validation

- `./scripts/test.sh` — exit 0; 17 tests in 4 suites passed; no warnings or failures.
- `swift build -c release` — exit 0; production build completed.
- `./scripts/build-app.sh` — exit 0; generated and ad-hoc signed `dist/Terminal Helper.app`.
- `plutil -lint Resources/Info.plist` — exit 0; `Resources/Info.plist: OK`.
- `codesign --verify --deep --strict "dist/Terminal Helper.app"` — exit 0 with no diagnostic output.
- `git diff --check` — exit 0 with no diagnostic output.

## Files Changed

- `Sources/TerminalHelper/FolderBatchPlanner.swift`
- `Sources/TerminalHelper/TerminalLauncher.swift`
- `Tests/TerminalHelperTests/FolderBatchPlannerTests.swift`
- `Tests/TerminalHelperTests/TerminalLauncherTests.swift`
- `Tests/TerminalHelperTests/AppDelegateTests.swift`
- `README.md`
- `.superpowers/sdd/final-fix-report.md`

## Self-Review

- Compared every change against all six findings and the approved design.
- Confirmed non-file rejection occurs before standardization and preserves the original rejected URL.
- Confirmed valid file-URL normalization, order, and duplicate removal remain unchanged.
- Confirmed directory accessibility now reflects the read plus execute/search requirements of `cd`.
- Confirmed the permission test restores directory permissions before recursive cleanup.
- Confirmed Apple event error `-1743` still maps to `.automationPermissionDenied` before the general `ScriptExecutionError` catch.
- Confirmed unrelated error types retain the existing `localizedDescription` fallback.
- Confirmed encoder and AppDelegate production behavior was not changed for coverage-only findings.
- Confirmed public scope and architecture remain unchanged; no terminal selection, history, shortcuts, update, or menu-bar features were added.
- Confirmed no Terminal.app launch or Automation permission prompt was triggered during verification.

## Concerns

None. The `.unreadable` planner reason continues to represent inaccessible folders and now includes the lack of directory execute/search permission; this preserves the existing internal failure categorization and public behavior.
