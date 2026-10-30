"""E9: list records vs fetches (order, count), list-size histogram, ids listed. Read-only."""
import os, sys, collections
sys.stdout.reconfigure(encoding="utf-8")
repo, path = sys.argv[1], sys.argv[2]
sys.path.insert(0, os.path.join(repo, "tools", "protocol-analyzer"))
import hsp_summary as H
header, frames = H.load(path)
events, errs = H.classify(frames)
recs, fetches, sizes, ids = [], [], collections.Counter(), collections.Counter()
for kind, hf, pf in H.pairs(events):
    if kind == "reply" and pf is not None:
        p = H.decode_payload("in", pf["bytes"])
        if p["id"] == 40:
            cnt, r = H.dirty_records(bytes(p["body"]))
            recs.extend(r); sizes[cnt] += 1
            for x in r: ids[x[0]] += 1
    if kind == "fetch":
        w = H.words(hf["bytes"]); fetches.append((w[2], w[3], w[4], w[5]))
print("list records", len(recs), "fetches", len(fetches), "equal in order:", recs == fetches)
print("list sizes", dict(sorted(sizes.items())))
print("ids listed", {H.name(k) + f"({k})": v for k, v in sorted(ids.items())})
print("c == 0 in all records:", all(r[3] == 0 for r in recs))
