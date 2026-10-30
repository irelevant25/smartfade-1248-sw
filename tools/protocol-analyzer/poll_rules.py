"""Poll-rule check over normalized captures (SC-1 evidence).

For every bulk transfer of each JSONL file given: verifies that every poll (type 0) carries in
w2..w5 the words of the most recent fetch or reply header (F52); counts the transitions between
transfer kinds (poll, fetch, reply, payload, data); per SmartSoft session (gap > 5 s) counts the
leading all-zero polls before the first fetch or data write; and measures, in ms, fetch submit to
next poll submit, payload completion to fetch submit, and poll submit to poll submit (the figures
behind UsbDeviceOptions.Floors). Usage: py -3.13 -I poll_rules.py <capture.jsonl> [...]
"""
import json, sys, struct
from collections import Counter

def load(path):
    frames = []
    notes = []
    for line in open(path, encoding="utf-8"):
        r = json.loads(line)
        if r["type"] == "frame" and r.get("xfer") == "bulk" and not r.get("synthetic"):
            frames.append(r)
        elif r["type"] == "note":
            notes.append(r)
    return frames, notes

def words(data):
    b = bytes.fromhex(data)
    return struct.unpack("<6H", b) if len(b) == 12 else None

for path in sys.argv[1:]:
    frames, notes = load(path)
    print("=====", path.split("/")[-1], len(frames), "bulk frames")
    # classify
    last_words = None          # w2..w5 of the most recent fetch or reply header
    last_reply_words = None
    last_fetch_words = None
    stale_ok = stale_bad = 0
    bad_examples = []
    first_polls = []
    # session starts: an OUT all-zero poll that follows a long gap or is the first frame
    pending_payload = None
    seq_events = []            # (kind, seq, t, tSubmit, words)
    for f in frames:
        d = f["dir"]; w = words(f["data"]) if f["len"] == 12 else None
        if d == "out":
            if pending_payload == "out":
                pending_payload = None
                seq_events.append(("DATA", f["seq"], f["t"], f.get("tSubmit"), None))
                continue
            if w is None:
                seq_events.append(("OUT?", f["seq"], f["t"], f.get("tSubmit"), None)); continue
            if w[0] == 0:
                kind = "POLL"
                tail = w[2:]
                if last_words is None:
                    if tail == (0, 0, 0, 0): stale_ok += 1
                    else: stale_bad += 1; bad_examples.append((f["seq"], tail, None))
                elif tail == last_words: stale_ok += 1
                else:
                    stale_bad += 1
                    if len(bad_examples) < 5: bad_examples.append((f["seq"], tail, last_words))
            elif w[0] == 2:
                kind = "FETCH"; last_words = w[2:]; last_fetch_words = w[2:]
            elif w[0] == 1:
                kind = "DATAHDR"; pending_payload = "out"
            else:
                kind = "OUT%d" % w[0]
            seq_events.append((kind, f["seq"], f["t"], f.get("tSubmit"), w))
        else:
            if pending_payload == "in":
                pending_payload = None
                seq_events.append(("PAYLOAD", f["seq"], f["t"], f.get("tSubmit"), None)); continue
            if w is None:
                seq_events.append(("IN?", f["seq"], f["t"], f.get("tSubmit"), None)); continue
            kind = "REPLY%d" % w[0]
            last_words = w[2:]; last_reply_words = w[2:]
            if w[1] > 0: pending_payload = "in"
            seq_events.append((kind, f["seq"], f["t"], f.get("tSubmit"), w))
    print("stale rule (poll w2..w5 == most recent fetch or reply header words): ok", stale_ok, "bad", stale_bad, bad_examples[:5])
    # kinds sequence patterns around fetches: what follows a FETCH and what precedes it
    kinds = [e[0] for e in seq_events]
    trans = Counter()
    for a, b in zip(kinds, kinds[1:]):
        trans[(a, b)] += 1
    for (a, b), n in sorted(trans.items(), key=lambda x: -x[1])[:16]:
        print(f"  {a:>8} -> {b:<8} {n}")
    # runs of all-zero polls answered by idle replies at session starts: count consecutive POLLs whose words are all zero before the first FETCH after each DATA
    # session boundaries: time gaps > 5 s between consecutive OUT frames
    sessions = []
    start = 0
    for i in range(1, len(seq_events)):
        if seq_events[i][2] - seq_events[i-1][2] > 5.0:
            sessions.append((start, i)); start = i
    sessions.append((start, len(seq_events)))
    for (a, b) in sessions:
        ev = seq_events[a:b]
        zero_polls = 0; replies_idle = 0; first_nonzero = None
        for e in ev:
            if e[0] == "POLL":
                if e[4][2:] == (0, 0, 0, 0): zero_polls += 1
                else:
                    first_nonzero = e[1]; break
            elif e[0] in ("FETCH", "DATAHDR"):
                first_nonzero = (e[0], e[1]); break
        idle = sum(1 for e in ev if e[0] == "REPLY4" and e[4][1] == 0)
        lists = sum(1 for e in ev if e[0] == "REPLY4" and e[4][1] > 0)
        print(f"  session frames {ev[0][1]}..{ev[-1][1]} ({ev[0][2]:.1f}-{ev[-1][2]:.1f}s): leading all-zero polls before the first fetch/data = {zero_polls}, ended by {first_nonzero}; idle replies {idle}, list replies {lists}")
    # timing: fetch -> next poll (submit times), list reply -> fetch
    import statistics
    d_fetch_poll = []; d_reply_fetch = []; d_poll_poll = []
    for i in range(1, len(seq_events)):
        a, b = seq_events[i-1], seq_events[i]
        if a[0] == "FETCH" and b[0] == "POLL" and a[3] and b[3]: d_fetch_poll.append((b[3]-a[3])*1000)
        if a[0] in ("PAYLOAD",) and b[0] == "FETCH" and b[3]: d_reply_fetch.append((b[3]-a[2])*1000)
    polls = [e for e in seq_events if e[0] == "POLL" and e[3]]
    for p, q in zip(polls, polls[1:]):
        dt = (q[3]-p[3])*1000
        if dt < 1000: d_poll_poll.append(dt)
    def stats(name, xs):
        if xs: print(f"  {name}: n={len(xs)} min={min(xs):.2f} median={statistics.median(xs):.2f} max={max(xs):.2f} ms")
    stats("fetch submit -> next poll submit", d_fetch_poll)
    stats("payload completion -> fetch submit", d_reply_fetch)
    stats("poll -> poll (submit)", d_poll_poll)
