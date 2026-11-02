"""E9d: checks every fader and bump step of the guided session against the physical-fader reading (F171, H111). Read-only.

Input: the normalized E9d JSONL with the action log merged as notes, multi-URB reads joined (`join_reads.py`). For each
step note of the passes (a: channel faders 1-24; b: the faders labelled 25-48 and the bump labelled 25 in bank 25-48;
c: memory faders 1-24 and memory bumps 1-24 of page 1; d: bump 7 held 5 s) it collects the writes until the next step
and the byte positions that changed in the objects the console delivered, and prints per step: the index or code used,
whether it is the expected one (fader N -> index N - 1; the fader labelled N in bank 25-48 -> index N - 25; memory fader
or bump N -> N - 1), and whether the expected objects moved (bank 1-24: FaderInfo record N byte 0, ChannelLevels[N], DMX N,
LED block N; bank 25-48: record N - 24, ChannelLevels[N], DMX N; memory mode: record N bytes 0 and 1 and, for a recorded
memory, some channel; a bump: the same with 0xFF while held). The d steps are printed with their diffs for the record.
Usage: py -3.13 -I check.py <joined.jsonl> --recorded 1,2,3,4,5,6,7,8,13,21
"""
import json
import re
import sys
from collections import defaultdict

path = sys.argv[1]
recorded = set()
if "--recorded" in sys.argv:
    recorded = {int(x) for x in sys.argv[sys.argv.index("--recorded") + 1].split(",")}

recs = [json.loads(l) for l in open(path, encoding="utf-8")]
events = []
for r in recs:
    if r.get("type") == "note":
        events.append((r["t"], "note", r["text"]))
    elif r.get("type") == "frame" and r.get("xfer") == "bulk" and not r.get("synthetic"):
        events.append((r["t"], "frame", r))
events.sort(key=lambda e: e[0])
steps = [(t, text) for (t, kind, text) in events if kind == "note" and re.search(r"\b(s[12]) (a|b|c|d) ", text)]
bounds = [(steps[i][0], steps[i + 1][0] if i + 1 < len(steps) else float("inf"), steps[i][1]) for i in range(len(steps))]
frames = [e for e in events if e[1] == "frame"]

last_body = {}
fi = 0
results = []
for (t0, t1, text) in bounds:
    writes = []          # ("fader", index, value, t) / ("key", code, state, t)
    changed = defaultdict(set)   # oid -> set of changed byte positions in the window
    values = defaultdict(dict)   # oid -> {pos: last value}
    pending = None
    while fi < len(frames) and frames[fi][0] < t0:
        f = frames[fi][2]; b = bytes.fromhex(f["data"])
        if f["dir"] == "in" and (f["len"] > 12 or (f["len"] == 12 and b[1] == 0 and b[4] == 40)):
            last_body[b[4]] = b[5:]
        fi += 1
    j = fi
    while j < len(frames) and frames[j][0] < t1:
        f = frames[j][2]; b = bytes.fromhex(f["data"])
        if f["dir"] == "out":
            if f["len"] == 12 and b[0] == 1:
                pending = f
            elif pending is not None:
                if len(b) >= 8 and b[3] == 20 and b[4] == 0:
                    writes.append(("fader", b[5], b[6], f["t"]))
                elif len(b) >= 7 and b[3] == 20 and b[4] == 1:
                    writes.append(("key", b[5], b[6], f["t"]))
                pending = None
        elif f["len"] > 12 or (f["len"] == 12 and b[1] == 0 and b[4] == 40):
            oid = b[4]; body = b[5:]
            prev = last_body.get(oid)
            if prev is not None and len(prev) == len(body):
                for i in range(len(body)):
                    if prev[i] != body[i]:
                        changed[oid].add(i); values[oid][i] = body[i]
            last_body[oid] = body
        j += 1
    fi = j
    m = re.search(r"\b(s[12]) (a|b|c|d) (.*)", text)
    sess, part, rest = m.group(1), m.group(2), m.group(3)
    fader_idx = sorted({w[1] for w in writes if w[0] == "fader"})
    key_codes = [(w[1], w[2], w[3]) for w in writes if w[0] == "key"]
    holds = []
    for k in range(len(key_codes) - 1):
        if key_codes[k][1] == 1 and key_codes[k + 1][1] == 0 and key_codes[k][0] == key_codes[k + 1][0]:
            holds.append((key_codes[k][0], key_codes[k + 1][2] - key_codes[k][2]))
    verdict = ""
    n = None
    mm = re.search(r"fader (?:labelled )?(\d+)|bump (?:labelled )?(\d+)|memory fader (\d+)|memory bump (\d+)", rest)
    if part == "a" and "sim fader" in rest:
        n = int(re.search(r"sim fader (\d+)", rest).group(1))
        ok_idx = fader_idx == [n - 1]
        ok_obj = (1 + 4 * (n - 1)) in changed[14] and n in changed[15] and (1 + n) in changed[13] and (2 + 6 * (n - 1)) in changed[22]
        verdict = f"index {fader_idx} {'OK' if ok_idx else 'UNEXPECTED'}; objects {'OK' if ok_obj else 'MISMATCH'} (14:{sorted(changed[14])[:4]} 15:{sorted(changed[15])[:4]} 13:{sorted(changed[13])[:4]} 22:{sorted(changed[22])[:4]})"
    elif part == "b" and "sim fader labelled" in rest:
        n = int(re.search(r"labelled (\d+)", rest).group(1))
        ok_idx = fader_idx == [n - 25]
        ok_obj = (1 + 4 * (n - 25)) in changed[14] and n in changed[15] and (1 + n) in changed[13]
        verdict = f"index {fader_idx} {'OK' if ok_idx else 'UNEXPECTED'}; objects {'OK' if ok_obj else 'MISMATCH'} (14:{sorted(changed[14])[:4]} 15:{sorted(changed[15])[:4]} 13:{sorted(changed[13])[:4]} 22:{sorted(changed[22])[:4]})"
    elif part == "b" and "bump labelled" in rest:
        n = int(re.search(r"labelled (\d+)", rest).group(1))
        ok_code = [h[0] for h in holds] == [n - 25]
        ok_obj = n in changed[15] and (1 + n) in changed[13] and values[15].get(n) in (0x00, 0xFF)
        verdict = f"code {[hex(h[0]) for h in holds]} held {[round(h[1], 2) for h in holds]} s {'OK' if ok_code else 'UNEXPECTED'}; objects {'OK' if ok_obj else 'MISMATCH'} (15:{sorted(changed[15])[:4]} 13:{sorted(changed[13])[:4]} 14:{sorted(changed[14])[:4]})"
    elif part == "c" and "memory fader" in rest:
        n = int(re.search(r"memory fader (\d+)", rest).group(1))
        ok_idx = fader_idx == [n - 1]
        rec_ok = (1 + 4 * (n - 1)) in changed[14] and (2 + 4 * (n - 1)) in changed[14]
        chan = len(changed[15]) > 0
        ok_obj = rec_ok and (chan if n in recorded else not chan)
        verdict = f"index {fader_idx} {'OK' if ok_idx else 'UNEXPECTED'}; record bytes 0,1 {'OK' if rec_ok else 'MISMATCH'}; channels {'moved' if chan else 'untouched'} ({'recorded' if n in recorded else 'empty'} memory: {'OK' if ok_obj else 'MISMATCH'}; 15:{sorted(changed[15])[:6]})"
    elif part == "c" and "memory bump" in rest:
        n = int(re.search(r"memory bump (\d+)", rest).group(1))
        ok_code = [h[0] for h in holds] == [n - 1]
        rec_ok = (1 + 4 * (n - 1)) in changed[14]
        chan = len(changed[15]) > 0
        ok_obj = rec_ok and (chan if n in recorded else not chan)
        verdict = f"code {[hex(h[0]) for h in holds]} held {[round(h[1], 2) for h in holds]} s {'OK' if ok_code else 'UNEXPECTED'}; record {'OK' if rec_ok else 'MISMATCH'}; channels {'moved' if chan else 'untouched'} ({'recorded' if n in recorded else 'empty'}: {'OK' if ok_obj else 'MISMATCH'}; 22:{sorted(changed[22])[:4]})"
    elif part == "d":
        det = []
        for oid in (14, 15, 13, 22, 23):
            if changed[oid]:
                det.append(f"{oid}:" + " ".join(f"[{i}]={values[oid][i]:02X}" for i in sorted(changed[oid])[:8]))
        verdict = f"faders {fader_idx} {[(hex(c), s, round(t, 3)) for c, s, t in key_codes][:4]} holds {[(hex(h[0]), round(h[1], 2)) for h in holds]} | " + "; ".join(det)
    else:
        verdict = f"faders {fader_idx} keys {[hex(c) for c, s, t in key_codes]}"
    results.append((sess, part, rest, verdict))
    print(f"{t0:9.3f} {sess} {part} {rest[:44]:44s} | {verdict}")

bad = [r for r in results if "UNEXPECTED" in r[3] or "MISMATCH" in r[3]]
print(f"\nsteps checked: {len(results)}; unexpected or mismatching: {len(bad)}")
for r in bad:
    print("  ", r)
