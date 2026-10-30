"""Compare the frame records of two normalized JSONL files field by field, ignoring the pcap frame numbers. Read-only."""
import json, sys
def recs(p):
    out = []
    with open(p, encoding="utf-8") as fh:
        for line in fh:
            r = json.loads(line)
            if r["type"] == "frame":
                r.pop("frames", None)
                out.append(r)
    return out
a, b = recs(sys.argv[1]), recs(sys.argv[2])
diff = sum(1 for x, y in zip(a, b) if x != y)
print("records", len(a), len(b), "differing (frames ignored)", diff + abs(len(a) - len(b)))
