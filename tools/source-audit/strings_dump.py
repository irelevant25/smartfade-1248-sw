#!/usr/bin/env python3
"""Dump printable ASCII (A) and UTF-16LE (U) strings with file offsets. Read-only.

Usage: python strings_dump.py <file> [--min 5] > out.txt
"""
from __future__ import annotations

import argparse
import re


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("file")
    ap.add_argument("--min", type=int, default=5)
    args = ap.parse_args()
    data = open(args.file, "rb").read()
    n = args.min
    found = [(m.start(), "A", m.group().decode("ascii"))
             for m in re.finditer(rb"[\x20-\x7e]{%d,}" % n, data)]
    found += [(m.start(), "U", m.group().decode("utf-16le"))
              for m in re.finditer(rb"(?:[\x20-\x7e]\x00){%d,}" % n, data)]
    for off, kind, s in sorted(found):
        print(f"{off:08x} {kind} {s}")


if __name__ == "__main__":
    main()
