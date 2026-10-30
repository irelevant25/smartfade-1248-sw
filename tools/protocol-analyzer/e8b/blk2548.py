"""Distinct values of LedStatus blocks 25-48 per capture, with the first seq of each value. Read-only.
Usage: py -3.13 -I blk2548.py <repo> <jsonl> [...]"""
import os, sys, collections
repo = sys.argv[1]
sys.path.insert(0, os.path.join(repo, "tools", "protocol-analyzer"))
import hsp_summary as H
for path in sys.argv[2:]:
    header, frames = H.load(path)
    events, _ = H.classify(frames)
    seen = collections.OrderedDict()
    for kind, hf, pf in H.pairs(events):
        if kind == "reply" and pf is not None:
            p = H.decode_payload("in", pf["bytes"])
            if p["id"] == 22:
                b = bytes(p["body"])
                vals = tuple(b[1 + 6 * (k - 1): 1 + 6 * k].hex() for k in range(25, 49))
                key = tuple(sorted(set(vals)))
                if key not in seen:
                    seen[key] = (pf["seq"], round(pf["t"], 3))
    print(path.rsplit("/", 1)[-1], {k: v for k, v in seen.items()})
