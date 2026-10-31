"""E12: the DirtyObjects lists (records as id or id:a/b/c), the PC -> console data writes and the action-log notes of
one capture, merged in time order; the large lists (downloads) are shown by their count only. Optional t range.
Read-only, stdlib only, never talks to a device.
Usage (from the repository root): py -3.13 -I -B tools/protocol-analyzer/e12/lists.py <repo> <jsonl> [t_from t_to]
"""
import os
import sys

sys.stdout.reconfigure(encoding="utf-8")
repo, path = sys.argv[1], sys.argv[2]
t_from = float(sys.argv[3]) if len(sys.argv) > 3 else float("-inf")
t_to = float(sys.argv[4]) if len(sys.argv) > 4 else float("inf")
sys.path.insert(0, os.path.join(repo, "tools", "protocol-analyzer"))
import hsp_summary as H  # noqa: E402

header, frames = H.load(path)
notes = H.load_notes(path)
events, _ = H.classify(frames)
rows = []
for kind, hf, pf in H.pairs(events):
    if pf is None:
        continue
    if kind == "data":
        p = H.decode_payload("out", pf["bytes"])
        body = bytes(p["body"])
        rows.append((hf["t"], f"{hf['t']:9.3f} seq{hf['seq']:<6d} f{pf['dframe']:<6d} WRITE pseq={p['seq']} {H.name(p['id'])}({p['id']}) "
                              f"[{len(body)}] {H.hexs(body[:16])}{' ...' if len(body) > 16 else ''}"))
    elif kind == "reply":
        q = H.decode_payload("in", pf["bytes"])
        if q["id"] != 40:
            continue
        cnt, recs = H.dirty_records(bytes(q["body"]))
        if cnt > 100:
            text = f"LIST of {cnt} records (download)"
        else:
            text = "list [" + ", ".join(f"{r[0]}" if r[1:] == (0, 0, 0) else f"{r[0]}:{r[1]}/{r[2]}/{r[3]}" for r in recs) + "]"
        rows.append((pf["t"], f"{pf['t']:9.3f} seq{pf['seq']:<6d} f{pf['dframe']:<6d} {text}"))
for n in notes:
    rows.append((n["t"], f"{n['t']:9.3f} NOTE {n['text'][:110]}"))
for t, line in sorted(rows, key=lambda r: r[0]):
    if t_from <= t <= t_to:
        print(line)
