# Terminal Helper Windows 版设计

日期：2026-07-22  
状态：已批准（用户授权按最优方案自主实施）

## 1. 目标

为 Windows 11 x64 提供与现有 macOS 版一致的核心体验：用户把一个或多个文件夹拖入应用窗口，或拖到应用可执行文件/快捷方式上，然后只选择一种打开方式，将全部有效文件夹在 Windows Terminal、Visual Studio Code 或 IntelliJ IDEA 中打开。

Windows 版是独立工程。`TerminalHelper-mac/` 保持只读，不建立源码、构建或运行时依赖。

## 2. 首版范围

### 包含

- Windows 11 x64。
- 窗口拖放与 EXE/快捷方式命令行参数两种入口。
- 一次接收一个或多个文件夹。
- 规范化、验证、大小写不敏感去重并保留首次出现的顺序。
- 三个互斥目标：Windows Terminal、Visual Studio Code、IntelliJ IDEA。
- 显示目标是否安装；未安装目标可见但不可点击。
- 成功后清空当前选择；失败后保留，允许重试或重新选择。
- 新拖入批次替换旧批次。
- 防止同一批次重复启动，并忽略旧异步任务对新选择的迟到结果。
- 中文 Fluent 界面、键盘导航、无障碍名称、浅色/深色主题。
- 免安装、自包含的便携 ZIP 和 SHA-256 校验文件。

### 不包含

- Windows 10、ARM64、安装器、商店/MSIX 发布、自动更新。
- 同时选择多个目标、默认目标、历史记录、右键菜单集成。
- 单实例进程重定向；拖到 EXE/快捷方式时允许打开新的选择窗口。
- 自动安装或修复第三方应用。
- 对 macOS 版本做任何修改。

## 3. 技术路线

- C# 与 .NET 10 LTS。
- WinUI 3 与 Windows App SDK 2.3.1，提供 Windows 11 原生 Fluent 体验。
- Windows App SDK 应用使用 unpackaged、自包含部署。
- MSTest 4.3.2。
- 不引入第三方 MVVM、DI 或命令行解析框架；首版规模下使用显式构造函数和小型接口更清晰，也减少便携包依赖。

选择 WinUI 3 是因为 Windows App SDK 是微软面向新 Windows 桌面应用的现代原生 UI 路线；Windows App SDK 2.3.1 与 MSTest 4.3.2 是设计日确认的稳定 NuGet 版本。

## 4. 工程结构

```text
TerminalHelper-windows/
├── TerminalHelper.Windows.slnx
├── Directory.Build.props
├── Directory.Packages.props
├── global.json
├── src/
│   ├── TerminalHelper.Core/
│   ├── TerminalHelper.WindowsPlatform/
│   └── TerminalHelper.Windows/
├── tests/
│   ├── TerminalHelper.Core.Tests/
│   └── TerminalHelper.WindowsPlatform.Tests/
├── assets/
├── scripts/
├── docs/
└── README.md
```

### TerminalHelper.Core

目标框架为 `net10.0`，不引用 Windows API，因此可在当前 macOS 开发机运行测试。

职责：

- `WorkspaceTarget`、`WorkspaceStatus`、`WorkspaceSummary` 等领域模型。
- `FolderBatchPlanner`：路径规范化、存在性与目录验证、去重、失败计数。
- `WorkspaceOpenCoordinator`：接收批次、选择目标、状态转换、成功清空、失败保留、重复启动保护与选择版本保护。
- `IWorkspaceLauncher`、`IPathInspector` 等可替换边界。

### TerminalHelper.WindowsPlatform

目标框架为 `net10.0-windows10.0.22000.0`。

职责：

- 检测 Windows Terminal、VS Code、IntelliJ IDEA 的可执行文件。
- 通过 `ProcessStartInfo.ArgumentList` 安全传递路径参数。
- 包装文件系统、环境变量、注册表与进程启动，使发现和参数生成逻辑可用假对象测试。
- 将底层异常转换为面向用户的启动错误。

### TerminalHelper.Windows

目标框架为 `net10.0-windows10.0.22000.0`。

职责：

- WinUI 3 XAML 界面与窗口生命周期。
- 从拖放数据和进程命令行参数提取本地路径。
- 将用户操作转发给协调器，并渲染其状态。
- 应用图标、主题、窗口尺寸、键盘和无障碍行为。

## 5. 核心数据流

1. 窗口拖放或启动参数产生原始路径列表。
2. `FolderBatchPlanner` 使用 `Path.GetFullPath` 规范化路径，通过 `Directory.Exists` 验证目录，并以 `StringComparer.OrdinalIgnoreCase` 去重；结果保留原输入顺序。
3. 协调器递增选择版本，使用新批次替换旧批次，并显示有效与无效数量。
4. UI 显示三个目标按钮。可用性由平台启动器查询，未安装目标显示“未安装”。
5. 用户点击一个目标后，协调器复制当前批次和版本，进入 `Launching`。再次点击不会重复启动。
6. 平台启动器生成结构化进程请求并依次执行。
7. 若成功且选择版本未改变，清空当前批次并显示完成状态；若失败且版本未改变，保留批次并显示错误；若期间出现了新批次，则忽略旧任务的完成结果。
8. “重新选择文件夹”递增选择版本、清空批次并返回空闲状态。

## 6. 文件夹输入规则

- 空输入不改变现有状态。
- 文件、缺失路径、非法路径均计为无效，不交给启动器。
- 同一目录以 Windows 大小写不敏感规则去重。
- 所有输入无效时显示“未找到可打开的文件夹”，不显示可执行的目标操作。
- 有效与无效混合时保留有效项并显示无效数量。
- 支持空格、中文、撇号、`&`、括号等合法 Windows 路径字符。

## 7. 目标发现与启动

所有进程都使用 `UseShellExecute = false` 与 `ProcessStartInfo.ArgumentList`。不拼接 shell 命令，不经 `cmd.exe`，避免引号错误和参数注入。

### Windows Terminal

发现顺序：

1. PATH 中的 `wt.exe`。
2. `%LOCALAPPDATA%\Microsoft\WindowsApps\wt.exe`。

每个文件夹启动一次：

```text
wt.exe -w new -d <folder>
```

这样与 macOS 版“一文件夹一新终端窗口”的行为一致。

### Visual Studio Code

发现顺序：

1. PATH 中的 `code.cmd`、`code.exe` 或 `Code.exe`。
2. `%LOCALAPPDATA%\Programs\Microsoft VS Code\Code.exe`。
3. `%ProgramFiles%\Microsoft VS Code\Code.exe`。
4. `%ProgramFiles(x86)%\Microsoft VS Code\Code.exe`。
5. 注册表 App Paths/卸载信息中可验证的安装位置。

一次启动并追加全部文件夹参数：

```text
Code.exe --new-window <folder1> <folder2> ...
```

VS Code 会把多个文件夹参数组成多根工作区。

### IntelliJ IDEA

支持 Ultimate 和 Community，若两者同时存在则优先 Ultimate。

发现顺序：

1. PATH 中的 `idea64.exe` 或 `idea.exe`。
2. JetBrains/Windows 注册表安装信息中的 `InstallLocation`。
3. `%LOCALAPPDATA%\JetBrains\Toolbox\apps` 下的 `product-info.json` 与对应启动器。
4. 常见 `%ProgramFiles%\JetBrains\...\bin\idea64.exe` 安装目录。

按照输入顺序为每个文件夹启动一次 IDEA 命令。发现结果在应用生命周期内缓存，但测试可显式刷新。

## 8. 界面与视觉

主窗口约 500×440，允许合理缩放，内容在一个圆角虚线拖放面板内。

### 空状态

- 文件夹加号图标。
- 标题“拖入文件夹”。
- 提示“拖入文件夹，然后选择打开方式”。
- 拖入悬停时使用系统强调色突出边框与背景。

### 已选择状态

- 文件夹图标与“已选择 N 个文件夹”。
- 提示“选择一个打开方式”。
- 三个 48px 高的全宽圆角按钮：Terminal、Visual Studio Code、IntelliJ IDEA。
- 正在启动的目标显示进度环；全部按钮暂时禁用。
- 未安装目标降低透明度并显示“未安装”。
- 底部状态文字和“重新选择文件夹”。

图标沿用 macOS 版的视觉语言：白色圆角底座、蓝色文件夹与深色 `>_` 终端符号。由现有 1024px PNG 派生 Windows ICO/PNG 资产，但不修改 macOS 原图。

## 9. 错误处理

- 目标未安装：按钮禁用；即使绕过 UI 直接调用，协调器也返回明确失败且不启动进程。
- 进程无法创建或可执行文件消失：显示“无法使用 … 打开”，保留选择以便重试。
- 部分目标批次启动失败：停止后续启动，报告失败；因为外部进程不可事务回滚，不宣称全部成功。
- 无管理员权限要求，不写系统目录或注册表。
- 日志仅使用调试输出，不记录用户文件内容；面向用户只显示必要路径/目标信息。

## 10. 测试策略

### macOS 本地可执行

- `TerminalHelper.Core.Tests`：规划器、状态机、错误文案、重复启动与迟到完成保护。
- Core 构建与格式检查。

### Windows CI/开发机

- `TerminalHelper.WindowsPlatform.Tests`：每种发现来源、优先级、路径参数、目标不可用与进程失败。
- 整个解决方案还原、编译、测试与 `win-x64` 发布。
- 便携 ZIP 结构与 SHA-256 生成脚本测试。

### Windows 11 手工验收

- 窗口拖入和拖到 EXE/快捷方式。
- 单个、多个、重复、全部无效、有效/无效混合路径。
- 中文、空格、撇号、`&`、括号路径。
- 三种目标分别处于安装和未安装状态。
- IDEA Ultimate/Community 优先与回退。
- 成功清空、失败保留、双击保护、启动中拖入新批次。
- 在未安装 .NET/Windows App SDK 的干净 Windows 11 x64 账户解压运行，无管理员权限。

## 11. 构建与交付

GitHub Actions 的 Windows runner 执行：

1. 安装固定的 .NET 10 SDK。
2. 还原、编译并运行全部测试，警告视为错误。
3. 以 `win-x64`、unpackaged、self-contained 模式发布。
4. 将发布目录压缩为 `TerminalHelper-windows-win-x64.zip`。
5. 生成同名 `.sha256` 并上传二者为 CI artifact。

仓库当前没有远程地址时，工作流只能作为可复现配置提交，不能声称已在 GitHub runner 上执行。最终完成报告必须区分本地验证与仍需 Windows runner 验证的项目。

## 12. 成功标准

- 不修改 `TerminalHelper-mac/` 下任何文件。
- Core 行为测试全部通过。
- Windows runner 上解决方案无警告编译、测试通过并生成可解压运行的 ZIP。
- Windows 11 手工验收清单通过后，首版才标记为可发布。

## 13. 参考资料

- [WinUI 3](https://learn.microsoft.com/en-us/windows/apps/winui/winui3/)
- [Windows App SDK release channels](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/release-channels)
- [Microsoft.WindowsAppSDK 2.3.1](https://www.nuget.org/packages/Microsoft.WindowsAppSDK/2.3.1)
- [.NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy)
- [MSTest 4.3.2](https://www.nuget.org/packages/MSTest/4.3.2)
- [Windows Terminal command-line arguments](https://learn.microsoft.com/en-us/windows/terminal/command-line-arguments)
- [Visual Studio Code command line](https://code.visualstudio.com/docs/configure/command-line)
- [IntelliJ IDEA command line](https://www.jetbrains.com/help/idea/opening-files-from-command-line.html)
- [Windows App SDK CI](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/ci-for-winui3)
- [Deploy unpackaged apps](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/unpackage-winui-app)
- [Deploy self-contained apps](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/self-contained-deploy/deploy-self-contained-apps)
