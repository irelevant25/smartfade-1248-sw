"""E9: kind-0 ConsoleEvent writes grouped into bursts per fader index (gap > 1 s splits). Read-only."""
import os, sys, datetime
sys.stdout.reconfigure(encoding="utf-8")
repo, path = sys.argv[1], sys.argv[2]
sys.path.insert(0, os.path.join(repo, "tools", "protocol-analyzer"))
import hsp_summary as H
header, frames = H.load(path)
notes = H.load_notes(path)
events, errs = H.classify(frames)
writes = []
for kind, hf, pf in H.pairs(events):
    if kind == "data":
        p = H.decode_payload("out", pf["bytes"])
        writes.append((hf, pf, p))
k0 = [(hf, pf, p) for hf, pf, p in writes if p["id"] == 20 and p["body"][0] == 0]
print("kind0 count", len(k0))
bursts = []
for w in k0:
    idx = w[2]["body"][1]
    if bursts and bursts[-1][0] == idx and w[0]["t"] - bursts[-1][1][-1][0]["t"] < 1.5:
        bursts[-1][1].append(w)
    else:
        bursts.append((idx, [w]))
def last_note(t):
    best = None
    for n in notes:
        if n["t"] <= t: best = n
    return best
prev_wire = {}
for idx, ws in bursts:
    n = last_note(ws[0][0]["t"])
    vals = [w[2]["body"][2] for w in ws]
    prevs = [w[2]["body"][3] for w in ws]
    mism = []
    for w in ws:
        v, pv = w[2]["body"][2], w[2]["body"][3]
        if idx in prev_wire and prev_wire[idx] != pv:
            mism.append((w[2]["seq"], prev_wire[idx], pv))
        prev_wire[idx] = v
    print(f"idx=0x{idx:02X} n={len(ws)} pseq {ws[0][2]['seq']}..{ws[-1][2]['seq']} seq{ws[0][0]['seq']}..seq{ws[-1][1]['seq']} f{ws[0][1]['dframe']}..f{ws[-1][1]['dframe']} t={ws[0][0]['t']:.3f}..{ws[-1][0]['t']:.3f} first={H.hexs(ws[0][2]['body'])} last={H.hexs(ws[-1][2]['body'])} note='{n['text'][:50]}'@{n['t']:.3f}")
    print("    values:", " ".join(f"{v:02X}" for v in vals))
    print("    prevs: ", " ".join(f"{v:02X}" for v in prevs))
    if mism:
        print("    prev != last wire value:", mism)
