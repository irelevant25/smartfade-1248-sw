#!/usr/bin/env python3
"""Statically unpack the SmartSoft installer WITHOUT running or installing anything.

Chain (see docs/protocol/smartsoft-analysis.md):
    SmartSoft 3.0.2.9.0.43b.exe   Microsoft Wextract self-extractor
      -> RT_RCDATA "CABINET"      Microsoft cabinet
        -> setup.exe, smartsoft.msi
           -> largest OLE stream  Microsoft cabinet (MSI Media table)
              -> files named by MSI File-table keys
              -> renamed to their real names using the MSI File table

The source file is only read. Output goes to a directory that must be outside
smartsoft-source-materials/ (default: analysis/extracted/<installer-stem>/), which is
git-ignored because it contains proprietary third-party binaries.

Requirements: Python 3.10+, `pip install pefile olefile`, and either Windows
`expand.exe` (built in) or `cabextract` on PATH.
"""
from __future__ import annotations

import argparse
import hashlib
import shutil
import struct
import subprocess
import sys
from pathlib import Path

import olefile
import pefile

RT_RCDATA = 10
MSI_B64 = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz._"


def sha256(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def extract_rcdata(exe: Path) -> dict[str, bytes]:
    pe = pefile.PE(str(exe))
    out: dict[str, bytes] = {}
    for rtype in pe.DIRECTORY_ENTRY_RESOURCE.entries:
        if rtype.struct.Id != RT_RCDATA:
            continue
        for entry in rtype.directory.entries:
            name = str(entry.name) if entry.name else str(entry.struct.Id)
            for lang in entry.directory.entries:
                s = lang.data.struct
                out[name] = pe.get_data(s.OffsetToData, s.Size)
    return out


def expand_cab(cab: Path, dest: Path) -> None:
    dest.mkdir(parents=True, exist_ok=True)
    if sys.platform == "win32":
        expand = Path(shutil.which("expand.exe", path=str(Path(r"C:\Windows\System32"))) or "expand.exe")
        subprocess.run([str(expand), str(cab), "-F:*", str(dest)], check=True, stdout=subprocess.DEVNULL)
    else:
        subprocess.run(["cabextract", "-q", "-d", str(dest), str(cab)], check=True)


def decode_msi_stream_name(name: str) -> str:
    """Decode the MSI base64-style OLE stream name encoding."""
    out = []
    for ch in name:
        c = ord(ch)
        if 0x3800 <= c < 0x4800:
            c -= 0x3800
            out.append(MSI_B64[c & 0x3F])
            out.append(MSI_B64[(c >> 6) & 0x3F])
        elif 0x4800 <= c < 0x4840:
            out.append(MSI_B64[c - 0x4800])
        elif c == 0x4840:
            out.append("!")  # table marker
        else:
            out.append(ch)
    return "".join(out)


def read_msi_file_table(msi: Path) -> dict[str, str]:
    """Return {File key: long file name} from the MSI File table.

    Assumes the standard File table schema (File s72, Component_ s72, FileName l255,
    FileSize i4, Version s72, Language s20, Attributes i2, Sequence i2), which is
    what Visual Studio setup projects emit.
    """
    ole = olefile.OleFileIO(str(msi))
    streams = {decode_msi_stream_name(p[0]): p for p in ole.listdir()}

    pool = ole.openstream(streams["!_StringPool"]).read()
    data = ole.openstream(streams["!_StringData"]).read()
    flags = struct.unpack_from("<H", pool, 2)[0]
    ref_size = 3 if flags & 0x8000 else 2

    strings = [""]  # string id 0 is the null string
    pos = 0
    i = 4
    while i + 4 <= len(pool):
        length, refs = struct.unpack_from("<HH", pool, i)
        i += 4
        if length == 0 and refs != 0:  # long string: real length in next entry
            length = (refs << 16) | struct.unpack_from("<H", pool, i)[0]
            i += 4
        strings.append(data[pos:pos + length].decode("cp1252"))
        pos += length

    table = ole.openstream(streams["!File"]).read()
    widths = [ref_size, ref_size, ref_size, 4, ref_size, ref_size, 2, 2]
    row_size = sum(widths)
    rows = len(table) // row_size
    columns: list[list[int]] = []
    off = 0
    for w in widths:
        col = []
        for r in range(rows):
            raw = table[off + r * w: off + r * w + w]
            col.append(int.from_bytes(raw, "little"))
        columns.append(col)
        off += rows * w

    result = {}
    for r in range(rows):
        key = strings[columns[0][r]]
        long_name = strings[columns[2][r]].split("|")[-1]
        result[key] = long_name
    return result


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("installer", type=Path)
    ap.add_argument("--out", type=Path, default=None)
    args = ap.parse_args()

    installer: Path = args.installer.resolve()
    repo = Path(__file__).resolve().parents[2]
    out: Path = (args.out or repo / "analysis" / "extracted" / installer.stem.replace(" ", "_")).resolve()
    if "smartsoft-source-materials" in out.parts:
        print("Refusing to write inside smartsoft-source-materials/ (immutable source material).")
        return 2

    work = out / "_work"
    work.mkdir(parents=True, exist_ok=True)

    print(f"installer sha256 {sha256(installer)}")
    res = extract_rcdata(installer)
    for k, v in sorted(res.items()):
        if k != "CABINET":
            print(f"  RCDATA {k}: {v.rstrip(bytes([0]))!r}")
    outer_cab = work / "outer.cab"
    outer_cab.write_bytes(res["CABINET"])
    expand_cab(outer_cab, work / "outer")

    msi = work / "outer" / "smartsoft.msi"
    ole = olefile.OleFileIO(str(msi))
    largest = max(ole.listdir(), key=ole.get_size)
    inner_cab = work / "inner.cab"
    inner_cab.write_bytes(ole.openstream(largest).read())
    ole.close()
    expand_cab(inner_cab, work / "inner")

    names = read_msi_file_table(msi)
    files_dir = out / "files"
    files_dir.mkdir(exist_ok=True)
    manifest = []
    for f in sorted((work / "inner").iterdir()):
        long_name = names.get(f.name)
        if not long_name:
            continue
        target = files_dir / long_name
        if target.exists():  # e.g. etchsp.sys exists for x86/amd64/ia64
            target = files_dir / f"{f.name}__{long_name}"
        shutil.copy2(f, target)
        manifest.append((target.name, target.stat().st_size, sha256(target)))
    for name in ("setup.exe", "smartsoft.msi"):
        p = work / "outer" / name
        manifest.append((f"(outer) {name}", p.stat().st_size, sha256(p)))

    with (out / "MANIFEST.tsv").open("w", encoding="utf-8") as fh:
        fh.write("file\tsize\tsha256\n")
        for n, s, h in manifest:
            fh.write(f"{n}\t{s}\t{h}\n")
            print(f"{s:>10}  {h}  {n}")
    print(f"\nExtracted {len(manifest)} files to {out}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
