"""E8b: PlaybackInfo (ID 17) records 1-5 over time, printing a line whenever a record's bytes 0-1, 4-9 or
the u16 times change, or the Live/Next pair changes 'phase' (start, reset). Read-only.
Usage: py -3.13 -I pbinfo.py <repo> <jsonl> [--all]"""
import os, sys
repo, path = sys.argv[1], sys.argv[2]
allp = "--all" in sys.argv
sys.path.insert(0, os.path.join(repo, "tools", "protocol-analyzer"))
import hsp_summary as H
header, frames = H.load(path)
events, _ = H.classify(frames)
prev = None
n = 0
for kind, hf, pf in H.pairs(events):
    if kind != "reply" or pf is None:
        continue
    p = H.decode_payload("in", pf["bytes"])
    if p["id"] != 17:
        continue
    body = bytes(p["body"]); n += 1
    recs = [body[1 + 16 * k: 17 + 16 * k] for k in range(5)]
    if prev is None:
        print(f"t={pf['t']:9.3f} seq{pf['seq']:<6d} first body[0]={body[0]:02X} " + " | ".join(r.hex(" ").upper() for r in recs))
    else:
        ch = []
        for k in range(5):
            a, b = prev[k], recs[k]
            if a == b:
                continue
            key = (a[0:2] != b[0:2]) or (a[4:16] != b[4:16]) or ((a[2], a[3]) in ((0xFF, 0), (0, 0xFF)) or (b[2], b[3]) in ((0xFF, 0), (0, 0xFF)))
            if key or allp:
                ch.append(f"r{k+1}: {a.hex(' ').upper()} -> {b.hex(' ').upper()}")
        if ch:
            print(f"t={pf['t']:9.3f} seq{pf['seq']:<6d} " + " || ".join(ch))
    prev = recs
print("PlaybackInfo payloads", n)
