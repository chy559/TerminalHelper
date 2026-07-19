import SwiftUI

@main
struct TerminalHelperApp: App {
    @NSApplicationDelegateAdaptor(AppDelegate.self) private var appDelegate
    @StateObject private var coordinator = FolderOpenCoordinator(
        planner: FolderBatchPlanner(),
        launcher: TerminalLauncher()
    )

    var body: some Scene {
        WindowGroup {
            DropView(coordinator: coordinator)
                .onAppear {
                    appDelegate.folderOpener = coordinator
                }
        }
        .defaultSize(width: 480, height: 320)
    }
}
