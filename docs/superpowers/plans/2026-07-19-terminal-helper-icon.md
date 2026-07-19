# Terminal Helper Icon Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Generate and integrate a polished custom macOS icon that visually combines a folder with a terminal prompt.

**Architecture:** A generated 1024-pixel alpha PNG is the stable source asset. Deterministic local scaling produces the standard macOS iconset, `iconutil` compiles it to one `.icns`, and the existing bundle script copies that resource into the signed application. A source-and-bundle verifier checks dimensions, alpha, metadata, and resource presence.

**Tech Stack:** built-in image generation, PNG with alpha, `sips`, `iconutil`, `plutil`, Bash, macOS `.icns`

## Global Constraints

- The visual must be a dark graphite macOS rounded-square tile with a centered cyan-blue folder and bold white `>_` prompt.
- The source master must be exactly 1024×1024 pixels with transparent corners.
- The symbol must remain legible at 16–32 pixels.
- Do not include words, watermark, separate arrows, Terminal.app branding, or decorative micro-detail.
- Keep `Resources/AppIcon/TerminalHelper-1024.png` as the stable source master.
- Build `Resources/TerminalHelper.icns` from all standard macOS iconset sizes.
- The signed app must contain `Contents/Resources/TerminalHelper.icns` and declare `CFBundleIconFile = TerminalHelper`.
- Do not add third-party dependencies.

---

### Task 1: Generate, Verify, and Integrate the App Icon

**Files:**
- Create: `Resources/AppIcon/TerminalHelper-1024.png`
- Create: `Resources/TerminalHelper.icns`
- Create: `scripts/verify-icon.sh`
- Modify: `Resources/Info.plist`
- Modify: `scripts/build-app.sh`
- Modify: `README.md`

**Interfaces:**
- Consumes: one inspected 1024×1024 alpha PNG and the existing application bundle pipeline.
- Produces: `Resources/TerminalHelper.icns` and a signed `dist/Terminal Helper.app` whose plist and Resources directory resolve the icon.

- [ ] **Step 1: Write the failing icon integration verifier**

Create executable `scripts/verify-icon.sh` using strict Bash. It must resolve the project root from its own location and assert:

```bash
master="$project_root/Resources/AppIcon/TerminalHelper-1024.png"
icns="$project_root/Resources/TerminalHelper.icns"
plist="$project_root/Resources/Info.plist"
bundled_icon="$project_root/dist/Terminal Helper.app/Contents/Resources/TerminalHelper.icns"

test -f "$master"
test -f "$icns"
test "$(sips -g pixelWidth "$master" | awk '/pixelWidth/ {print $2}')" = "1024"
test "$(sips -g pixelHeight "$master" | awk '/pixelHeight/ {print $2}')" = "1024"
test "$(sips -g hasAlpha "$master" | awk '/hasAlpha/ {print $2}')" = "yes"
test "$(plutil -extract CFBundleIconFile raw -o - "$plist")" = "TerminalHelper"
test -f "$bundled_icon"
cmp -s "$icns" "$bundled_icon"
```

Print `Icon verification passed` only after every assertion succeeds.

- [ ] **Step 2: Run the verifier and observe RED**

Run: `./scripts/verify-icon.sh`

Expected: non-zero exit because the master PNG and `.icns` do not exist and `CFBundleIconFile` has not been declared.

- [ ] **Step 3: Generate and inspect the 1024-pixel master**

Use the built-in image generation tool with this production prompt:

```text
Use case: logo-brand
Asset type: native macOS application icon source, 1024×1024
Primary request: a concise visual metaphor for opening a folder in Terminal
Scene/backdrop: perfectly flat solid #ff00ff chroma-key background for later removal; no texture, gradient, shadow, floor, or reflection in the backdrop
Subject: centered dark graphite macOS rounded-square tile; centered cyan-blue folder with a clear upper-left tab; bold white terminal prompt >_ integrated on the folder face
Style/medium: polished vector-like native macOS utility icon, minimal geometric shapes, restrained depth
Composition: centered and symmetrical, generous transparent-corner padding after key removal, folder about 58% of canvas width, prompt optically centered and thick enough for 16px display
Lighting/mood: subtle top-left highlight and gentle internal depth only
Constraints: no words besides the exact symbolic prompt >_; no arrows; no Terminal.app branding; no tiny detail; no watermark; do not use #ff00ff in the icon subject
Avoid: photorealism, glossy plastic, neon glow, busy gradients, thin prompt strokes, background shadow
```

Copy the generated source into the project, remove the chroma key using the imagegen skill helper, and save the alpha result at `Resources/AppIcon/TerminalHelper-1024.png`. Verify 1024×1024 dimensions, alpha, transparent corners, and inspect both the master and a 32×32 preview.

- [ ] **Step 4: Export and compile the iconset**

Create a temporary `TerminalHelper.iconset` and use `sips -z` to generate:

```text
icon_16x16.png       16×16
icon_16x16@2x.png    32×32
icon_32x32.png       32×32
icon_32x32@2x.png    64×64
icon_128x128.png     128×128
icon_128x128@2x.png  256×256
icon_256x256.png     256×256
icon_256x256@2x.png  512×512
icon_512x512.png     512×512
icon_512x512@2x.png  1024×1024
```

Run `iconutil -c icns <iconset> -o Resources/TerminalHelper.icns` and verify the output is non-empty.

- [ ] **Step 5: Integrate the icon resource**

Add to `Resources/Info.plist`:

```xml
<key>CFBundleIconFile</key>
<string>TerminalHelper</string>
```

After the existing Info.plist copy in `scripts/build-app.sh`, add:

```bash
cp "$project_root/Resources/TerminalHelper.icns" "$contents_dir/Resources/TerminalHelper.icns"
```

Update README build documentation to state that the generated app contains the custom folder/terminal icon and that the editable source master is under `Resources/AppIcon`.

- [ ] **Step 6: Build and verify GREEN**

Run:

```bash
./scripts/test.sh
swift build -c release
./scripts/build-app.sh
./scripts/verify-icon.sh
plutil -lint Resources/Info.plist
plutil -lint "dist/Terminal Helper.app/Contents/Info.plist"
codesign --verify --deep --strict "dist/Terminal Helper.app"
git diff --check
```

Expected: 17 tests pass, release build and app assembly succeed, the verifier prints `Icon verification passed`, both property lists are valid, strict signing passes, and Git reports no whitespace errors.

- [ ] **Step 7: Commit**

Run:

```bash
git add Resources/AppIcon/TerminalHelper-1024.png Resources/TerminalHelper.icns Resources/Info.plist scripts/build-app.sh scripts/verify-icon.sh README.md
git commit -m "feat: add custom application icon"
```
