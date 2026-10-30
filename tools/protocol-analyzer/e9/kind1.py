"""E9: kind-1 ConsoleEvent writes, paired press/release per code, with the hold time, the preceding log note and the
next list. Read-only."""
import os, sys, collections
sys.stdout.reconfigure(encoding="utf-8")
repo, path = sys.argv[1], sys.argv[2]
sys.path.insert(0, os.path.join(repo, "tools", "protocol-analyzer"))
import hsp_summary as H
header, frames = H.load(path)
notes = H.load_notes(path)
events, errs = H.classify(frames)
pl = list(H.pairs(events))
def last_note(t):
    best = None
    for n in notes:
        if n["t"] <= t and not n["text"].startswith("analyst"): best = n
    return best
k1 = []
for i, (kind, hf, pf) in enumerate(pl):
    if kind == "data":
        p = H.decode_payload("out", pf["bytes"])
        if p["id"] == 20 and p["body"][0] == 1:
            k1.append((i, hf, pf, p))
cnt = collections.Counter((p["body"][1], p["body"][2]) for _, _, _, p in k1)
print("kind1", len(k1), dict(sorted(cnt.items())))
open_press = {}
for i, hf, pf, p in k1:
    code, st = p["body"][1], p["body"][2]
    if st == 1:
        open_press[code] = (hf, pf, p)
        continue
    ph, pp, ppp = open_press.pop(code)
    n = last_note(ph["t"])
    print(f"code 0x{code:02X} press pseq {ppp['seq']} seq{ph['seq']}/{pp['seq']} f{ph['dframe']}/f{pp['dframe']} t={ph['t']:.3f} release pseq {p['seq']} seq{hf['seq']}/{pf['seq']} f{hf['dframe']}/f{pf['dframe']} t={hf['t']:.3f} held {hf['t']-ph['t']:.3f}s note '{n['text'][:45]}' @{n['t']:.3f} (+{ph['t']-n['t']:.2f}s)")
print("unreleased:", open_press)
