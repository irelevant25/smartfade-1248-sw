"""E9: timing and structure statistics of the data writes. Read-only."""
import os, sys, statistics as st
sys.stdout.reconfigure(encoding="utf-8")
repo, path = sys.argv[1], sys.argv[2]
sys.path.insert(0, os.path.join(repo, "tools", "protocol-analyzer"))
import hsp_summary as H
header, frames = H.load(path)
events, errs = H.classify(frames)
bulk = [f for f in frames if f["xfer"] == "bulk"]
idx = {f["seq"]: i for i, f in enumerate(bulk)}
hdr2pl, pl2next, ev_iv = [], [], []
lastw = None
same = 0; k0 = 0; prevmis = []
lastval = {}
hdr_w1 = {}
for kind, hf, pf in H.pairs(events):
    if kind != "data":
        continue
    w = H.words(hf["bytes"]); p = H.decode_payload("out", pf["bytes"])
    hdr_w1.setdefault((p["id"], len(p["body"])), set()).add(w[1])
    hdr2pl.append((pf["tSubmit"] - hf["tSubmit"]) * 1000)
    nxt = bulk[idx[pf["seq"]] + 1]
    pl2next.append(((nxt["tSubmit"] - pf["tSubmit"]) * 1000, nxt["seq"]))
    if lastw is not None:
        ev_iv.append((hf["tSubmit"] - lastw) * 1000)
    lastw = hf["tSubmit"]
    b = p["body"]
    if p["id"] == 20 and b[0] == 0:
        k0 += 1
        if b[2] == b[3]: same += 1
        if b[1] in lastval and lastval[b[1]] != b[3]:
            prevmis.append((p["seq"], pf["seq"], f"idx {b[1]:02X} prev {b[3]:02X} last wire {lastval[b[1]]:02X}"))
        lastval[b[1]] = b[2]
print("header w1 per (id, body len):", {k: sorted(v) for k, v in hdr_w1.items()})
print(f"header submit -> payload submit ms: min {min(hdr2pl):.3f} median {st.median(hdr2pl):.3f} max {max(hdr2pl):.3f}")
v = [x for x, _ in pl2next]
print(f"payload submit -> next OUT submit ms: min {min(v):.2f} median {st.median(v):.2f} max {max(v):.2f}")
iv = [x for x in ev_iv if x < 200]
print(f"write -> next write (gaps < 200 ms) n={len(iv)} min {min(iv):.2f} median {st.median(iv):.2f} max {max(iv):.2f}")
print("kind0", k0, "value == previous", same, "prev != last wire value of the index:", prevmis)
