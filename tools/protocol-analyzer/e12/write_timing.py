"""E12: per PC -> console data write, the header -> payload submit delay, the payload's submit -> completion time, the
previous poll or fetch and the previous inbound transfer, and the next OUT transfer with its gap; then SmartSoft's idle
poll-to-poll intervals (no fetch, write or payload in between) below a threshold, grouped in time. Complements
write_rules.py (placement and floors, all writes). Read-only, stdlib only, never talks to a device.
Usage (from the repository root): py -3.13 -I -B tools/protocol-analyzer/e12/write_timing.py <repo> <jsonl> [threshold_ms]
"""
import os
import sys

sys.stdout.reconfigure(encoding="utf-8")
repo, path = sys.argv[1], sys.argv[2]
threshold = float(sys.argv[3]) if len(sys.argv) > 3 else 21.0
sys.path.insert(0, os.path.join(repo, "tools", "protocol-analyzer"))
import hsp_summary as H  # noqa: E402

header, frames = H.load(path)
events, _ = H.classify(frames)
n = 0
delays = []
for i, (role, f) in enumerate(events):
    if role != "data-hdr":
        continue
    n += 1
    pay = events[i + 1][1]
    p = H.decode_payload("out", pay["bytes"])
    j = i - 1
    while j >= 0 and events[j][1]["dir"] != "in":
        j -= 1
    k = i - 1
    while k >= 0 and events[k][0] not in ("poll", "fetch"):
        k -= 1
    m = i + 2
    while m < len(events) and events[m][0] not in ("poll", "fetch", "data-hdr"):
        m += 1
    nxt_role, nxt = events[m] if m < len(events) else (None, None)
    d = (pay["tSubmit"] - f["tSubmit"]) * 1000
    delays.append(d)
    gap = f"{(nxt['tSubmit'] - pay['tSubmit']) * 1000:6.2f} ms after the payload's submit ({(nxt['tSubmit'] - pay['t']) * 1000:6.2f} after its completion)" if nxt else "-"
    print(f"W{n:2d} seq{f['seq']:<6d} {H.name(p['id']):13s} payload {len(pay['bytes']):4d} B: header->payload {d:5.3f} ms, payload submit->complete "
          f"{(pay['t'] - pay['tSubmit']) * 1000:6.3f} ms; previous {events[k][0]} {(f['tSubmit'] - events[k][1]['tSubmit']) * 1000:6.2f} ms before, "
          f"previous inbound: {events[j][0]} seq{events[j][1]['seq']}; next OUT: {nxt_role} {gap}")
if delays:
    s = sorted(delays)
    print(f"header -> payload submit: min {s[0]:.3f} median {s[len(s) // 2]:.3f} max {s[-1]:.3f} ms (n = {len(s)})")

prev, busy, low = None, False, []
for role, f in events:
    if role == "poll":
        if prev is not None and not busy:
            iv = (f["tSubmit"] - prev["tSubmit"]) * 1000
            if iv < threshold:
                low.append((f["t"], f["seq"], iv))
        prev, busy = f, False
    elif role in ("fetch", "data-hdr", "out-data", "in-payload"):
        busy = True
print(f"idle poll-to-poll intervals below {threshold} ms: {len(low)}" + (f", minimum {min(x[2] for x in low):.3f} ms" if low else ""))
groups = []
for item in low:
    if groups and item[0] - groups[-1][-1][0] < 5:
        groups[-1].append(item)
    else:
        groups.append([item])
for g in groups:
    print(f"  {g[0][0]:.1f}-{g[-1][0]:.1f} s (seq{g[0][1]}..{g[-1][1]}): n = {len(g)}, min {min(x[2] for x in g):.3f} ms")
