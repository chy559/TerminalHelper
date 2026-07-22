# Platform Directory Layout Design

## Goal

Restructure the existing repository into a single Git-managed workspace with separate macOS and Windows project directories. The completed macOS application must remain functionally unchanged, while the Windows directory establishes a clean boundary for future development.

## Repository Layout

The repository root remains the shared version-control boundary:

```text
TerminalHelper/
├── .git/
├── .gitignore
├── README.md
├── TerminalHelper-mac/
└── TerminalHelper-windows/
```

`TerminalHelper-mac/` receives the complete current macOS project:

- `Package.swift`
- `Sources/`
- `Tests/`
- `Resources/`
- `scripts/`
- `docs/`
- the existing macOS-specific `README.md`
- `dist/Terminal Helper.app` and local Swift build output when present

`TerminalHelper-windows/` initially contains a short README that records the intended platform and prevents the development directory from disappearing from Git while it is otherwise empty.

The root README becomes a concise workspace guide linking to both platform projects. The root `.gitignore` remains shared so build output from either platform can be excluded consistently. Git-owned infrastructure such as `.git/` and `.worktrees/` stays at the repository root.

## Migration Rules

- Move tracked macOS files with Git-aware renames so file history remains traceable.
- Do not edit macOS Swift source, tests, resources, application metadata, icon assets, or build scripts as part of this migration.
- Keep the existing macOS README content with the macOS project.
- Move existing local `dist/` output under `TerminalHelper-mac/` so the currently built application remains directly available.
- Move local `.build/` output under `TerminalHelper-mac/` when present; Swift may rebuild cache entries whose absolute paths changed.
- Do not move unrelated or user-owned files into either platform directory.
- Do not begin Windows implementation during this migration.

## Build and Usage

After migration, macOS commands run from `TerminalHelper-mac/`:

```bash
cd TerminalHelper-mac
./scripts/test.sh
./scripts/build-app.sh
```

The packaged application is available at:

```text
TerminalHelper-mac/dist/Terminal Helper.app
```

Windows development will later be scoped entirely to `TerminalHelper-windows/`, except for intentionally shared root-level documentation or automation.

## Verification

The migration is complete only when:

- the full macOS test suite passes from `TerminalHelper-mac/`;
- the macOS Release build and application packaging succeed;
- the custom icon, Info.plist, and application signature verify successfully;
- the packaged application exists at its new path;
- Git reports the macOS files as renames where possible;
- the only functional content added is the root workspace documentation and Windows placeholder documentation;
- `git diff --check` reports no whitespace errors.

## Out of Scope

- Changes to macOS behavior, UI, icons, launch targets, or permissions
- Windows framework selection or application implementation
- Sharing code between the macOS and Windows projects
- Publishing releases or changing bundle/package identifiers
