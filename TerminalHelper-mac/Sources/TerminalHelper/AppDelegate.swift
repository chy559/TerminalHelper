import AppKit
import Foundation

@MainActor
final class AppDelegate: NSObject, NSApplicationDelegate {
    var folderReceiver: (any FolderReceiving)? {
        didSet {
            flushPendingURLs()
        }
    }

    private var pendingURLs: [URL] = []

    func application(_ application: NSApplication, open urls: [URL]) {
        route(urls)
    }

    func route(_ urls: [URL]) {
        guard let folderReceiver else {
            pendingURLs.append(contentsOf: urls)
            return
        }

        folderReceiver.receive(urls)
    }

    private func flushPendingURLs() {
        guard let folderReceiver, !pendingURLs.isEmpty else { return }

        let urls = pendingURLs
        pendingURLs.removeAll()
        folderReceiver.receive(urls)
    }
}
