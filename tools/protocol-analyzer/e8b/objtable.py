"""E8b: per-object payload counts (changed / identical vs previous of the id), first/last seq; latency maxima. Read-only.
Usage: py -3.13 -I objtable.py <repo> <jsonl>"""
import os, sys, collections
repo, path = sys.argv[1], sys.argv[2]
sys.path.insert(0, os.path.join(repo, "tools", "protocol-analyzer"))
import hsp_summary as H
header, frames = H.load(path)
events, _ = H.classify(frames)
last = {}; stats = collections.defaultdict(lambda: [0, 0, 0, None, None])
for kind, hf, pf in H.pairs(events):
    if kind != "reply" or pf is None:
        continue
    p = H.decode_payload("in", pf["bytes"])
    oid = p["id"]
    if oid == 40:
        continue
    b = bytes(p["body"]); s = stats[oid]
    s[0] += 1
    if oid in last:
        if last[oid] == b: s[2] += 1
        else: s[1] += 1
    if s[3] is None: s[3] = (pf["seq"], round(pf["t"], 3))
    s[4] = (pf["seq"], round(pf["t"], 3))
    last[oid] = b
for oid, s in sorted(stats.items()):
    print(f"{H.name(oid)}({oid}): payloads {s[0]} changed {s[1]} identical {s[2]} first {s[3]} last {s[4]}")
# latency maxima with seq
hdr = [(f["t"] - f["tSubmit"], f["seq"], f["t"]) for r, f in events if r == "reply"]
pl = [(f["t"] - f["tSubmit"], f["seq"], f["t"]) for r, f in events if r == "in-payload"]
out = [(f["t"] - f["tSubmit"], f["seq"], f["t"]) for r, f in events if r in ("poll", "fetch")]
for name, xs in (("reply header", hdr), ("payload", pl), ("OUT", out)):
    m = max(xs); print(f"max {name} submit->completion {m[0]*1000:.2f} ms at seq{m[1]} t={m[2]:.3f}")
# lists after the connect list: sizes and the last list time
lists = []
for kind, hf, pf in H.pairs(events):
    if kind == "reply" and pf is not None:
        p = H.decode_payload("in", pf["bytes"])
        if p["id"] == 40:
            cnt, recs = H.dirty_records(bytes(p["body"])); lists.append((pf["seq"], pf["t"], cnt, recs))
print("lists", len(lists), "last list", lists[-1][:3])
# identical-to-previous PlaybackInfo etc. after the connect list is covered above
# idle replies count
idle = sum(1 for r, f in events if r == "reply" and H.words(f["bytes"])[0] == 4 and H.words(f["bytes"])[1] == 0)
print("idle replies", idle)
