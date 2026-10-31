"""E12b: a merged, time-ordered view of one window of a capture: the PC -> console data writes (first 16 body bytes),
the DirtyObjects lists, PlaybackInfo records 5 and 1 (body[65..80], body[1..16]) when they change, LED blocks 50, 51,
58, 59, 60, 69 and 70 when any of them changes, every ConsoleStatus and SequenceMain body, and the LCD (DisplayContent)
when it changes (bytes 0x80-0x87 printed as <0>..<7>, placeholders). Used for the unlogged playback of 617-660 s, the
SD save and the step commands on a live stack. Read-only, stdlib only, never talks to a device; a decoding aid, not a
protocol definition.
Usage (from the repository root): py -3.13 -I -B tools/protocol-analyzer/e12b/playback.py <repo> <jsonl> <t_from> <t_to>
"""
import os
import sys

sys.stdout.reconfigure(encoding="utf-8")
repo, path = sys.argv[1], sys.argv[2]
t0 = float(sys.argv[3]); t1 = float(sys.argv[4])
sys.path.insert(0, os.path.join(repo, "tools", "protocol-analyzer"))
import hsp_summary as H  # noqa: E402

header, frames = H.load(path)
events, _ = H.classify(frames)
rows = []
last = {}
for kind, hf, pf in H.pairs(events):
    if pf is None:
        continue
    if not (t0 <= pf["t"] <= t1):
        continue
    if kind == "data":
        p = H.decode_payload("out", pf["bytes"])
        b = bytes(p["body"])
        rows.append((hf["t"], f"{hf['t']:9.3f} seq{hf['seq']} f{pf['dframe']} WRITE pseq={p['seq']} {H.name(p['id'])} {H.hexs(b[:16])}"))
        continue
    q = H.decode_payload("in", pf["bytes"])
    b = bytes(q["body"])
    oid = q["id"]
    if oid == 40:
        cnt, recs = H.dirty_records(b)
        rows.append((pf["t"], f"{pf['t']:9.3f} seq{pf['seq']} list {[r[0] if r[1:] == (0, 0, 0) else r for r in recs][:12]}"))
    elif oid == 17:
        r5 = b[65:81]
        r1 = b[1:17]
        if last.get(17) != (r1, r5):
            rows.append((pf["t"], f"{pf['t']:9.3f} seq{pf['seq']} f{pf['dframe']} PlaybackInfo rec5 {H.hexs(r5)} | rec1 {H.hexs(r1)}"))
        last[17] = (r1, r5)
    elif oid == 22:
        blk = {k: b[1 + 6 * (k - 1):1 + 6 * k] for k in (50, 51, 58, 59, 60, 69, 70)}
        sig = tuple(blk.values())
        if last.get(22) != sig:
            rows.append((pf["t"], f"{pf['t']:9.3f} seq{pf['seq']} f{pf['dframe']} LED " + " ".join(f"{k}:{H.hexs(v)}" for k, v in blk.items())))
        last[22] = sig
    elif oid == 23:
        rows.append((pf["t"], f"{pf['t']:9.3f} seq{pf['seq']} f{pf['dframe']} ConsoleStatus {H.hexs(b)}"))
    elif oid == 21:
        def cell(c):
            if 0x80 <= c <= 0x87:
                return f"<{c - 0x80}>"
            return chr(c) if 0x20 <= c < 0x7F else f"\\x{c:02X}"
        lines = ("".join(cell(c) for c in b[1:21]), "".join(cell(c) for c in b[21:41]))
        if last.get(21) != lines:
            rows.append((pf["t"], f"{pf['t']:9.3f} seq{pf['seq']} f{pf['dframe']} LCD |{lines[0]}|{lines[1]}|"))
        last[21] = lines
    elif oid == 3:
        rows.append((pf["t"], f"{pf['t']:9.3f} seq{pf['seq']} f{pf['dframe']} SequenceMain {H.hexs(b[:8])}"))
for t, line in sorted(rows):
    print(line)
