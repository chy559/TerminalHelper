import SwiftUI

struct DropView: View {
    @ObservedObject var coordinator: FolderOpenCoordinator
    @State private var isTargeted = false

    var body: some View {
        VStack(spacing: 20) {
            Image(systemName: "folder.badge.plus")
                .font(.system(size: 54, weight: .light))
                .foregroundStyle(isTargeted ? Color.accentColor : .secondary)

            Text(coordinator.statusText)
                .multilineTextAlignment(.center)
                .foregroundStyle(.secondary)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .padding(32)
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
            coordinator.open(urls)
            return !urls.isEmpty
        } isTargeted: {
            isTargeted = $0
        }
    }
}
