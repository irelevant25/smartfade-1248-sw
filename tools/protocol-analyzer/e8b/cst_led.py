"""E8b: every ConsoleStatus payload (changed or not) and LedStatus payload counts in given windows. Read-only.
Usage: py -3.13 -I cst_led.py <repo> <jsonl>"""
import os, sys
repo, path = sys.argv[1], sys.argv[2]
sys.path.insert(0, os.path.join(repo, "tools", "protocol-analyzer"))
import hsp_summary as H
header, frames = H.load(path)
events, _ = H.classify(frames)
prev = None; led = []
for kind, hf, pf in H.pairs(events):
    if kind != "reply" or pf is None:
        continue
    p = H.decode_payload("in", pf["bytes"]); b = bytes(p["body"])
    if p["id"] == 23:
        print(f"CST seq{pf['seq']} t={pf['t']:.3f} {b.hex(' ').upper()} {'changed' if prev is not None and b != prev else ('first' if prev is None else 'unchanged')}")
        prev = b
    if p["id"] == 22:
        led.append(pf["t"])
for a, b, name in ((349.0, 359.4, "REC MEM"), (368.0, 377.3, "EDIT MEM"), (388.0, 398.1, "REC SEQ"), (406.0, 413.4, "COPY"), (413.4, 447.9, "after COPY to STACK activity")):
    print(name, "LedStatus payloads in window:", sum(1 for t in led if a <= t <= b))
