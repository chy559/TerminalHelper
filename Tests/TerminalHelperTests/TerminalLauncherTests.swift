import Foundation
import Testing
@testable import TerminalHelper

@Suite("Terminal launcher")
struct TerminalLauncherTests {
    @Test("quotes paths using POSIX single-quote escaping")
    func quotesPath() {
        #expect(ShellQuoter.quote("/tmp/项目 O'Brien & Co") == "'/tmp/项目 O'\"'\"'Brien & Co'")
    }

    @Test("escapes AppleScript string literal characters")
    func escapesAppleScriptLiteralCharacters() {
        #expect(
            AppleScriptLiteral.encode("say \"C:\\Temp\"\r\nnext")
                == "say \\\"C:\\\\Temp\\\"\\r\\nnext"
        )
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

    @Test("preserves readable messages for non-permission script failures")
    func preservesScriptFailureMessage() {
        let executor = RecordingScriptExecutor(
            error: ScriptExecutionError(number: -2700, message: "Expected end of line")
        )
        let launcher = TerminalLauncher(executor: executor)

        #expect(throws: TerminalLaunchError.scriptFailed("Expected end of line")) {
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
