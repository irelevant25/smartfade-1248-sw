"""Write-rule check over normalized captures (SC-2 evidence: E9, E9b).

For every Data write (type-1 header + payload) of each JSONL file given: verifies that the header's w2..w5 carry
the words of the most recent fetch or reply header (F52), that the payload counter starts at 0x00 in each SmartSoft
session and rises by one per data payload (F131), and that a fader event's previous byte is SmartSoft's last wire
value of that index in the session, 0x00 before the first (F132; exceptions listed); reports where each write sat
(after an idle reply, a list or an object; a fetch outstanding or a poll due after a fetch; writes since the last
poll; listed records still unfetched), what followed it, whether any write fell between a key's press and its
release, and measures, in ms: poll submit to write header submit, last inbound completion to write header submit,
write payload submit to the next poll or fetch submit (and from the payload's completion), header submit to the next
write's header submit, and a key's press to its release (header submits). These are the figures behind
UsbDeviceOptions.Floors.WriteAfterPoll / PollAfterWrite / WriteInterval and HspReadSession's placement rule.
Usage: py -3.13 -I write_rules.py <capture.jsonl> [...]
"""
import json, sys
from collections import Counter


def load(path):
    frames = []
    for line in open(path, encoding="utf-8"):
        r = json.loads(line)
        if r["type"] == "frame" and r.get("xfer") == "bulk" and not r.get("synthetic"):
            frames.append(r)
    return frames


def u16(b, i):
    return b[i] | (b[i + 1] << 8)


def stats(name, values):
    if not values:
        print(f"  {name}: none")
        return
    v = sorted(values)
    print(f"  {name}: min {v[0] * 1000:.2f}  median {v[len(v) // 2] * 1000:.2f}  max {v[-1] * 1000:.2f}  (n = {len(v)})")


for path in sys.argv[1:]:
    frames = load(path)
    print("=====", path.replace("\\", "/").split("/")[-1], len(frames), "bulk frames")
    stale = (0, 0, 0, 0)
    pending = []                 # listed, unfetched records
    fetch_outstanding = poll_due = False
    writes_since_poll = 0
    last_poll_submit = last_in = None
    in_header = None             # (type, length) of a reply header whose payload is due
    before = None                # what the last completed inbound transfer was
    writes = []
    i = 0
    while i < len(frames):
        f = frames[i]
        b = bytes.fromhex(f["data"])
        ts = f.get("tSubmit", f["t"])
        if f["dir"] == "out":
            if len(b) == 12 and b[0] == 1 and b[1] == 0:
                p = frames[i + 1]
                payload = bytes.fromhex(p["data"])
                writes.append(dict(seq=f["seq"], ts=ts, payload=payload, payload_t=p["t"], payload_ts=p.get("tSubmit", p["t"]),
                                   stale_ok=tuple(u16(b, k) for k in (4, 6, 8, 10)) == stale, before=before,
                                   fetch=fetch_outstanding, poll_due=poll_due, since_poll=writes_since_poll,
                                   pending=len(pending), poll_gap=None if last_poll_submit is None else ts - last_poll_submit,
                                   in_gap=None if last_in is None else ts - last_in, index=len(writes)))
                writes_since_poll += 1
                i += 2
                continue
            kind = u16(b, 0)
            if kind == 0:
                poll_due = False
                writes_since_poll = 0
                last_poll_submit = ts
            elif kind == 2:
                rec = tuple(u16(b, k) for k in (4, 6, 8, 10))
                stale = rec
                if rec in pending:
                    pending.remove(rec)
                fetch_outstanding = poll_due = True
            for w in writes[-1:]:
                if "next" not in w:
                    w["next"] = ("poll" if kind == 0 else "fetch", ts)
            i += 1
            continue
        if in_header is None:
            typ, ln = u16(b, 0), u16(b, 2)
            stale = tuple(u16(b, k) for k in (4, 6, 8, 10))
            if typ == 3:
                fetch_outstanding = False
            if ln:
                in_header = (typ, ln)
            else:
                before = "idle reply"
        else:
            typ, _ = in_header
            in_header = None
            if typ == 4 and b[4] == 40:
                cnt = (b[5] << 8) | b[6]
                pending += [(b[7 + 5 * k], (b[8 + 5 * k] << 8) | b[9 + 5 * k], b[10 + 5 * k], b[11 + 5 * k]) for k in range(cnt)]
                before = "list"
            else:
                before = "object"
        last_in = f["t"]
        i += 1

    print(" data writes:", len(writes), "by id:", dict(Counter(w["payload"][3] for w in writes)))
    print(" header w2..w5 = the most recent fetch or reply header's:", sum(w["stale_ok"] for w in writes), "of", len(writes))
    # sessions: the counter restarts at 0x00
    session = 0
    previous = None
    breaks = []
    for w in writes:
        p = w["payload"][0]
        if previous is not None and p != (previous + 1) & 0xFF:
            if p == 0:
                session += 1
            else:
                breaks.append((w["seq"], previous, p))
        w["session"] = session
        previous = p
    print(" SmartSoft sessions (counter restarts at 0x00):", session + 1, "; first counter", writes[0]["payload"][0] if writes else None, "; other breaks", breaks)
    print(" placement:", dict(Counter((w["before"], "fetch outstanding" if w["fetch"] else "-", "poll due after fetch" if w["poll_due"] else "-",
                                         f"writes since poll {w['since_poll']}") for w in writes)))
    print(" with listed records still unfetched:", sum(w["pending"] > 0 for w in writes), "; followed by:", dict(Counter(w.get("next", ("end",))[0] for w in writes)))
    # previous byte and press / release
    last_value = {}
    exceptions = []
    held = None
    between = 0
    pairs = []
    for w in writes:
        pl = w["payload"]
        if pl[3] != 20:
            between += held is not None
            continue
        if pl[4] == 0:
            key = (w["session"], pl[5])
            if pl[7] != last_value.get(key, 0):
                exceptions.append((w["seq"] + 1, f"index 0x{pl[5]:02X}", f"previous 0x{pl[7]:02X}", f"last wire value 0x{last_value.get(key, 0):02X}"))
            last_value[key] = pl[6]
            between += held is not None
        elif pl[6] == 1:
            between += held is not None
            held = w
        else:
            if held is not None:
                pairs.append(w["ts"] - held["ts"])
            held = None
    print(" fader events whose previous byte is not the last wire value of the index (payload seq):", exceptions)
    print(" writes between a press and its release:", between)
    stats("poll submit -> write header submit", [w["poll_gap"] for w in writes if w["poll_gap"] is not None])
    stats("last inbound completion -> write header submit", [w["in_gap"] for w in writes if w["in_gap"] is not None])
    stats("write payload submit -> next poll / fetch submit", [w["next"][1] - w["payload_ts"] for w in writes if "next" in w])
    stats("write payload completion -> next poll / fetch submit", [w["next"][1] - w["payload_t"] for w in writes if "next" in w])
    stats("write header submit -> next write header submit (same session)", [b["ts"] - a["ts"] for a, b in zip(writes, writes[1:]) if a["session"] == b["session"]])
    stats("key press -> release, header submits", pairs)
