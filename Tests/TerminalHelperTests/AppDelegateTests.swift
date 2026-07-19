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

    @Test @MainActor
    func flushesMultipleBufferedCallsOnceInOrderWithoutReplayingOnReassignment() {
        let delegate = AppDelegate()
        let firstOpener = RecordingFolderOpener()
        let replacementOpener = RecordingFolderOpener()
        let first = URL(fileURLWithPath: "/tmp/first")
        let second = URL(fileURLWithPath: "/tmp/second")
        let third = URL(fileURLWithPath: "/tmp/third")
        let fourth = URL(fileURLWithPath: "/tmp/fourth")

        delegate.route([first])
        delegate.route([second, third])
        delegate.folderOpener = firstOpener

        #expect(firstOpener.received == [[first, second, third]])

        delegate.folderOpener = replacementOpener

        #expect(firstOpener.received == [[first, second, third]])
        #expect(replacementOpener.received.isEmpty)

        delegate.route([fourth])

        #expect(replacementOpener.received == [[fourth]])
    }
}

@MainActor
private final class RecordingFolderOpener: FolderOpening {
    var received: [[URL]] = []

    func open(_ urls: [URL]) {
        received.append(urls)
    }
}
