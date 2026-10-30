"""Ad-hoc E8 analysis helper (committed with the E8 record). Reads the backend's transfer log
(smartfade-capture JSONL without pcap frame numbers) with the committed decoding functions of
tools/protocol-analyzer/hsp_summary.py and prints a per-object change timeline keyed by JSONL seq.

Usage: py -3.13 -I e8_timeline.py <repo> <jsonl> [--id N ...] [--from T] [--to T] [--max N] [--all]
Read-only; never talks to a device.
"""
import argparse
import os
import sys

ap = argparse.ArgumentParser()
ap.add_argument("repo")
ap.add_argument("jsonl")
ap.add_argument("--id", type=int, action="append")
ap.add_argument("--from", dest="t_from", type=float)
ap.add_argument("--to", dest="t_to", type=float)
ap.add_argument("--max", type=int, default=40)
ap.add_argument("--all", action="store_true", help="also print unchanged payloads")
args = ap.parse_args()
sys.path.insert(0, os.path.join(args.repo, "tools", "protocol-analyzer"))
import hsp_summary as H  # noqa: E402

header, frames = H.load(args.jsonl)
notes = H.load_notes(args.jsonl)
events, errors = H.classify(frames)
last = {}
rows = []
ids = set(args.id or [])
for kind, hf, pf in H.pairs(events):
    if kind != "reply" or pf is None:
        continue
    p = H.decode_payload("in", pf["bytes"])
    key = H.object_key(hf, p)
    body = bytes(p["body"])
    prev = last.get(key)
    last[key] = body
    if ids and key[0] not in ids:
        continue
    if key[0] == 40:
        cnt, recs = H.dirty_records(body)
        desc = "list " + ",".join(str(r[0]) for r in recs)
    elif prev is None:
        desc = f"first ({len(body)} bytes): " + " ".join(f"[{i}]={b:02X}" for i, b in enumerate(body) if b)[:600]
    elif prev == body:
        if not args.all:
            continue
        desc = "unchanged"
    else:
        ch = [i for i in range(len(body)) if prev[i] != body[i]]
        parts = [f"[{i}]{prev[i]:02X}->{body[i]:02X}" for i in ch[:args.max]]
        more = f" ...(+{len(ch) - args.max})" if len(ch) > args.max else ""
        desc = f"{len(ch)} changed: " + " ".join(parts) + more
    rows.append((pf["t"], 1, f"t={pf['t']:10.6f} seq{pf['seq']:<6d} {H.name(key[0])}({key[0]}) pseq={p['seq']}: {desc}"))
for n in notes:
    rows.append((n["t"], 0, f"t={n['t']:10.6f} NOTE {n['text']}"))
for t, _, line in sorted(rows, key=lambda r: (r[0], r[1])):
    if (args.t_from is None or t >= args.t_from) and (args.t_to is None or t <= args.t_to):
        print(line)
