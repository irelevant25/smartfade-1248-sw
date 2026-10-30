"""E8b: FaderInfo records 1-24 bytes 0/1 by fader mode (ConsoleStatus body[0]); mode before the first
ConsoleStatus taken from argv[3] (0/1). Read-only. Usage: py -3.13 -I fader_bytes.py <repo> <jsonl> <initial mode>"""
import os, sys, collections
repo, path, mode = sys.argv[1], sys.argv[2], int(sys.argv[3])
sys.path.insert(0, os.path.join(repo, "tools", "protocol-analyzer"))
import hsp_summary as H
header, frames = H.load(path)
events, _ = H.classify(frames)
c = collections.Counter(); ex = {}
for kind, hf, pf in H.pairs(events):
    if kind != "reply" or pf is None:
        continue
    p = H.decode_payload("in", pf["bytes"]); b = bytes(p["body"])
    if p["id"] == 23:
        mode = b[0]
    elif p["id"] == 14:
        for n in range(1, 25):
            r = b[1 + 4 * (n - 1): 1 + 4 * n]
            if r[0] or r[1]:
                k = ("memory" if mode else "channel", "equal" if r[0] == r[1] else ("b1=0" if r[1] == 0 else "differ"))
                c[k] += 1
                ex.setdefault(k, (pf["seq"], n, r.hex(" ").upper()))
        for n in range(25, 49):
            r = b[1 + 4 * (n - 1): 1 + 4 * n]
            if r != (bytes([0, 0, 1, 0]) if n == 25 else bytes(4)):
                c[("rec25-48 changed", n)] += 1
print(dict(c)); print(ex)
