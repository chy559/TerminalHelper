import Foundation
import Testing
@testable import TerminalHelper

@Suite("Folder open coordinator")
struct FolderOpenCoordinatorTests {
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
    func explainsAutomationPermissionDenialAndStopsBatch() throws {
        let folders = try TemporaryFolders(count: 2)
        defer { folders.remove() }
        let launcher = RecordingTerminalLauncher(error: TerminalLaunchError.automationPermissionDenied)
        let coordinator = FolderOpenCoordinator(planner: FolderBatchPlanner(), launcher: launcher)

        coordinator.open(folders.urls)

        #expect(launcher.opened == [folders.urls[0].standardizedFileURL])
        #expect(coordinator.status == .automationPermissionDenied(.init(succeeded: 0, failed: 1)))
        #expect(coordinator.statusText == "请前往系统设置 → 隐私与安全性 → 自动化，允许 Terminal Helper 控制终端")
    }

    @Test @MainActor
    func reportsAllInvalidInputFailures() throws {
        let folders = try TemporaryFolders(count: 0)
        defer { folders.remove() }
        let file = folders.root.appending(path: "note.txt")
        let missing = folders.root.appending(path: "missing", directoryHint: .isDirectory)
        try Data("x".utf8).write(to: file)
        let launcher = RecordingTerminalLauncher()
        let coordinator = FolderOpenCoordinator(planner: FolderBatchPlanner(), launcher: launcher)

        coordinator.open([file, missing])

        #expect(launcher.opened.isEmpty)
        #expect(coordinator.status == .completed(.init(succeeded: 0, failed: 2)))
        #expect(coordinator.statusText == "未打开文件夹，请选择文件夹（2 项失败）")
    }

    @Test @MainActor
    func leavesStatusIdleForEmptyInput() {
        let launcher = RecordingTerminalLauncher()
        let coordinator = FolderOpenCoordinator(planner: FolderBatchPlanner(), launcher: launcher)

        coordinator.open([])

        #expect(launcher.opened.isEmpty)
        #expect(coordinator.status == .idle)
        #expect(coordinator.statusText == "拖入文件夹，在终端中打开")
    }
}

private struct TemporaryFolders {
    let root: URL
    let urls: [URL]

    init(count: Int) throws {
        let root = FileManager.default.temporaryDirectory
            .appending(path: UUID().uuidString, directoryHint: .isDirectory)
        let urls = (0..<count).map {
            root.appending(path: "folder-\($0)", directoryHint: .isDirectory)
        }
        try FileManager.default.createDirectory(at: root, withIntermediateDirectories: true)
        for url in urls {
            try FileManager.default.createDirectory(at: url, withIntermediateDirectories: false)
        }
        self.root = root
        self.urls = urls
    }

    func remove() {
        try? FileManager.default.removeItem(at: root)
    }
}

private final class RecordingTerminalLauncher: TerminalLaunching {
    var opened: [URL] = []
    let failingPaths: Set<String>
    let error: Error?

    init(failingPaths: Set<String> = [], error: Error? = nil) {
        self.failingPaths = failingPaths
        self.error = error
    }

    func open(directory: URL) throws {
        opened.append(directory)
        if let error {
            throw error
        }
        if failingPaths.contains(directory.path) {
            throw TerminalLaunchError.scriptFailed("Unable to open folder")
        }
    }
}
