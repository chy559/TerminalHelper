# Terminal Helper Icon Design

## Goal

Create a clean, distinctive macOS application icon that communicates “open a folder in Terminal” at a glance and remains legible from 16×16 pixels through 1024×1024 pixels.

## Chosen Concept

Use a centered cyan-blue folder with a white terminal prompt (`>_`) on its front, placed on a clean white rounded-square macOS icon tile. The folder represents the dropped working directory; the prompt represents Terminal.app. Combining them into one compact symbol makes the app’s purpose recognizable without text.

Alternative concepts were considered and rejected:

- A standalone terminal window is familiar but does not communicate folder handling.
- A `TH` monogram is brandable but requires users to learn its meaning.
- A folder with a separate arrow and terminal window is descriptive but too detailed at Dock size.

## Visual Specification

- Canvas: 1024×1024, transparent outside the icon tile.
- Tile: centered macOS-style rounded square, approximately 86% of the canvas width, clean white with subtle light-gray edge definition and restrained depth.
- Folder: centered, approximately 58% of the canvas width, saturated cyan-blue (`#23B5E8` to `#4CC9F0`), simple silhouette with a clear upper-left tab.
- Terminal prompt: bold white `>_`, optically centered on the folder face, with thick strokes and generous spacing.
- Lighting: subtle top-left highlight and gentle internal depth; no glossy reflections or decorative texture.
- Style: vector-like, modern native macOS utility icon, friendly but professional.
- Avoid: words, tiny details, separate arrows, literal Terminal.app branding, excessive gradients, neon glow, watermark, and photographic materials.

## Small-Size Behavior

At 16–32 pixels, the icon should read as three shapes only: white tile, blue folder, white prompt. The prompt must remain distinguishable, so its stroke weight takes priority over typographic finesse. Downscaled assets will be generated from the inspected 1024-pixel master using high-quality macOS image scaling.

## Asset Pipeline

1. Generate a 1024×1024 raster master on a flat removable magenta chroma-key background.
2. Remove the chroma key locally and verify transparent corners, an alpha channel, and clean edges.
3. Inspect the 1024-pixel master and a 32-pixel preview.
4. Export the standard macOS iconset sizes: 16, 32, 128, 256, 512, and their `@2x` variants.
5. Compile `Resources/TerminalHelper.icns` with `iconutil`.

## Application Integration

- Add `CFBundleIconFile` with value `TerminalHelper` to `Resources/Info.plist`.
- Copy `Resources/TerminalHelper.icns` into the built app’s `Contents/Resources` directory.
- Keep the icon source PNG in `Resources/AppIcon/TerminalHelper-1024.png` so future iterations have a stable master.
- Document the custom icon in the README.

## Verification

- Validate the PNG alpha channel and dimensions.
- Confirm every required iconset size exists with exact pixel dimensions.
- Compile the `.icns` successfully.
- Rebuild the `.app` and confirm `CFBundleIconFile` resolves to the bundled resource.
- Run the existing 17-test suite, release build, plist lint, and strict code-sign verification.
- Inspect the final master and small preview visually for clarity and unintended artifacts.
