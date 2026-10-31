"""E12b: every IN transfer where a reply header was due (hsp_summary roles "reply" and "in-unexpected") whose submit ->
completion time exceeds a threshold (default 12 ms), every failed one, the distribution (median, p99, p99.9, max), and
the poll-to-poll intervals above 100 ms. Used for the cancelled read of E12b seq 85008 during the console's SD save.
Read-only, stdlib only, never talks to a device.
Usage (from the repository root): py -3.13 -I -B tools/protocol-analyzer/e12b/reply_latency.py <repo> <jsonl> [threshold_ms]
"""
import os
import sys

sys.stdout.reconfigure(encoding="utf-8")
repo, path = sys.argv[1], sys.argv[2]
thr = float(sys.argv[3]) if len(sys.argv) > 3 else 12.0
sys.path.insert(0, os.path.join(repo, "tools", "protocol-analyzer"))
import hsp_summary as H  # noqa: E402

header, frames = H.load(path)
events, errors = H.classify(frames)
n = 0
lat = []
for role, f in events:
    if role in ("reply", "in-unexpected"):
        d = (f["t"] - f["tSubmit"]) * 1000
        lat.append(d)
        if d > thr or role == "in-unexpected":
            n += 1
            print(f"{role:13s} seq{f['seq']} f{f['dframe']} t={f['t']:.6f} submit->complete {d:8.3f} ms len {len(f['bytes'])} status {f.get('status')}")
lat.sort()
print(f"reply-header reads: n={len(lat)}, above {thr} ms: {n}; median {lat[len(lat)//2]:.3f}; p99 {lat[int(len(lat)*0.99)]:.3f}; p999 {lat[int(len(lat)*0.999)]:.3f}; max {lat[-1]:.3f}")
prev = None
for role, f in events:
    if role == "poll":
        if prev is not None:
            iv = (f["tSubmit"] - prev["tSubmit"]) * 1000
            if iv > 100:
                print(f"poll interval {iv:9.3f} ms: seq{prev['seq']} (t={prev['tSubmit']:.6f}) -> seq{f['seq']} (t={f['tSubmit']:.6f})")
        prev = f
