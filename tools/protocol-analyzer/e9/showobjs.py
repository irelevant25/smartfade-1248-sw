"""E9: every fetched object of a show/system id (not the live ids 13,14,15,17,21,22,23 and not DirtyObjects) after a given t. Read-only."""
import os, sys
sys.stdout.reconfigure(encoding="utf-8")
repo, path, tmin = sys.argv[1], sys.argv[2], float(sys.argv[3])
sys.path.insert(0, os.path.join(repo, "tools", "protocol-analyzer"))
import hsp_summary as H
header, frames = H.load(path)
events, errs = H.classify(frames)
LIVE = {13, 14, 15, 17, 21, 22, 23, 40}
for kind, hf, pf in H.pairs(events):
    if kind != "reply" or pf is None or pf["t"] < tmin:
        continue
    w = H.words(hf["bytes"])
    p = H.decode_payload("in", pf["bytes"])
    if p["id"] in LIVE:
        continue
    print(f"seq{pf['seq']} f{pf['dframe']} t={pf['t']:.3f} pseq={p['seq']} {H.name(p['id'])}({p['id']}) echo a={w[3]} b={w[4]} c={w[5]} len={len(p['body'])}")
    print("   " + H.hexs(p["body"]))
