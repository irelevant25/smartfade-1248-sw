"""Ad-hoc E8 LED state view (committed with the E8 record): for every LedStatus payload that changed,
the compact state of blocks 1-24 and every indicator block 49-75 that differs from the session's
first LedStatus payload, plus the LCD text at that moment. Read-only.

Codes: '.' = 000000/000000, 'r' = 0A0000 both phases (dim red), 'R' = 460000 both (recorded red),
'F' = FF0000 both (full red), 'b' = 460000/0A0000 (blink 46/0A), 'c' = C80000/0A0000 (blink C8/0A),
'y' = 464600 both (yellow), 'Y' = C8C800/0A0A00 (blink yellow), 'z' = 464600/0A0A00 (blink 4646/0A0A),
'g' = green steady (any G only), otherwise '?'.

Usage: py -3.13 -I e8_ledstate.py <repo> <jsonl> [--from T] [--to T]
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

CODES = {
    "000000/000000": ".", "0A0000/0A0000": "r", "460000/460000": "R", "FF0000/FF0000": "F",
    "460000/0A0000": "b", "C80000/0A0000": "c", "464600/464600": "y", "C8C800/0A0A00": "Y",
    "464600/0A0A00": "z",
}


def blk(body, k):
    b = body[1 + 6 * (k - 1): 1 + 6 * k]
    return f"{b[0]:02X}{b[1]:02X}{b[2]:02X}/{b[3]:02X}{b[4]:02X}{b[5]:02X}"


def code(v):
    if v in CODES:
        return CODES[v]
    a, b = v.split("/")
    if a[0:2] == "00" and a[4:6] == "00" and a == b:
        return "g"
    return "?"


header, frames = H.load(args.jsonl)
events, _ = H.classify(frames)
base = None
prev = None
lcd_text = ""
for kind, hf, pf in H.pairs(events):
    if kind != "reply" or pf is None:
        continue
    p = H.decode_payload("in", pf["bytes"])
    body = bytes(p["body"])
    if p["id"] == 21:
        lcd_text = "".join(chr(x) if 0x20 <= x < 0x7F else "#" for x in body[1:21]).rstrip() + " / " + \
            "".join(chr(x) if 0x20 <= x < 0x7F else "#" for x in body[21:41]).rstrip()
        continue
    if p["id"] != 22:
        continue
    if base is None:
        base = body
    if prev is not None and prev == body:
        continue
    prev = body
    t = pf["t"]
    if (args.t_from is not None and t < args.t_from) or (args.t_to is not None and t > args.t_to):
        continue
    s1 = "".join(code(blk(body, k)) for k in range(1, 13)) + " " + "".join(code(blk(body, k)) for k in range(13, 25))
    ind = [f"{k}:{blk(body, k)}" for k in range(49, 76) if blk(body, k) != blk(base, k)]
    odd = [f"{k}:{blk(body, k)}" for k in range(1, 25) if code(blk(body, k)) == "?"]
    hi = [k for k in range(25, 49) if blk(body, k) != blk(base, k)]
    print(f"t={t:8.3f} seq{pf['seq']:<6d} 1-24 [{s1}] ind {' '.join(ind) or '-'}"
          + (f" odd {' '.join(odd)}" if odd else "") + (f" 25-48 changed {hi}" if hi else "") + f"  | LCD {lcd_text}")
