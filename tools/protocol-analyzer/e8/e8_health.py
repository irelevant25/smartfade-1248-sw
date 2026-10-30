"""Ad-hoc E8 session-health figures (committed with the E8 record). Uses the committed decoding
functions of tools/protocol-analyzer/hsp_summary.py. Read-only.

Usage: py -3.13 -I e8_health.py <repo> <jsonl>
"""
import collections
import os
import statistics
import sys

repo, path = sys.argv[1], sys.argv[2]
sys.path.insert(0, os.path.join(repo, "tools", "protocol-analyzer"))
import hsp_summary as H  # noqa: E402

OBSERVED_LISTED_IDS = {0, 2, 3, 4, 5, 8, 9, 12, 13, 14, 15, 17, 19, 21, 22, 23, 36, 37, 38, 39, 41}

header, frames = H.load(path)
events, errors = H.classify(frames)
print("framing errors", len(errors))
roles = collections.Counter(r for r, _ in events)
print("roles", dict(roles))
print("status values", collections.Counter(str(f.get("status")) for f in frames))
print("dirs/eps", collections.Counter((f["dir"], f["ep"], f["xfer"], f["iface"]) for f in frames))
print("OUT lengths", collections.Counter(f["len"] for f in frames if f["dir"] == "out"))

# per-transfer latency (submit -> completion)
lat_out = [(f["t"] - f["tSubmit"]) * 1000 for f in frames if f["dir"] == "out"]
lat_hdr = [(f["t"] - f["tSubmit"]) * 1000 for r, f in events if r == "reply"]
lat_pl = [(f["t"] - f["tSubmit"]) * 1000 for r, f in events if r == "in-payload"]

def st(name, xs):
    xs = list(xs)
    if xs:
        q = statistics.quantiles(xs, n=100)
        print(f"{name}: n={len(xs)} min={min(xs):.2f} p1={q[0]:.2f} median={statistics.median(xs):.2f} p99={q[98]:.2f} max={max(xs):.2f} ms")

st("OUT submit->completion", lat_out)
st("reply header IN submit->completion", lat_hdr)
st("payload IN submit->completion", lat_pl)

# idle poll cadence: poll whose reply is idle, next transfer a poll
seq = [(r, f) for r, f in events]
idle_pp, all_pp, fetch_poll, pay_fetch, poll_after_payload = [], [], [], [], []
polls = [f for r, f in seq if r == "poll"]
for a, b in zip(polls, polls[1:]):
    all_pp.append((b["tSubmit"] - a["tSubmit"]) * 1000)
for i in range(len(seq) - 2):
    r0, f0 = seq[i]
    r1, f1 = seq[i + 1]
    r2, f2 = seq[i + 2]
    if r0 == "poll" and r1 == "reply" and r2 == "poll":
        w = H.words(f1["bytes"])
        if w[0] == 4 and w[1] == 0:
            idle_pp.append((f2["tSubmit"] - f0["tSubmit"]) * 1000)
    if r0 == "fetch" and r1 == "poll":
        fetch_poll.append((f1["tSubmit"] - f0["tSubmit"]) * 1000)
    if r0 == "in-payload" and r1 == "fetch":
        pay_fetch.append((f1["tSubmit"] - f0["t"]) * 1000)
    if r0 == "in-payload" and r1 == "poll":
        poll_after_payload.append((f1["tSubmit"] - f0["t"]) * 1000)
st("idle poll -> next poll (submit)", idle_pp)
st("any poll -> next poll (submit)", all_pp)
st("fetch submit -> next poll submit", fetch_poll)
st("payload completion -> fetch submit", pay_fetch)
st("payload completion -> next poll submit (list with no fetch? / after object)", poll_after_payload)
hist = collections.Counter(int(x // 5) * 5 for x in idle_pp)
print("idle poll interval histogram (5-ms bins):", sorted(hist.items()))
print("floors: polls < 21 ms:", sum(1 for x in all_pp if x < 21), "; fetch->poll < 11 ms:", sum(1 for x in fetch_poll if x < 11),
      "; payload->fetch < 21 ms:", sum(1 for x in pay_fetch if x < 21))
print("idle intervals > 64 ms:", sum(1 for x in idle_pp if x > 64), "of", len(idle_pp))
# largest gaps between consecutive transfers (completion -> next submit)
gaps = sorted(((frames[i + 1]["tSubmit"] - frames[i]["t"]) * 1000, frames[i]["seq"], frames[i]["t"]) for i in range(len(frames) - 1))[-5:]
print("largest completion->next-submit gaps (ms, seq, t):", [(round(g, 2), s, round(t, 3)) for g, s, t in gaps])

# lists: ids, connect list, membership in ObservedListedIds, list sizes
lists = []
for kind, hf, pf in H.pairs(events):
    if kind == "reply" and pf is not None:
        p = H.decode_payload("in", pf["bytes"])
        if p["id"] == 40:
            cnt, recs = H.dirty_records(bytes(p["body"]))
            lists.append((pf, p, cnt, recs))
ids = collections.Counter(r[0] for _, _, _, recs in lists for r in recs)
print("listed ids:", dict(sorted(ids.items())), "all in ObservedListedIds:", set(ids) <= OBSERVED_LISTED_IDS)
print("records with a,b,c != 0:", sum(1 for _, _, _, recs in lists for r in recs if r[1] or r[2] or r[3]))
print("list sizes:", sorted(collections.Counter(c for _, _, c, _ in lists).items()))
pf, p, cnt, recs = lists[0]
print(f"connect list seq{pf['seq']} t={pf['t']:.6f} pseq={p['seq']} count={cnt}")
print("  ids in order:", " ".join(str(r[0]) for r in recs))
print("  id counts:", dict(collections.Counter(r[0] for r in recs)))
# PlaybackInfo-only lists over time
pb_only = [pf["t"] for pf, p, c, recs in lists if c == 1 and recs[0][0] == 17]
print("PlaybackInfo-only lists:", len(pb_only), "first", round(pb_only[0], 3), "last", round(pb_only[-1], 3))
by_bucket = collections.Counter(int(t // 20) * 20 for pf, p, c, recs in lists for r in recs if r[0] == 17 for t in [pf["t"]])
print("PlaybackInfo records per 20-s bucket:", sorted(by_bucket.items()))
# inbound pseq continuity
pseqs = []
for kind, hf, pf in H.pairs(events):
    if kind == "reply" and pf is not None:
        pseqs.append((pf["seq"], pf["bytes"][0], pf["bytes"][1]))
breaks = [(a, b) for a, b in zip(pseqs, pseqs[1:]) if (a[1] + 1) % 256 != b[1]]
print("payloads", len(pseqs), "first pseq", pseqs[0][1], "last", pseqs[-1][1], "breaks", len(breaks), "byte1 values", collections.Counter(x[2] for x in pseqs))
print("wraps", sum(1 for a, b in zip(pseqs, pseqs[1:]) if a[1] == 0xFF and b[1] == 0))
# body lengths per id
bl = collections.defaultdict(collections.Counter)
for kind, hf, pf in H.pairs(events):
    if kind == "reply" and pf is not None:
        p = H.decode_payload("in", pf["bytes"])
        bl[p["id"]][len(p["body"])] += 1
print("body lengths per id:", {k: dict(v) for k, v in sorted(bl.items())})
print("max IN length", max(f["len"] for f in frames if f["dir"] == "in"))
# notes
for n in H.load_notes(path):
    print("note", n["t"], n["text"][:120])
