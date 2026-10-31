"""E12: every PC -> console data write (type-1 header + payload) with its seq / frame numbers, time (capture-relative
and local), header words, a decoded summary of the body, the action-log note before it, and the console's lists and
objects after it until the next write (at most 20 s). Read-only, stdlib only, never talks to a device; a decoding aid,
not a protocol definition (nothing here may be used to build bytes for a console).
Usage (from the repository root): py -3.13 -I -B tools/protocol-analyzer/e12/writes.py <repo> <jsonl> [--utc-offset +01:00]
"""
import datetime
import os
import sys

sys.stdout.reconfigure(encoding="utf-8")
repo, path = sys.argv[1], sys.argv[2]
offset_h = 1.0
if "--utc-offset" in sys.argv:
    sign, hh_mm = sys.argv[sys.argv.index("--utc-offset") + 1][0], sys.argv[sys.argv.index("--utc-offset") + 1][1:]
    hh, mm = hh_mm.split(":")
    offset_h = (int(hh) + int(mm) / 60) * (-1 if sign == "-" else 1)
sys.path.insert(0, os.path.join(repo, "tools", "protocol-analyzer"))
import hsp_summary as H  # noqa: E402

header, frames = H.load(path)
notes = H.load_notes(path)
events, _ = H.classify(frames)
start = datetime.datetime.strptime(header["captureStart"], "%Y-%m-%dT%H:%M:%S.%fZ") + datetime.timedelta(hours=offset_h)


def local(t):
    return (start + datetime.timedelta(seconds=t)).strftime("%H:%M:%S.%f")[:-3]


def label(block):
    """[03][06] + 18 UTF-16BE code units (reading A, H40) -> three lines cut at the first 0 (display only)."""
    units = [block[2 + 2 * i] << 8 | block[3 + 2 * i] for i in range(18)]
    lines = []
    for k in range(3):
        s = units[6 * k:6 * k + 6]
        if 0 in s:
            s = s[:s.index(0)]
        lines.append("".join(chr(u) for u in s))
    raw_nonzero = [i for i, u in enumerate(units) if u]
    return "/".join(lines), raw_nonzero


def record(r):
    """12-byte step record (H41): cue x10, CUEPARAMS, memory index, x, Up, Down, Wait (tenths)."""
    return (f"cue {(r[0] << 8 | r[1]) / 10:.1f} type {r[2]} mem 0x{r[3] << 8 | r[4]:04X} x {r[5]} "
            f"up {(r[6] << 8 | r[7]) / 10:.1f} down {(r[8] << 8 | r[9]) / 10:.1f} wait {(r[10] << 8 | r[11]) / 10:.1f}")


def summary(oid, b):
    if oid == 39:
        return "System " + H.hexs(b)
    if oid == 20:
        return f"ConsoleEvent kind {b[0]} code/index 0x{b[1]:02X} value {b[2]}" + (f" previous {b[3]}" if len(b) > 3 else "")
    if oid in (32, 33, 34, 42):
        return f"a {b[0]} b {b[1]} c 0x{b[2] << 8 | b[3]:04X} ({(b[2] << 8 | b[3]) / 10:.1f})"
    if oid == 35:
        return f"a {b[0]} b {b[1]} c {b[2]}"
    if oid == 5:
        diff = {d: b[1 + d] for d in range(1, 513) if b[1 + d] and not (d <= 46 and b[1 + d] == d)}
        return f"count {b[0] << 8 | b[1]}; non-identity entries (DMX: channel) {diff}"
    if oid == 12:
        return f"count {b[0]}; record 1 {H.hexs(b[1:5])} label {label(b[5:43])[0]!r}; record 2 {H.hexs(b[43:47])} label {label(b[47:85])[0]!r}"
    if oid == 4:
        s = f"step index {b[0]}: " + record(b[1:13])
        if len(b) > 13:
            n = b[13]
            lv = b[14:14 + n]
            text, nz = label(b[14 + n:])
            s += f"; level count 0x{n:02X}, non-zero levels {[(i + 1, lv[i]) for i in range(n) if lv[i]]}; label {text!r} (non-zero code units {nz})"
        return s
    if oid == 2:
        text, _ = label(b[10:48])
        recs = [record(b[48 + 12 * i:60 + 12 * i]) for i in range(b[2])]
        return f"page/seq {b[0]}/{b[1]} steps {b[2]} mode {b[3]} c..e {H.hexs(b[4:10])} label {text!r}; records: " + " | ".join(recs)
    if oid == 0:
        n = b[3]
        lv = b[4:4 + n]
        text, nz = label(b[4 + n:])
        return f"page-1 {b[0]} slot-1 {b[1]} flag {b[2]} level count 0x{n:02X}; non-zero levels {[(i + 1, f'{lv[i]:02X}') for i in range(n) if lv[i]]}; label {text!r} (non-zero code units {nz})"
    if oid == 8:
        text, nz = label(b[1:])
        return f"page-1 {b[0]} label {text!r} (non-zero code units {nz})"
    return H.hexs(b)


pl = list(H.pairs(events))
data_idx = [i for i, (k, h, p) in enumerate(pl) if k == "data"]
for j, i in enumerate(data_idx):
    _, hf, pf = pl[i]
    w = H.words(hf["bytes"])
    p = H.decode_payload("out", pf["bytes"])
    note = None
    for n in notes:
        if n["t"] <= hf["t"]:
            note = n
    print(f"W{j + 1} header seq{hf['seq']} f{hf['dframe']} / payload seq{pf['seq']} f{pf['dframe']}  t={hf['t']:.6f} ({local(hf['t'])})  "
          f"w={w}  pseq={p['seq']}  {H.name(p['id'])}({p['id']})  len16={p['len']} body[{len(p['body'])}]")
    print(f"    {summary(p['id'], bytes(p['body']))}")
    if note:
        print(f"    note before: {note['text'][:120]!r} at t={note['t']:.3f}")
    end_i = data_idx[j + 1] if j + 1 < len(data_idx) else len(pl)
    shown = 0
    for k2, h2, p2 in pl[i + 1:end_i]:
        if k2 != "reply" or p2 is None:
            continue
        if p2["t"] - pf["t"] > 20 or shown >= 12:
            print("    ...")
            break
        q = H.decode_payload("in", p2["bytes"])
        hw = H.words(h2["bytes"])
        if q["id"] == 40:
            cnt, recs = H.dirty_records(bytes(q["body"]))
            txt = ", ".join(f"{r[0]}" if r[1:] == (0, 0, 0) else f"{r[0]}:{r[1]}/{r[2]}/{r[3]}" for r in recs[:12]) + (" ..." if cnt > 12 else "")
            print(f"    <- list seq{p2['seq']} f{p2['dframe']} +{p2['t'] - pf['t']:.3f} s: {cnt} records [{txt}]")
        else:
            print(f"    <- {H.name(q['id'])}({q['id']}) {hw[3]}/{hw[4]}/{hw[5]} seq{p2['seq']} f{p2['dframe']} +{p2['t'] - pf['t']:.3f} s, {len(q['body'])} bytes")
        shown += 1
