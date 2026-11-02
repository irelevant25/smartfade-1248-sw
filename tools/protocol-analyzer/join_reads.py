"""Join the multi-URB reads of a normalized USBPcap JSONL into whole transfers. Read-only on its input.

On the bus a read longer than 64 bytes is several URBs (F122: 200 = 192 + 8, 456 = 448 + 8, 519 = 512 + 7), so a bus
file shows an IN record whose length is a non-zero multiple of 64 followed by the IN record with the rest. The backend's
own transfer log has whole transfers. This script writes a copy of the bus file in which such pairs are one frame (the
first frame's seq, t and tSubmit, the joined data and length; a note "joined" is added to the frame), so that the tools
written for whole transfers (`e11/writes.py`, `hsp_summary.py timeline`) read the bus like a backend log. The rule is
the one of `e11/bus_vs_log.py`. Notes and non-bulk frames are copied unchanged.
Usage: py -3.13 -I join_reads.py <bus.jsonl> <joined.jsonl>
"""
import json
import sys

src, dst = sys.argv[1], sys.argv[2]
joins = 0
pending = None
with open(src, encoding="utf-8") as fin, open(dst, "w", encoding="utf-8", newline="\n") as fout:
    for line in fin:
        r = json.loads(line)
        if r.get("type") == "frame" and r.get("xfer") == "bulk" and r.get("dir") == "in" and not r.get("synthetic"):
            if pending is not None:
                pending["data"] += r["data"]
                pending["len"] = len(pending["data"]) // 2
                pending["joined"] = pending.get("joined", 1) + 1
                joins += 1
                n = len(r["data"]) // 2
                if n % 64 == 0 and n > 0:
                    continue        # a further part may follow (never seen; kept for completeness)
                fout.write(json.dumps(pending, ensure_ascii=False) + "\n")
                pending = None
                continue
            n = len(r["data"]) // 2
            if n % 64 == 0 and n > 0:
                pending = r
                continue
            fout.write(line if line.endswith("\n") else line + "\n")
            continue
        if pending is not None:
            fout.write(json.dumps(pending, ensure_ascii=False) + "\n")
            pending = None
        fout.write(line if line.endswith("\n") else line + "\n")
    if pending is not None:
        fout.write(json.dumps(pending, ensure_ascii=False) + "\n")
print(f"joined {joins} multi-URB reads -> {dst}")
