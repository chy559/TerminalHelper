import AppKit
import Foundation

@MainActor
protocol FolderOpening: AnyObject {
    func open(_ urls: [URL])
}

extension FolderOpenCoordinator: FolderOpening {}

@MainActor
final class AppDelegate: NSObject, NSApplicationDelegate {
    var folderOpener: (any FolderOpening)? {
        didSet {
            flushPendingURLs()
        }
    }

    private var pendingURLs: [URL] = []

    func application(_ application: NSApplication, open urls: [URL]) {
        route(urls)
    }

    func route(_ urls: [URL]) {
        guard let folderOpener else {
            pendingURLs.append(contentsOf: urls)
            return
        }

        folderOpener.open(urls)
    }

    private func flushPendingURLs() {
        guard let folderOpener, !pendingURLs.isEmpty else { return }

        let urls = pendingURLs
        pendingURLs.removeAll()
        folderOpener.open(urls)
    }
}
