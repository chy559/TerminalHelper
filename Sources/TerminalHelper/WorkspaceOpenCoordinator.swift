import Combine
import Foundation

struct WorkspaceSummary: Equatable {
    let valid: Int
    let invalid: Int
}

enum WorkspaceStatus: Equatable {
    case idle
    case ready(WorkspaceSummary)
    case launching(WorkspaceTarget)
    case completed(WorkspaceTarget, count: Int)
    case failed(WorkspaceTarget, message: String)
    case automationPermissionDenied
}

@MainActor
protocol FolderReceiving: AnyObject {
    func receive(_ urls: [URL])
}

@MainActor
final class WorkspaceOpenCoordinator: ObservableObject, FolderReceiving {
    @Published private(set) var pendingFolders: [URL] = []
    @Published private(set) var status: WorkspaceStatus = .idle

    private let planner: any FolderBatchPlanning
    private let launcher: any WorkspaceLaunching
    private var selectionVersion = 0

    init(planner: any FolderBatchPlanning, launcher: any WorkspaceLaunching) {
        self.planner = planner
        self.launcher = launcher
    }

    func receive(_ urls: [URL]) {
        guard !urls.isEmpty else { return }

        let plan = planner.makePlan(for: urls)
        selectionVersion += 1
        pendingFolders = plan.validFolders
        status = .ready(
            .init(valid: plan.validFolders.count, invalid: plan.failures.count)
        )
    }

    func isAvailable(_ target: WorkspaceTarget) -> Bool {
        launcher.isAvailable(target)
    }

    func launch(in target: WorkspaceTarget) async {
        guard !pendingFolders.isEmpty else { return }

        guard launcher.isAvailable(target) else {
            status = .failed(
                target,
                message: WorkspaceLaunchError.targetUnavailable(target).localizedDescription
            )
            return
        }

        let folders = pendingFolders
        let launchedSelectionVersion = selectionVersion
        status = .launching(target)

        do {
            try await launcher.launch(folders: folders, in: target)
            guard selectionVersion == launchedSelectionVersion else { return }
            pendingFolders.removeAll()
            status = .completed(target, count: folders.count)
        } catch TerminalLaunchError.automationPermissionDenied {
            guard selectionVersion == launchedSelectionVersion else { return }
            status = .automationPermissionDenied
        } catch {
            guard selectionVersion == launchedSelectionVersion else { return }
            status = .failed(target, message: error.localizedDescription)
        }
    }

    func reset() {
        selectionVersion += 1
        pendingFolders.removeAll()
        status = .idle
    }

    var statusText: String {
        switch status {
        case .idle:
            "拖入文件夹，然后选择打开方式"
        case let .ready(summary) where summary.valid == 0:
            "未找到可打开的文件夹（\(summary.invalid) 项无效）"
        case let .ready(summary) where summary.invalid == 0:
            "已选择 \(summary.valid) 个文件夹"
        case let .ready(summary):
            "已选择 \(summary.valid) 个文件夹，\(summary.invalid) 项无效"
        case let .launching(target):
            "正在使用 \(target.displayName) 打开…"
        case let .completed(target, count):
            "已在 \(target.displayName) 中打开 \(count) 个文件夹"
        case let .failed(target, message):
            "无法使用 \(target.displayName) 打开：\(message)"
        case .automationPermissionDenied:
            "请前往系统设置 → 隐私与安全性 → 自动化，允许 Terminal Helper 控制终端"
        }
    }
}
