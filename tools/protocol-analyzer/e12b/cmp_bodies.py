"""E12b: compares chosen payloads by their JSONL seq, A:B pairs (OUT or IN payloads; A's body is expanded from SmartSoft's
48-level layout to the console's 96-level layout as in e12/cmp_writes.py, INFERRED from the two layouts, F143), printing
the differing body positions (a StepContent's index byte, body[0], shows as a difference when the step has moved), and
prints the body of every single seq given without a colon. Read-only, stdlib only, never talks to a device.
Usage (from the repository root): py -3.13 -I -B tools/protocol-analyzer/e12b/cmp_bodies.py <repo> <jsonl> [A:B ...] [seq ...]
"""
import os
import sys

sys.stdout.reconfigure(encoding="utf-8")
repo, path = sys.argv[1], sys.argv[2]
pairs = [tuple(int(x) for x in a.split(":")) for a in sys.argv[3:] if ":" in a]
shows = [int(a) for a in sys.argv[3:] if ":" not in a]
sys.path.insert(0, os.path.join(repo, "tools", "protocol-analyzer"))
import hsp_summary as H  # noqa: E402

header, frames = H.load(path)
by_seq = {f["seq"]: f for f in frames}


def body_of(seq):
    f = by_seq[seq]
    p = H.decode_payload("out" if f["dir"] == "out" else "in", f["bytes"])
    return p["id"], bytes(p["body"])


def console_layout(oid, body):
    if oid == 0 and len(body) == 90 and body[3] == 0x30:
        return body[:3] + bytes([0x60]) + body[4:52] + bytes(48) + body[52:]
    if oid == 4 and len(body) == 100 and body[13] == 0x30:
        return body[:13] + bytes([0x60]) + body[14:62] + bytes(48) + body[62:]
    return body


for a, b in pairs:
    ia, ba = body_of(a)
    ib, bb = body_of(b)
    ma = console_layout(ia, ba)
    d = [i for i in range(max(len(ma), len(bb))) if i >= len(ma) or i >= len(bb) or ma[i] != bb[i]]
    print(f"seq{a} ({H.name(ia)}, {len(ba)} B, in the console's layout {len(ma)} B) vs seq{b} ({H.name(ib)}, {len(bb)} B): "
          f"{len(d)} positions differ: " + " ".join(f"[{i}]{ma[i]:02X}->{bb[i]:02X}" for i in d[:20] if i < len(ma) and i < len(bb)))
for s in shows:
    i, b = body_of(s)
    print(f"seq{s} {H.name(i)} [{len(b)}]: {H.hexs(b)}")
