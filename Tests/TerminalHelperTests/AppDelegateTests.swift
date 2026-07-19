import Foundation
import Testing
@testable import TerminalHelper

@Suite("Application URL routing")
struct AppDelegateTests {
    @Test @MainActor
    func buffersURLsUntilAnOpenerIsAttached() {
        let delegate = AppDelegate()
        let opener = RecordingFolderOpener()
        let urls = [URL(fileURLWithPath: "/tmp/first"), URL(fileURLWithPath: "/tmp/second")]

        delegate.route(urls)
        #expect(opener.received.isEmpty)
        delegate.folderOpener = opener

        #expect(opener.received == [urls])
    }

    @Test @MainActor
    func forwardsURLsImmediatelyAfterAttachment() {
        let delegate = AppDelegate()
        let opener = RecordingFolderOpener()
        delegate.folderOpener = opener
        let urls = [URL(fileURLWithPath: "/tmp/project")]

        delegate.route(urls)

        #expect(opener.received == [urls])
    }
}

@MainActor
private final class RecordingFolderOpener: FolderOpening {
    var received: [[URL]] = []

    func open(_ urls: [URL]) {
        received.append(urls)
    }
}
