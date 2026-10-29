#!/usr/bin/env python3
"""Normalize a USBPcap capture (classic pcap, link type 249) into smartfade-capture JSONL.

Stdlib only. Parses the pcap container and the USBPcap pseudo-header itself (no tshark).
Read-only: it never talks to any device. The capture is untrusted input: run with
`py -3.13 -I` from the repository root (see README.md), never from the capture directory.

One output `frame` record per USB transfer (URB): the submission frame (host -> driver,
carries OUT data and the control SETUP packet) and the completion frame (driver -> host,
carries IN data, final status) are merged by IRP ID. The zero-length completion frames of
OUT transfers and the zero-length submission frames of IN transfers are therefore not
emitted separately; their pcap frame numbers and timestamps are kept in `frames` and
`tSubmit`. Records are ordered by completion frame number (by submission frame number for
a transfer whose completion is missing from the capture).

Only frames of the device address given with --device-address are read into records;
frames of other devices are counted and skipped without their data being kept.

Operator notes: `--note T TEXT` (capture-relative seconds) and, since version 2,
`--notes-log FILE --notes-utc-offset +HH:MM`, which turns every line
`YYYY-MM-DD HH:MM:SS.fff text` of an action log (tools/usb-probe/action-log.ps1,
e3-guided-session.ps1) into a `note` record. The log is written in the PC's local time; the
offset converts it to UTC (the pcap timestamps are UTC). It is an argument, never taken from
the machine running the normalizer, so the output does not depend on where it runs.

Output format: docs/protocol/reverse-engineering.md, section "Capture format".
"""
from __future__ import annotations

import argparse
import calendar
import datetime as _dt
import hashlib
import json
import re
import struct
import sys

NORMALIZER_NAME = "usbpcap_to_jsonl.py"
# Version 2 (2026-10-29): adds --notes-log / --notes-utc-offset. Without them the output is
# identical to version 1 except this number in the header (checked on the E2 capture).
NORMALIZER_VERSION = 2

LOG_LINE = re.compile(r"^(\d{4})-(\d{2})-(\d{2}) (\d{2}):(\d{2}):(\d{2})\.(\d{3}) (.*)$")
UTC_OFFSET = re.compile(r"^([+-])(\d{2}):(\d{2})$")

# Sources for the constants below (not console protocol, just container formats):
# - LINKTYPE_USBPCAP = 249: tcpdump.org link-layer header types registry.
# - USBPcap pseudo-header layout, transfer-type codes and the INFO PDO->FDO flag:
#   USBPcap capture format description, desowin.org/usbpcap/captureformat.html.
# - Descriptor type codes (1 device, 2 configuration, 3 string): USB 2.0 specification ch. 9.
LINKTYPE_USBPCAP = 249
XFER_NAMES = {0: "isochronous", 1: "interrupt", 2: "control", 3: "bulk"}
USBPCAP_INFO_PDO_TO_FDO = 0x01  # set on completion frames
CONTROL_STAGE_SETUP = 0


class CaptureError(Exception):
    pass


def read_pcap(raw: bytes):
    """Yield (frame_no, ts_sec, ts_frac, frac_digits, packet_bytes) from a classic pcap."""
    if len(raw) < 24:
        raise CaptureError("file shorter than a pcap global header")
    magic = raw[:4]
    if magic == b"\xd4\xc3\xb2\xa1":
        endian, digits = "<", 6
    elif magic == b"\xa1\xb2\xc3\xd4":
        endian, digits = ">", 6
    elif magic == b"\x4d\x3c\xb2\xa1":
        endian, digits = "<", 9
    elif magic == b"\xa1\xb2\x3c\x4d":
        endian, digits = ">", 9
    elif magic == b"\x0a\x0d\x0d\x0a":
        raise CaptureError("pcapng is not supported; convert with: tshark -r in.pcapng -F pcap -w out.pcap")
    else:
        raise CaptureError(f"unknown pcap magic {magic.hex()}")
    _vmaj, _vmin, _tz, _sig, _snap, linktype = struct.unpack(endian + "HHiIII", raw[4:24])
    if linktype != LINKTYPE_USBPCAP:
        raise CaptureError(f"link type {linktype}, expected {LINKTYPE_USBPCAP} (USBPcap)")
    off, n = 24, 0
    while off < len(raw):
        if off + 16 > len(raw):
            raise CaptureError(f"truncated record header at file offset {off}")
        ts_sec, ts_frac, incl, _orig = struct.unpack(endian + "IIII", raw[off:off + 16])
        off += 16
        if off + incl > len(raw):
            raise CaptureError(f"truncated record data at file offset {off}")
        n += 1
        yield n, ts_sec, ts_frac, digits, raw[off:off + incl]
        off += incl


def parse_usbpcap(pkt: bytes) -> dict:
    """USBPCAP_BUFFER_PACKET_HEADER (packed, little-endian) + optional control stage byte."""
    if len(pkt) < 27:
        raise CaptureError("packet shorter than the USBPcap header")
    (hlen, irp, status, function, info, bus, device, endpoint, transfer,
     dlen) = struct.unpack("<HQIHBHHBBI", pkt[:27])
    if hlen < 27 or hlen > len(pkt):
        raise CaptureError(f"bad USBPcap header length {hlen}")
    stage = pkt[27] if transfer == 2 and hlen >= 28 else None
    data = pkt[hlen:]
    return {
        "irp": irp, "status": status, "function": function,
        "completion": bool(info & USBPCAP_INFO_PDO_TO_FDO), "bus": bus, "device": device,
        "endpoint": endpoint, "transfer": transfer, "stage": stage, "data": data, "dlen": dlen,
    }


def ts_to_us(ts_sec: int, ts_frac: int, digits: int) -> int:
    return ts_sec * 1_000_000 + (ts_frac if digits == 6 else ts_frac // 1000)


def iso_utc(us: int) -> str:
    d = _dt.datetime.fromtimestamp(us // 1_000_000, _dt.timezone.utc)
    return d.strftime("%Y-%m-%dT%H:%M:%S") + f".{us % 1_000_000:06d}Z"


def endpoint_interfaces(config: bytes) -> dict:
    """Map endpoint address -> interface number from a configuration descriptor."""
    result, iface, i = {}, None, 0
    while i + 2 <= len(config):
        blen, btype = config[i], config[i + 1]
        if blen < 2 or i + blen > len(config):
            break
        if btype == 0x04 and blen >= 3:
            iface = config[i + 2]
        elif btype == 0x05 and blen >= 3 and iface is not None:
            result[config[i + 2]] = iface
        i += blen
    return result


def build_records(raw: bytes, address: int, bus: int | None):
    pending: dict[int, dict] = {}
    transfers: list[dict] = []
    stats = {"pcapFrames": 0, "deviceFrames": 0, "otherFrames": 0}
    first_us = None
    for frame_no, ts_sec, ts_frac, digits, pkt in read_pcap(raw):
        stats["pcapFrames"] += 1
        us = ts_to_us(ts_sec, ts_frac, digits)
        if first_us is None:
            first_us = us
        hdr = parse_usbpcap(pkt)
        if hdr["device"] != address or (bus is not None and hdr["bus"] != bus):
            stats["otherFrames"] += 1
            continue  # other devices: counted only, data discarded
        if len(hdr["data"]) != hdr["dlen"]:
            raise CaptureError(f"frame {frame_no}: USBPcap dataLength {hdr['dlen']} != captured payload {len(hdr['data'])}")
        stats["deviceFrames"] += 1
        hdr["frame"], hdr["us"] = frame_no, us
        if not hdr["completion"]:
            old = pending.pop(hdr["irp"], None)
            if old is not None:
                transfers.append({"sub": old, "cmp": None})
            pending[hdr["irp"]] = hdr
        else:
            sub = pending.pop(hdr["irp"], None)
            if sub is not None and sub["endpoint"] != hdr["endpoint"]:
                transfers.append({"sub": sub, "cmp": None})
                sub = None
            transfers.append({"sub": sub, "cmp": hdr})
    for sub in pending.values():
        transfers.append({"sub": sub, "cmp": None})
    transfers.sort(key=lambda x: (x["cmp"] or x["sub"])["frame"])
    return transfers, stats, first_us


def make_frame_record(seq: int, tr: dict, first_us: int, ep_iface: dict) -> dict:
    sub, cmp_ = tr["sub"], tr["cmp"]
    ref = cmp_ or sub
    ep = ref["endpoint"]
    xfer = XFER_NAMES.get(ref["transfer"], f"type{ref['transfer']}")
    setup = None
    if xfer == "control":
        setup_bytes = b""
        out_data = b""
        if sub is not None and sub["stage"] == CONTROL_STAGE_SETUP:
            setup_bytes, out_data = sub["data"][:8], sub["data"][8:]
        setup = setup_bytes.hex().upper() if setup_bytes else None
        is_in = bool(setup_bytes[0] & 0x80) if setup_bytes else bool(ep & 0x80)
        data = (cmp_["data"] if cmp_ is not None else b"") if is_in else out_data
    else:
        is_in = bool(ep & 0x80)
        if is_in:
            data = cmp_["data"] if cmp_ is not None else b""
        else:
            data = sub["data"] if sub is not None else b""
    rec = {
        "type": "frame",
        "seq": seq,
        "t": round((ref["us"] - first_us) / 1e6, 6),
        "ts": iso_utc(ref["us"]),
        "dir": "in" if is_in else "out",
        "iface": None if (ep & 0x0F) == 0 else ep_iface.get(ep),
        "ep": f"0x{ep:02X}",
        "xfer": xfer,
        "len": len(data),
        "data": data.hex().upper(),
    }
    if setup is not None or xfer == "control":
        rec["setup"] = setup
    rec["status"] = f"0x{cmp_['status']:08X}" if cmp_ is not None else None
    rec["tSubmit"] = round((sub["us"] - first_us) / 1e6, 6) if sub is not None else None
    rec["frames"] = [sub["frame"] if sub is not None else None,
                     cmp_["frame"] if cmp_ is not None else None]
    if ref["irp"] == 0:
        rec["synthetic"] = True  # USBPcapCMD --inject-descriptors (IRP ID 0, not on the wire)
    return rec


def read_notes_log(path: str, utc_offset: str, first_us: int):
    """Action-log lines -> note records with capture-relative t. Strict: a line that is not
    'YYYY-MM-DD HH:MM:SS.fff text' aborts instead of being dropped silently."""
    m = UTC_OFFSET.match(utc_offset or "")
    if not m:
        raise CaptureError("--notes-utc-offset must look like +01:00")
    off_min = (int(m.group(2)) * 60 + int(m.group(3))) * (1 if m.group(1) == "+" else -1)
    with open(path, "rb") as fh:
        raw = fh.read()
    text = raw.decode("utf-8")
    if text.startswith("﻿"):
        text = text[1:]
    name = path.replace("\\", "/").rsplit("/", 1)[-1]
    notes = []
    for lineno, line in enumerate(text.splitlines(), 1):
        if not line.strip():
            continue
        lm = LOG_LINE.match(line)
        if not lm:
            raise CaptureError(f"{name}:{lineno}: not a 'YYYY-MM-DD HH:MM:SS.fff text' line")
        y, mo, d, h, mi, s, ms = (int(x) for x in lm.groups()[:7])
        epoch_s = calendar.timegm((y, mo, d, h, mi, s, 0, 0, 0)) - off_min * 60
        us = epoch_s * 1_000_000 + ms * 1000
        notes.append({"type": "note", "t": round((us - first_us) / 1e6, 6), "text": lm.group(8),
                      "source": name, "logTime": line[:23]})
    meta = {"file": name, "sha256": hashlib.sha256(raw).hexdigest().upper(), "utcOffset": utc_offset,
            "notes": len(notes)}
    return notes, meta


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("pcap", help="USBPcap capture (classic pcap, link type 249)")
    ap.add_argument("-o", "--output", required=True, help="output .jsonl path")
    ap.add_argument("--device-address", type=int, required=True, help="USB device address to keep")
    ap.add_argument("--bus", type=int, help="USBPcap bus (root hub) number to keep")
    ap.add_argument("--vid", required=True, help="expected idVendor, hex (checked against an injected device descriptor if present)")
    ap.add_argument("--pid", required=True, help="expected idProduct, hex")
    ap.add_argument("--console-model", required=True)
    ap.add_argument("--firmware", required=True, help="console firmware as read in Menu > Diagnostics")
    ap.add_argument("--host-app", required=True)
    ap.add_argument("--host-version", required=True)
    ap.add_argument("--host-os", required=True)
    ap.add_argument("--host-driver", required=True)
    ap.add_argument("--note", nargs=2, action="append", default=[], metavar=("T", "TEXT"),
                    help="operator note at capture-relative time T seconds (repeatable)")
    ap.add_argument("--notes-log", help="action log with 'YYYY-MM-DD HH:MM:SS.fff text' lines (PC local time)")
    ap.add_argument("--notes-utc-offset", help="UTC offset of the action log's clock, e.g. +01:00 (required with --notes-log)")
    args = ap.parse_args(argv)
    if args.notes_log and not args.notes_utc_offset:
        ap.error("--notes-log needs --notes-utc-offset")

    with open(args.pcap, "rb") as fh:
        raw = fh.read()
    sha = hashlib.sha256(raw).hexdigest().upper()
    transfers, stats, first_us = build_records(raw, args.device_address, args.bus)
    if first_us is None:
        raise CaptureError("capture contains no frames")

    # Endpoint -> interface map and VID/PID check from injected/observed descriptors.
    ep_iface, seen_vid_pid = {}, None
    for tr in transfers:
        c = tr["cmp"]
        if c is None or c["endpoint"] != 0x80:
            continue
        d = c["data"]
        if len(d) >= 18 and d[0] == 18 and d[1] == 0x01 and seen_vid_pid is None:
            seen_vid_pid = (f"{d[9]:02X}{d[8]:02X}", f"{d[11]:02X}{d[10]:02X}")
        if len(d) >= 9 and d[1] == 0x02 and not ep_iface:
            ep_iface = endpoint_interfaces(d)
    want = (args.vid.upper().zfill(4), args.pid.upper().zfill(4))
    if seen_vid_pid is not None and seen_vid_pid != want:
        raise CaptureError(f"device descriptor VID/PID {seen_vid_pid} != expected {want}")

    header = {
        "type": "header",
        "format": "smartfade-capture",
        "version": 1,
        "transport": "usb",
        "source": "usbpcap",
        "sourceFile": args.pcap.replace("\\", "/").rsplit("/", 1)[-1],
        "sourceSha256": sha,
        "sourceFrames": stats["pcapFrames"],
        "captureStart": iso_utc(first_us),
        "console": {"model": args.console_model, "firmware": args.firmware},
        "host": {"app": args.host_app, "version": args.host_version, "os": args.host_os,
                 "driver": args.host_driver},
        "device": {"vid": want[0], "pid": want[1], "bus": args.bus, "address": args.device_address,
                   "descriptorChecked": seen_vid_pid is not None},
        "normalizer": {"name": NORMALIZER_NAME, "version": NORMALIZER_VERSION},
        "deviceFrames": stats["deviceFrames"],
        "otherDeviceFramesSkipped": stats["otherFrames"],
        "transfers": len(transfers),
    }
    frame_recs = [make_frame_record(i + 1, tr, first_us, ep_iface) for i, tr in enumerate(transfers)]
    notes = [{"type": "note", "t": round(float(t), 6), "text": text} for t, text in args.note]
    if args.notes_log:
        log_notes, meta = read_notes_log(args.notes_log, args.notes_utc_offset, first_us)
        notes.extend(log_notes)
        header["notesLog"] = meta
    # Merge notes by time; a note sorts before frames with the same t.
    merged, ni = [], 0
    notes.sort(key=lambda r: r["t"])
    for fr in frame_recs:
        while ni < len(notes) and notes[ni]["t"] <= fr["t"]:
            merged.append(notes[ni]); ni += 1
        merged.append(fr)
    merged.extend(notes[ni:])

    with open(args.output, "w", encoding="utf-8", newline="\n") as out:
        for rec in [header] + merged:
            out.write(json.dumps(rec, ensure_ascii=False, separators=(",", ":")) + "\n")
    print(f"{args.output}: {len(frame_recs)} transfers from {stats['deviceFrames']} device frames "
          f"({stats['otherFrames']} other-device frames skipped), {len(notes)} notes, source SHA-256 {sha}",
          file=sys.stderr)
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except CaptureError as exc:
        print(f"error: {exc}", file=sys.stderr)
        sys.exit(2)
