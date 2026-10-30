"""Run tools/protocol-analyzer/hsp_summary.py unchanged on a backend transfer log that has no
pcap frame numbers: the loader's dframe (None for such files) is replaced by the JSONL seq, so
every 'fNNN' printed by the tool means JSONL seq NNN for this file. Read-only; stdlib only.
Usage: py -3.13 -I hsp_seq_wrapper.py <repo-root> <capture.jsonl> <mode> [tool args...]"""
import importlib.util, sys, os

root, jsonl, mode, *rest = sys.argv[1:]
spec = importlib.util.spec_from_file_location(
    "hsp_summary", os.path.join(root, "tools", "protocol-analyzer", "hsp_summary.py"))
hs = importlib.util.module_from_spec(spec)
spec.loader.exec_module(hs)
_orig_load = hs.load

def load_seq(path):
    header, frames = _orig_load(path)
    for f in frames:
        if f["dframe"] is None:
            f["dframe"] = f["seq"]
    return header, frames

hs.load = load_seq
sys.exit(hs.main([jsonl, mode, *rest]))
