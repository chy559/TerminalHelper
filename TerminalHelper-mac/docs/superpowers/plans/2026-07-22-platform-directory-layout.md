# Platform Directory Layout Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Reorganize the repository into `TerminalHelper-mac/` and `TerminalHelper-windows/` without changing the completed macOS application.

**Architecture:** Keep one Git repository at the workspace root and move every tracked macOS project file into `TerminalHelper-mac/` using Git-aware renames. Add only platform navigation documentation at the root and a placeholder README in `TerminalHelper-windows/`; verify the relocated Swift package and rebuild the packaged application at its new path.

**Tech Stack:** Git, Swift 6, Swift Package Manager, Swift Testing, macOS application packaging scripts

## Global Constraints

- Keep `.git/`, `.gitignore`, `.worktrees/`, and repository-wide metadata at the repository root.
- Move the current macOS source, tests, resources, scripts, documentation, package manifest, README, build cache, and packaged application into `TerminalHelper-mac/`.
- Do not edit macOS Swift source, tests, resources, Info.plist, icon assets, or build scripts.
- Create `TerminalHelper-windows/` only as a future-development boundary; do not implement Windows functionality in this migration.
- Preserve tracked-file history with Git-aware renames.
- Preserve existing root files that are unrelated to Terminal Helper.
- Run every macOS build and test command from `TerminalHelper-mac/` after migration.

---

### Task 1: Establish the Dual-Platform Repository Layout

**Files:**
- Move: `Package.swift` → `TerminalHelper-mac/Package.swift`
- Move: `README.md` → `TerminalHelper-mac/README.md`
- Move: `Sources/` → `TerminalHelper-mac/Sources/`
- Move: `Tests/` → `TerminalHelper-mac/Tests/`
- Move: `Resources/` → `TerminalHelper-mac/Resources/`
- Move: `scripts/` → `TerminalHelper-mac/scripts/`
- Move: `docs/` → `TerminalHelper-mac/docs/`
- Create: `README.md`
- Create: `TerminalHelper-windows/README.md`
- Preserve: `.gitignore`

**Interfaces:**
- Consumes: the existing repository-root Swift package and its current Git history
- Produces: an independently buildable Swift package rooted at `TerminalHelper-mac/` and an isolated `TerminalHelper-windows/` development directory

- [ ] **Step 1: Verify the pre-migration baseline**

Run from the repository root:

```bash
./scripts/test.sh
git status --short
```

Expected: all 30 tests pass; the working tree contains no tracked modifications.

- [ ] **Step 2: Create the platform directories and move tracked macOS files**

Run from the repository root:

```bash
mkdir -p TerminalHelper-mac TerminalHelper-windows
git mv Package.swift README.md Resources Sources Tests scripts docs TerminalHelper-mac/
```

Expected: Git reports the existing project files as moves under `TerminalHelper-mac/`; `.gitignore`, `.git/`, and `.worktrees/` remain at the root.

- [ ] **Step 3: Add the root workspace README**

Create `README.md` with exactly:

```markdown
# Terminal Helper

Terminal Helper 在同一个仓库中分别维护 macOS 和 Windows 版本。

## 项目目录

- [`TerminalHelper-mac/`](TerminalHelper-mac/)：已经完成并可使用的原生 macOS 版本。
- [`TerminalHelper-windows/`](TerminalHelper-windows/)：Windows 版本的独立开发目录。

两个平台项目彼此独立。macOS 版本的构建、测试和使用说明位于 [`TerminalHelper-mac/README.md`](TerminalHelper-mac/README.md)。
```

- [ ] **Step 4: Add the Windows development placeholder**

Create `TerminalHelper-windows/README.md` with exactly:

```markdown
# Terminal Helper for Windows

此目录用于独立开发 Terminal Helper 的 Windows 版本。

Windows 版本将复现 macOS 版本的核心体验，但不会修改或依赖 `TerminalHelper-mac/` 中的 SwiftUI 应用。具体技术方案与实现将在后续 Windows 开发阶段确定。
```

- [ ] **Step 5: Verify the relocated macOS package**

Run:

```bash
cd TerminalHelper-mac
./scripts/test.sh
swift build -c release
./scripts/build-app.sh
./scripts/verify-icon.sh
plutil -lint Resources/Info.plist
codesign --verify --deep --strict --verbose=2 "dist/Terminal Helper.app"
```

Expected: all 30 tests pass, the Release build and application packaging succeed, icon verification passes, `Resources/Info.plist` is valid, and the packaged application satisfies its designated requirement.

- [ ] **Step 6: Verify repository structure and rename detection**

Run from the repository root:

```bash
test -f TerminalHelper-mac/Package.swift
test -d "TerminalHelper-mac/dist/Terminal Helper.app"
test -f TerminalHelper-windows/README.md
git diff --check
git status --short
git diff --summary --find-renames
```

Expected: the required platform files exist, whitespace validation passes, and Git detects the macOS project files as renames where their contents did not change.

- [ ] **Step 7: Commit the tracked migration**

Run:

```bash
git add README.md TerminalHelper-mac TerminalHelper-windows
git commit -m "chore: split mac and windows project directories"
```

Expected: the commit contains only directory moves plus the new root and Windows README files.

### Task 2: Integrate the Migration and Preserve Local Build Artifacts

**Files:**
- Move locally: `.build/` → `TerminalHelper-mac/.build/`
- Move locally: `dist/` → `TerminalHelper-mac/dist/`
- Verify: `TerminalHelper-mac/dist/Terminal Helper.app`

**Interfaces:**
- Consumes: the reviewed directory-layout commit and the existing ignored macOS build artifacts in the primary checkout
- Produces: the final `main` layout with both source and local application artifacts under `TerminalHelper-mac/`

- [ ] **Step 1: Merge the reviewed migration into `main`**

From the primary checkout, fast-forward merge the feature branch:

```bash
git merge --ff-only feature/platform-directories
```

Expected: `main` advances to the reviewed migration commit without conflicts.

- [ ] **Step 2: Relocate ignored local artifacts if they remain at the root**

Before moving, resolve each exact source and destination. If `TerminalHelper-mac/.build/` or `TerminalHelper-mac/dist/` already exists, preserve the newly generated platform-local directory and do not overwrite it. Otherwise run:

```bash
mv .build TerminalHelper-mac/.build
mv dist TerminalHelper-mac/dist
```

Expected: no Terminal Helper build cache or packaged application remains at the repository root, and no unrelated path is moved.

- [ ] **Step 3: Rebuild and verify from the merged primary checkout**

Run:

```bash
cd TerminalHelper-mac
./scripts/test.sh
./scripts/build-app.sh
./scripts/verify-icon.sh
codesign --verify --deep --strict --verbose=2 "dist/Terminal Helper.app"
```

Expected: all 30 tests pass and the final application at `TerminalHelper-mac/dist/Terminal Helper.app` passes icon and signature verification.

- [ ] **Step 4: Clean the task-owned worktree and branch**

From the repository root:

```bash
git worktree remove .worktrees/platform-directories
git worktree prune
git branch -d feature/platform-directories
```

Expected: the temporary worktree and merged feature branch are removed; `main` remains at the migration commit.

- [ ] **Step 5: Record the final repository state**

Run:

```bash
git status --short
git branch --show-current
git log -1 --oneline
test -d "TerminalHelper-mac/dist/Terminal Helper.app"
```

Expected: the current branch is `main`, the application exists at its new path, and any remaining untracked entries predate or are unrelated to this migration.
