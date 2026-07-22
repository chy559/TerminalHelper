import SwiftUI

struct DropView: View {
    @ObservedObject var coordinator: WorkspaceOpenCoordinator
    @State private var isTargeted = false

    var body: some View {
        VStack(spacing: 18) {
            if coordinator.pendingFolders.isEmpty {
                emptyState
            } else {
                targetSelection
            }
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .padding(28)
        .background {
            RoundedRectangle(cornerRadius: 18, style: .continuous)
                .fill(isTargeted ? Color.accentColor.opacity(0.12) : Color.secondary.opacity(0.06))
                .overlay {
                    RoundedRectangle(cornerRadius: 18, style: .continuous)
                        .strokeBorder(
                            isTargeted ? Color.accentColor : Color.secondary.opacity(0.35),
                            style: StrokeStyle(lineWidth: isTargeted ? 2 : 1, dash: [7])
                        )
                }
        }
        .padding(24)
        .animation(.easeOut(duration: 0.15), value: isTargeted)
        .dropDestination(for: URL.self) { urls, _ in
            coordinator.receive(urls)
            return !urls.isEmpty
        } isTargeted: {
            isTargeted = $0
        }
    }

    private var emptyState: some View {
        VStack(spacing: 18) {
            Image(systemName: "folder.badge.plus")
                .font(.system(size: 56, weight: .light))
                .foregroundStyle(isTargeted ? Color.accentColor : .secondary)

            VStack(spacing: 7) {
                Text("拖入文件夹")
                    .font(.title2.weight(.semibold))

                Text(coordinator.statusText)
                    .font(.callout)
                    .multilineTextAlignment(.center)
                    .foregroundStyle(.secondary)
            }
        }
    }

    private var targetSelection: some View {
        VStack(spacing: 16) {
            VStack(spacing: 6) {
                Image(systemName: "folder.fill")
                    .font(.system(size: 34, weight: .medium))
                    .foregroundStyle(Color.accentColor)

                Text("已选择 \(coordinator.pendingFolders.count) 个文件夹")
                    .font(.title3.weight(.semibold))

                Text("选择一个打开方式")
                    .font(.callout)
                    .foregroundStyle(.secondary)
            }

            VStack(spacing: 10) {
                ForEach(WorkspaceTarget.allCases) { target in
                    targetButton(for: target)
                }
            }

            Text(coordinator.statusText)
                .font(.caption)
                .multilineTextAlignment(.center)
                .foregroundStyle(statusColor)
                .lineLimit(3)

            Button("重新选择文件夹") {
                coordinator.reset()
            }
            .buttonStyle(.link)
            .disabled(isLaunching)
        }
    }

    private func targetButton(for target: WorkspaceTarget) -> some View {
        let available = coordinator.isAvailable(target)

        return Button {
            Task {
                await coordinator.launch(in: target)
            }
        } label: {
            HStack(spacing: 12) {
                Image(systemName: target.systemImageName)
                    .frame(width: 22)
                    .font(.system(size: 17, weight: .medium))

                Text(target.displayName)
                    .fontWeight(.medium)

                Spacer()

                if isLaunchingTarget(target) {
                    ProgressView()
                        .controlSize(.small)
                } else if !available {
                    Text("未安装")
                        .font(.caption)
                        .foregroundStyle(.tertiary)
                } else {
                    Image(systemName: "chevron.right")
                        .font(.caption.weight(.semibold))
                        .foregroundStyle(.tertiary)
                }
            }
            .contentShape(Rectangle())
            .padding(.horizontal, 16)
            .frame(height: 48)
            .background {
                RoundedRectangle(cornerRadius: 12, style: .continuous)
                    .fill(Color(nsColor: .controlBackgroundColor))
            }
            .overlay {
                RoundedRectangle(cornerRadius: 12, style: .continuous)
                    .strokeBorder(Color.secondary.opacity(0.18))
            }
        }
        .buttonStyle(.plain)
        .disabled(!available || isLaunching)
        .opacity(available ? 1 : 0.55)
        .accessibilityLabel(available ? target.displayName : "\(target.displayName)，未安装")
    }

    private var isLaunching: Bool {
        if case .launching = coordinator.status {
            return true
        }
        return false
    }

    private func isLaunchingTarget(_ target: WorkspaceTarget) -> Bool {
        if case let .launching(activeTarget) = coordinator.status {
            return activeTarget == target
        }
        return false
    }

    private var statusColor: Color {
        switch coordinator.status {
        case .failed, .automationPermissionDenied:
            .red
        default:
            .secondary
        }
    }
}
