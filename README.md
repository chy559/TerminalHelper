# Terminal Helper

Terminal Helper 是一个原生 macOS 小工具：把一个或多个文件夹拖入窗口或 Dock 图标，即可为每个有效文件夹打开一个新的“终端”窗口。

应用使用专属的“文件夹 + `>_`”图标；1024 像素主图保存在 `Resources/AppIcon/TerminalHelper-1024.png`，构建时会将编译后的 `Resources/TerminalHelper.icns` 打包进应用。

## 系统要求

- macOS 13.0 或更高版本
- 用命令行构建时需要兼容 Swift 6 的 Apple Command Line Tools 或 Xcode 工具链

## 测试、构建与启动

在仓库根目录运行：

```bash
./scripts/test.sh
./scripts/build-app.sh
open "dist/Terminal Helper.app"
```

构建脚本会生成并临时签名 `dist/Terminal Helper.app`。也可以直接在 Xcode 中打开 `Package.swift` 来查看、构建或运行项目。

## 使用方法

- **窗口拖放：** 将文件夹拖到 Terminal Helper 窗口中的虚线区域。
- **Dock 拖放：** 先让 Terminal Helper 保留在 Dock 中，再将文件夹拖到它的 Dock 图标上。
- **多个文件夹：** 一次拖入多个文件夹时，应用会按顺序为每个有效文件夹打开一个新的“终端”窗口。
- **混合内容：** 文件或无效路径不会打开；其余有效文件夹仍会继续处理，窗口状态会显示成功和失败数量。

## 自动化权限

首次打开文件夹时，macOS 会询问是否允许 Terminal Helper 控制“终端”。请选择“允许”。如果曾经拒绝，前往“系统设置”→“隐私与安全性”→“自动化”，找到 Terminal Helper 并启用对“终端”的访问，然后重新尝试。

仓库中的自动化测试使用记录器替代真实的“终端”控制，不会弹出权限提示或打开“终端”窗口。
