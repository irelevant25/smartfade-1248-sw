"""Compare the largest DirtyObjects list body of two JSONL captures. Read-only."""
import os, sys
repo, a, b = sys.argv[1], sys.argv[2], sys.argv[3]
sys.path.insert(0, os.path.join(repo, "tools", "protocol-analyzer"))
import hsp_summary as H
def lists(path):
    _, frames = H.load(path)
    ev, _ = H.classify(frames)
    out = []
    for kind, hf, pf in H.pairs(ev):
        if kind == "reply" and pf is not None:
            p = H.decode_payload("in", pf["bytes"])
            if p["id"] == 40:
                out.append((pf["seq"], pf["dframe"], bytes(p["body"])))
    return out
la, lb = lists(a), lists(b)
ma = [x for x in la if len(x[2]) > 700]
mb = [x for x in lb if len(x[2]) > 700]
print("A big lists", [(s, f, len(x)) for s, f, x in ma])
print("B big lists", [(s, f, len(x)) for s, f, x in mb])
print("identical to B's first big list:", [x == mb[0][2] for _, _, x in ma])
print("A lists total", len(la), "sizes", sorted(set((len(x) - 2) // 5 for _, _, x in la)))
