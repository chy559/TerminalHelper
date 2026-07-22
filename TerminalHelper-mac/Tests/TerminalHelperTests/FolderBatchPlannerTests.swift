import Foundation
import Testing
@testable import TerminalHelper

@Suite("Folder batch planner")
struct FolderBatchPlannerTests {
    @Test("preserves order while removing duplicate standardized paths")
    func preservesOrderAndRemovesDuplicates() throws {
        let root = FileManager.default.temporaryDirectory
            .appending(path: UUID().uuidString, directoryHint: .isDirectory)
        let first = root.appending(path: "第一 个", directoryHint: .isDirectory)
        let second = root.appending(path: "second", directoryHint: .isDirectory)
        try FileManager.default.createDirectory(at: first, withIntermediateDirectories: true)
        try FileManager.default.createDirectory(at: second, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: root) }

        let plan = FolderBatchPlanner().makePlan(for: [first, second, first])

        #expect(plan.validFolders == [first.standardizedFileURL, second.standardizedFileURL])
        #expect(plan.failures.isEmpty)
    }

    @Test("rejects regular files and missing paths")
    func rejectsFilesAndMissingPaths() throws {
        let root = FileManager.default.temporaryDirectory
            .appending(path: UUID().uuidString, directoryHint: .isDirectory)
        try FileManager.default.createDirectory(at: root, withIntermediateDirectories: true)
        let file = root.appending(path: "note.txt")
        let missing = root.appending(path: "missing", directoryHint: .isDirectory)
        try Data("x".utf8).write(to: file)
        defer { try? FileManager.default.removeItem(at: root) }

        let plan = FolderBatchPlanner().makePlan(for: [file, missing])

        #expect(plan.validFolders.isEmpty)
        #expect(plan.failures.map(\.reason) == [.notDirectory, .missing])
    }

    @Test("rejects non-file URLs before interpreting their paths")
    func rejectsNonFileURLWithExistingDirectoryPath() throws {
        let directory = FileManager.default.temporaryDirectory
            .appending(path: UUID().uuidString, directoryHint: .isDirectory)
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: directory) }
        let httpURL = try #require(URL(string: "https://example.invalid\(directory.path)"))

        let plan = FolderBatchPlanner().makePlan(for: [httpURL])

        #expect(plan.validFolders.isEmpty)
        #expect(plan.failures == [.init(url: httpURL, reason: .notFileURL)])
    }

    @Test("rejects readable directories without search permission")
    func rejectsDirectoryWithoutSearchPermission() throws {
        let root = FileManager.default.temporaryDirectory
            .appending(path: UUID().uuidString, directoryHint: .isDirectory)
        let directory = root.appending(path: "readable-only", directoryHint: .isDirectory)
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        try FileManager.default.setAttributes([.posixPermissions: 0o400], ofItemAtPath: directory.path)
        defer {
            try? FileManager.default.setAttributes([.posixPermissions: 0o700], ofItemAtPath: directory.path)
            try? FileManager.default.removeItem(at: root)
        }
        try #require(FileManager.default.isReadableFile(atPath: directory.path))
        try #require(!FileManager.default.isExecutableFile(atPath: directory.path))

        let plan = FolderBatchPlanner().makePlan(for: [directory])

        #expect(plan.validFolders.isEmpty)
        #expect(plan.failures == [.init(url: directory.standardizedFileURL, reason: .unreadable)])
    }
}
