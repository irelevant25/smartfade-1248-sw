"""E9c: the PC -> console writes and the console's answers, grouped by the action log's steps. Read-only.

Input: a normalized capture JSONL with the action log merged as notes (multi-URB reads joined, `join_reads.py`).
For every note (a step of the guided session) it prints the writes that followed before the next note, compacted
by control: fader events as (index, first value .. last value, count) and key events as (code, press / release
pairs with the hold time); then the objects the console listed and delivered in that window, each with the byte
positions that changed against the previous delivery of the same object (the first eight), so that a fader index
can be tied to the FaderInfo record, the channel, the DMX address and the LED block it moved, in every bank and
mode. Nothing here promotes anything: the readings are made in the record.
Usage: py -3.13 -I steps.py <joined.jsonl> [--all-notes]
"""
import json
import sys

IDS = {0: "Memory", 3: "SequenceMain", 5: "Patch", 9: "ChannelText", 12: "Independents", 13: "OutputLevels", 14: "FaderInfo",
       15: "ChannelLevels", 17: "PlaybackInfo", 20: "ConsoleEvent", 21: "DisplayContent", 22: "LedStatus", 23: "ConsoleStatus",
       36: "FileName", 37: "ConsoleVersion", 38: "SystemSizes", 39: "System", 40: "DirtyObjects", 41: "SystemSettings"}
KEYS = {0x00: "bump 1", 0x06: "bump 7", 0x08: "bump 9", 0x09: "bump 10", 0x31: "GO", 0x32: "PAUSE?", 0x33: "SOLO", 0x34: "BLACKOUT",
        0x36: "FADERS 1-24", 0x37: "FADERS 25-48", 0x38: "CLEAR", 0x39: "FADERS MEMS", 0x3A: "NEXT?", 0x3B: "STACK",
        0x41: "REC MEM", 0x42: "EDIT MEM", 0x47: "IND 1", 0x48: "IND 2"}
SHOW_OBJECTS = {3, 12, 13, 14, 15, 17, 21, 22, 23, 36, 37, 39, 40}

path = sys.argv[1]
all_notes = "--all-notes" in sys.argv
recs = [json.loads(l) for l in open(path, encoding="utf-8")]
events = []   # (t, kind, payload)
for r in recs:
    if r.get("type") == "note":
        events.append((r["t"], "note", r["text"]))
    elif r.get("type") == "frame" and r.get("xfer") == "bulk" and not r.get("synthetic"):
        events.append((r["t"], "frame", r))
events.sort(key=lambda e: e[0])


def u16(b, i):
    return b[i] | (b[i + 1] << 8)


last_body = {}
pending_header = None
step_notes = [e for e in events if e[1] == "note" and (all_notes or not e[2].startswith("wait ") and not e[2].startswith("analyst"))]
# windows: from each step note to the next step note
bounds = [(n[0], (step_notes[i + 1][0] if i + 1 < len(step_notes) else float("inf")), n[2]) for i, n in enumerate(step_notes)]
frames = [e for e in events if e[1] == "frame"]
fi = 0
for (t0, t1, text) in bounds:
    writes = []       # (t, kind, index/code, value/state, previous)
    listed = []       # (t, [ids])
    objects = []      # (t, id, changes)
    while fi < len(frames) and frames[fi][0] < t0:
        # frames before the first step still update last_body
        f = frames[fi][2]
        b = bytes.fromhex(f["data"])
        if f["dir"] == "in" and f["len"] > 12 or (f["dir"] == "in" and f["len"] == 12 and b[1] == 0 and b[4] in IDS and b[0] not in (3, 4)):
            pass
        fi += 1
    j = fi
    while j < len(frames) and frames[j][0] < t1:
        f = frames[j][2]
        b = bytes.fromhex(f["data"])
        if f["dir"] == "out":
            if f["len"] == 12 and b[0] == 1:
                pending_header = f
            elif pending_header is not None:
                if len(b) >= 7 and b[3] == 20 and b[4] == 1:
                    writes.append((f["t"], "key", b[5], b[6], None))
                elif len(b) >= 8 and b[3] == 20 and b[4] == 0:
                    writes.append((f["t"], "fader", b[5], b[6], b[7]))
                else:
                    writes.append((f["t"], "data", b[3] if len(b) > 3 else -1, len(b), None))
                pending_header = None
        else:
            if f["len"] >= 12 and not (f["len"] == 12 and b[0] in (3, 4) and b[1] == 0 and u16(b, 2) <= 600 and b[4] not in (40,)):
                # an IN payload: [pseq][0][len16][id][body]; a 12-byte frame is a payload only when it is a 1-record list
                if f["len"] == 12 and not (b[1] == 0 and b[4] == 40):
                    j += 1
                    continue
                oid = b[4]
                body = b[5:]
                if oid == 40:
                    n = body[1] if len(body) > 1 else 0
                    ids = [body[2 + 5 * k] for k in range(n) if 2 + 5 * k < len(body)]
                    listed.append((f["t"], ids))
                elif oid in SHOW_OBJECTS:
                    prev = last_body.get(oid)
                    if prev is None:
                        changes = "first"
                    elif len(prev) != len(body):
                        changes = f"len {len(prev)}->{len(body)}"
                    else:
                        diffs = [(i, prev[i], body[i]) for i in range(len(body)) if prev[i] != body[i]]
                        changes = ("unchanged" if not diffs else
                                   f"{len(diffs)} changed: " + " ".join(f"[{i}]{a:02X}->{c:02X}" for i, a, c in diffs[:8]) + (" ..." if len(diffs) > 8 else ""))
                    last_body[oid] = body
                    objects.append((f["t"], oid, changes))
        j += 1
    fi = j
    print(f"\n=== t={t0:9.3f} {text}")
    # compact the writes by control
    groups = []
    for w in writes:
        if w[1] == "fader":
            if groups and groups[-1][0] == "fader" and groups[-1][1] == w[2]:
                groups[-1][2].append(w)
            else:
                groups.append(["fader", w[2], [w]])
        elif w[1] == "key":
            if groups and groups[-1][0] == "key" and groups[-1][1] == w[2] and w[3] == 0 and groups[-1][2][-1][3] == 1:
                groups[-1][2].append(w)
            else:
                groups.append(["key", w[2], [w]])
        else:
            groups.append(["data", w[2], [w]])
    for g in groups:
        if g[0] == "fader":
            vals = [w[3] for w in g[2]]
            print(f"  fader index 0x{g[1]:02X}: {len(vals)} events, values 0x{vals[0]:02X} .. 0x{vals[-1]:02X} "
                  f"(min 0x{min(vals):02X}, max 0x{max(vals):02X}), t {g[2][0][0]:.3f}-{g[2][-1][0]:.3f}")
        elif g[0] == "key":
            states = "/".join("P" if w[3] == 1 else "R" if w[3] == 0 else str(w[3]) for w in g[2])
            hold = f", held {(g[2][1][0] - g[2][0][0]) * 1000:.0f} ms" if len(g[2]) == 2 else ""
            print(f"  key 0x{g[1]:02X} ({KEYS.get(g[1], '?')}): {states} at t {g[2][0][0]:.3f}{hold}")
        else:
            print(f"  data write id {g[1]} ({IDS.get(g[1], '?')}) {g[2][0][3]} bytes at t {g[2][0][0]:.3f}")
    if listed:
        from collections import Counter
        c = Counter()
        for _, ids in listed:
            for i in ids:
                c[IDS.get(i, str(i))] += 1
        print(f"  lists: {len(listed)}; records: " + ", ".join(f"{k} x{v}" for k, v in c.most_common()))
    for (t, oid, changes) in objects:
        if changes != "unchanged":
            print(f"    {t:9.3f} {IDS.get(oid, oid)}({oid}): {changes}")
