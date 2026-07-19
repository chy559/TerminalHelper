import Combine
import Foundation

struct OpenSummary: Equatable {
    let succeeded: Int
    let failed: Int
}

enum OpenStatus: Equatable {
    case idle
    case completed(OpenSummary)
    case automationPermissionDenied(OpenSummary)
}

@MainActor
final class FolderOpenCoordinator: ObservableObject {
    @Published private(set) var status: OpenStatus = .idle

    private let planner: any FolderBatchPlanning
    private let launcher: any TerminalLaunching

    init(planner: any FolderBatchPlanning, launcher: any TerminalLaunching) {
        self.planner = planner
        self.launcher = launcher
    }

    func open(_ urls: [URL]) {
        guard !urls.isEmpty else { return }

        let plan = planner.makePlan(for: urls)
        var succeeded = 0
        var failed = plan.failures.count

        for folder in plan.validFolders {
            do {
                try launcher.open(directory: folder)
                succeeded += 1
            } catch TerminalLaunchError.automationPermissionDenied {
                failed += 1
                status = .automationPermissionDenied(.init(succeeded: succeeded, failed: failed))
                return
            } catch {
                failed += 1
            }
        }

        status = .completed(.init(succeeded: succeeded, failed: failed))
    }

    var statusText: String {
        switch status {
        case .idle:
            return "拖入文件夹，在终端中打开"
        case let .completed(summary) where summary.failed == 0:
            return "已在终端打开 \(summary.succeeded) 个文件夹"
        case let .completed(summary) where summary.succeeded == 0:
            return "未打开文件夹，请选择文件夹（\(summary.failed) 项失败）"
        case let .completed(summary):
            return "已打开 \(summary.succeeded) 个文件夹，\(summary.failed) 项失败"
        case .automationPermissionDenied:
            return "请前往系统设置 → 隐私与安全性 → 自动化，允许 Terminal Helper 控制终端"
        }
    }
}
