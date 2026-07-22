# Terminal Helper for Windows

此目录用于独立开发 Terminal Helper 的 Windows 版本。

Windows 版本复现 macOS 版本的核心体验，但不会修改或依赖 `TerminalHelper-mac/` 中的 SwiftUI 应用。

解决方案将平台中立的文件夹规划、启动状态与 presentation/input 行为分别放在 `TerminalHelper.Core` 和 `TerminalHelper.Presentation`，Windows 可执行文件发现与安全进程启动放在 `TerminalHelper.WindowsPlatform`，WinUI 3 自包含应用壳位于 `TerminalHelper.Windows`。这一拆分让 presentation/input 行为可以在非 Windows 开发机测试，而 WinUI 项目继续以 unpackaged、self-contained `win-x64` 方式构建。
