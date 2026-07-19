// swift-tools-version: 6.0
import PackageDescription

let package = Package(
    name: "TerminalHelper",
    platforms: [.macOS(.v13)],
    targets: [
        .executableTarget(
            name: "TerminalHelper",
            swiftSettings: [.unsafeFlags(["-parse-as-library"])]
        ),
        .testTarget(name: "TerminalHelperTests", dependencies: ["TerminalHelper"]),
    ]
)
