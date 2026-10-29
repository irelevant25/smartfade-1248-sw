#!/usr/bin/env python3
"""Offline structural summary of a normalized HSP capture (smartfade-capture JSONL).

Stdlib only, read-only, never talks to a device. Run with `py -3.13 -I` from the
repository root (see README.md).

Field names used here (header w0..w5, payload seq / id / body, DirtyObjects records) follow
the hypotheses H15-H20 in docs/protocol/hypotheses.md and the static analysis in
docs/protocol/smartsoft-analysis.md S11. They are a decoding aid, not established protocol:
every check below prints PASS/FAIL so the capture can contradict the hypothesis. Nothing
in this file may be used to build bytes for a console.

Modes:
  summary   checks and statistics (default)
  log       one line per bulk transfer with the hypothesis-based decode
  payloads  every console->PC payload of one object ID (--id), body as hex
  diff      body bytes that change between successive payloads of one object ID (--id)
  timeline  every IN payload whose body changed against the previous one of the same
            (id, a, b, c), with the changed byte positions, interleaved with the JSONL's
            `note` records (operator action log); optional --id filters, --from/--to
  startdiff first body of every (id, a, b, c) compared with another capture (--other)
  census    for every (id, a, b, c): body positions that ever changed, value ranges, frames
  lcd       DisplayContent (ID 21) as two 20-character lines, changes only (0x80-0x87 -> <0>..<7>)

Frame numbers printed as fNNN are pcap frame numbers of the data-carrying frame
(OUT: submission frame, IN: completion frame); seqNNN are JSONL record numbers;
pseq=N is the payload's own sequence byte (payload byte 0), not a record number.
"""
from __future__ import annotations

import argparse
import collections
import json
import struct
import sys

# Names from the sfEngine.dll object-name table (smartsoft-analysis.md S11.6, INFERRED).
OBJECT_NAMES = [
    "Memory", "Palette", "Sequence", "SequenceMain", "StepContent", "Patch", "Device", "Group",
    "Page", "ChannelText", "Effects", "MIDIPatch", "Independents", "OutputLevels", "FaderInfo",
    "ChannelLevels", "OutputPalettes", "PlaybackInfo", "BlindLevels", "InputLevels",
    "ConsoleEvent", "DisplayContent", "LedStatus", "ConsoleStatus", "DeviceStatus",
    "ChangedStatus", "Personality", "PersonalityLibraryReset", "PersonalityUpdate",
    "PatchDevice", "UnpatchDevice", "RepatchDevice", "InsertStep", "InsertPointQStep",
    "DeleteStep", "MoveStep", "FileName", "ConsoleVersion", "SystemSizes", "System",
    "DirtyObjects", "SystemSettings",
]


def name(oid: int) -> str:
    return OBJECT_NAMES[oid] if 0 <= oid < len(OBJECT_NAMES) else f"?{oid}"


def load(path):
    header, frames = None, []
    with open(path, encoding="utf-8") as fh:
        for line in fh:
            rec = json.loads(line)
            if rec["type"] == "header":
                header = rec
            elif rec["type"] == "frame":
                rec["bytes"] = bytes.fromhex(rec["data"])
                rec["dframe"] = rec["frames"][0] if rec["dir"] == "out" else rec["frames"][1]
                frames.append(rec)
    return header, frames


def load_notes(path):
    """The `note` records of a JSONL file, in file order."""
    with open(path, encoding="utf-8") as fh:
        return [r for r in (json.loads(line) for line in fh) if r["type"] == "note"]


def words(b: bytes):
    return struct.unpack("<6H", b[:12])


def classify(frames):
    """Walk bulk transfers on interface 1 and assign roles per H15/H16. Returns events, errors."""
    bulk = [f for f in frames if f["xfer"] == "bulk"]
    events, errors = [], []
    expect_out_data = None  # w1 of the last type-1 header
    expect_in = "hdr"       # "hdr" or an int payload length
    last_hdr_in = None
    for f in bulk:
        b = f["bytes"]
        if f["dir"] == "out":
            if expect_out_data is not None:
                role = "out-data"
                if len(b) != expect_out_data:
                    errors.append((f, f"OUT data len {len(b)} != header w1 {expect_out_data}"))
                expect_out_data = None
            elif len(b) == 12:
                w = words(b)
                role = {0: "poll", 1: "data-hdr", 2: "fetch"}.get(w[0], "out-hdr?")
                if role == "out-hdr?":
                    errors.append((f, f"OUT header w0={w[0]} not in {{0,1,2}}"))
                if w[0] == 1:
                    expect_out_data = w[1]
                elif w[1] != 0:
                    errors.append((f, f"OUT type {w[0]} with w1={w[1]}"))
            else:
                role = "out-unexpected"
                errors.append((f, f"OUT transfer of {len(b)} bytes without a type-1 header"))
            if expect_in != "hdr":
                errors.append((f, "OUT while an IN payload was still expected"))
        else:
            if expect_in == "hdr":
                if len(b) != 12:
                    role = "in-unexpected"
                    errors.append((f, f"IN {len(b)} bytes where a 12-byte reply header was expected"))
                else:
                    role = "reply"
                    w = words(b)
                    last_hdr_in = w
                    if w[1] > 0:
                        expect_in = w[1]
            else:
                role = "in-payload"
                if len(b) != expect_in:
                    errors.append((f, f"IN payload len {len(b)} != reply w1 {expect_in}"))
                expect_in = "hdr"
        events.append((role, f))
    return events, errors


def decode_payload(direction: str, b: bytes):
    """H18 layouts. OUT: [seq][u16 LE len][id][body]. IN: [seq][b1][u16 LE len][id][body]."""
    if direction == "out":
        if len(b) < 4:
            return None
        return {"seq": b[0], "b1": None, "len": b[1] | b[2] << 8, "id": b[3], "body": b[4:]}
    if len(b) < 5:
        return None
    return {"seq": b[0], "b1": b[1], "len": b[2] | b[3] << 8, "id": b[4], "body": b[5:]}


def dirty_records(body: bytes):
    if len(body) < 2:
        return None, []
    count = body[0] << 8 | body[1]
    recs = []
    for i in range(count):
        r = body[2 + 5 * i: 7 + 5 * i]
        if len(r) < 5:
            break
        recs.append((r[0], r[1] << 8 | r[2], r[3], r[4]))
    return count, recs


def pairs(events):
    """Yield (role, header_frame, payload_frame_or_None) for poll replies and data writes."""
    i = 0
    while i < len(events):
        role, f = events[i]
        nxt = events[i + 1] if i + 1 < len(events) else (None, None)
        if role == "reply" and nxt[0] == "in-payload":
            yield "reply", f, nxt[1]; i += 2; continue
        if role == "data-hdr" and nxt[0] == "out-data":
            yield "data", f, nxt[1]; i += 2; continue
        yield role, f, None
        i += 1


def hexs(b: bytes) -> str:
    return " ".join(f"{x:02X}" for x in b)


def cmd_log(frames, out):
    events, _ = classify(frames)
    for role, f in events:
        b = f["bytes"]
        desc = ""
        if role in ("poll", "fetch", "data-hdr", "reply") and len(b) == 12:
            w = words(b)
            desc = "w=" + ",".join(str(x) for x in w)
            if role == "fetch":
                desc += f"  fetch {name(w[2])}({w[2]}) a={w[3]} b={w[4]} c={w[5]}"
            if role == "reply" and w[1] > 0:
                desc += f"  type{w[0]} len={w[1]}" + (f" echo {name(w[2])}({w[2]}) {w[3]} {w[4]} {w[5]}" if w[0] == 3 else "")
        elif role in ("in-payload", "out-data"):
            p = decode_payload("in" if role == "in-payload" else "out", b)
            if p:
                desc = f"pseq={p['seq']:3d} b1={p['b1']} len16={p['len']} id={name(p['id'])}({p['id']}) body[{len(p['body'])}]"
        out.write(f"seq{f['seq']:5d} f{f['dframe']:6d} t={f['t']:10.6f} {f['dir']:3s} {role:10s} {len(b):4d} {desc}\n")
        if role in ("in-payload", "out-data"):
            out.write(f"      {hexs(b)}\n")


def cmd_summary(header, frames, out):
    w = out.write
    w(f"source {header['sourceFile']} sha256 {header['sourceSha256']}\n")
    w(f"transfers {len(frames)}\n")
    by_ep = collections.Counter((f["ep"], f["xfer"], f["dir"]) for f in frames)
    for k, v in sorted(by_ep.items()):
        w(f"  ep {k[0]} {k[1]:8s} {k[2]:3s}: {v}\n")
    bulk = [f for f in frames if f["xfer"] == "bulk"]
    if bulk:
        w(f"bulk first t={bulk[0]['t']} (seq {bulk[0]['seq']}, f{bulk[0]['dframe']}), last t={bulk[-1]['t']} (seq {bulk[-1]['seq']}, f{bulk[-1]['dframe']})\n")
    nonzero = [f for f in frames if f["status"] not in ("0x00000000", None)]
    w(f"non-zero USBD status: {len(nonzero)}\n")
    for f in nonzero[:20]:
        w(f"  seq {f['seq']} f{f['frames']} ep {f['ep']} status {f['status']}\n")

    events, errors = classify(frames)
    roles = collections.Counter(r for r, _ in events)
    w(f"H15/H16 role counts: {dict(sorted(roles.items()))}\n")
    w(f"H15/H16 framing errors: {len(errors)}\n")
    for f, msg in errors[:30]:
        w(f"  seq {f['seq']} f{f['dframe']} t={f['t']}: {msg}\n")

    # Reply header types and lengths
    rep = collections.Counter()
    for role, f in events:
        if role == "reply":
            ww = words(f["bytes"])
            rep[(ww[0], ww[1] > 0)] += 1
    w(f"reply (w0, w1>0) counts: {dict(sorted(rep.items()))}\n")

    # Stale words check (H15): poll/data headers w2..w5 vs last fetch or last reply header
    last = None
    stale_ok = stale_bad = 0
    bad_examples = []
    for role, f in events:
        b = f["bytes"]
        if role in ("fetch", "reply"):
            last = words(b)[2:]
        elif role in ("poll", "data-hdr"):
            cur = words(b)[2:]
            if last is None:
                bad_examples.append((f, cur, None)) if any(cur) else None
                continue
            if cur == last:
                stale_ok += 1
            else:
                stale_bad += 1
                if len(bad_examples) < 10:
                    bad_examples.append((f, cur, last))
    w(f"H15 stale w2..w5 in poll/data headers equal last fetch/reply w2..w5: ok={stale_ok} differ={stale_bad}\n")
    for f, cur, lastw in bad_examples:
        w(f"  seq {f['seq']} f{f['dframe']}: w2..w5={cur} last={lastw}\n")

    # Fetch echo (H16)
    echo_ok = echo_bad = 0
    pending = None
    echo_bad_ex = []
    for role, f in events:
        if role == "fetch":
            pending = words(f["bytes"])[2:]
        elif role == "reply":
            ww = words(f["bytes"])
            if ww[0] == 3:
                if pending is not None and tuple(ww[2:]) == tuple(pending):
                    echo_ok += 1
                else:
                    echo_bad += 1
                    echo_bad_ex.append((f, ww, pending))
                pending = None
    w(f"H16 type-3 replies echoing the preceding fetch (id,a,b,c): ok={echo_ok} differ={echo_bad}\n")
    for f, ww, p in echo_bad_ex[:10]:
        w(f"  seq {f['seq']} f{f['dframe']}: reply {ww} pending {p}\n")

    # Sequence bytes (H17) and inbound/outbound layout (H18)
    in_seq, out_seq = [], []
    len_le_ok = len_le_bad = 0
    b1_vals = collections.Counter()
    id_range_bad = 0
    out_layout = []
    reply_kinds = collections.Counter()
    for kind, hf, pf in pairs(events):
        if kind == "reply" and pf is not None:
            p = decode_payload("in", pf["bytes"])
            hw = words(hf["bytes"])
            in_seq.append((p["seq"], pf))
            b1_vals[p["b1"]] += 1
            if p["len"] == len(pf["bytes"]) - 5:
                len_le_ok += 1
            else:
                len_le_bad += 1
            if not 0 <= p["id"] <= 41:
                id_range_bad += 1
            reply_kinds[(hw[0], p["id"])] += 1
        if kind == "data" and pf is not None:
            p = decode_payload("out", pf["bytes"])
            out_seq.append((p["seq"], pf))
            out_layout.append((hf, pf, p))
    gaps = [(a, b) for a, b in zip(in_seq, in_seq[1:]) if (a[0] + 1) % 256 != b[0]]
    w(f"H17 IN payload seq: n={len(in_seq)} first={in_seq[0][0] if in_seq else None} (f{in_seq[0][1]['dframe'] if in_seq else '-'}) last={in_seq[-1][0] if in_seq else None} breaks={len(gaps)}\n")
    for a, b in gaps[:10]:
        w(f"  break: {a[0]} (f{a[1]['dframe']}) -> {b[0]} (f{b[1]['dframe']})\n")
    for a, b in zip(in_seq, in_seq[1:]):
        if a[0] == 0xFF and b[0] == 0x00:
            w(f"  wrap: f{a[1]['dframe']} starts {hexs(a[1]['bytes'][:5])} -> f{b[1]['dframe']} starts {hexs(b[1]['bytes'][:5])}\n")
    # Position of each data write in the session: reply headers so far, idle replies just before
    replies = idle_run = 0
    for role, f in events:
        if role == "reply":
            replies += 1
            hw = words(f["bytes"])
            idle_run = idle_run + 1 if (hw[0] == 4 and hw[1] == 0) else 0
        elif role == "out-data":
            w(f"data write f{f['dframe']} t={f['t']} [{hexs(f['bytes'])}]: after reply header #{replies}, after {idle_run} consecutive idle replies\n")
    w(f"H17 OUT data payload seq: {[(s, f['dframe']) for s, f in out_seq]}\n")
    w(f"H18 IN payload u16 LE at bytes 2-3 == len-5: ok={len_le_ok} bad={len_le_bad}; byte1 values {dict(b1_vals)}; id outside 0..41: {id_range_bad}\n")
    for hf, pf, p in out_layout:
        hw = words(hf["bytes"])
        w(f"H18 OUT data: header f{hf['dframe']} w={hw} payload f{pf['dframe']} [{hexs(pf['bytes'])}] pseq={p['seq']} len16={p['len']} (w1-4={hw[1]-4}) id={name(p['id'])}({p['id']}) body=[{hexs(p['body'])}]\n")
    w("reply (w0, payload id) counts:\n")
    for (t, oid), v in sorted(reply_kinds.items()):
        w(f"  type {t} {name(oid)}({oid}): {v}\n")

    # DirtyObjects (H20): records vs following fetches
    w("H20 DirtyObjects payloads and the fetches that follow:\n")
    fetch_seq = [(f, words(f["bytes"])) for r, f in events if r == "fetch"]
    dirty = []
    for kind, hf, pf in pairs(events):
        if kind == "reply" and pf is not None:
            p = decode_payload("in", pf["bytes"])
            if p["id"] == 40:
                cnt, recs = dirty_records(p["body"])
                ok_len = len(pf["bytes"]) == 5 + 2 + 5 * cnt
                dirty.append((hf, pf, cnt, recs, ok_len))
    for hf, pf, cnt, recs, ok_len in dirty:
        hw = words(hf["bytes"])
        w(f"  hdr f{hf['dframe']} w0={hw[0]} payload f{pf['dframe']} t={pf['t']} count={cnt} len_ok={ok_len} records={[(name(r[0]), r[0], r[1], r[2], r[3]) for r in recs]}\n")
    # Records in order vs fetches in order. A re-fetch (H23/S11.9) is a fetch of the same
    # tuple sent while the previous fetch has not yet been answered by a type-3 reply.
    rec_tuples = [(r[0], r[1], r[2], r[3]) for _, pf, _, recs, _ in dirty for r in recs]
    first_fetches, refetches, answered = [], [], True
    last_tuple = None
    for role, f in events:
        if role == "fetch":
            tup = tuple(words(f["bytes"])[2:])
            if not answered and tup == last_tuple:
                refetches.append(f)
            else:
                first_fetches.append((tup, f))
            last_tuple, answered = tup, False
        elif role == "reply" and words(f["bytes"])[0] == 3:
            answered = True
    fetch_only = [t for t, _ in first_fetches]
    w(f"  records total={len(rec_tuples)} fetches total={len(first_fetches) + len(refetches)} first-time fetches={len(fetch_only)} re-fetches={len(refetches)}\n")
    w(f"  records == first-time fetch sequence: {rec_tuples == fetch_only}\n")
    if rec_tuples != fetch_only:
        for i, (x, y) in enumerate(zip(rec_tuples, fetch_only)):
            if x != y:
                w(f"  first difference at index {i}: record {x} fetch {y}\n")
                break
    for f in refetches:
        w(f"  re-fetch seq {f['seq']} f{f['dframe']} t={f['t']} {hexs(f['bytes'])}\n")
    # every fetch issued only after the DirtyObjects payload that listed it?
    w("  fetch -> listing DirtyObjects payload frame (first and last of each list):\n")
    k = 0
    for hf, pf, cnt, recs, _ in dirty:
        seg = first_fetches[k:k + len(recs)]
        k += len(recs)
        if seg:
            ok = all(f["dframe"] > pf["dframe"] for _, f in seg)
            w(f"    list f{pf['dframe']}: fetches f{seg[0][1]['dframe']}..f{seg[-1][1]['dframe']} all after list: {ok}\n")

    # Per-object payload lengths and distinct bodies
    w("per object: payload lengths and number of distinct bodies (IN):\n")
    per = collections.defaultdict(list)
    for kind, hf, pf in pairs(events):
        if kind == "reply" and pf is not None:
            p = decode_payload("in", pf["bytes"])
            per[p["id"]].append((pf, p))
    for oid in sorted(per):
        lens = collections.Counter(len(p["body"]) for _, p in per[oid])
        bodies = collections.OrderedDict()
        for pf, p in per[oid]:
            bodies.setdefault(bytes(p["body"]), []).append(pf["dframe"])
        w(f"  {name(oid)}({oid}): n={len(per[oid])} body lengths {dict(lens)} distinct bodies {len(bodies)}\n")

    # Poll cadence (H23)
    polls = [f for r, f in events if r == "poll"]
    iv = [round((b["tSubmit"] - a["tSubmit"]) * 1000, 3) for a, b in zip(polls, polls[1:])]
    hist = collections.Counter(int(x // 2) * 2 for x in iv)
    w(f"H23 polls n={len(polls)}; poll-to-poll (submit) interval ms histogram (2 ms bins): {dict(sorted(hist.items()))}\n")
    if iv:
        s = sorted(iv)
        w(f"  min {s[0]} median {s[len(s)//2]} p95 {s[int(len(s)*0.95)]} max {s[-1]}\n")
    # fetch -> next poll gap and data -> next poll gap
    gaps_f, gaps_d = [], []
    for i, (role, f) in enumerate(events):
        if role in ("fetch", "out-data"):
            for r2, f2 in events[i + 1:]:
                if r2 == "poll":
                    (gaps_f if role == "fetch" else gaps_d).append(round((f2["tSubmit"] - f["t"]) * 1000, 3))
                    break
    if gaps_f:
        w(f"  fetch completion -> next poll submit ms: min {min(gaps_f)} max {max(gaps_f)} n={len(gaps_f)}\n")
    if gaps_d:
        w(f"  data completion -> next poll submit ms: {gaps_d}\n")
    # idle-only intervals: consecutive polls with no fetch or data write between them
    idle, prev_poll, busy = [], None, False
    for role, f in events:
        if role == "poll":
            if prev_poll is not None and not busy:
                idle.append(round((f["tSubmit"] - prev_poll["tSubmit"]) * 1000, 3))
            prev_poll, busy = f, False
        elif role in ("fetch", "data-hdr", "out-data", "in-payload"):
            busy = True
    if idle:
        s = sorted(idle)
        hist = collections.Counter(int(x // 2) * 2 for x in idle)
        w(f"  idle-only poll intervals (no fetch/data/payload in between) n={len(idle)}: min {s[0]} median {s[len(s)//2]} p95 {s[int(len(s)*0.95)]} max {s[-1]}; 2 ms bins {dict(sorted(hist.items()))}\n")
    # bus round trip
    rt = [round((f["t"] - f["tSubmit"]) * 1000, 3) for r, f in events if r == "reply"]
    if rt:
        s = sorted(rt)
        w(f"  reply-header IN submit->complete ms: min {s[0]} median {s[len(s)//2]} max {s[-1]}\n")
    # long silences in bulk traffic
    w("gaps > 100 ms between consecutive bulk transfers:\n")
    for a, b in zip(bulk, bulk[1:]):
        if b["tSubmit"] - a["t"] > 0.1:
            w(f"  after seq {a['seq']} f{a['dframe']} t={a['t']} -> seq {b['seq']} f{b['dframe']} t={b['tSubmit']} ({(b['tSubmit']-a['t'])*1000:.1f} ms)\n")


def in_payloads(frames, oid):
    events, _ = classify(frames)
    for kind, hf, pf in pairs(events):
        if kind == "reply" and pf is not None:
            p = decode_payload("in", pf["bytes"])
            if p["id"] == oid:
                yield hf, pf, p


def cmd_payloads(frames, oid, out):
    """Every IN payload of one object ID: header frame, payload frame, t, qualifiers, body."""
    for hf, pf, p in in_payloads(frames, oid):
        hw = words(hf["bytes"])
        out.write(f"f{pf['dframe']} t={pf['t']:.6f} pseq={p['seq']} w0={hw[0]} a={hw[3]} b={hw[4]} c={hw[5]} body[{len(p['body'])}]: {hexs(p['body'])}\n")


def cmd_diff(frames, oid, out):
    """Body byte positions that differ from the previous payload of the same object ID."""
    prev = None
    for hf, pf, p in in_payloads(frames, oid):
        body = p["body"]
        if prev is None:
            out.write(f"f{pf['dframe']} t={pf['t']:.6f} first body, {len(body)} bytes, non-zero positions: "
                      + " ".join(f"[{i}]={b:02X}" for i, b in enumerate(body) if b) + "\n")
        else:
            ch = [(i, prev[i] if i < len(prev) else None, body[i]) for i in range(len(body))
                  if i >= len(prev) or prev[i] != body[i]]
            out.write(f"f{pf['dframe']} t={pf['t']:.6f} {len(ch)} changed: "
                      + " ".join(f"[{i}]{a:02X}->{b:02X}" if a is not None else f"[{i}]new {b:02X}" for i, a, b in ch) + "\n")
        prev = body


def object_key(hf, p):
    """(id, a, b, c) of an IN payload: the type-3 header echo, or (id, 0, 0, 0) for type 4."""
    hw = words(hf["bytes"])
    return (p["id"], hw[3], hw[4], hw[5]) if hw[0] == 3 else (p["id"], 0, 0, 0)


def cmd_timeline(frames, notes, ids, t_from, t_to, max_changes, out):
    """Per-object change timeline: every IN payload whose body differs from the previous body
    of the same (id, a, b, c), with the changed byte positions, interleaved with the notes.
    The first payload of each key is reported as 'first' (no comparison)."""
    events, _ = classify(frames)
    last = {}
    rows = []
    for kind, hf, pf in pairs(events):
        if kind != "reply" or pf is None:
            continue
        p = decode_payload("in", pf["bytes"])
        key = object_key(hf, p)
        body = bytes(p["body"])
        prev = last.get(key)
        last[key] = body
        if ids and key[0] not in ids:
            continue
        if prev is None:
            desc = f"first ({len(body)} bytes)"
        elif prev == body:
            desc = "unchanged"
        else:
            ch = [i for i in range(max(len(prev), len(body)))
                  if i >= len(prev) or i >= len(body) or prev[i] != body[i]]
            parts = []
            for i in ch[:max_changes]:
                a = f"{prev[i]:02X}" if i < len(prev) else "--"
                b = f"{body[i]:02X}" if i < len(body) else "--"
                parts.append(f"[{i}]{a}->{b}")
            more = f" ...(+{len(ch) - max_changes})" if len(ch) > max_changes else ""
            desc = f"{len(ch)} changed: " + " ".join(parts) + more
        rows.append((pf["t"], 1, f"t={pf['t']:10.6f} f{pf['dframe']:<6d} seq{pf['seq']:<6d} {name(key[0])}({key[0]}) a={key[1]} b={key[2]} c={key[3]} pseq={p['seq']}: {desc}"))
    for n in notes:
        rows.append((n["t"], 0, f"t={n['t']:10.6f} NOTE {n['text']}" + (f" (log {n['logTime']})" if "logTime" in n else "")))
    for t, _, line in sorted(rows, key=lambda r: (r[0], r[1])):
        if (t_from is None or t >= t_from) and (t_to is None or t <= t_to):
            out.write(line + "\n")


def cmd_census(frames, out):
    """For every (id, a, b, c) fetched more than once: the body positions that changed at least
    once, with first / last / min / max value and the frames of the first and last change."""
    events, _ = classify(frames)
    hist = {}
    for kind, hf, pf in pairs(events):
        if kind == "reply" and pf is not None:
            p = decode_payload("in", pf["bytes"])
            hist.setdefault(object_key(hf, p), []).append((pf, bytes(p["body"])))
    for key in sorted(hist):
        h = hist[key]
        if key[0] == 40 or len(h) < 2:
            continue
        n = max(len(b) for _, b in h)
        pos = [i for i in range(n) if len({b[i] if i < len(b) else None for _, b in h}) > 1]
        tag = f"{name(key[0])}({key[0]}) a={key[1]} b={key[2]} c={key[3]}: {len(h)} payloads"
        if not pos:
            out.write(f"{tag}, never changed\n")
            continue
        out.write(f"{tag}, {len(pos)} positions changed\n")
        for i in pos:
            vals = [b[i] for _, b in h if i < len(b)]
            chg = [pf for (pf, b), (_, pb) in zip(h[1:], h[:-1]) if i < len(b) and i < len(pb) and b[i] != pb[i]]
            out.write(f"   [{i}] first {vals[0]:02X} last {vals[-1]:02X} min {min(vals):02X} max {max(vals):02X}; "
                      f"{len(chg)} changes, f{chg[0]['dframe']} (t={chg[0]['t']:.3f}) .. f{chg[-1]['dframe']} (t={chg[-1]['t']:.3f})\n")


def cmd_lcd(frames, out):
    """DisplayContent (ID 21) payloads as `[count]` + two 20-character lines, changes only.
    Bytes 0x80-0x87 are printed as <0>..<7>: placeholders, not a decoded meaning (ConsoleLink
    describes them as bar glyphs on the ML; see docs/protocol/consolelink-analysis.md)."""
    def cell(c):
        if 0x80 <= c <= 0x87:
            return f"<{c - 0x80}>"
        return chr(c) if 0x20 <= c < 0x7F else f"\\x{c:02X}"
    prev = None
    for hf, pf, p in in_payloads(frames, 21):
        b = p["body"]
        lines = ("".join(cell(c) for c in b[1:21]), "".join(cell(c) for c in b[21:41]))
        if lines != prev:
            out.write(f"f{pf['dframe']} seq{pf['seq']} t={pf['t']:.6f} count={b[0]} |{lines[0]}|{lines[1]}|\n")
        prev = lines


def first_bodies(frames):
    """First IN body of every (id, a, b, c) in a capture, with its payload frame."""
    events, _ = classify(frames)
    first = {}
    for kind, hf, pf in pairs(events):
        if kind == "reply" and pf is not None:
            p = decode_payload("in", pf["bytes"])
            first.setdefault(object_key(hf, p), (bytes(p["body"]), pf))
    return first


def cmd_startdiff(frames_a, frames_b, label_a, label_b, max_changes, out):
    """Compare the first body of every object key between two captures."""
    fa, fb = first_bodies(frames_a), first_bodies(frames_b)
    same = 0
    for key in sorted(set(fa) | set(fb)):
        tag = f"{name(key[0])}({key[0]}) a={key[1]} b={key[2]} c={key[3]}"
        if key not in fa or key not in fb:
            where = label_a if key in fa else label_b
            body, pf = (fa.get(key) or fb.get(key))
            out.write(f"only in {where}: {tag} f{pf['dframe']} ({len(body)} bytes)\n")
            continue
        (ba, pa), (bb, pb) = fa[key], fb[key]
        if ba == bb:
            same += 1
            continue
        ch = [i for i in range(max(len(ba), len(bb))) if i >= len(ba) or i >= len(bb) or ba[i] != bb[i]]
        parts = " ".join(f"[{i}]{(f'{ba[i]:02X}' if i < len(ba) else '--')}->{(f'{bb[i]:02X}' if i < len(bb) else '--')}" for i in ch[:max_changes])
        more = f" ...(+{len(ch) - max_changes})" if len(ch) > max_changes else ""
        out.write(f"differs: {tag} {label_a} f{pa['dframe']} vs {label_b} f{pb['dframe']}: {len(ch)} bytes: {parts}{more}\n")
    out.write(f"identical first bodies: {same}; keys {label_a} {len(fa)}, {label_b} {len(fb)}\n")


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("jsonl")
    ap.add_argument("mode", nargs="?", default="summary",
                    choices=["summary", "log", "payloads", "diff", "timeline", "startdiff", "census", "lcd"])
    ap.add_argument("--id", type=int, action="append",
                    help="object ID for payloads / diff (one), or a filter for timeline (repeatable)")
    ap.add_argument("--other", help="second JSONL for startdiff")
    ap.add_argument("--from", dest="t_from", type=float, help="timeline: first t to print")
    ap.add_argument("--to", dest="t_to", type=float, help="timeline: last t to print")
    ap.add_argument("--max-changes", type=int, default=24, help="timeline / startdiff: changed bytes listed per line")
    args = ap.parse_args(argv)
    header, frames = load(args.jsonl)
    out = sys.stdout
    if args.mode == "log":
        cmd_log(frames, out)
    elif args.mode in ("payloads", "diff"):
        if not args.id or len(args.id) != 1:
            ap.error("exactly one --id is required for this mode")
        (cmd_payloads if args.mode == "payloads" else cmd_diff)(frames, args.id[0], out)
    elif args.mode == "timeline":
        cmd_timeline(frames, load_notes(args.jsonl), set(args.id or []), args.t_from, args.t_to,
                     args.max_changes, out)
    elif args.mode == "census":
        cmd_census(frames, out)
    elif args.mode == "lcd":
        cmd_lcd(frames, out)
    elif args.mode == "startdiff":
        if not args.other:
            ap.error("--other is required for startdiff")
        _, frames_b = load(args.other)
        la = header["sourceFile"] if header else "A"
        hb, _ = load(args.other)
        lb = hb["sourceFile"] if hb else "B"
        cmd_startdiff(frames, frames_b, la, lb, args.max_changes, out)
    else:
        cmd_summary(header, frames, out)
    return 0


if __name__ == "__main__":
    sys.exit(main())
