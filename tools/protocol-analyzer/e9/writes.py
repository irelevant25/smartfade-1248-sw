"""E9: every PC->console data write (type-1 header + payload) with time, header words, payload, the
preceding action-log note, and the next list the console sent. Read-only.
Usage: py -3.13 -I writes.py <repo> <jsonl>"""
import os, sys, datetime
sys.stdout.reconfigure(encoding="utf-8")
repo, path = sys.argv[1], sys.argv[2]
sys.path.insert(0, os.path.join(repo, "tools", "protocol-analyzer"))
import hsp_summary as H
header, frames = H.load(path)
notes = H.load_notes(path)
events, errs = H.classify(frames)
t0 = datetime.datetime(2026, 10, 30, 22, 19, 59, 957156)
def local(t):
    return (t0 + datetime.timedelta(seconds=t)).strftime("%H:%M:%S.%f")[:-3]
def last_note(t):
    best = None
    for n in notes:
        if n["t"] <= t:
            best = n
    return best
pl = list(H.pairs(events))
for i, (kind, hf, pf) in enumerate(pl):
    if kind != "data":
        continue
    w = H.words(hf["bytes"]); b = pf["bytes"]
    p = H.decode_payload("out", b)
    n = last_note(hf["t"])
    # next list or object after this write
    nxt = []
    for k2, h2, p2 in pl[i+1:i+40]:
        if k2 == "reply" and p2 is not None:
            q = H.decode_payload("in", p2["bytes"])
            if q["id"] == 40:
                cnt, recs = H.dirty_records(bytes(q["body"]))
                nxt.append(f"list@seq{p2['seq']} t={p2['t']:.3f} [{','.join(str(r[0]) if r[1:]==(0,0,0) else f'{r[0]}:{r[1]}/{r[2]}/{r[3]}' for r in recs)}]")
                break
    print(f"seq{hf['seq']}/{pf['seq']} f{hf['dframe']}/f{pf['dframe']} t={hf['t']:.6f} ({local(hf['t'])}) w={w} pseq={p['seq']:3d} id={H.name(p['id'])}({p['id']}) len={p['len']} body={H.hexs(p['body'])} | note: {n['text'][:60] if n else None} @{n['t']:.3f} | {nxt[0] if nxt else ''}")
