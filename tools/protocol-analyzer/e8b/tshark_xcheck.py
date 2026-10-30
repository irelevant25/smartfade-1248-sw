import json, sys
cap = {}
for line in open(sys.argv[2], encoding="utf-8"):
    f, d = line.rstrip("\n").split("\t")
    cap[int(f)] = d.replace(":", "").upper()
n = ok = bad = 0; missing = 0; used = set()
for line in open(sys.argv[1], encoding="utf-8"):
    r = json.loads(line)
    if r["type"] != "frame" or r["xfer"] != "bulk" or r["len"] == 0:
        continue
    fr = r["frames"][0] if r["dir"] == "out" else r["frames"][1]
    n += 1
    if fr not in cap:
        missing += 1; continue
    used.add(fr)
    if cap[fr] == r["data"]:
        ok += 1
    else:
        bad += 1
print("data-carrying bulk records", n, "equal", ok, "differ", bad, "no tshark capdata", missing)
print("tshark capdata frames not used by a bulk record:", sorted(set(cap) - used)[:20], len(set(cap) - used))
