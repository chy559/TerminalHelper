import Foundation

enum ShellQuoter {
    static func quote(_ value: String) -> String {
        "'" + value.replacingOccurrences(of: "'", with: "'\"'\"'") + "'"
    }
}

enum AppleScriptLiteral {
    static func encode(_ value: String) -> String {
        value
            .replacingOccurrences(of: "\\", with: "\\\\")
            .replacingOccurrences(of: "\"", with: "\\\"")
            .replacingOccurrences(of: "\r", with: "\\r")
            .replacingOccurrences(of: "\n", with: "\\n")
    }
}

protocol ScriptExecuting {
    func execute(source: String) throws
}

struct ScriptExecutionError: Error, Equatable {
    let number: Int?
    let message: String
}

struct NSAppleScriptExecutor: ScriptExecuting {
    func execute(source: String) throws {
        guard let script = NSAppleScript(source: source) else {
            throw ScriptExecutionError(number: nil, message: "Unable to create AppleScript")
        }

        var info: NSDictionary?
        script.executeAndReturnError(&info)
        if let info {
            throw ScriptExecutionError(
                number: (info[NSAppleScript.errorNumber] as? NSNumber)?.intValue,
                message: (info[NSAppleScript.errorMessage] as? String) ?? "Terminal automation failed"
            )
        }
    }
}

protocol TerminalLaunching {
    func open(directory: URL) throws
}

enum TerminalLaunchError: Error, Equatable {
    case automationPermissionDenied
    case scriptFailed(String)
}

struct TerminalLauncher: TerminalLaunching {
    let executor: any ScriptExecuting

    init(executor: any ScriptExecuting = NSAppleScriptExecutor()) {
        self.executor = executor
    }

    func open(directory: URL) throws {
        let command = "cd -- \(ShellQuoter.quote(directory.path))"
        let source = "tell application \"Terminal\"\n  do script \"\(AppleScriptLiteral.encode(command))\"\n  activate\nend tell"

        do {
            try executor.execute(source: source)
        } catch let error as ScriptExecutionError where error.number == -1743 {
            throw TerminalLaunchError.automationPermissionDenied
        } catch let error as ScriptExecutionError {
            throw TerminalLaunchError.scriptFailed(error.message)
        } catch {
            throw TerminalLaunchError.scriptFailed(error.localizedDescription)
        }
    }
}
