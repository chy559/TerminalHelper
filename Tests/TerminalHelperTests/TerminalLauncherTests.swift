import Foundation
import Testing
@testable import TerminalHelper

@Suite("Terminal launcher")
struct TerminalLauncherTests {
    @Test("quotes paths using POSIX single-quote escaping")
    func quotesPath() {
        #expect(ShellQuoter.quote("/tmp/项目 O'Brien & Co") == "'/tmp/项目 O'\"'\"'Brien & Co'")
    }

    @Test("builds a new-window Terminal script and activates Terminal")
    func buildsScript() throws {
        let executor = RecordingScriptExecutor()
        let launcher = TerminalLauncher(executor: executor)

        try launcher.open(directory: URL(fileURLWithPath: "/tmp/O'Brien"))

        #expect(executor.sources == [
            "tell application \"Terminal\"\n  do script \"cd -- '/tmp/O'\\\"'\\\"'Brien'\"\n  activate\nend tell"
        ])
    }

    @Test("maps Apple event denial to automation permission error")
    func mapsPermissionDenial() {
        let executor = RecordingScriptExecutor(error: ScriptExecutionError(number: -1743, message: "Not authorized"))
        let launcher = TerminalLauncher(executor: executor)

        #expect(throws: TerminalLaunchError.automationPermissionDenied) {
            try launcher.open(directory: URL(fileURLWithPath: "/tmp/project"))
        }
    }
}

private final class RecordingScriptExecutor: ScriptExecuting {
    var sources: [String] = []
    let error: Error?

    init(error: Error? = nil) {
        self.error = error
    }

    func execute(source: String) throws {
        sources.append(source)
        if let error {
            throw error
        }
    }
}
