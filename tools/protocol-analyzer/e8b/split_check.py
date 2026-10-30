"""E8b: compare the raw-derived and the split-derived usbpcap JSONL (frame records identical except `frames`),
build the raw -> split frame map, check it against an independent list of the raw file's address-10 frame
numbers (one per line, in order), and print the map for given raw frame numbers. Read-only.
Usage: py -3.13 -I split_check.py <raw.usbpcap.jsonl> <split.usbpcap.jsonl> <raw_addr10_frames.txt> <out_map.tsv> [raw frame ...]"""
import json, sys
def frames(p):
    hdr, fr, notes = None, [], []
    for line in open(p, encoding="utf-8"):
        r = json.loads(line)
        if r["type"] == "header": hdr = r
        elif r["type"] == "frame": fr.append(r)
        else: notes.append(r)
    return hdr, fr, notes
ha, a, na = frames(sys.argv[1]); hb, b, nb = frames(sys.argv[2])
print("records", len(a), len(b))
diff = 0; m = {}
for x, y in zip(a, b):
    kx = {k: v for k, v in x.items() if k != "frames"}; ky = {k: v for k, v in y.items() if k != "frames"}
    if kx != ky: diff += 1
    for fx, fy in zip(x["frames"], y["frames"]):
        if (fx is None) != (fy is None): diff += 1
        elif fx is not None: m[fx] = fy
print("records differing outside `frames`:", diff)
print("header keys differing:", sorted(k for k in set(ha) | set(hb) if ha.get(k) != hb.get(k)))
print("notes differing:", sum(1 for x, y in zip(na, nb) if x != y), "of", len(na), len(nb))
raw_list = [int(l) for l in open(sys.argv[3]) if l.strip()]
print("raw address-10 frames (tshark):", len(raw_list), "mapped frames:", len(m))
ok = all(m.get(f) == k for k, f in enumerate(raw_list, 1))
print("k-th address-10 frame of the raw file == frame k of the split, for all k:", ok)
with open(sys.argv[4], "w") as out:
    for f in raw_list: out.write(f"{f}\t{m[f]}\n")
for s in sys.argv[5:]:
    print(f"raw f{s} -> split f{m.get(int(s))}")
