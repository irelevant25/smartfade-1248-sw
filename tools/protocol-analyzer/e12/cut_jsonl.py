"""Write the header line and the frame records with t_from <= t < t_to of a normalized capture JSONL to a NEW file, so
that a tool written for one session (e2_asc_crosscheck.py) can run on one download of a capture with several. The
output is a derived scratch file: write it outside the repository (never into docs/protocol/captures/, whose files are
hashed evidence); an existing output file is refused. Read-only on the input, stdlib only, never talks to a device.
Usage (from the repository root): py -3.13 -I -B tools/protocol-analyzer/e12/cut_jsonl.py <in.jsonl> <out.jsonl> <t_from> <t_to>
"""
import json
import os
import sys

src, dst, t_from, t_to = sys.argv[1], sys.argv[2], float(sys.argv[3]), float(sys.argv[4])
norm = os.path.normcase(os.path.abspath(dst))
if os.path.join("docs", "protocol", "captures") in norm or os.path.exists(dst):
    sys.exit("refused: the output must be a new file outside docs/protocol/captures/")
count = 0
with open(src, encoding="utf-8") as fin, open(dst, "x", encoding="utf-8", newline="\n") as fout:
    for line in fin:
        rec = json.loads(line)
        if rec["type"] == "header" or (rec["type"] == "frame" and t_from <= rec["t"] < t_to):
            fout.write(line)
            count += rec["type"] == "frame"
print("frame records written:", count)
