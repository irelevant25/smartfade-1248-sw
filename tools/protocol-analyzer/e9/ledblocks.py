"""E9: LedStatus blocks (1-based, 6 bytes: phase A RGB, phase B RGB) of the LedStatus payloads in a window. Read-only.
Usage: ledblocks.py <repo> <jsonl> <t0> <t1> <comma-separated blocks or 'changed'>"""
import os, sys
sys.stdout.reconfigure(encoding="utf-8")
repo, path, t0, t1, which = sys.argv[1], sys.argv[2], float(sys.argv[3]), float(sys.argv[4]), sys.argv[5]
sys.path.insert(0, os.path.join(repo, "tools", "protocol-analyzer"))
import hsp_summary as H
header, frames = H.load(path)
events, errs = H.classify(frames)
prev = None
def blk(body, k):
    return body[1 + 6 * (k - 1): 1 + 6 * k]
for kind, hf, pf in H.pairs(events):
    if kind != "reply" or pf is None:
        continue
    p = H.decode_payload("in", pf["bytes"])
    if p["id"] != 22:
        continue
    body = bytes(p["body"])
    if t0 <= pf["t"] <= t1:
        if which == "changed":
            ch = [k for k in range(1, 76) if prev is None or blk(body, k) != blk(prev, k)]
            print(f"seq{pf['seq']} f{pf['dframe']} t={pf['t']:.3f}: " + "; ".join(f"{k}:{H.hexs(blk(body, k))}" for k in ch))
        else:
            ks = [int(x) for x in which.split(",")]
            print(f"seq{pf['seq']} f{pf['dframe']} t={pf['t']:.3f}: " + "; ".join(f"{k}:{H.hexs(blk(body, k))}" for k in ks))
    prev = body
