# tools/protocol-analyzer

Offline tools for USB captures of the SmartFade 1248. Python 3.10+, **standard library only**.
They read files and print text; none of them opens a device, and nobody adds such a feature
here. Captures are untrusted input: run the scripts from the repository root with
`py -3.13 -I` (isolated mode), never from inside the capture directory.

| Script | Input | Output |
|---|---|---|
| `usbpcap_to_jsonl.py` | USBPcap capture, classic pcap, link type 249; optionally an operator action log | Normalized capture JSONL (format: `docs/protocol/reverse-engineering.md`, section "Capture format"); the action log becomes `note` records (version 2) |
| `hsp_summary.py` | Normalized JSONL | Structural checks and statistics (`summary`), one decoded line per bulk transfer (`log`), all payloads of one object ID (`payloads --id N`), body bytes that change between successive payloads (`diff --id N`), a per-object change timeline interleaved with the notes (`timeline`), the body positions that ever changed (`census`), the LCD object as two text lines (`lcd`), the first body of every object compared with another capture (`startdiff --other X`), and a per-SmartSoft-session view for captures with several sessions (`sessions`: span, first replies and lists, position of each data write, inbound sequence continuity, repetition of the large list) |
| `write_rules.py` | Normalized JSONL with SmartSoft's Data writes (E9, E9b) | Per write: the stale-word check of its header (F52), the counter per SmartSoft session (F131), the previous-byte rule with its exceptions (F132), where it sat (after an idle reply, a list or an object; fetch state; writes since the poll; records still listed) and what followed, writes between a press and its release, and the timing figures behind `UsbDeviceOptions.Floors` for writes (SC-2 condition 7) |
| `e2_asc_crosscheck.py` | E2 JSONL + the console's show file `SHOW04.ASC` | Field-by-field comparison of the fetched objects (Patch, Memory, labels, sequences and steps, SystemSettings, Independents, live levels) with the show file; prints agreement counts. Written for E2; runs unchanged on the E3 JSONL (same objects) |
| `e12/writes.py` | Repository root + a normalized JSONL with SmartSoft data writes (written for E12) | Every PC → console data write (type-1 header + payload) with its seq / frame numbers, capture-relative and local time (`--utc-offset`, default +01:00), header words, a decoded summary of the body, the action-log note before it, and the console's lists and objects after it until the next write (at most 20 s); a decoding aid, never a way to build bytes |
| `e12/cmp_writes.py` | Repository root + JSONL (E12) | Each object write (ids 0, 2, 4, 5, 8, 12) compared with the previous write of the object, with the console's last body before it and its next body after it (SmartSoft's 48-level Memory and type-2 StepContent bodies expanded to the console's 96-level layout for the comparison, INFERRED from the two layouts), and the DirtyObjects lists between the write and the next write (at most 15 s) |
| `e12/downloads.py` | Repository root + JSONL (E12) | The large DirtyObjects lists (the downloads after SmartSoft's System `07 00 00`) compared object by object (presence and body), the SHA-256 of each list's body and the records one list has that the first has not; then every show-object body (ids 0, 2, 3, 4, 5, 8, 12, 39) the console delivered outside the downloads, the Patch as its non-identity entries |
| `e12/lists.py` | Repository root + JSONL, optional t range | The DirtyObjects lists (records as id or id:a/b/c), the PC → console data writes and the action-log notes, merged in time order; the downloads shown by their count only |
| `e12/write_timing.py` | Repository root + JSONL, optional threshold in ms (default 21.0) | Per data write: header → payload submit delay, the payload's submit → completion time, the previous poll or fetch and inbound transfer, the next OUT transfer and its gap; then SmartSoft's idle poll-to-poll intervals below the threshold, grouped in time (complements `write_rules.py`) |
| `e12/cut_jsonl.py` | A JSONL, a new output path, t_from, t_to | Writes the header and the frame records with t_from ≤ t < t_to to a **new** file outside `docs/protocol/captures/` (an existing output or a path inside the captures folder is refused), so that a one-session tool such as `e2_asc_crosscheck.py` can run on one download of a multi-session capture |
| `e12b/write_rules.py` | One or more normalized JSONL files with SmartSoft's data writes (written for E12b) | As `write_rules.py` (the stale words, the counter per SmartSoft session, the previous-byte rule, where each write sat, what followed it and the timing figures), changed in one place: a reply-header read shorter than 12 bytes (E12b seq 85008, a cancelled 0-byte read) is counted and reported instead of stopping the tool; its output for the E12 JSONL equals the E12 record's figures |
| `e12b/cmp_writes.py` | Repository root + JSONL (E12b) | As `e12/cmp_writes.py`, changed in one place: a Sequence write is matched with the console's Sequence bodies of the same page and slot under any qualifier c (E12b's console listed c = 2 and 7) |
| `e12b/playback.py` | Repository root + JSONL + t_from, t_to | A merged, time-ordered view of one window: the data writes (first 16 body bytes), the DirtyObjects lists, PlaybackInfo records 5 and 1 when they change, LED blocks 50, 51, 58, 59, 60, 69 and 70 when one changes, every ConsoleStatus and SequenceMain body, and the LCD when it changes; a decoding aid, not a protocol definition |
| `e12b/reply_latency.py` | Repository root + JSONL, optional threshold in ms (default 12) | Every reply-header read slower than the threshold or failed, their distribution (median, p99, p99.9, max) and the poll-to-poll intervals above 100 ms (E12b: the read cancelled during the console's SD save) |
| `e12b/cmp_bodies.py` | Repository root + JSONL + seq pairs `A:B` and single seqs | Chosen payloads compared by JSONL seq (SmartSoft's 48-level bodies expanded to the console's 96-level layout, INFERRED from the two layouts), the differing body positions, and the body of every single seq given |
| `e11/writes.py` | A backend transfer log or a normalized JSONL, optional window in s (default 1.5) | Every Data write of our software with its neighbours: the poll and the idle reply before it (ms from the reply's completion to the header's submit: SC-2 MAJOR 1's E11 check, at most 62 ms), the payload decoded as a ConsoleEvent (kind 0: index, value, previous; kind 1: key code with its SC-2 name, press / release), the next poll, and the console's answer in the window (lists with their records, fetches, objects with their first body bytes; `hsp_summary.py timeline` stays the reference for the objects' changes). Written for E11 attempt 2; key events and the DirtyObjects layout (`[0][count]` then 5-byte records) added for attempt 3 (2026-11-01). Read-only |
| `e11/bus_vs_log.py` | The normalized USBPcap JSONL of the console-only split, then the backend's transfer log(s) in session order | Direction and bytes of every bulk transfer compared in order, after joining the bus's multi-URB reads (a read longer than 64 bytes is several URBs, F122); reports the joins, the differing transfers and the log's poll / idle-reply / data-header counts. Written for E11 attempt 2 (attempt 1 used the same comparison without the join; both sessions: 0 differing). Read-only |
| `join_reads.py` | A normalized USBPcap JSONL (the bus), an output path | A copy of the bus file in which each multi-URB read (an IN record whose length is a non-zero multiple of 64 followed by the record with the rest, F122) is one frame with the joined data, the rule of `e11/bus_vs_log.py`, so that the tools written for whole transfers (`e11/writes.py`, `hsp_summary.py timeline`, `e9c/steps.py`) read the bus like a backend log. Written for E9c (2026-11-02). Read-only on its input |
| `e9c/steps.py` | A normalized JSONL with the action log merged as notes, multi-URB reads joined (`--all-notes` to include the wait notes) | For every step of the guided session (each note until the next): the PC → console writes compacted by control (fader index with its first, last, min and max values and count; key code with press / release and the hold time; other Data writes by id), then the lists the console sent and, for the live and show objects it delivered, the byte positions that changed against the previous delivery (the first eight), so that a fader index can be tied to the FaderInfo record, channel, DMX address and LED block it moved in every bank and mode. Written for E9c (2026-11-02). Read-only |
| `e9d/check.py` | The normalized E9d JSONL with the action log merged as notes (reads joined), `--recorded <memory numbers>` for page 1's recorded memories | Checks every fader and bump step of E9d's two passes against the physical-fader reading: the index or code sent (fader N → N − 1; the fader labelled N in bank 25-48 → N − 25; memory fader or bump N → N − 1) and the objects that moved (the FaderInfo record, ChannelLevels, the DMX address, the LED block; in memory mode the record's bytes 0 and 1 and, for a recorded memory, some channel); prints a verdict per step and the list of mismatches (E9d: channel 47 unpatched, one double drag); the E9e channel-bump steps ("a sim bump N") added the same day. Written for E9d (2026-11-02). Read-only |

The `e12/` scripts (added with the E12 analysis of 2026-10-31; hashes in the [E12 record](../../docs/protocol/captures/2026-10-31-e12-smartsoft-patch-stack-edits.md)) import `hsp_summary.py` from this folder, read files only and never open a device; run them from the repository root with `py -3.13 -I -B` (the record lists the exact commands).

The `e12b/` scripts (added with the E12b analysis of 2026-10-31, late evening; hashes in the [E12b record](../../docs/protocol/captures/2026-10-31-e12b-smartsoft-patch-stack-edits-repeat.md)) import `hsp_summary.py` from this folder (`e12b/write_rules.py` stands alone, like its original), read files only and never open a device; run them from the repository root with `py -3.13 -I -B`. **`hsp_summary.py timeline` needs `-X utf8`** when an action-log note holds a character outside the Windows console's code page, such as the "Č" of E12b's D5 label (`py -3.13 -I -X utf8 -B tools/protocol-analyzer/hsp_summary.py <jsonl> timeline …`); without it Python stops with a UnicodeEncodeError (checked on the E12b JSONL; the E12b analysis ran `startdiff` with it too; the `e12b/` scripts set UTF-8 output themselves).

## Reproduce the E2 normalization

Single line, works in PowerShell and Git Bash, from the repository root:

```
py -3.13 -I tools/protocol-analyzer/usbpcap_to_jsonl.py docs/protocol/captures/2026-10-28-e2-smartsoft-connect.pcap -o %TEMP%\e2-reproduced.jsonl --device-address 10 --bus 1 --vid 14D5 --pid 0200 --console-model SF1248 --firmware 3.0.2.9.0.43 --host-app SmartSoft --host-version 3.0.2.9.0.43b --host-os "Windows 11 Pro build 26300 (Memory Integrity off)" --host-driver "etchsp.sys 2.00.000" --note 0 "owner: USBPcapCMD started at 20:16:37 local time on root hub USBPcap1; SmartSoft was launched later, time not recorded" --note 280.5 "owner: USBPcapCMD stopped by Stop-Process at 20:21:18 local time; the raw all-devices file ends at 280.540118 s and holds no console frames after 125.252552 s"
# Then compare: sha256sum %TEMP%\e2-reproduced.jsonl against the hash in the E2 record (normalizer v2 differs from the committed v1 file only in the header version). Never write into docs/protocol/captures/: the committed JSONL files are hashed evidence.
```

Expected: source SHA-256 `8F350496438DE66E4AEA70B6BE5206ADBBFEC4D8A61F8C3782FA6A122580CB82`,
6483 transfers from 12966 frames, output SHA-256
`808834C94D42CEFB9B3F930F267C4D8714A4FCADE757B73A0CE5A12502AA4E8B` (normalizer version 1;
two runs gave identical bytes). **Normalizer version 2** (2026-10-29) run with the same
command writes `"version":2` in the header's `normalizer` field and is otherwise byte-identical
to the committed version-1 file (checked: every line after the header is identical, and the
header differs only in that number), so the committed E2 file stays as it is.

Then, for the numbers quoted in the E2 record:

```
py -3.13 -I tools/protocol-analyzer/hsp_summary.py docs/protocol/captures/2026-10-28-e2-smartsoft-connect.jsonl summary
py -3.13 -I tools/protocol-analyzer/hsp_summary.py docs/protocol/captures/2026-10-28-e2-smartsoft-connect.jsonl log
py -3.13 -I tools/protocol-analyzer/hsp_summary.py docs/protocol/captures/2026-10-28-e2-smartsoft-connect.jsonl diff --id 14
py -3.13 -I -B tools/protocol-analyzer/e2_asc_crosscheck.py docs/protocol/captures/2026-10-28-e2-smartsoft-connect.jsonl smartfade-sd-card-backup/SHOW04.ASC
```

(`-B` keeps Python from writing a `__pycache__` folder next to `hsp_summary.py`, which the
cross-check imports by path.)

## Reproduce the E3 normalization

Single line, from the repository root. The action log is in the PC's local time, which was
UTC+1 on 2026-10-29 (CET); the pcap's first frame, 19:40:21.625362Z, is 20:40:21.625362 on
that clock. The three `--note` lines are facts from the coordinator that the log does not
contain (capture start, the time SmartSoft was started, capture stop).

```
py -3.13 -I tools/protocol-analyzer/usbpcap_to_jsonl.py docs/protocol/captures/2026-10-29-e3-console-actions.pcap -o %TEMP%\e3-reproduced.jsonl --device-address 7 --bus 1 --vid 14D5 --pid 0200 --console-model SF1248 --firmware 3.0.2.9.0.43 --host-app SmartSoft --host-version 3.0.2.9.0.43b --host-os "Windows 11 Pro build 26300 (Memory Integrity off)" --host-driver "etchsp.sys 2.00.000" --notes-log docs/protocol/captures/2026-10-29-e3-console-actions.log --notes-utc-offset +01:00 --note 0 "coordinator: USBPcapCMD started at 20:40:21 local time on root hub USBPcap1 (elevated); frame 1 is at 20:40:21.625362" --note 5.750638 "coordinator: SmartSoft 3.0.43 started by Claude via Start-Process at 20:40:27.376 local time" --note 371.4 "coordinator: USBPcapCMD stopped by Stop-Process at 20:46:33 local time (second resolution); the last console frame is at 313.336854 s, the raw all-devices file ends at 372.253081 s"
# Then compare: sha256sum %TEMP%\e3-reproduced.jsonl against the hash in the E3 record. Never write into docs/protocol/captures/: the committed JSONL files are hashed evidence.
```

Expected: source SHA-256 `0DCC3E1DCA277F97F471D6DDF12F5A39AADF7898A126471606A2579888058340`,
20326 transfers from 40652 frames, 30 notes (27 from the log, SHA-256
`EF10EBC2F3949A37B3F76EC881D43EE2C2C3B0FF37A61CEE2A79AB69A2B1645B`), output SHA-256
`3B2B6861C25CE7B90BDF4212E20445F7B53EF8DE1A44E308F75B2C43E9AE59CF` (normalizer version 2; two
runs gave identical bytes).

Then, for the numbers quoted in the E3 record:

```
py -3.13 -I -B tools/protocol-analyzer/hsp_summary.py docs/protocol/captures/2026-10-29-e3-console-actions.jsonl summary
py -3.13 -I -B tools/protocol-analyzer/hsp_summary.py docs/protocol/captures/2026-10-29-e3-console-actions.jsonl timeline
py -3.13 -I -B tools/protocol-analyzer/hsp_summary.py docs/protocol/captures/2026-10-29-e3-console-actions.jsonl census
py -3.13 -I -B tools/protocol-analyzer/hsp_summary.py docs/protocol/captures/2026-10-29-e3-console-actions.jsonl lcd
py -3.13 -I -B tools/protocol-analyzer/hsp_summary.py docs/protocol/captures/2026-10-29-e3-console-actions.jsonl startdiff --other docs/protocol/captures/2026-10-28-e2-smartsoft-connect.jsonl
py -3.13 -I -B tools/protocol-analyzer/e2_asc_crosscheck.py docs/protocol/captures/2026-10-29-e3-console-actions.jsonl smartfade-sd-card-backup/SHOW04.ASC
```

`timeline` accepts `--id N` (repeatable) and `--from` / `--to` (seconds) to narrow the output.

## Reproduce the E3b normalization

Single line, from the repository root. The action log (written by
`tools/usb-probe/e3b-guided-session.ps1`, which also started SmartSoft three times) is in the
PC's local time, UTC+1 on 2026-10-30; the pcap's first frame, 07:54:52.222585Z, is
08:54:52.222585 on that clock. The two `--note` lines are the coordinator's capture start and
stop.

```
py -3.13 -I tools/protocol-analyzer/usbpcap_to_jsonl.py docs/protocol/captures/2026-10-30-e3b-live-objects.pcap -o %TEMP%\e3b-reproduced.jsonl --device-address 7 --bus 1 --vid 14D5 --pid 0200 --console-model SF1248 --firmware 3.0.2.9.0.43 --host-app SmartSoft --host-version 3.0.2.9.0.43b --host-os "Windows 11 Pro build 26300 (Memory Integrity off)" --host-driver "etchsp.sys 2.00.000" --notes-log docs/protocol/captures/2026-10-30-e3b-live-objects.log --notes-utc-offset +01:00 --note 0 "coordinator: USBPcapCMD started at 08:54:52 local time on root hub USBPcap1 (elevated); frame 1 is at 08:54:52.222585" --note 1107.8 "coordinator: USBPcapCMD stopped by Stop-Process at about 09:13:20 local time; the last console frame is at 1008.974036 s (09:11:41.20), the raw all-devices file ends at 1122.644005 s"
# Then compare: sha256sum %TEMP%\e3b-reproduced.jsonl against the hash in the E3b record. Never write into docs/protocol/captures/: the committed JSONL files are hashed evidence.
```

Expected: source SHA-256 `E470719DBCF2B723EEB1A8E5CE4177B8A6BB0689D62A56C5652ACCDBD4E1B6A2`,
53935 transfers from 107870 frames, 88 notes (86 from the log, SHA-256
`E69ECA64C6FF32633754E1147E49FA4054E4EF4D63E15BAE80483A8A223097A1`), output SHA-256
`0EA97056712F1656026AE42831E766B0B1F53662F5C2A07879C5CF0D5D944594` (normalizer version 2; two
runs gave identical bytes).

Then, for the numbers quoted in the E3b record:

```
py -3.13 -I -B tools/protocol-analyzer/hsp_summary.py docs/protocol/captures/2026-10-30-e3b-live-objects.jsonl summary
py -3.13 -I -B tools/protocol-analyzer/hsp_summary.py docs/protocol/captures/2026-10-30-e3b-live-objects.jsonl sessions
py -3.13 -I -B tools/protocol-analyzer/hsp_summary.py docs/protocol/captures/2026-10-30-e3b-live-objects.jsonl timeline
py -3.13 -I -B tools/protocol-analyzer/hsp_summary.py docs/protocol/captures/2026-10-30-e3b-live-objects.jsonl census
py -3.13 -I -B tools/protocol-analyzer/hsp_summary.py docs/protocol/captures/2026-10-30-e3b-live-objects.jsonl lcd
py -3.13 -I -B tools/protocol-analyzer/hsp_summary.py docs/protocol/captures/2026-10-30-e3b-live-objects.jsonl payloads --id 17
py -3.13 -I -B tools/protocol-analyzer/hsp_summary.py docs/protocol/captures/2026-10-29-e3-console-actions.jsonl startdiff --other docs/protocol/captures/2026-10-30-e3b-live-objects.jsonl
py -3.13 -I -B tools/protocol-analyzer/e2_asc_crosscheck.py docs/protocol/captures/2026-10-30-e3b-live-objects.jsonl smartfade-sd-card-backup/SHOW05.ASC
```

## How `usbpcap_to_jsonl.py` works

- Parses the pcap container and the USBPcap pseudo-header (`USBPCAP_BUFFER_PACKET_HEADER`,
  27 bytes, plus the stage byte for control transfers) itself; tshark is not needed. pcapng
  is refused with a hint to convert it (`tshark -r in.pcapng -F pcap -w out.pcap`).
- Keeps only frames of `--device-address` (and `--bus` if given). Frames of other devices are
  counted in the header (`otherDeviceFramesSkipped`) and their data is discarded, so the
  tool can also be pointed at a raw all-devices capture without copying anyone's keystrokes.
- **One `frame` record per USB transfer (URB).** USBPcap writes two frames per URB: the
  submission (carries OUT data and the control SETUP packet) and the completion (carries IN
  data and the final USBD status). They are merged by IRP ID. The zero-length frames (OUT
  completions, IN submissions) are therefore not emitted as records of their own; their
  frame numbers and times survive in `frames` and `tSubmit`.
- Record order: by completion frame number (submission frame for a URB whose completion is
  missing). A `note` sorts before frames with the same `t`.
- `t` = completion time in seconds since the first frame of the file (µs precision);
  `tSubmit` = submission time; `ts` = completion time as UTC ISO 8601.
- `frames` = `[submission frame, completion frame]`, pcap frame numbers (1-based), so every
  record points back to the evidence. The data-carrying frame is `frames[0]` for OUT and
  `frames[1]` for IN.
- `iface` comes from the configuration descriptor found in the capture (USBPcapCMD
  `--inject-descriptors` or a real GET_DESCRIPTOR); `null` for endpoint 0.
- `synthetic: true` marks frames with IRP ID 0, which USBPcapCMD `--inject-descriptors`
  writes at capture start. They are copies of descriptors, not bus traffic of the session.
- `--vid/--pid` are checked against the first device descriptor in the capture; a mismatch
  aborts.
- Notes: `--note T TEXT` adds one note at capture-relative time T. Since version 2,
  `--notes-log FILE --notes-utc-offset +HH:MM` adds one note per line of an action log
  (`YYYY-MM-DD HH:MM:SS.fff text`, as written by `tools/usb-probe/action-log.ps1` and
  `e3-guided-session.ps1`). The offset converts the log's local clock to UTC; it is an argument
  and never read from the machine that runs the normalizer. Such a note carries
  `"source"` (the log's file name) and `"logTime"` (the log's own time stamp, verbatim); the
  header gets `"notesLog": {"file", "sha256", "utcOffset", "notes"}`. A line in another format
  aborts the run instead of being dropped.
- Hex is uppercase, keys are in a fixed order, numbers are rounded to 6 decimals, the file
  is written with LF line endings: the same input and arguments give the same bytes.

Keys added to the format proposal in `reverse-engineering.md` (to be adopted there by the
knowledge-base curator): header `sourceFrames`, `captureStart`, `host.driver`,
`device.bus/address/descriptorChecked`, `normalizer`, `deviceFrames`,
`otherDeviceFramesSkipped`, `transfers`; frame `setup` (control only), `status`, `tSubmit`,
`frames`, `synthetic`. Version 2 adds header `notesLog` and note `source`, `logTime` (only
when `--notes-log` is used).

## How `hsp_summary.py` decodes

Field names follow hypotheses H15–H20 and `docs/protocol/smartsoft-analysis.md` S11
(12-byte LE header words w0..w5; payload `[seq][u16 LE len][id][body]` outbound and
`[seq][byte1][u16 LE len][id][body]` inbound; DirtyObjects records `[id][u16 BE a][b][c]`;
object names from the sfEngine.dll table). Every check prints counts of agreements and
disagreements so a capture can contradict the hypothesis. The decode is an analysis aid,
not a protocol definition: nothing here may be used to build bytes for a console.

- An object instance is identified by `(id, a, b, c)`: the type-3 header echo of the fetch, or
  `(40, 0, 0, 0)` for a DirtyObjects list. `timeline`, `census` and `startdiff` compare bodies
  per instance.
- `lcd` prints bytes 0x80–0x87 as `<0>`..`<7>`. These are placeholders: ConsoleLink describes
  them as bar glyphs on the SmartFade ML (`docs/protocol/consolelink-analysis.md`); for the 1248
  that reading is a hypothesis.
- In `log` and `payloads`, `pseq=` is the payload's own sequence byte, `seqN` / `fN` are the
  JSONL record and pcap frame numbers.
