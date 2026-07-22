import SwiftUI

@main
struct TerminalHelperApp: App {
    @NSApplicationDelegateAdaptor(AppDelegate.self) private var appDelegate
    @StateObject private var coordinator = WorkspaceOpenCoordinator(
        planner: FolderBatchPlanner(),
        launcher: WorkspaceLauncher()
    )

    var body: some Scene {
        WindowGroup {
            DropView(coordinator: coordinator)
                .onAppear {
                    appDelegate.folderReceiver = coordinator
                }
        }
        .defaultSize(width: 500, height: 440)
    }
}
