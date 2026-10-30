"""E8b cadence bins (same definitions as e8_health.py). Read-only. Usage: py -3.13 -I cadence.py <repo> <jsonl>"""
import os, sys, collections
repo, path = sys.argv[1], sys.argv[2]
sys.path.insert(0, os.path.join(repo, "tools", "protocol-analyzer"))
import hsp_summary as H
header, frames = H.load(path)
events, _ = H.classify(frames)
idle_pp, fetch_poll, pay_fetch = [], [], []
for i in range(len(events) - 2):
    (r0, f0), (r1, f1), (r2, f2) = events[i], events[i + 1], events[i + 2]
    if r0 == "poll" and r1 == "reply" and r2 == "poll":
        w = H.words(f1["bytes"])
        if w[0] == 4 and w[1] == 0:
            idle_pp.append((f2["tSubmit"] - f0["tSubmit"]) * 1000)
    if r0 == "fetch" and r1 == "poll":
        fetch_poll.append((f1["tSubmit"] - f0["tSubmit"]) * 1000)
    if r0 == "in-payload" and r1 == "fetch":
        pay_fetch.append((f1["tSubmit"] - f0["t"]) * 1000)
def bins(xs, edges):
    out = []
    for a, b in zip(edges, edges[1:]):
        out.append((a, b, sum(1 for x in xs if a <= x < b)))
    return out
print("idle", len(idle_pp), bins(idle_pp, [0, 21, 30, 40, 53, 64, 1000]))
print("payload->fetch", len(pay_fetch), bins(pay_fetch, [0, 21, 28, 32, 34, 36, 43, 44, 47, 1000]), "over 43 ms:", sum(1 for x in pay_fetch if x > 43))
print("fetch->poll", len(fetch_poll), bins(fetch_poll, [0, 11, 14, 18, 28, 32, 1000]), "over 28 ms:", sum(1 for x in fetch_poll if x > 28))
