#!/bin/bash
set -euo pipefail

script_dir=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)
project_root=$(cd -- "$script_dir/.." && pwd -P)
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

printf 'Icon verification passed\n'
