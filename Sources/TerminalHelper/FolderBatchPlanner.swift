import Foundation

protocol FolderBatchPlanning {
    func makePlan(for urls: [URL]) -> FolderBatchPlan
}

struct FolderBatchPlan: Equatable {
    let validFolders: [URL]
    let failures: [FolderInputFailure]
}

struct FolderInputFailure: Equatable {
    enum Reason: Equatable {
        case notFileURL
        case missing
        case notDirectory
        case unreadable
    }

    let url: URL
    let reason: Reason
}

struct FolderBatchPlanner: FolderBatchPlanning {
    let fileManager: FileManager

    init(fileManager: FileManager = .default) {
        self.fileManager = fileManager
    }

    func makePlan(for urls: [URL]) -> FolderBatchPlan {
        var seen = Set<String>()
        var valid: [URL] = []
        var failures: [FolderInputFailure] = []

        for original in urls {
            guard original.isFileURL else {
                failures.append(.init(url: original, reason: .notFileURL))
                continue
            }

            let url = original.standardizedFileURL
            guard seen.insert(url.path).inserted else { continue }

            var isDirectory: ObjCBool = false
            guard fileManager.fileExists(atPath: url.path, isDirectory: &isDirectory) else {
                failures.append(.init(url: url, reason: .missing))
                continue
            }
            guard isDirectory.boolValue else {
                failures.append(.init(url: url, reason: .notDirectory))
                continue
            }
            guard fileManager.isReadableFile(atPath: url.path),
                  fileManager.isExecutableFile(atPath: url.path) else {
                failures.append(.init(url: url, reason: .unreadable))
                continue
            }
            valid.append(url)
        }

        return FolderBatchPlan(validFolders: valid, failures: failures)
    }
}
