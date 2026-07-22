#!/usr/bin/env python3
"""Verify that the Windows icon assets are reproducible from the macOS source."""

from __future__ import annotations

import hashlib
import struct
import sys
from pathlib import Path


PNG_SIGNATURE = b"\x89PNG\r\n\x1a\n"
EXPECTED_ICO_SIZES = [16, 24, 32, 48, 64, 128, 256]


def fail(message: str) -> None:
    raise ValueError(message)


def png_dimensions(path: Path) -> tuple[int, int]:
    data = path.read_bytes()
    if len(data) < 24 or data[:8] != PNG_SIGNATURE or data[12:16] != b"IHDR":
        fail(f"不是有效的 PNG / Invalid PNG: {path}")
    return struct.unpack(">II", data[16:24])


def ico_frames(path: Path) -> list[int]:
    data = path.read_bytes()
    if len(data) < 6:
        fail(f"不是有效的 ICO / Invalid ICO: {path}")

    reserved, image_type, count = struct.unpack_from("<HHH", data)
    if reserved != 0 or image_type != 1:
        fail(f"不是图标文件 / Not an icon file: {path}")
    if len(data) < 6 + count * 16:
        fail(f"ICO 目录不完整 / Truncated ICO directory: {path}")

    sizes: list[int] = []
    for index in range(count):
        offset = 6 + index * 16
        width_byte, height_byte, _, _, _, bit_count, byte_count, image_offset = struct.unpack_from(
            "<BBBBHHII", data, offset
        )
        width = width_byte or 256
        height = height_byte or 256
        if width != height:
            fail(f"ICO 帧不是正方形 / Non-square ICO frame: {width}x{height}")
        if bit_count != 32:
            fail(f"ICO 帧不是 32 位 / ICO frame is not 32-bit: {width}px ({bit_count}-bit)")
        if image_offset + byte_count > len(data):
            fail(f"ICO 帧数据越界 / ICO frame exceeds file bounds: {width}px")

        payload = data[image_offset : image_offset + byte_count]
        if payload.startswith(PNG_SIGNATURE):
            payload_width, payload_height = struct.unpack(">II", payload[16:24])
            if (payload_width, payload_height) != (width, height):
                fail(
                    "ICO PNG 帧尺寸不匹配 / ICO PNG frame dimensions do not match: "
                    f"{width}px vs {payload_width}x{payload_height}"
                )
        sizes.append(width)

    return sorted(sizes)


def main() -> int:
    windows_root = Path(__file__).resolve().parents[1]
    repository_root = windows_root.parent
    mac_source = repository_root / "TerminalHelper-mac/Resources/AppIcon/TerminalHelper-1024.png"
    windows_source = windows_root / "assets/TerminalHelper-1024.png"
    app_assets = windows_root / "src/TerminalHelper.Windows/Assets"

    if not mac_source.is_file():
        fail(f"缺少 macOS 图标源 / Missing macOS icon source: {mac_source}")
    if not windows_source.is_file():
        fail(f"缺少 Windows 图标源副本 / Missing Windows icon source copy: {windows_source}")

    mac_bytes = mac_source.read_bytes()
    windows_bytes = windows_source.read_bytes()
    if mac_bytes != windows_bytes:
        fail(
            "Windows 1024px 源与 macOS 源不完全相同 / "
            "Windows 1024px source is not byte-for-byte identical to the macOS source"
        )
    source_hash = hashlib.sha256(mac_bytes).hexdigest()
    source_dimensions = png_dimensions(windows_source)
    if source_dimensions != (1024, 1024):
        fail(
            "Windows 源尺寸错误 / Wrong Windows source dimensions: "
            f"{source_dimensions[0]}x{source_dimensions[1]}, expected 1024x1024"
        )

    expected_pngs = {
        "Square44x44Logo.png": (44, 44),
        "Square150x150Logo.png": (150, 150),
    }
    for file_name, expected_dimensions in expected_pngs.items():
        path = app_assets / file_name
        if not path.is_file():
            fail(f"缺少 PNG 资源 / Missing PNG asset: {path}")
        actual_dimensions = png_dimensions(path)
        if actual_dimensions != expected_dimensions:
            fail(
                f"PNG 尺寸错误 / Wrong PNG dimensions: {file_name} "
                f"is {actual_dimensions[0]}x{actual_dimensions[1]}, expected "
                f"{expected_dimensions[0]}x{expected_dimensions[1]}"
            )

    ico_path = app_assets / "TerminalHelper.ico"
    if not ico_path.is_file():
        fail(f"缺少 ICO 资源 / Missing ICO asset: {ico_path}")
    actual_sizes = ico_frames(ico_path)
    if actual_sizes != EXPECTED_ICO_SIZES:
        fail(f"ICO 帧尺寸错误 / Wrong ICO frame sizes: {actual_sizes}; expected {EXPECTED_ICO_SIZES}")

    print(f"图标资源验证通过 / Icon assets verified (source SHA-256: {source_hash})")
    print(f"ICO frames: {', '.join(f'{size}x{size}' for size in actual_sizes)}")
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (OSError, ValueError, struct.error) as error:
        print(f"图标资源验证失败 / Icon asset verification failed: {error}", file=sys.stderr)
        sys.exit(1)
