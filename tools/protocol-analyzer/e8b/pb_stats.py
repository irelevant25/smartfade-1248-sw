"""E8b PlaybackInfo statistics: record 1 step changes, pair sums; record 5 values in a window. Read-only.
Usage: py -3.13 -I pb_stats.py <repo> <jsonl> <t_from> <t_to>"""
import os, sys, statistics, collections
repo, path, t0, t1 = sys.argv[1], sys.argv[2], float(sys.argv[3]), float(sys.argv[4])
sys.path.insert(0, os.path.join(repo, "tools", "protocol-analyzer"))
import hsp_summary as H
header, frames = H.load(path)
events, _ = H.classify(frames)
r1 = []; r5 = []; others = collections.Counter(); b0 = collections.Counter()
for kind, hf, pf in H.pairs(events):
    if kind != "reply" or pf is None:
        continue
    p = H.decode_payload("in", pf["bytes"])
    if p["id"] != 17:
        continue
    b = bytes(p["body"]); b0[b[0]] += 1
    recs = [b[1 + 16 * k: 17 + 16 * k] for k in range(5)]
    r1.append((pf["seq"], pf["t"], recs[0])); r5.append((pf["seq"], pf["t"], recs[4]))
    for k in (1, 2, 3):
        others[recs[k].hex()] += 1
print("body[0] values", dict(b0))
print("records 2-4 distinct values", {k: v for k, v in others.items()})
eq01 = sum(1 for s, t, r in r1 if r[0] == r[1]); s255 = sum(1 for s, t, r in r1 if r[2] + r[3] == 255)
print("record 1: payloads", len(r1), "bytes0=1", eq01, "Live+Next=255", s255, "bytes4-9 const", collections.Counter(r[4:10].hex() for s, t, r in r1))
running = [(s, t, r) for s, t, r in r1 if t >= 975]
ch = [(running[i][1], running[i - 1][2][0], running[i][2][0]) for i in range(1, len(running)) if running[i][2][0] != running[i - 1][2][0]]
order_ok = sum(1 for t, a, b in ch if b == (a + 1) % 7)
durs = [ch[i][0] - ch[i - 1][0] for i in range(1, len(ch))]
print("record 1 from 975 s: step changes", len(ch), "in order", order_ok, "median step", round(statistics.median(durs), 3), "min", round(min(durs), 3), "max", round(max(durs), 3), "last payload", running[-1][0], round(running[-1][1], 3), running[-1][2].hex(" "))
s5 = sum(1 for s, t, r in r5 if r[2] + r[3] == 255)
print("record 5: payloads", len(r5), "Live+Next=255", s5, "bytes0=1", sum(1 for s, t, r in r5 if r[0] == r[1]))
for s, t, r in r5:
    if t0 <= t <= t1:
        print(f"  seq{s} t={t:.3f} r5={r.hex(' ').upper()} sum={r[2] + r[3]}")
