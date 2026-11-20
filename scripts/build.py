#!/usr/bin/env python3
"""构建确定性的 FunctionGraph C#(.NET Core 8.0) 部署 ZIP。"""
from __future__ import annotations

import argparse
import hashlib
import json
import shutil
import subprocess
import sys
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PROJECT = ROOT / "src" / "ProductApi" / "ProductApi.csproj"
OUTPUT = ROOT / "src" / "ProductApi" / "bin" / "Release" / "net8.0" / "publish"
BUILD = ROOT / "build"
ARCHIVE = BUILD / "function.zip"
FIXED_TIME = (1980, 1, 1, 0, 0, 0)
REQUIRED = {
    "ProductApi.dll",
    "ProductApi.deps.json",
    "ProductApi.runtimeconfig.json",
    "ProductApi.pdb",
    "HC.Serverless.Function.Common.dll",
}


def fail(message: str) -> None:
    print("[ERROR] " + message, file=sys.stderr)
    raise SystemExit(1)


def executable(explicit: str | None, name: str) -> str:
    candidate = explicit or shutil.which(name) or shutil.which(name + ".exe")
    if not candidate:
        fail(f"未找到 {name}，请传 --{name}")
    return candidate


def run(command: list[str]) -> None:
    result = subprocess.run(command, cwd=ROOT, check=False)
    if result.returncode:
        fail("命令失败: " + " ".join(command))


def zip_info(name: str) -> zipfile.ZipInfo:
    info = zipfile.ZipInfo(name, FIXED_TIME)
    info.compress_type = zipfile.ZIP_DEFLATED
    info.create_system = 3
    info.external_attr = 0o100644 << 16
    return info


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main() -> None:
    parser = argparse.ArgumentParser(description="构建确定性的 .NET 8 函数 ZIP")
    parser.add_argument("--dotnet")
    args = parser.parse_args()
    dotnet = executable(args.dotnet, "dotnet")

    run([dotnet, "restore", "--locked-mode", str(PROJECT)])
    run([dotnet, "publish", "-c", "Release", "--no-restore", "--no-self-contained", "-o", str(OUTPUT), str(PROJECT)])
    if not OUTPUT.is_dir():
        fail(f"构建输出目录不存在: {OUTPUT}")

    entries = sorted(
        (path for path in OUTPUT.iterdir() if path.is_file() and path.suffix.lower() != ".exe"),
        key=lambda path: path.name,
    )
    names = {path.name for path in entries}
    missing = REQUIRED - names
    if missing:
        fail("构建输出缺少: " + ", ".join(sorted(missing)))

    BUILD.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(ARCHIVE, "w") as package:
        for path in entries:
            package.writestr(zip_info(path.name), path.read_bytes())

    with zipfile.ZipFile(ARCHIVE) as package:
        packaged = package.namelist()
    if any("/" in name or name.lower().endswith(".exe") for name in packaged):
        fail("ZIP 必须为根目录平铺且不含 Windows apphost")

    digest = sha256(ARCHIVE)
    (BUILD / "sha256.json").write_text(
        json.dumps({"function.zip": digest, "entries": packaged}, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    print("[PASS] locked restore 完成")
    print("[PASS] Release publish 完成")
    print("[PASS] ZIP entries=" + ", ".join(packaged))
    print("[PASS] function.zip sha256=" + digest)


if __name__ == "__main__":
    main()
