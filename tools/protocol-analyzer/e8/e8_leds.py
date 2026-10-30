"""Ad-hoc E8 LED / LCD / ConsoleStatus / FaderInfo block view (committed with the E8 record).
Uses the committed decoding functions of tools/protocol-analyzer/hsp_summary.py. Read-only.

LED block k = LedStatus body[1 + 6(k - 1) .. 6k] (F84 / F100 numbering), printed as
"k:RRGGBB/RRGGBB" (phase A / phase B). FaderInfo record N = body[1 + 4(N - 1) .. 4N].

Usage: py -3.13 -I e8_leds.py <repo> <jsonl> [--from T] [--to T]
"""
import argparse
import os
import sys

ap = argparse.ArgumentParser()
ap.add_argument("repo")
ap.add_argument("jsonl")
ap.add_argument("--from", dest="t_from", type=float)
ap.add_argument("--to", dest="t_to", type=float)
args = ap.parse_args()
sys.path.insert(0, os.path.join(args.repo, "tools", "protocol-analyzer"))
import hsp_summary as H  # noqa: E402

header, frames = H.load(args.jsonl)
events, _ = H.classify(frames)


def blk(body, k):
    b = body[1 + 6 * (k - 1): 1 + 6 * k]
    return f"{b[0]:02X}{b[1]:02X}{b[2]:02X}/{b[3]:02X}{b[4]:02X}{b[5]:02X}"


def lcd(body):
    def c(x):
        return f"<{x - 0x80}>" if 0x80 <= x <= 0x87 else (chr(x) if 0x20 <= x < 0x7F else f"\\x{x:02X}")
    return "|" + "".join(c(x) for x in body[1:21]) + "|" + "".join(c(x) for x in body[21:41]) + "|"


prev = {}
for kind, hf, pf in H.pairs(events):
    if kind != "reply" or pf is None:
        continue
    p = H.decode_payload("in", pf["bytes"])
    oid = p["id"]
    body = bytes(p["body"])
    t = pf["t"]
    old = prev.get(oid)
    prev[oid] = body
    if (args.t_from is not None and t < args.t_from) or (args.t_to is not None and t > args.t_to):
        continue
    head = f"t={t:9.3f} seq{pf['seq']:<6d}"
    if oid == 22:
        if old is None:
            print(head, "LED first:", " ".join(f"{k}:{blk(body, k)}" for k in range(1, 76)))
        else:
            ch = [k for k in range(1, 76) if blk(body, k) != blk(old, k)]
            if ch:
                print(head, "LED", " ".join(f"{k}:{blk(old, k)}->{blk(body, k)}" for k in ch))
    elif oid == 21:
        if old is None or old != body:
            print(head, "LCD", lcd(body))
    elif oid == 23:
        if old is None or old != body:
            print(head, "CST", " ".join(f"{x:02X}" for x in body))
    elif oid == 14:
        if old is None or old != body:
            recs = [body[1 + 4 * (n - 1): 1 + 4 * n] for n in range(1, 49)]
            nz = [f"r{n + 1}={r.hex().upper()}" for n, r in enumerate(recs) if any(r) and not (n + 1 in (21, 25) and r == bytes([0, 0, 1, 0]))]
            print(head, "FDR", " ".join(nz), f"BUMPS={body[193]:02X} MASTER={body[194]:02X}")
    elif oid == 12:
        if old is None or old != body:
            print(head, "IND", " ".join(f"{x:02X}" for x in body[:5]), "|", " ".join(f"{x:02X}" for x in body[43:47]))
    elif oid in (13, 15):
        if old is None or old != body:
            nz = [f"[{i}]={x:02X}" for i, x in enumerate(body) if x and i > (1 if oid == 13 else 0)]
            print(head, "OUT" if oid == 13 else "CHL", " ".join(nz))
    elif oid == 17:
        if old is None or old[65:81] != body[65:81]:
            print(head, "PB5", " ".join(f"{x:02X}" for x in body[65:81]), "| r1", " ".join(f"{x:02X}" for x in body[1:17]))
