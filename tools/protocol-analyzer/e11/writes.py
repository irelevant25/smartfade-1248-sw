"""E11: every Data write of the backend's transfer log with its placement and the console's answer. Read-only.
For each type-1 header: the poll before it and the reply header before it (ms from the reply's completion to the
header's submit: SC-2 MAJOR 1's E11 analysis, at most 62 ms for a write that is not a release), the payload, the next
poll after the payload, and every listed object (DirtyObjects records) and fetched payload in the following window."""
import json, sys

IDS = {3: "SequenceMain", 13: "OutputLevels", 14: "FaderInfo", 15: "ChannelLevels", 17: "PlaybackInfo", 20: "ConsoleEvent",
       21: "DisplayContent", 22: "LedStatus", 23: "ConsoleStatus", 40: "DirtyObjects"}
# SC-2 class B1 key codes (each = its LED block number - 1, H82; the names from E9 / E9b)
KEYS = {0x00: "bump 1 / memory bump 1", 0x06: "bump 7", 0x08: "bump 9", 0x31: "GO", 0x33: "SOLO", 0x34: "BLACKOUT",
        0x36: "FADERS 1-24", 0x39: "FADERS MEMS"}
path = sys.argv[1]
window = float(sys.argv[2]) if len(sys.argv) > 2 else 1.5
recs = [json.loads(l) for l in open(path, encoding="utf-8")]
frames = [r for r in recs if r.get("type") == "frame" and r.get("xfer") == "bulk"]
notes = [r for r in recs if r.get("type") == "note"]
print("notes:")
for n in notes:
    print(f"  t={n['t']:.3f} {n['text'][:160]}")


def u16(b, i):
    return b[i] | (b[i + 1] << 8)


def in_payload(b):
    # H18: [pseq][0][len16 lo][len16 hi][id] body
    return b[0], u16(b, 2), b[4], b[5:]


writes = [i for i, f in enumerate(frames) if f["dir"] == "out" and f["len"] == 12 and f["data"].startswith("01")]
print(f"bulk frames {len(frames)}, data headers {len(writes)}")
for i in writes:
    h = frames[i]
    hdr = bytes.fromhex(h["data"])
    payload = frames[i + 1]
    pb = bytes.fromhex(payload["data"])
    prev_in = next(frames[j] for j in range(i - 1, -1, -1) if frames[j]["dir"] == "in")
    prev_out = next(frames[j] for j in range(i - 1, -1, -1) if frames[j]["dir"] == "out")
    next_out = next(frames[j] for j in range(i + 2, len(frames)) if frames[j]["dir"] == "out")
    print(f"\n=== write seq {h['seq']} header submit t={h.get('tSubmit', h['t']):.6f} ({h.get('ts')}) hdr={hdr.hex(' ')}")
    if pb[3] != 20:
        # another Data payload (SmartSoft's System (39) pair, a whole-object write): shown, not decoded here
        print(f"  payload {pb.hex(' ')} : pseq={pb[0]} len16={u16(pb, 1)} id={pb[3]} ({IDS.get(pb[3], '?')}) body={pb[4:].hex(' ')}")
    elif pb[4] == 1 and len(pb) == 7:
        # kind 1, key event (E9, E9b; H82): [kind=1][code][state], 1 = press, 0 = release; 7-byte payload
        print(f"  payload {pb.hex(' ')} : pseq={pb[0]} len16={u16(pb, 1)} id={pb[3]} ({IDS.get(pb[3])}) body={pb[4:].hex(' ')}"
              f"  (kind=1 key, code=0x{pb[5]:02X} ({KEYS.get(pb[5], '?')}), {'press' if pb[6] == 1 else 'release' if pb[6] == 0 else pb[6]})")
    else:
        print(f"  payload {pb.hex(' ')} : pseq={pb[0]} len16={u16(pb, 1)} id={pb[3]} ({IDS.get(pb[3])}) body={pb[4:].hex(' ')}"
              f"  (kind={pb[4]}, index=0x{pb[5]:02X}, value=0x{pb[6]:02X}={pb[6]}, previous=0x{pb[7]:02X}={pb[7]})")
    print(f"  previous out: seq {prev_out['seq']} {prev_out['data'][:8]} submit t={prev_out.get('tSubmit'):.6f}"
          f"  -> header submit {(h.get('tSubmit') - prev_out.get('tSubmit')) * 1000:.2f} ms after the poll's submit")
    print(f"  previous in : seq {prev_in['seq']} {prev_in['data'][:24]} completed t={prev_in['t']:.6f}"
          f"  -> header submit {(h.get('tSubmit') - prev_in['t']) * 1000:.2f} ms after the reply's completion"
          f"  (reply submit->complete {(prev_in['t'] - prev_in.get('tSubmit')) * 1000:.2f} ms)")
    print(f"  header complete t={h['t']:.6f} ({(h['t'] - h.get('tSubmit')) * 1000:.2f} ms); payload submit t={payload.get('tSubmit'):.6f}"
          f" complete t={payload['t']:.6f} ({(payload['t'] - payload.get('tSubmit')) * 1000:.2f} ms)")
    print(f"  next out    : seq {next_out['seq']} {next_out['data'][:8]} submit t={next_out.get('tSubmit'):.6f}"
          f"  -> {(next_out.get('tSubmit') - payload['t']) * 1000:.2f} ms after the payload's completion")
    # the console's answer in the window after the payload
    t0 = payload["t"]
    j = i + 2
    polls = fetches = idle = 0
    while j < len(frames) and frames[j]["t"] <= t0 + window:
        f = frames[j]
        b = bytes.fromhex(f["data"])
        if f["dir"] == "out":
            if b[0] == 0:
                polls += 1
            elif b[0] == 2:
                fetches += 1
                print(f"    fetch  t={f.get('tSubmit'):.6f} ({(f.get('tSubmit') - t0) * 1000:7.1f} ms) {f['data']}")
            elif b[0] == 1:
                break
        else:
            if f["len"] == 12:
                if b[0] == 4 and b[1] == 0 and b[2] == 0:
                    idle += 1
                else:
                    print(f"    reply  t={f['t']:.6f} ({(f['t'] - t0) * 1000:7.1f} ms) type={b[0]} {f['data']}")
            else:
                pseq, ln, oid, body = in_payload(b)
                if oid == 40:
                    # DirtyObjects body (F56 / F57): [0][count] then count records of 5 bytes [id][a][b][c][d] from body[2]
                    n = body[1]
                    recsx = [(IDS.get(body[2 + 5 * k], body[2 + 5 * k]), body[3 + 5 * k], body[4 + 5 * k], body[5 + 5 * k]) for k in range(n)]
                    print(f"    LIST   t={f['t']:.6f} ({(f['t'] - t0) * 1000:7.1f} ms) pseq={pseq} count={n} {recsx}")
                else:
                    print(f"    OBJECT t={f['t']:.6f} ({(f['t'] - t0) * 1000:7.1f} ms) pseq={pseq} {IDS.get(oid, oid)}({oid}) len={f['len']} body[0:8]={body[:8].hex(' ')}"
                          + (f"  channel1={body[0]}" if oid in (15,) else "") + (f"  output1={body[0]}" if oid == 13 else ""))
        j += 1
    print(f"  window {window} s after the payload: polls {polls}, idle replies {idle}, fetches {fetches}")
