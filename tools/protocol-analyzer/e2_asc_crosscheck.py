#!/usr/bin/env python3
"""Cross-check the E2 capture payloads against the console's own show file (SHOW04.ASC).

Stdlib only, read-only, never talks to a device. Run from the repository root with
`py -3.13 -I` (see README.md). Both inputs are untrusted data.

The byte layouts tested here are HYPOTHESES derived from E2 (record
docs/protocol/captures/2026-10-28-e2-smartsoft-connect.md); every comparison prints its
agreement count so the data can contradict them. Nothing here may be used to build bytes
for a console.
"""
from __future__ import annotations

import argparse
import importlib.util
import math
import pathlib
import sys

_here = pathlib.Path(__file__).resolve().parent
_spec = importlib.util.spec_from_file_location("hsp_summary", _here / "hsp_summary.py")
hs = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(hs)

HEADS = ("CUE", "$CUE", "GROUP", "$SEQUENCE", "$INDEPENDENT", "$CHANNEL", "$PAGE")


def parse_time(tok: str) -> int:
    """ASC time token -> tenths of a second ('5.0' -> 50, '1:30' -> 900, '58' -> 580)."""
    if ":" in tok:
        m, s = tok.split(":")
        return (int(m) * 60 + int(s)) * 10
    return int(round(float(tok) * 10))


def parse_asc(path):
    raw = open(path, "rb").read()
    text = raw.decode("ascii")
    lines = text.split("\n")
    info = {"crlf": raw.count(b"\r\n"), "lf": raw.count(b"\n"), "records": [], "patch": {}, "set": [],
            "section": None}
    cur = None
    section = None
    for no, line in enumerate(lines, 1):
        line = line.rstrip("\r")
        if line.startswith("!"):
            section = line[1:].strip()
            continue
        tok = line.split(" ")
        key = tok[0]
        if key in HEADS:
            cur = {"kind": key, "args": [t for t in tok[1:] if t], "line": no, "section": section,
                   "chan": {}, "text": {}}
            info["records"].append(cur)
        elif key == "PATCH":
            for trip in tok[2:]:
                if trip:
                    d, c, lv = (int(x) for x in trip.split("/"))
                    info["patch"][d] = (c, lv)
        elif key == "SET":
            info["set"].append((tok[1], int(tok[2])))
        elif cur is not None and key == "CHAN":
            for pair in tok[1:]:
                if pair:
                    c, lv = pair.split("/")
                    cur["chan"][int(c)] = int(lv)
        elif cur is not None and key == "Text":
            cur["text"]["Text"] = line[5:]
        elif cur is not None and key == "$$Text":
            cur["text"][int(tok[1])] = line[len("$$Text ") + len(tok[1]) + 1:]
        elif cur is not None and key in ("$$MEMORY", "$$CUEPARAMS", "UP", "DOWN", "FOLLOWON"):
            cur[key] = [t for t in tok[1:] if t]
    return info


def decode_text(block: bytes):
    """[lines][width][lines*width u16 BE] -> list of strings cut at the first NUL."""
    n, w = block[0], block[1]
    units = [block[2 + 2 * i] << 8 | block[3 + 2 * i] for i in range(n * w)]
    out = []
    for k in range(n):
        s = units[k * w:(k + 1) * w]
        if 0 in s:
            s = s[:s.index(0)]
        out.append("".join(chr(u) for u in s))
    return out


def step_record(r: bytes):
    """12-byte step record hypothesis: [u16 cue*10][params][u16 memory index][x][u16 up][u16 down][u16 followon]."""
    return {"cue10": r[0] << 8 | r[1], "params": r[2], "mem": r[3] << 8 | r[4], "x": r[5],
            "up": r[6] << 8 | r[7], "down": r[8] << 8 | r[9], "follow": r[10] << 8 | r[11]}


def asc_step(rec):
    p, f = (int(x) for x in rec["$$MEMORY"]) if "$$MEMORY" in rec else (None, None)
    return {"cue10": parse_time(rec["args"][0]), "params": int(rec["$$CUEPARAMS"][0]),
            "mem": None if p is None else p * 24 + f, "up": parse_time(rec["UP"][0]),
            "down": parse_time(rec["DOWN"][0]), "follow": parse_time(rec["FOLLOWON"][0]),
            "updelay": rec["UP"][1], "downdelay": rec["DOWN"][1]}


def asc_texts(rec):
    return [rec["text"].get(k, "").rstrip(" ") for k in (1, 2, 3)]


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("jsonl")
    ap.add_argument("asc")
    args = ap.parse_args(argv)
    _, frames = hs.load(args.jsonl)
    asc = parse_asc(args.asc)
    w = sys.stdout.write
    P = {oid: list(hs.in_payloads(frames, oid)) for oid in (0, 2, 3, 4, 5, 8, 9, 12, 13, 14, 15, 41)}
    recs = asc["records"]
    w(f"ASC: {len(recs)} records, CRLF {asc['crlf']}, LF {asc['lf']}\n")

    # (1) Patch
    w("\n(1) Patch\n")
    for hf, pf, p in P[5]:
        b = p["body"]
        cnt = b[0] << 8 | b[1]
        ent = b[2:]
        ok_p = ok_u = bad = 0
        for d in range(1, 513):
            c, _lv = asc["patch"][d]
            if ent[d - 1] == c:
                if c:
                    ok_p += 1
                else:
                    ok_u += 1
            else:
                bad += 1
                w(f"  mismatch DMX {d}: wire {ent[d - 1]} asc {c}\n")
        levels = sorted({lv for _, lv in asc["patch"].values()})
        w(f"  f{pf['dframe']}: count {cnt}, entries {len(ent)}; patched match {ok_p}, unpatched (0) match {ok_u}, mismatch {bad}; ASC levels used {levels}\n")

    # (2) Memories
    w("\n(2) Memory objects vs GROUP\n")
    groups = {int(r["args"][0]): r for r in recs if r["kind"] == "GROUP"}
    fetched = set()
    lvl_ok = lvl_bad = 0
    pairs = []
    hdr_bytes = set()
    zeros_hi = 0
    for hf, pf, p in P[0]:
        hw = hs.words(hf["bytes"])
        a, bq = hw[3], hw[4]
        n = a * 24 + bq + 1
        fetched.add(n)
        b = p["body"]
        cnt = b[3]
        lv = b[4:4 + cnt]
        hdr_bytes.add((b[2], b[3]))
        g = groups.get(n)
        if g is None:
            w(f"  f{pf['dframe']} (a={a}, b={bq}) -> GROUP {n}: not in ASC\n")
            continue
        for c in range(1, 49):
            asc_pct = g["chan"].get(c, 0)
            raw = lv[c - 1]
            if (raw == 0) == (asc_pct == 0) and (raw == 0 or math.floor(raw * 100 / 255 + 0.5) == asc_pct):
                lvl_ok += 1
            else:
                lvl_bad += 1
                w(f"  level mismatch GROUP {n} ch {c}: wire {raw:02X} asc {asc_pct}\n")
            if raw:
                pairs.append((raw, asc_pct, pf["dframe"], n, c))
        zeros_hi += sum(1 for x in lv[48:] if x == 0)
        texts = decode_text(b[4 + cnt:])
        w(f"  f{pf['dframe']} (a={a}, b={bq}) -> GROUP {n}: body[0..3] {b[0]:02X} {b[1]:02X} {b[2]:02X} {b[3]:02X}; text {texts} vs ASC {asc_texts(g)}; text block ends at body end: {4 + cnt + 2 + 2 * b[4 + cnt] * b[5 + cnt] == len(b)}\n")
    w(f"  GROUPs in ASC {sorted(groups)}; fetched (a*24+b+1) {sorted(fetched)}; equal: {sorted(groups) == sorted(fetched)}\n")
    w(f"  channel 1..48 levels: agree {lvl_ok}, disagree {lvl_bad}; body[2..3] values {hdr_bytes}; zero bytes among levels 49..96: {zeros_hi}\n")
    tests = {
        "pct == round_half_up(raw*100/255)": lambda r, q: math.floor(r * 100 / 255 + 0.5) == q,
        "pct == floor(raw*100/255)": lambda r, q: math.floor(r * 100 / 255) == q,
        "raw == ceil(pct*255/100)": lambda r, q: math.ceil(q * 255 / 100) == r,
        "raw == round_half_up(pct*255/100)": lambda r, q: math.floor(q * 255 / 100 + 0.5) == r,
    }
    for name, fn in tests.items():
        okc = sum(1 for r, q, *_ in pairs if fn(r, q))
        w(f"  scale test {name}: {okc}/{len(pairs)}\n")
    w("  distinct (raw, pct) pairs: " + ", ".join(f"{r:02X}={q}%" for r, q in sorted({(r, q) for r, q, *_ in pairs})) + "\n")

    # (3) labels: Page, ChannelText, Independents handled below
    w("\n(3) Page / ChannelText labels\n")
    pages = {int(r["args"][0]): r for r in recs if r["kind"] == "$PAGE"}
    chans = {int(r["args"][0]): r for r in recs if r["kind"] == "$CHANNEL"}
    for oid, table, label in ((8, pages, "$PAGE"), (9, chans, "$CHANNEL")):
        okc = badc = 0
        fit = 0
        for hf, pf, p in P[oid]:
            a = hs.words(hf["bytes"])[3]
            b = p["body"]
            t = decode_text(b[1:])
            fit += (1 + 2 + 2 * b[1] * b[2] == len(b))
            if t == asc_texts(table[a + 1]):
                okc += 1
            else:
                badc += 1
                w(f"  {label} {a + 1} f{pf['dframe']}: wire {t} asc {asc_texts(table[a + 1])}\n")
        w(f"  {label}: {len(P[oid])} objects, ASC blocks {len(table)}, labels agree {okc}, disagree {badc}, text block = rest of body in {fit}\n")

    # (4) sequences
    w("\n(4) Sequences and steps\n")
    seqs = [r for r in recs if r["kind"] == "$SEQUENCE"]
    main_steps = [r for r in recs if r["kind"] == "CUE"]
    seq_steps = [r for r in recs if r["kind"] == "$CUE"]
    for hf, pf, p in P[3]:
        b = p["body"]
        w(f"  SequenceMain f{pf['dframe']}: body[0]={b[0]} (ASC CUE steps in main stack: {len(main_steps)}), body[1]={b[1]}, u16 BE body[2..3]={b[2] << 8 | b[3]}, body[4..5]={b[4] << 8 | b[5]}, body[6..7]={b[6] << 8 | b[7]}; ASC header {seqs[0]['args']}; text {decode_text(b[8:])}\n")
    for hf, pf, p in P[2]:
        b = p["body"]
        w(f"  Sequence f{pf['dframe']}: body[0..1]=({b[0]},{b[1]}) body[2]={b[2]} (ASC $CUE steps: {len(seq_steps)}), body[3]={b[3]}, body[4..5]={b[4] << 8 | b[5]}, body[6..7]={b[6] << 8 | b[7]}, body[8..9]={b[8] << 8 | b[9]}; ASC header {seqs[1]['args']}; text {decode_text(b[10:])} vs ASC {asc_texts(seqs[1])}\n")
        tstart = 10 + 2 + 2 * b[10] * b[11]
        rest = b[tstart:]
        w(f"    after text: {len(rest)} bytes = {len(rest) / 12:g} x 12\n")
        for i in range(len(rest) // 12):
            wr = step_record(rest[12 * i:12 * i + 12])
            ar = asc_step(seq_steps[i])
            diffs = [k for k in ("cue10", "params", "mem", "up", "down", "follow") if wr[k] != ar[k]]
            w(f"    step {i}: wire {wr} | ASC $CUE {seq_steps[i]['args'][0]} mem {ar['mem']} up {ar['up']} down {ar['down']} follow {ar['follow']} | differ: {diffs}\n")
    agree = {k: 0 for k in ("cue10", "params", "mem", "up", "down", "follow")}
    for i, (hf, pf, p) in enumerate(P[4]):
        a = hs.words(hf["bytes"])[3]
        wr = step_record(p["body"][1:13])
        ar = asc_step(main_steps[a])
        for k in agree:
            agree[k] += wr[k] == ar[k]
        if any(wr[k] != ar[k] for k in agree):
            w(f"  StepContent a={a} f{pf['dframe']}: wire {wr} asc {ar}\n")
    w(f"  StepContent ({len(P[4])}) vs main-stack CUE ({len(main_steps)}), fields agreeing: {agree}; byte 'x' values {sorted({p['body'][6] for _, _, p in P[4]})}; ASC delays {sorted({(asc_step(r)['updelay'], asc_step(r)['downdelay']) for r in main_steps})}\n")

    # (5) settings
    w("\n(5) SystemSettings vs SET\n")
    sv = [v for _, v in asc["set"]]
    for hf, pf, p in P[41]:
        b = list(p["body"])
        hits = [i for i in range(len(b) - len(sv) + 1) if b[i:i + len(sv)] == sv]
        w(f"  f{pf['dframe']} body {' '.join(f'{x:02X}' for x in b)}; SET in file order {asc['set']}; contiguous match of the SET values at body offsets {hits}\n")

    # (6) independents
    w("\n(6) Independents\n")
    inds = [r for r in recs if r["kind"] == "$INDEPENDENT"]
    for hf, pf, p in P[12]:
        b = p["body"]
        n = b[0]
        size = (len(b) - 1) // n
        for k in range(n):
            r = b[1 + k * size:1 + (k + 1) * size]
            w(f"  record {k + 1}: {' '.join(f'{x:02X}' for x in r[:4])} text {decode_text(r[4:])} | ASC $INDEPENDENT {inds[k]['args']} text {asc_texts(inds[k])}\n")
    for hf, pf, p in P[15]:
        b = p["body"]
        w(f"  ChannelLevels f{pf['dframe']} body[49], body[50] = {b[49]:02X} {b[50]:02X}\n")
        break

    # (7) live levels at session start
    w("\n(7) Live levels at session start\n")
    for oid, label, base in ((15, "ChannelLevels", 1), (13, "OutputLevels", 2), (14, "FaderInfo", 1)):
        hf, pf, p = P[oid][0]
        b = p["body"]
        nz = [(i, b[i]) for i in range(base, len(b)) if b[i]]
        w(f"  {label} f{pf['dframe']}: non-zero body bytes {[(i, f'{v:02X}') for i, v in nz]}\n")
    return 0


if __name__ == "__main__":
    sys.exit(main())
