#!/bin/bash
set -euo pipefail

clt_frameworks="/Library/Developer/CommandLineTools/Library/Developer/Frameworks"
clt_testing_framework="$clt_frameworks/Testing.framework"
clt_interop_libraries="/Library/Developer/CommandLineTools/Library/Developer/usr/lib"

if [[ -d "$clt_testing_framework" ]]; then
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
