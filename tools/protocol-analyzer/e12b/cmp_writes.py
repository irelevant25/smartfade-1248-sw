"""E12: each PC -> console object write (ids 0, 2, 4, 5, 8, 12) compared with (1) the previous write of the same object
(StepContent compared without its index byte, which shifts), (2) the console's last body of that object before the
write and (3) the console's next body of it after the write, plus the DirtyObjects lists that came between the write
and the next write (at most 15 s). SmartSoft's 48-level bodies (Memory 90 bytes, type-2 StepContent 100 bytes) are
expanded to the console's 96-level layout for (2) and (3): [count 0x30][48 levels] -> [0x60][48 levels][48 zeros]
(a comparison aid; INFERRED from the two layouts, F143). Read-only, stdlib only, never talks to a device.
Usage (from the repository root): py -3.13 -I -B tools/protocol-analyzer/e12b/cmp_writes.py <repo> <jsonl>

E12b copy (2026-10-31, late evening) of tools/protocol-analyzer/e12/cmp_writes.py (SHA-256 eb316aa6...a7968cca), changed
in one place only: a Sequence write is matched with the console's Sequence bodies of the same page and slot under ANY
qualifier c (the original listed c = 0 and E12's c = 6; E12b's console listed the Sequence with c = 2 and c = 7).
"""
import os
import sys

sys.stdout.reconfigure(encoding="utf-8")
repo, path = sys.argv[1], sys.argv[2]
sys.path.insert(0, os.path.join(repo, "tools", "protocol-analyzer"))
import hsp_summary as H  # noqa: E402

header, frames = H.load(path)
events, _ = H.classify(frames)
console, writes, lists = [], [], []
for kind, hf, pf in H.pairs(events):
    if kind == "data" and pf is not None:
        p = H.decode_payload("out", pf["bytes"])
        writes.append((hf["t"], hf["seq"], pf["seq"], pf["dframe"], p["id"], bytes(p["body"])))
    elif kind == "reply" and pf is not None:
        q = H.decode_payload("in", pf["bytes"])
        if q["id"] == 40:
            lists.append((pf["t"], H.dirty_records(bytes(q["body"]))[1]))
        else:
            console.append((pf["t"], pf["seq"], H.object_key(hf, q), bytes(q["body"])))


def keys_of(oid, body):
    """The console's object keys a write of this body concerns (Sequence: the page and slot under any c, see below)."""
    if oid == 0:
        return [(0, body[0], body[1], 0)]
    if oid in (4, 8):
        return [(oid, body[0], 0, 0)]
    if oid == 2:
        return [(2, body[0], body[1], c) for c in range(256)]
    return [(oid, 0, 0, 0)]


def console_layout(oid, body):
    if oid == 0 and len(body) == 90 and body[3] == 0x30:
        return body[:3] + bytes([0x60]) + body[4:52] + bytes(48) + body[52:]
    if oid == 4 and len(body) == 100 and body[13] == 0x30:
        return body[:13] + bytes([0x60]) + body[14:62] + bytes(48) + body[62:]
    return body


def diff(a, b, limit=12):
    d = [i for i in range(max(len(a), len(b))) if i >= len(a) or i >= len(b) or a[i] != b[i]]
    if not d:
        return "identical"
    shown = " ".join(f"[{i}]{(f'{a[i]:02X}' if i < len(a) else '--')}->{(f'{b[i]:02X}' if i < len(b) else '--')}" for i in d[:limit])
    return f"{len(d)} bytes differ: {shown}{' ...' if len(d) > limit else ''}"


previous = {}
for n, (t, sh, sp, fr, oid, body) in enumerate(writes, 1):
    if oid not in (0, 2, 4, 5, 8, 12):
        continue
    ident = (oid, body[0], body[1]) if oid == 0 else (oid, body[0]) if oid == 8 else (oid,)
    cmp_body = body[1:] if oid == 4 else body
    keys = keys_of(oid, body)
    mapped = console_layout(oid, body)
    before = [c for c in console if c[2] in keys and c[0] < t]
    after = [c for c in console if c[2] in keys and c[0] > t]
    t_next = writes[n][0] if n < len(writes) else t + 15
    between = [r for lt, recs in lists if t < lt < min(t_next, t + 15) for r in recs]
    print(f"W{n} header seq{sh} / payload seq{sp} f{fr} t={t:.3f} {H.name(oid)}({oid}) body[{len(body)}]")
    if ident in previous:
        pn, pb = previous[ident]
        print(f"    vs previous write W{pn}: {diff(pb, cmp_body)}")
    else:
        print("    first write of this object in the capture")
    previous[ident] = (n, cmp_body)
    if before:
        bt, bs, bk, bb = before[-1]
        print(f"    vs the console's last body (seq{bs}, key {bk}, {len(bb)} bytes): {diff(bb, mapped)}")
    if after:
        at, as_, ak, ab = after[0]
        print(f"    the console's next body (seq{as_}, t={at:.1f}, key {ak}, {len(ab)} bytes) vs the write: {diff(mapped, ab)}")
    print(f"    records listed before the next write (max 15 s): {len(between)}; ids {sorted({r[0] for r in between})}")
