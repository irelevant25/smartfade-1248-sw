"""Compare the bulk transfers on the bus (a USBPcap-normalized JSONL) with the backend's own transfer log(s), in order:
direction and bytes of every bulk transfer. A read longer than 64 bytes is several URBs on the bus (F122: 200 = 192 + 8,
456 = 448 + 8), so a bus IN record whose length is a non-zero multiple of 64 is joined with the IN record that follows it
before the comparison; the number of joins is reported. Read-only."""
import json, sys


def bulk(path, join_multi_urb):
    out, joins = [], 0
    with open(path, encoding="utf-8") as fh:
        for line in fh:
            r = json.loads(line)
            if r.get("type") != "frame" or r.get("xfer") != "bulk" or r.get("synthetic"):
                continue
            if r.get("len", 0) == 0 and not r.get("data"):
                continue
            d = (r["dir"], (r.get("data") or "").upper())
            if join_multi_urb and out and out[-1][0] == "in" and d[0] == "in" and len(out[-1][1]) // 2 % 64 == 0 and len(out[-1][1]) > 0:
                out[-1] = ("in", out[-1][1] + d[1])
                joins += 1
            else:
                out.append(d)
    return out, joins


bus, joins = bulk(sys.argv[1], True)
log = []
for p in sys.argv[2:]:
    l, _ = bulk(p, False)
    log.extend(l)
print("bus bulk transfers", len(bus), "(multi-URB reads joined:", joins, ") backend log transfers", len(log))
diff = [(i, a, b) for i, (a, b) in enumerate(zip(bus, log)) if a != b]
print("differing (in order)", len(diff) + abs(len(bus) - len(log)))
for i, a, b in diff[:5]:
    print("  first difference at", i, "bus", a[0], a[1][:40], "log", b[0], b[1][:40])
polls = sum(1 for d, x in log if d == "out" and x.startswith("00"))
idle = sum(1 for d, x in log if d == "in" and x.startswith("04") and x[2:] == "0" * (len(x) - 2))
writes = [x for d, x in log if d == "out" and x.startswith("01") and len(x) == 24]
print("polls in the log", polls, "idle replies", idle, "data headers", len(writes))
