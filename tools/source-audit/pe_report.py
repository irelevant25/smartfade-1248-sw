#!/usr/bin/env python3
"""Print a static PE report: machine, build timestamp, .NET/CLR flag, version resources,
imports (Qt imports summarised), exports. Read-only.

Usage: python pe_report.py <file.exe|file.dll|file.sys> [...]
"""
from __future__ import annotations

import datetime
import sys

import pefile


def report(path: str) -> None:
    pe = pefile.PE(path)
    ts = datetime.datetime.fromtimestamp(pe.FILE_HEADER.TimeDateStamp, datetime.UTC)
    clr = pe.OPTIONAL_HEADER.DATA_DIRECTORY[14].VirtualAddress != 0
    print(f"===== {path}")
    print(f"machine=0x{pe.FILE_HEADER.Machine:x} timestamp={ts.isoformat()} "
          f"linker={pe.OPTIONAL_HEADER.MajorLinkerVersion}.{pe.OPTIONAL_HEADER.MinorLinkerVersion} clr={clr}")
    for fi in getattr(pe, "FileInfo", []):
        for entry in fi:
            for st in getattr(entry, "StringTable", []):
                for k, v in st.entries.items():
                    print(f"  version: {k.decode()} = {v.decode(errors='replace')}")
    for dll in getattr(pe, "DIRECTORY_ENTRY_IMPORT", []):
        names = [i.name.decode() if i.name else f"#{i.ordinal}" for i in dll.imports]
        if dll.dll.decode().lower().startswith("qt"):
            print(f"  import {dll.dll.decode()}: {len(names)} symbols")
        else:
            print(f"  import {dll.dll.decode()}: {', '.join(names)}")
    if hasattr(pe, "DIRECTORY_ENTRY_EXPORT"):
        exports = [e.name.decode() if e.name else f"#{e.ordinal}" for e in pe.DIRECTORY_ENTRY_EXPORT.symbols]
        print(f"  exports: {', '.join(exports)}")


if __name__ == "__main__":
    for p in sys.argv[1:]:
        report(p)
