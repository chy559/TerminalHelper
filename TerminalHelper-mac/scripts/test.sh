#!/bin/bash
set -euo pipefail

clt_frameworks="/Library/Developer/CommandLineTools/Library/Developer/Frameworks"
clt_testing_framework="$clt_frameworks/Testing.framework"
clt_interop_libraries="/Library/Developer/CommandLineTools/Library/Developer/usr/lib"
active_developer_directory=""

if active_developer_directory=$(xcode-select -p 2>/dev/null) \
    && [[ "$active_developer_directory" == "/Library/Developer/CommandLineTools" ]] \
    && [[ -d "$clt_testing_framework" ]]; then
    exec swift test --enable-swift-testing --disable-xctest \
        -Xswiftc -F \
        -Xswiftc "$clt_frameworks" \
        -Xlinker -F \
        -Xlinker "$clt_frameworks" \
        -Xlinker -rpath \
        -Xlinker "$clt_frameworks" \
        -Xlinker -rpath \
        -Xlinker "$clt_interop_libraries" \
        -Xlinker -w \
        "$@"
fi

exec swift test --enable-swift-testing --disable-xctest "$@"
