import Foundation

enum WorkspaceTarget: String, CaseIterable, Equatable, Identifiable, Sendable {
    case terminal
    case visualStudioCode
    case intelliJIdea

    var id: Self { self }

    var displayName: String {
        switch self {
        case .terminal:
            "终端"
        case .visualStudioCode:
            "Visual Studio Code"
        case .intelliJIdea:
            "IntelliJ IDEA"
        }
    }

    var systemImageName: String {
        switch self {
        case .terminal:
            "terminal"
        case .visualStudioCode:
            "chevron.left.forwardslash.chevron.right"
        case .intelliJIdea:
            "hammer"
        }
    }

    var bundleIdentifiers: [String] {
        switch self {
        case .terminal:
            []
        case .visualStudioCode:
            ["com.microsoft.VSCode"]
        case .intelliJIdea:
            ["com.jetbrains.intellij", "com.jetbrains.intellij.ce"]
        }
    }
}
