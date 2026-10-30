"""E8b: frame-by-frame cross-check of the backend's transfer log against the USBPcap-derived JSONL.
Read-only. Usage: py -3.13 -I xcheck.py <backend.jsonl> <usbpcap.jsonl>"""
import json, sys, datetime as dt, statistics, collections

def ts_us(s):
    s = s.replace("\u002B", "+")
    if s.endswith("Z"):
        s = s[:-1] + "+00:00"
    main, frac_tz = s.split(".")
    frac = frac_tz[:frac_tz.index("+")]
    base = dt.datetime.strptime(main, "%Y-%m-%dT%H:%M:%S").replace(tzinfo=dt.timezone.utc)
    return int(base.timestamp()) * 1_000_000 + int((frac + "0000000")[:7]) / 10

def load(p):
    hdr, fr = None, []
    for line in open(p, encoding="utf-8"):
        r = json.loads(line)
        if r["type"] == "header":
            hdr = r
        elif r["type"] == "frame":
            fr.append(r)
    return hdr, fr

bh, bf = load(sys.argv[1]); ph, pf = load(sys.argv[2])
b0 = ts_us(bh["captureStart"]); p0 = ts_us(ph["captureStart"])
bulk = [r for r in pf if r["xfer"] == "bulk"]
qs = {"0x04": [r for r in bulk if r["ep"] == "0x04"], "0x83": [r for r in bulk if r["ep"] == "0x83"]}
idx = {"0x04": 0, "0x83": 0}
ok = bad = split = 0; dsub = []; dcmp = []; splitlens = collections.Counter(); badlist = []
first_last = []
for r in bf:
    q = qs[r["ep"]]; i = idx[r["ep"]]
    L = r["len"]
    n = 2 if (r["dir"] == "in" and L > 64 and L % 64 != 0) else 1
    urbs = q[i:i + n]; idx[r["ep"]] = i + n
    data = "".join(u["data"] for u in urbs)
    lens_ok = n == 1 or (urbs[0]["len"] == (L // 64) * 64 and urbs[1]["len"] == L % 64)
    if data == r["data"] and lens_ok and all(u["status"] == "0x00000000" for u in urbs):
        ok += 1
    else:
        bad += 1; badlist.append(r["seq"])
    if n == 2:
        split += 1; splitlens[L] += 1
    bsub = b0 + r["tSubmit"] * 1e6; bcmp = ts_us(r["ts"])
    psub = p0 + urbs[0]["tSubmit"] * 1e6; pcmp = p0 + urbs[-1]["t"] * 1e6
    dsub.append((bsub - psub) / 1000); dcmp.append((bcmp - pcmp) / 1000)
    first_last.append((r["seq"], [u["frames"] for u in urbs]))
print("backend frames", len(bf), "matched", ok, "mismatched", bad, badlist[:10])
print("split IN reads", split, dict(sorted(splitlens.items())))
print("pcap bulk URBs used", idx, "of", {k: len(v) for k, v in qs.items()})
for name, xs in (("backend tSubmit - pcap submit (ms)", dsub), ("backend ts - pcap completion (ms)", dcmp)):
    print(name, "min %.3f median %.3f max %.3f" % (min(xs), statistics.median(xs), max(xs)))
print("first", first_last[0], "last", first_last[-1])
nonbulk = [r for r in pf if r["xfer"] != "bulk"]
for r in nonbulk:
    print("nonbulk seq%d t=%.6f ts=%s %s %s %s len=%d setup=%s status=%s frames=%s synth=%s data=%s" % (
        r["seq"], r["t"], r["ts"], r["dir"], r["ep"], r["xfer"], r["len"], r.get("setup"), r["status"], r["frames"], r.get("synthetic", False), r["data"][:80]))
