"""Search the console manual PDF text for terms (committed with the E8 record, read-only).
Usage: py -3.13 -I manual_grep.py <pdf> <term> [<term> ...]
Prints PDF page number (1-based), printed page (PDF page - 6, per known-facts F5) and the line context.
"""
import sys

import pymupdf

pdf, terms = sys.argv[1], [t.lower() for t in sys.argv[2:]]
doc = pymupdf.open(pdf)
for i, page in enumerate(doc, 1):
    text = page.get_text()
    lines = text.splitlines()
    for j, line in enumerate(lines):
        low = line.lower()
        if any(t in low for t in terms):
            ctx = " / ".join(x.strip() for x in lines[max(0, j - 2): j + 4])
            print(f"PDF p.{i} (printed p.{i - 6}): {ctx[:400]}")
