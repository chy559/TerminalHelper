import Foundation
import Testing
@testable import TerminalHelper

@Suite("Application URL routing")
struct AppDelegateTests {
    @Test @MainActor
    func buffersURLsUntilAReceiverIsAttached() {
        let delegate = AppDelegate()
        let receiver = RecordingFolderReceiver()
        let urls = [URL(fileURLWithPath: "/tmp/first"), URL(fileURLWithPath: "/tmp/second")]

        delegate.route(urls)
        #expect(receiver.received.isEmpty)
        delegate.folderReceiver = receiver

        #expect(receiver.received == [urls])
    }

    @Test @MainActor
    func forwardsURLsImmediatelyAfterAttachment() {
        let delegate = AppDelegate()
        let receiver = RecordingFolderReceiver()
        delegate.folderReceiver = receiver
        let urls = [URL(fileURLWithPath: "/tmp/project")]

        delegate.route(urls)

        #expect(receiver.received == [urls])
    }

    @Test @MainActor
    func flushesMultipleBufferedCallsOnceInOrderWithoutReplayingOnReassignment() {
        let delegate = AppDelegate()
        let firstReceiver = RecordingFolderReceiver()
        let replacementReceiver = RecordingFolderReceiver()
        let first = URL(fileURLWithPath: "/tmp/first")
        let second = URL(fileURLWithPath: "/tmp/second")
        let third = URL(fileURLWithPath: "/tmp/third")
        let fourth = URL(fileURLWithPath: "/tmp/fourth")

        delegate.route([first])
        delegate.route([second, third])
        delegate.folderReceiver = firstReceiver

        #expect(firstReceiver.received == [[first, second, third]])

        delegate.folderReceiver = replacementReceiver

        #expect(firstReceiver.received == [[first, second, third]])
        #expect(replacementReceiver.received.isEmpty)

        delegate.route([fourth])

        #expect(replacementReceiver.received == [[fourth]])
    }
}

@MainActor
private final class RecordingFolderReceiver: FolderReceiving {
    var received: [[URL]] = []

    func receive(_ urls: [URL]) {
        received.append(urls)
    }
}
