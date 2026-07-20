import Foundation
import Testing
@testable import TerminalHelper

@Suite("Workspace launcher")
struct WorkspaceLauncherTests {
    @Test
    func targetBundleIdentifiersMatchSupportedApplications() {
        #expect(WorkspaceTarget.visualStudioCode.bundleIdentifiers == ["com.microsoft.VSCode"])
        #expect(WorkspaceTarget.intelliJIdea.bundleIdentifiers == [
            "com.jetbrains.intellij",
            "com.jetbrains.intellij.ce",
        ])
    }

    @Test @MainActor
    func terminalIsAlwaysAvailableAndOpensFoldersInOrder() async throws {
        let terminal = RecordingTerminalLauncher()
        let applications = RecordingApplicationWorkspace()
        let launcher = WorkspaceLauncher(
            terminalLauncher: terminal,
            applicationWorkspace: applications
        )
        let folders = [
            URL(fileURLWithPath: "/tmp/first"),
            URL(fileURLWithPath: "/tmp/second"),
        ]

        #expect(launcher.isAvailable(.terminal))
        try await launcher.launch(folders: folders, in: .terminal)

        #expect(terminal.opened == folders)
        #expect(applications.openRequests.isEmpty)
    }

    @Test @MainActor
    func visualStudioCodeUsesItsInstalledApplicationForOneBatch() async throws {
        let applicationURL = URL(fileURLWithPath: "/Applications/Visual Studio Code.app")
        let applications = RecordingApplicationWorkspace(installedApplications: [
            "com.microsoft.VSCode": applicationURL,
        ])
        let terminal = RecordingTerminalLauncher()
        let launcher = WorkspaceLauncher(
            terminalLauncher: terminal,
            applicationWorkspace: applications
        )
        let folders = [
            URL(fileURLWithPath: "/tmp/first"),
            URL(fileURLWithPath: "/tmp/second"),
        ]

        #expect(launcher.isAvailable(.visualStudioCode))
        try await launcher.launch(folders: folders, in: .visualStudioCode)

        #expect(terminal.opened.isEmpty)
        #expect(applications.openRequests == [
            .init(folders: folders, applicationURL: applicationURL),
        ])
    }

    @Test @MainActor
    func intelliJIdeaFallsBackToCommunityEdition() async throws {
        let applicationURL = URL(fileURLWithPath: "/Applications/IntelliJ IDEA CE.app")
        let applications = RecordingApplicationWorkspace(installedApplications: [
            "com.jetbrains.intellij.ce": applicationURL,
        ])
        let launcher = WorkspaceLauncher(
            terminalLauncher: RecordingTerminalLauncher(),
            applicationWorkspace: applications
        )
        let folder = URL(fileURLWithPath: "/tmp/project")

        #expect(launcher.isAvailable(.intelliJIdea))
        try await launcher.launch(folders: [folder], in: .intelliJIdea)

        #expect(applications.bundleIdentifierQueries == [
            "com.jetbrains.intellij",
            "com.jetbrains.intellij.ce",
            "com.jetbrains.intellij",
            "com.jetbrains.intellij.ce",
        ])
        #expect(applications.openRequests == [
            .init(folders: [folder], applicationURL: applicationURL),
        ])
    }

    @Test @MainActor
    func intelliJIdeaPrefersUltimateEditionWhenBothAreInstalled() async throws {
        let ultimateURL = URL(fileURLWithPath: "/Applications/IntelliJ IDEA.app")
        let communityURL = URL(fileURLWithPath: "/Applications/IntelliJ IDEA CE.app")
        let applications = RecordingApplicationWorkspace(installedApplications: [
            "com.jetbrains.intellij": ultimateURL,
            "com.jetbrains.intellij.ce": communityURL,
        ])
        let launcher = WorkspaceLauncher(
            terminalLauncher: RecordingTerminalLauncher(),
            applicationWorkspace: applications
        )
        let folder = URL(fileURLWithPath: "/tmp/project")

        try await launcher.launch(folders: [folder], in: .intelliJIdea)

        #expect(applications.bundleIdentifierQueries == ["com.jetbrains.intellij"])
        #expect(applications.openRequests == [
            .init(folders: [folder], applicationURL: ultimateURL),
        ])
    }

    @Test @MainActor
    func propagatesEditorOpenErrors() async {
        let applicationURL = URL(fileURLWithPath: "/Applications/Visual Studio Code.app")
        let applications = RecordingApplicationWorkspace(
            installedApplications: ["com.microsoft.VSCode": applicationURL],
            error: TestApplicationOpenError.failed
        )
        let launcher = WorkspaceLauncher(
            terminalLauncher: RecordingTerminalLauncher(),
            applicationWorkspace: applications
        )

        do {
            try await launcher.launch(
                folders: [URL(fileURLWithPath: "/tmp/project")],
                in: .visualStudioCode
            )
            Issue.record("Expected the application open error")
        } catch {
            #expect(error as? TestApplicationOpenError == .failed)
        }
    }

    @Test @MainActor
    func reportsAnUnavailableEditorWithoutOpeningAnything() async {
        let applications = RecordingApplicationWorkspace()
        let terminal = RecordingTerminalLauncher()
        let launcher = WorkspaceLauncher(
            terminalLauncher: terminal,
            applicationWorkspace: applications
        )

        #expect(!launcher.isAvailable(.visualStudioCode))

        do {
            try await launcher.launch(
                folders: [URL(fileURLWithPath: "/tmp/project")],
                in: .visualStudioCode
            )
            Issue.record("Expected an unavailable target error")
        } catch {
            #expect(error as? WorkspaceLaunchError == .targetUnavailable(.visualStudioCode))
        }

        #expect(terminal.opened.isEmpty)
        #expect(applications.openRequests.isEmpty)
    }
}

private enum TestApplicationOpenError: Error {
    case failed
}

private final class RecordingTerminalLauncher: TerminalLaunching {
    var opened: [URL] = []

    func open(directory: URL) throws {
        opened.append(directory)
    }
}

@MainActor
private final class RecordingApplicationWorkspace: ApplicationWorkspaceOpening {
    struct OpenRequest: Equatable {
        let folders: [URL]
        let applicationURL: URL
    }

    let installedApplications: [String: URL]
    let error: Error?
    var bundleIdentifierQueries: [String] = []
    var openRequests: [OpenRequest] = []

    init(installedApplications: [String: URL] = [:], error: Error? = nil) {
        self.installedApplications = installedApplications
        self.error = error
    }

    func applicationURL(forBundleIdentifier identifier: String) -> URL? {
        bundleIdentifierQueries.append(identifier)
        return installedApplications[identifier]
    }

    func open(_ folders: [URL], withApplicationAt applicationURL: URL) async throws {
        openRequests.append(.init(folders: folders, applicationURL: applicationURL))
        if let error {
            throw error
        }
    }
}
