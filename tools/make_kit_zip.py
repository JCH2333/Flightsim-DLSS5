#!/usr/bin/env python3
"""打包 XP12 组件包（dlss5-feeder-kit）。

内容 = 教程组件包（网络资源/DLSS5 的必需子集）+ ReShade Vulkan 层（assets/ReShade64.dll、ReShade64.json）
     + ReShade 框架头文件（assets/ReShade.fxh、ReShadeUI.fxh、DrawText.fxh）。
产出 server/dlss5/packages/<名称>，并打印大小与 SHA256（写入客户端 PackageCatalog 硬编码常量）。

用法: python tools/make_kit_zip.py
"""
import hashlib
import os
import sys
import zipfile

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
KIT_SRC = os.path.join(ROOT, "网络资源", "DLSS5")
ASSETS = os.path.join(ROOT, "src", "DLSS5Patcher", "assets")
OUT_DIR = os.path.join(ROOT, "server", "dlss5", "packages")
TAG = "v0.13.1-beta.1-dfc-1.4.8"
NAME = f"dlss5-feeder-kit-{TAG}.zip"

ROOT_FILES = [
    "dlss5-feed.addon64",
    "deep-fried-chicken.addon64",
    "deep-fried-chicken-nvngx.dll",
    "deep-fried-chicken.cfg",
    "nvngx_dlssnr.dll",
    "nvngx_dlss.dll",
]
ASSET_ROOT_FILES = ["ReShade64.dll", "ReShade64.json"]
SHADER_DIR = os.path.join(KIT_SRC, "reshade-shaders", "Shaders")
ASSET_SHADERS = ["ReShade.fxh", "ReShadeUI.fxh", "DrawText.fxh"]


def main() -> int:
    entries = []
    for f in ROOT_FILES:
        entries.append((os.path.join(KIT_SRC, f), f))
    for f in ASSET_ROOT_FILES:
        entries.append((os.path.join(ASSETS, f), f))
    for f in sorted(os.listdir(SHADER_DIR)):
        p = os.path.join(SHADER_DIR, f)
        if os.path.isfile(p):
            entries.append((p, f"reshade-shaders/Shaders/{f}"))
    for f in ASSET_SHADERS:
        entries.append((os.path.join(ASSETS, f), f"reshade-shaders/Shaders/{f}"))

    missing = [src for src, _ in entries if not os.path.isfile(src)]
    if missing:
        print("缺少文件：")
        for m in missing:
            print("  ", m)
        return 1

    os.makedirs(OUT_DIR, exist_ok=True)
    out_path = os.path.join(OUT_DIR, NAME)
    raw = 0
    with zipfile.ZipFile(out_path, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as z:
        for src, arc in entries:
            raw += os.path.getsize(src)
            z.write(src, arc)

    size = os.path.getsize(out_path)
    h = hashlib.sha256()
    with open(out_path, "rb") as fh:
        for chunk in iter(lambda: fh.read(1 << 20), b""):
            h.update(chunk)
    print(f"已生成 {out_path}")
    print(f"原始 {raw:,} B → 压缩 {size:,} B")
    print(f"SHA256 = {h.hexdigest()}")
    print(f"PackageSpec: new(\"xp12kit\", \"{NAME}\", {size}, \"{h.hexdigest()}\")")
    return 0


if __name__ == "__main__":
    sys.exit(main())
