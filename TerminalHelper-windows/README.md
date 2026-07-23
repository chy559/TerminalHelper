# Terminal Helper for Windows

Terminal Helper 是一款面向 **Windows 11 x64** 的原生文件夹启动器。把一个或多个文件夹交给应用，再从 Windows Terminal、Visual Studio Code、IntelliJ IDEA 中**只选择一个目标**，即可打开这一批文件夹。Windows 版本与仓库中的 macOS 应用独立构建，也不会依赖其 SwiftUI 源码。

## 下载与使用

Windows 版本以免安装的便携 ZIP 分发：

1. 下载 `TerminalHelper-windows-win-x64.zip`，并可使用同名的 `.sha256` 文件校验 SHA-256。
2. 将 ZIP 完整解压到可写目录；不要直接在压缩包预览中运行。
3. 双击 `TerminalHelper.Windows.exe`。这是自包含的 x64 应用，无需另行安装 .NET 或 Windows App SDK，也无需管理员权限。
4. 用以下任一入口提交文件夹：
   - 把一个或多个文件夹拖进已打开的 Terminal Helper 窗口；
   - 把一个或多个文件夹拖到 `TerminalHelper.Windows.exe`（或将路径转发给它的快捷方式）上。
5. 在 `Terminal`、`Visual Studio Code`、`IntelliJ IDEA` 三项中点击**恰好一个**可用目标。每批操作不会同时启动多个目标。

应用会保留有效文件夹的输入顺序，把文件和其他不可用路径计为无效输入，对 Windows 下大小写不同或仅末尾目录分隔符不同的重复路径去重，并提示无效路径数量。提交新的非空批次会替换当前选择；启动成功后选择会清空，启动失败时则保留，便于重试。

## 目标发现与启动行为

目标程序在应用生命周期内按以下来源自动发现；未发现的目标会显示“未安装”且不可点击：

- **Windows Terminal**：先查找 `PATH` 中的 `wt.exe`，再检查 `%LOCALAPPDATA%\Microsoft\WindowsApps\wt.exe`。每个文件夹打开一个新的 Terminal 窗口。
- **Visual Studio Code**：依次检查 `PATH`，用户级/系统级固定安装目录，以及 App Paths 和卸载信息注册表。只启动验证存在的 `Code.exe`；若 `PATH` 仅暴露安装目录 `bin\code.cmd`，会验证并改用相邻安装根目录中的 `Code.exe`，绝不启动批处理文件。所有文件夹通过一次 `--new-window` 启动作为多根工作区打开。
- **IntelliJ IDEA**：把 `PATH`、Program Files、Windows 注册表与 JetBrains Toolbox 元数据中的可执行文件统一排序；同时存在 Ultimate 和 Community 时优先 Ultimate，同一版本系列选择较新的有效安装。`PATH` 项会尽可能通过相邻 `product-info.json` 识别版本；无法识别版本的有效项仅在没有更优已知安装时作为回退。文件夹按输入顺序逐个交给 IDEA。

所有启动参数均通过 .NET 参数列表直接传给目标进程，不拼接 shell 命令，也不经 `cmd.exe`。

## 开发与验证

开发环境需要 Windows 11 x64、[.NET SDK 10.0.302](https://dotnet.microsoft.com/)、PowerShell 7 和 Python 3（用于图标资源校验）。克隆仓库后从仓库根目录执行：

```powershell
dotnet restore TerminalHelper-windows/TerminalHelper.Windows.slnx
dotnet build TerminalHelper-windows/TerminalHelper.Windows.slnx -c Release -p:Platform=x64
dotnet test TerminalHelper-windows/tests/TerminalHelper.Core.Tests/TerminalHelper.Core.Tests.csproj -c Release
dotnet test TerminalHelper-windows/tests/TerminalHelper.WindowsPlatform.Tests/TerminalHelper.WindowsPlatform.Tests.csproj -c Release
python TerminalHelper-windows/scripts/verify-icon-assets.py
python TerminalHelper-windows/scripts/verify-static-contracts.py
pwsh -File TerminalHelper-windows/scripts/verify-powershell-syntax.ps1
pwsh -File TerminalHelper-windows/scripts/build-portable.ps1
```

Core 测试是平台中立的，可在安装了 .NET 10 的 macOS/Linux 上单独运行：

```bash
dotnet test TerminalHelper-windows/tests/TerminalHelper.Core.Tests/TerminalHelper.Core.Tests.csproj -c Release
```

便携打包脚本会先删除固定名称的旧 ZIP/校验文件，再发布并检查必要运行时文件及 PDB 缺失约束，然后生成：

- `TerminalHelper-windows/artifacts/TerminalHelper-windows-win-x64.zip`
- `TerminalHelper-windows/artifacts/TerminalHelper-windows-win-x64.zip.sha256`

生成校验文件后，脚本还会重新计算实际 ZIP 的 SHA-256，把 ZIP 解压到唯一临时目录，并对解压根目录再次执行便携内容及 PDB 检查；任何一步失败都不会把旧 artifact 当作本次成功产物。也可在 Windows 上独立执行：

```powershell
pwsh -File TerminalHelper-windows/scripts/verify-package.ps1 `
  -ArchivePath TerminalHelper-windows/artifacts/TerminalHelper-windows-win-x64.zip `
  -ChecksumPath TerminalHelper-windows/artifacts/TerminalHelper-windows-win-x64.zip.sha256
```

Windows CI 的测试结果 artifact 名称为 `TerminalHelper-windows-test-results`；构建、测试和打包全部成功后，便携包 artifact 名称为 `TerminalHelper-windows-win-x64`，其中包含上述 ZIP 与校验文件。

## 架构

- `TerminalHelper.Core`：平台中立的文件夹批次规划、目标模型和启动状态协调。
- `TerminalHelper.Presentation`：平台中立、可测试的启动参数读取、窗口尺寸与视图模型行为；命名空间保持为 Windows UI 使用的接口。
- `TerminalHelper.WindowsPlatform`：Windows 可执行文件发现、安全参数构造与进程启动适配器。
- `TerminalHelper.Windows`：WinUI 3 应用壳、窗口生命周期、拖放和原生资源。
- `tests/TerminalHelper.Core.Tests`：Core 单元测试。
- `tests/TerminalHelper.WindowsPlatform.Tests`：Windows 平台适配器与 Presentation 行为测试。

## 当前限制与人工验收

- 仅支持 Windows 11 x64；不提供 x86、Arm64、Windows 10 或安装器包。
- 只能打开本地存在的文件夹；文件和无效路径会计入无效数量，但不会传给目标应用。
- 出于阻止非预期 SMB/NTLM 网络访问的安全边界，原始或规范化后的 UNC 路径（`\\server\share`、`//server/share`）及设备命名空间（`\\?\`、`\\.\`）会在任何存在性探测前被拒绝。已由用户/管理员建立的映射驱动器使用普通盘符路径（如 `Z:\Work`），按本地盘符路径处理并允许验证。
- 目标发现结果会在当前应用进程内缓存；安装或移除目标程序后应重启 Terminal Helper。
- 便携包未包含代码签名或自动更新机制，Windows 可能显示来源/信誉提示。
- 自动化测试不能替代 WinUI 的拖放、主题、缩放、键盘、讲述人和并发交互验收。

发布前必须在 Windows 11 x64 上完成 [`docs/manual-test-checklist.md`](docs/manual-test-checklist.md)，并同时确认 Windows CI 的构建、测试与打包 job 已通过。未满足这两项门槛时，不应将 Windows 版本标记为发布就绪。
