import AppKit
import Foundation

@MainActor
protocol ApplicationWorkspaceOpening {
    func applicationURL(forBundleIdentifier identifier: String) -> URL?
    func open(_ folders: [URL], withApplicationAt applicationURL: URL) async throws
}

@MainActor
protocol WorkspaceLaunching {
    func isAvailable(_ target: WorkspaceTarget) -> Bool
    func launch(folders: [URL], in target: WorkspaceTarget) async throws
}

enum WorkspaceLaunchError: Error, Equatable, LocalizedError {
    case targetUnavailable(WorkspaceTarget)

    var errorDescription: String? {
        switch self {
        case let .targetUnavailable(target):
            "未找到 \(target.displayName)，请先安装后重试"
        }
    }
}

@MainActor
struct WorkspaceLauncher: WorkspaceLaunching {
    let terminalLauncher: any TerminalLaunching
    let applicationWorkspace: any ApplicationWorkspaceOpening

    init(
        terminalLauncher: any TerminalLaunching = TerminalLauncher(),
        applicationWorkspace: any ApplicationWorkspaceOpening = NSWorkspaceApplicationOpener()
    ) {
        self.terminalLauncher = terminalLauncher
        self.applicationWorkspace = applicationWorkspace
    }

    func isAvailable(_ target: WorkspaceTarget) -> Bool {
        target == .terminal || applicationURL(for: target) != nil
    }

    func launch(folders: [URL], in target: WorkspaceTarget) async throws {
        switch target {
        case .terminal:
            for folder in folders {
                try terminalLauncher.open(directory: folder)
            }
        case .visualStudioCode, .intelliJIdea:
            guard let applicationURL = applicationURL(for: target) else {
                throw WorkspaceLaunchError.targetUnavailable(target)
            }
            try await applicationWorkspace.open(folders, withApplicationAt: applicationURL)
        }
    }

    private func applicationURL(for target: WorkspaceTarget) -> URL? {
        for identifier in target.bundleIdentifiers {
            if let url = applicationWorkspace.applicationURL(forBundleIdentifier: identifier) {
                return url
            }
        }
        return nil
    }
}

@MainActor
struct NSWorkspaceApplicationOpener: ApplicationWorkspaceOpening {
    let workspace: NSWorkspace

    init(workspace: NSWorkspace = .shared) {
        self.workspace = workspace
    }

    func applicationURL(forBundleIdentifier identifier: String) -> URL? {
        workspace.urlForApplication(withBundleIdentifier: identifier)
    }

    func open(_ folders: [URL], withApplicationAt applicationURL: URL) async throws {
        try await withCheckedThrowingContinuation {
            (continuation: CheckedContinuation<Void, Error>) in
            let configuration = NSWorkspace.OpenConfiguration()
            workspace.open(
                folders,
                withApplicationAt: applicationURL,
                configuration: configuration
            ) { _, error in
                if let error {
                    continuation.resume(throwing: error)
                } else {
                    continuation.resume()
                }
            }
        }
    }
}
