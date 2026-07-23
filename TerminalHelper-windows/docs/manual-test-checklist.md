# Terminal Helper Windows 11 Manual Acceptance Checklist

Run this checklist against the Release `win-x64` build on Windows 11. Unless a case
states otherwise, begin with no selected folders and no launch in progress. Record
the Windows build, app commit, target application versions, theme, and display scale.

## Environment and visual states

- [ ] On a clean Windows 11 x64 account without .NET or Windows App SDK installed,
  extract the portable build and launch it without elevation; no installer or
  administrator permission is requested.
- [ ] At 100% display scaling in light theme, the window opens at approximately
  500×440 with 24px outer spacing, an 18px rounded dashed drop surface, a folder
  glyph, “拖入文件夹”, and “拖入文件夹，然后选择打开方式”.
- [ ] Repeat the empty and selected states in dark theme; text, dashed border,
  buttons, focus indicators, status, and error text remain legible.
- [ ] Repeat light and dark theme checks at 150% scaling; no title, status, target
  name, missing label, progress ring, or reset action is clipped or overlaps.
- [ ] After selecting folders, the header reads “已选择 N 个文件夹”, the prompt
  reads “选择一个打开方式”, and the 48px rows appear in exact Terminal, Visual
  Studio Code, IntelliJ IDEA order.
- [ ] Each target row has a 12px corner radius, icon and name, and exactly one
  appropriate right-side state: chevron, active progress ring, or “未安装”.
- [ ] Drag a folder over both the empty state and selected state. The system copy
  operation is shown and the border/background switch to the accent highlight.
- [ ] Move the pointer out or complete/cancel the drop. The highlight returns to
  its idle appearance.
- [ ] Drag data that does not expose StorageItems. It is rejected and the accent
  drop highlight is not shown.

## Keyboard and Narrator

- [ ] Using only Tab, Shift+Tab, Space, and Enter, operate the selected state. Focus
  follows Terminal → Visual Studio Code → IntelliJ IDEA → 重新选择文件夹, skipping
  disabled targets, with a visible focus indicator and a complete-row hit target.
- [ ] Press Space and Enter on each available target in separate runs; each invokes
  exactly that target. Press either key on 重新选择文件夹; the app returns to empty.
- [ ] During a launch, verify all target buttons and 重新选择文件夹 are disabled and
  keyboard activation cannot start a second launch or clear the selection.
- [ ] With Narrator enabled, installed target buttons are announced as “Terminal”,
  “Visual Studio Code”, and “IntelliJ IDEA” in visual/tab order.
- [ ] With Narrator enabled, each missing target is announced as
  “DISPLAY，未安装” (for example “IntelliJ IDEA，未安装”) and cannot be invoked.
- [ ] Narrator announces the reset action as “重新选择文件夹”; empty/selected
  headings are exposed as headings, and dynamic status/failure changes are announced
  through the appropriate polite/assertive live regions without pointer use.

## Folder input and replacement

Run applicable cases both by dropping into the window and by dropping onto the EXE
or a shortcut that forwards path arguments.

- [ ] One valid folder selects one folder and launches the selected target once.
- [ ] Multiple valid folders preserve input order and the header shows their count.
- [ ] Duplicate folder paths, including paths differing only by letter case, are
  de-duplicated using Windows case-insensitive behavior while preserving first order.
- [ ] `C:\Work` and `C:\Work\` are de-duplicated without changing a drive root such
  as `C:\`.
- [ ] An empty input does not replace or otherwise change an existing selection.
- [ ] A file-only drop replaces an older valid selection with an all-invalid result;
  no file path reaches a target launch.
- [ ] A mixed folder/file drop keeps only folders and counts every path-bearing file
  as invalid, matching the same folder/file list supplied through command-line args.
- [ ] Missing and syntactically invalid paths are rejected and counted as invalid.
- [ ] An all-invalid batch replaces the old batch, reports “未找到可打开的文件夹”,
  and exposes no executable target action.
- [ ] A mixed valid/invalid batch keeps only valid folders, reports the invalid count,
  and launches only the valid folders.
- [ ] A new non-empty batch replaces the previous selection rather than appending.
- [ ] Start two drops whose storage-item extraction finishes in reverse order. Only
  the later drop is applied; the older completion never overwrites it.
- [ ] UNC paths (`\\server\share` and `//server/share`) and device namespace paths
  (`\\?\` and `\\.\`) are rejected before network/filesystem existence probing.
  A pre-established mapped drive path such as `Z:\Work` remains accepted.
- [ ] Paths containing spaces, Chinese characters, apostrophes, `&`, and parentheses
  arrive at each target unchanged and cannot inject an extra command or argument.
- [ ] Reset clears the current batch, restores the exact empty-state copy, and a
  subsequent drop works normally.

## Installed and missing targets

- [ ] With Windows Terminal installed, its row is enabled and opens one new Terminal
  window per folder at that folder. With it missing, the row is disabled and shows
  “未安装”.
- [ ] With Visual Studio Code installed, its row is enabled and opens one new VS Code
  window containing all selected folder arguments. With it missing, the row is
  disabled and shows “未安装”.
- [ ] With IntelliJ IDEA installed, its row is enabled and opens folders in input
  order. With it missing, the row is disabled and shows “未安装”.
- [ ] With IDEA Ultimate and Community installed, Ultimate is selected. Remove or
  hide Ultimate and verify Community is used as the fallback.
- [ ] Verify each target when discovered from its supported PATH, fixed installation,
  registry, and (for IDEA) Toolbox source as available in the test environment.

## Launch completion, failure, and concurrency

- [ ] A successful unchanged launch clears the folder batch and reports completion.
- [ ] If a process cannot be created or the discovered executable disappears, the
  app shows “无法使用 … 打开” in the error text brush and retains the selection for retry.
- [ ] If a multi-folder launch fails partway through, no later launch is attempted,
  the selection remains, and the UI does not claim full success.
- [ ] Double-click or rapidly press Enter/Space on a target. Only one batch launch
  begins; the active row shows a progress ring and all actions remain disabled.
- [ ] While a launch is pending, drop a new valid batch. The UI adopts the new batch;
  the old target keeps its progress ring and all target/reset actions remain disabled
  until the old launch releases its gate. The new selection then becomes actionable
  automatically, without another user action, and late success does not clear it.
- [ ] While a launch is pending, drop a new batch and force the old launch to fail.
  The old failure does not replace the new batch's status or selection.
- [ ] Close the window while a launch or storage-item extraction is pending. The app
  exits without an unhandled exception or a UI update after close.

## Portable package integrity

- [ ] Start a build with stale fixed-name ZIP and `.sha256` files present, then force
  publishing to fail. The stale artifacts were deleted before publishing and cannot
  be mistaken for current output.
- [ ] A successful build verifies the exact one-line checksum sidecar name/syntax,
  recomputes the actual ZIP SHA-256, extracts to a unique temporary directory, and
  reruns required-content and no-PDB checks against the extracted root.
- [ ] Tampering with the ZIP, checksum hash, sidecar filename, spacing, or referenced
  archive name makes `scripts/verify-package.ps1` fail; its temporary directory is
  cleaned on both success and failure.
