"""E12: the large DirtyObjects lists (the downloads after SmartSoft's System 07 00 00) of one capture compared object by
object (presence and body), the SHA-256 of each large list's body and the records a list has that the first has not;
then every body of the show objects (ids 0, 2, 3, 4, 5, 8, 12, 39) that the console delivered outside the downloads,
with the Patch shown as its non-identity entries. Read-only, stdlib only, never talks to a device.
Usage (from the repository root): py -3.13 -I -B tools/protocol-analyzer/e12/downloads.py <repo> <jsonl>
"""
import hashlib
import os
import sys

sys.stdout.reconfigure(encoding="utf-8")
repo, path = sys.argv[1], sys.argv[2]
sys.path.insert(0, os.path.join(repo, "tools", "protocol-analyzer"))
import hsp_summary as H  # noqa: E402

header, frames = H.load(path)
events, _ = H.classify(frames)
downloads, outside, pending, last_list = [], [], [], None
for kind, hf, pf in H.pairs(events):
    if kind != "reply" or pf is None:
        continue
    q = H.decode_payload("in", pf["bytes"])
    if q["id"] == 40:
        cnt, recs = H.dirty_records(bytes(q["body"]))
        last_list = pf["seq"]
        if cnt > 100:
            downloads.append({"seq": pf["seq"], "frame": pf["dframe"], "t": pf["t"], "records": recs,
                              "sha": hashlib.sha256(bytes(q["body"])).hexdigest(), "objs": {}})
            pending = list(recs)
        continue
    key = H.object_key(hf, q)
    body = bytes(q["body"])
    if pending and key == pending[0]:
        pending.pop(0)
        downloads[-1]["objs"][key] = (body, pf["seq"], pf["dframe"])
    else:
        outside.append((key, body, pf["seq"], pf["dframe"], pf["t"], last_list))

for i, d in enumerate(downloads, 1):
    extra = [r for r in d["records"] if r not in downloads[0]["records"]]
    missing = [r for r in downloads[0]["records"] if r not in d["records"]]
    print(f"download {i}: list seq{d['seq']} f{d['frame']} t={d['t']:.3f}, {len(d['records'])} records, {len(d['objs'])} objects "
          f"fetched, body sha256 {d['sha'][:16]}; vs download 1: extra {[(H.name(r[0]),) + r for r in extra]} "
          f"at positions {[d['records'].index(r) for r in extra]}, missing {missing}")

keys = []
for d in downloads:
    keys += [k for k in d["objs"] if k not in keys]
same = [0] * len(downloads)
for k in keys:
    bodies = [d["objs"].get(k) for d in downloads]
    tag = f"{H.name(k[0])}({k[0]}) {k[1]}/{k[2]}/{k[3]}"
    if any(b is None for b in bodies):
        print(f"  present in {[i + 1 for i, b in enumerate(bodies) if b is not None]} only: {tag} "
              f"{[f'seq{b[1]} f{b[2]} {len(b[0])} bytes' for b in bodies if b is not None]}")
        continue
    for i, b in enumerate(bodies):
        same[i] += b[0] == bodies[0][0]
    if any(b[0] != bodies[0][0] for b in bodies):
        parts = []
        for i, b in enumerate(bodies[1:], 2):
            dpos = [j for j in range(max(len(b[0]), len(bodies[0][0]))) if j >= len(b[0]) or j >= len(bodies[0][0]) or b[0][j] != bodies[0][0][j]]
            parts.append(f"download {i} (seq{b[1]} f{b[2]}, {len(b[0])} bytes) {'= download 1' if not dpos else f'differs at {dpos[:24]}'}")
        print(f"  differs: {tag}: download 1 seq{bodies[0][1]} f{bodies[0][2]}; " + "; ".join(parts))
print("objects identical to download 1 (shared keys):", same)

print("\nshow-object bodies outside the downloads (ids 0, 2, 3, 4, 5, 8, 12, 39):")
for key, body, seq, frame, t, lseq in outside:
    if key[0] not in (0, 2, 3, 4, 5, 8, 12, 39):
        continue
    tag = f"seq{seq} f{frame} t={t:.3f} (list seq{lseq}) {H.name(key[0])}({key[0]}) {key[1]}/{key[2]}/{key[3]} [{len(body)}]"
    if key[0] == 5:
        ent = {d: body[1 + d] for d in range(1, 513) if body[1 + d] and not (d <= 46 and body[1 + d] == d)}
        print(f"  {tag}: non-identity entries (DMX: channel) {ent}")
    else:
        print(f"  {tag}: {H.hexs(body)}")
