import Foundation
import Testing
@testable import TerminalHelper

@Suite("Workspace open coordinator")
struct WorkspaceOpenCoordinatorTests {
    @Test @MainActor
    func allInvalidInputShowsAnErrorWithoutPendingFolders() throws {
        let folders = try TemporaryWorkspaceFolders(count: 0)
        defer { folders.remove() }
        let file = folders.root.appending(path: "notes.txt")
        let missing = folders.root.appending(path: "missing", directoryHint: .isDirectory)
        try Data("x".utf8).write(to: file)
        let coordinator = makeCoordinator()

        coordinator.receive([file, missing])

        #expect(coordinator.pendingFolders.isEmpty)
        #expect(coordinator.status == .ready(.init(valid: 0, invalid: 2)))
        #expect(coordinator.statusText == "未找到可打开的文件夹（2 项无效）")
    }

    @Test @MainActor
    func emptyInputLeavesTheCurrentSelectionUnchanged() throws {
        let folders = try TemporaryWorkspaceFolders(count: 1)
        defer { folders.remove() }
        let coordinator = makeCoordinator()
        coordinator.receive(folders.urls)

        coordinator.receive([])

        #expect(coordinator.pendingFolders == folders.urls.map(\.standardizedFileURL))
        #expect(coordinator.status == .ready(.init(valid: 1, invalid: 0)))
    }

    @Test @MainActor
    func receivingFoldersReplacesThePendingSelectionAndReportsInvalidItems() throws {
        let first = try TemporaryWorkspaceFolders(count: 2)
        let replacement = try TemporaryWorkspaceFolders(count: 1)
        defer {
            first.remove()
            replacement.remove()
        }
        let invalidFile = replacement.root.appending(path: "notes.txt")
        try Data("x".utf8).write(to: invalidFile)
        let coordinator = makeCoordinator()

        coordinator.receive(first.urls)
        #expect(coordinator.pendingFolders == first.urls.map(\.standardizedFileURL))

        coordinator.receive([replacement.urls[0], invalidFile])

        #expect(coordinator.pendingFolders == [replacement.urls[0].standardizedFileURL])
        #expect(coordinator.status == .ready(.init(valid: 1, invalid: 1)))
        #expect(coordinator.statusText == "已选择 1 个文件夹，1 项无效")
    }

    @Test @MainActor
    func successfulLaunchClearsPendingFolders() async throws {
        let folders = try TemporaryWorkspaceFolders(count: 2)
        defer { folders.remove() }
        let launcher = RecordingWorkspaceLauncher()
        let coordinator = makeCoordinator(launcher: launcher)
        coordinator.receive(folders.urls)

        await coordinator.launch(in: .visualStudioCode)

        #expect(launcher.launchRequests == [
            .init(folders: folders.urls.map(\.standardizedFileURL), target: .visualStudioCode),
        ])
        #expect(coordinator.pendingFolders.isEmpty)
        #expect(coordinator.status == .completed(.visualStudioCode, count: 2))
        #expect(coordinator.statusText == "已在 Visual Studio Code 中打开 2 个文件夹")
    }

    @Test @MainActor
    func aNewDropDuringLaunchIsNotClearedByTheOlderLaunchCompletion() async throws {
        let original = try TemporaryWorkspaceFolders(count: 1)
        let replacement = try TemporaryWorkspaceFolders(count: 2)
        defer {
            original.remove()
            replacement.remove()
        }
        let launcher = RecordingWorkspaceLauncher()
        let coordinator = makeCoordinator(launcher: launcher)
        coordinator.receive(original.urls)
        launcher.onLaunch = {
            coordinator.receive(replacement.urls)
        }

        await coordinator.launch(in: .visualStudioCode)

        #expect(coordinator.pendingFolders == replacement.urls.map(\.standardizedFileURL))
        #expect(coordinator.status == .ready(.init(valid: 2, invalid: 0)))
    }

    @Test @MainActor
    func failedLaunchKeepsPendingFoldersForRetry() async throws {
        let folders = try TemporaryWorkspaceFolders(count: 1)
        defer { folders.remove() }
        let launcher = RecordingWorkspaceLauncher(error: TestLaunchError.failed)
        let coordinator = makeCoordinator(launcher: launcher)
        coordinator.receive(folders.urls)

        await coordinator.launch(in: .intelliJIdea)

        #expect(coordinator.pendingFolders == folders.urls.map(\.standardizedFileURL))
        #expect(coordinator.status == .failed(.intelliJIdea, message: "测试启动失败"))
        #expect(coordinator.statusText == "无法使用 IntelliJ IDEA 打开：测试启动失败")
    }

    @Test @MainActor
    func unavailableTargetIsRejectedWithoutLaunching() async throws {
        let folders = try TemporaryWorkspaceFolders(count: 1)
        defer { folders.remove() }
        let launcher = RecordingWorkspaceLauncher(availableTargets: [.terminal])
        let coordinator = makeCoordinator(launcher: launcher)
        coordinator.receive(folders.urls)

        await coordinator.launch(in: .visualStudioCode)

        #expect(launcher.launchRequests.isEmpty)
        #expect(coordinator.pendingFolders == folders.urls.map(\.standardizedFileURL))
        #expect(coordinator.status == .failed(
            .visualStudioCode,
            message: "未找到 Visual Studio Code，请先安装后重试"
        ))
    }

    @Test @MainActor
    func terminalPermissionDenialKeepsPendingFoldersAndExplainsRecovery() async throws {
        let folders = try TemporaryWorkspaceFolders(count: 1)
        defer { folders.remove() }
        let launcher = RecordingWorkspaceLauncher(error: TerminalLaunchError.automationPermissionDenied)
        let coordinator = makeCoordinator(launcher: launcher)
        coordinator.receive(folders.urls)

        await coordinator.launch(in: .terminal)

        #expect(coordinator.pendingFolders == folders.urls.map(\.standardizedFileURL))
        #expect(coordinator.status == .automationPermissionDenied)
        #expect(
            coordinator.statusText
                == "请前往系统设置 → 隐私与安全性 → 自动化，允许 Terminal Helper 控制终端"
        )
    }

    @Test @MainActor
    func availabilityAndResetAreExposedToTheView() throws {
        let folders = try TemporaryWorkspaceFolders(count: 1)
        defer { folders.remove() }
        let launcher = RecordingWorkspaceLauncher(availableTargets: [.terminal, .visualStudioCode])
        let coordinator = makeCoordinator(launcher: launcher)
        coordinator.receive(folders.urls)

        #expect(coordinator.isAvailable(.terminal))
        #expect(coordinator.isAvailable(.visualStudioCode))
        #expect(!coordinator.isAvailable(.intelliJIdea))

        coordinator.reset()

        #expect(coordinator.pendingFolders.isEmpty)
        #expect(coordinator.status == .idle)
        #expect(coordinator.statusText == "拖入文件夹，然后选择打开方式")
    }

    @MainActor
    private func makeCoordinator(
        launcher: RecordingWorkspaceLauncher = RecordingWorkspaceLauncher()
    ) -> WorkspaceOpenCoordinator {
        WorkspaceOpenCoordinator(planner: FolderBatchPlanner(), launcher: launcher)
    }
}

private enum TestLaunchError: Error, LocalizedError {
    case failed

    var errorDescription: String? { "测试启动失败" }
}

@MainActor
private final class RecordingWorkspaceLauncher: WorkspaceLaunching {
    struct LaunchRequest: Equatable {
        let folders: [URL]
        let target: WorkspaceTarget
    }

    let availableTargets: Set<WorkspaceTarget>
    let error: Error?
    var launchRequests: [LaunchRequest] = []
    var onLaunch: (() -> Void)?

    init(
        availableTargets: Set<WorkspaceTarget> = Set(WorkspaceTarget.allCases),
        error: Error? = nil
    ) {
        self.availableTargets = availableTargets
        self.error = error
    }

    func isAvailable(_ target: WorkspaceTarget) -> Bool {
        availableTargets.contains(target)
    }

    func launch(folders: [URL], in target: WorkspaceTarget) async throws {
        launchRequests.append(.init(folders: folders, target: target))
        onLaunch?()
        if let error {
            throw error
        }
    }
}

private struct TemporaryWorkspaceFolders {
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
