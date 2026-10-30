"""E9: every DirtyObjects list (records as id or id:a/b/c) in a time window, with the payloads that follow. Read-only."""
import os, sys
sys.stdout.reconfigure(encoding="utf-8")
repo, path, t0, t1 = sys.argv[1], sys.argv[2], float(sys.argv[3]), float(sys.argv[4])
sys.path.insert(0, os.path.join(repo, "tools", "protocol-analyzer"))
import hsp_summary as H
header, frames = H.load(path)
events, errs = H.classify(frames)
for kind, hf, pf in H.pairs(events):
    if kind == "data" and t0 <= hf["t"] <= t1:
        p = H.decode_payload("out", pf["bytes"])
        print(f"  WRITE seq{hf['seq']}/{pf['seq']} f{pf['dframe']} t={hf['t']:.3f} pseq={p['seq']} {H.name(p['id'])} body={H.hexs(p['body'])[:60]}")
    if kind != "reply" or pf is None or not (t0 <= pf["t"] <= t1):
        continue
    p = H.decode_payload("in", pf["bytes"])
    if p["id"] == 40:
        cnt, recs = H.dirty_records(bytes(p["body"]))
        print(f"LIST seq{pf['seq']} f{pf['dframe']} t={pf['t']:.3f} pseq={p['seq']} [" + ",".join(str(r[0]) if r[1:] == (0, 0, 0) else f"{r[0]}:{r[1]}/{r[2]}/{r[3]}" for r in recs) + "]")
