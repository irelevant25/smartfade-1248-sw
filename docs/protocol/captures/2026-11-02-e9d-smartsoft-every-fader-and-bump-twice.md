# 2026-11-02 E9d: every fader and bump of every bank from SmartSoft's Console Simulator, twice (USBPcap, passive, guided script)

- **Date:** 2026-11-02, 09:47–10:14 local time (CET, UTC+1). USBPcap ran 09:48:49.631–10:13:45.793 (the raw root-hub capture's
  first and last frames); the committed console-only split runs from 09:48:49.631 (f1) to 10:13:40.823 (f237880, t = 1491.192). The
  guided script `tools/usb-probe/e9d-guided-session.ps1` (1a7136c) ran 09:47:35.737–10:14:18.340, started SmartSoft twice (09:49:35.710,
  10:02:19.574) and logged every step; SmartSoft was closed at 10:02:03–10:02:08 and 10:13:38–10:13:42.
- **Experiment:** **E9d**, the passive repeat that SC-5 names ([../safety-checks.md](../safety-checks.md) "E9d"; the runbook in
  [../reverse-engineering.md](../reverse-engineering.md)): in each of two SmartSoft sessions every channel fader 1–24 of bank 1-24 (A),
  FADERS 25-48 with every fader labelled 25–48 and the bump labelled 25 (B), FADERS MEMS with every fader 1–24 as a memory fader of
  page 1 and every memory bump 1–24 (C), bump 7 held about 5 s and E9c's S4a mode-switch case (D). SmartSoft Online, its Console
  Simulator driven by the owner; **our own software was not involved and sent nothing;** every write is SmartSoft's, made as for any
  user. **E9d clears nothing by itself:** it is the second (and, with E9c, the third) session for what SC-5 left "after E9d"; the
  decisions are in the SC-5 addendum, never in this record.
- **Console:** ETC SmartFade 1248, USB device address **46** on bus 1 (38 in E11 and E9c: the console re-enumerated after its power
  cycle; the owner: switched off since E9c, 09:48:09). Firmware: ConsoleVersion "3.0.2.9.0.43" in both sessions. The inbound counter
  started at 0 (f20, F80) and the list pending at the first poll was the power-on pair [LedStatus, DisplayContent] (seq 10, H65 case (a)).
- **Host:** the owner's PC, Windows 11 Pro build 26300; Memory Integrity **off**; interface 1 on ETC's driver, service **ETCHSP** (both
  read by the script); SmartSoft 3.0.43, started by the script. USBD status: 0 non-zero over 118 940 transfers.
- **Connection:** USB A-B cable straight into the PC; USBPcapCMD on root hub `USBPcap1` with `-A --inject-descriptors` into
  `e9d-20261102-0948.pcap`. Other devices in the raw file (not committed): a hub, the owner's keyboard, an isochronous device, a bulk
  device at address 44.
- **Show backup (safety rule 3): taken.** "backup taken: SHOW26.ASC" (09:48:04), before USBPcap and SmartSoft; after the session
  `SHOW27.ASC` (10:14:18). Both copied into the git-ignored `smartfade-sd-card-backup/`: **byte-identical** (15 944 bytes, SHA-256
  `559636399c1d82e0f95e737c961bfee31f88962fe8c486bbc6eba7f7d2177a90`, the same bytes as every save since `SHOW23.ASC`): **no show data
  changed.**
- **Starting position** (script; owner 09:49:04): channel mode 1-24, every channel fader at zero, MASTER and BUMPS at full, BLACKOUT,
  SOLO, IND 1 / 2, STACK and NEXT off, page 1, nothing armed. Page 1's recorded memories: **1, 2, 3, 4, 5, 6, 7, 8, 13, 21** (owner,
  09:49:33). The rig: one twelve-channel fixture at DMX 5–16 (owner, 09:48:35).
- **Action log:** `2026-11-02-e9d-actions.txt` (464 lines, CRLF), PC clock, local time; stamp semantics as in E9c. **Owner statements:**
  `2026-11-02-e9d-owner-statements.txt`.

**Conventions:** as in the E9c record (`seqN`, `fN`, `t` = seconds since the split's first frame, 09:48:49.631 local, the action log's
stamps merged as notes; LED block k = body[1 + 6(k − 1) .. 6k]; FaderInfo record N = body[1 + 4(N − 1) .. 4N]; ChannelLevels[c] =
body[c]; OutputLevels DMX d = body[1 + d]). Sessions: **S1** (seq 6 … 57 701, 46.2–795.9 s), **S2** (seq 57 702 … 118 940, 810.0–1491.2 s).

## Evidence files

| File | SHA-256 | Notes |
|---|---|---|
| `2026-11-02-e9d-smartsoft-every-fader-and-bump-twice.pcap` | `50826555ef0717edb4dd501421b72c48c8ee60cfd83ed6412392a4a9eea22afa` | console-only split of device address 46, 237 880 frames, 13 226 137 bytes |
| `2026-11-02-e9d-smartsoft-every-fader-and-bump-twice.jsonl` | `7b669fe43a22d3091d51bfaffc30f1adaebeb71fbbbe3b60fc79613439471db4` | the split normalized (`usbpcap_to_jsonl.py` v2) with the action log merged as notes: 118 940 transfers (57 033 bulk OUT, 61 901 bulk IN, 6 control); no multi-URB reads (ETC's driver) |
| `2026-11-02-e9d-actions.txt` | `739b3d7c5d7ded34adf9915a67abf221e002f8edfebfed0a40620e6aeb656104` | the guided script's log |
| `2026-11-02-e9d-owner-statements.txt` | (hash in `README.md`) | the owner's answers with readings |
| not committed | `c5b1960340eec75237a2d5e936e166c770dfab520d78fd4b73019fbd942b41b7` | the raw root-hub capture `e9d-20261102-0948.pcap`, 961 109 753 bytes, 1 113 806 frames |
| `smartfade-sd-card-backup/SHOW26.ASC`, `SHOW27.ASC` | `559636399c1d82e0f95e737c961bfee31f88962fe8c486bbc6eba7f7d2177a90` both | git-ignored; byte-identical |

Tools (read-only): `hsp_summary.py`, `write_rules.py`, `join_reads.py`, `e9c/steps.py`, and **`e9d/check.py`**, which checks every step of
the two passes against the expected index or code and the expected objects (228 steps).

## Session health

| Check | Result |
|---|---|
| Framing | 0 errors over 118 940 transfers; H16 echoes 13 765 of 13 765; H17 the inbound counter 0 … 22 with wraps and no break; one H15 remark: the first poll of S2 (seq 57 702, t = 810.028) carried zero stale words while the last header of S1 named PlaybackInfo (a fresh SmartSoft process has no reply to echo; F52 holds within a session) |
| Writes | **2 855 Data writes: 4 System (the pair, twice) and 2 851 ConsoleEvents** (2 787 fader events, 64 key pairs); the header's w2..w5 the most recent reply's 2 855 of 2 855 (F52); the counter from 0x00 in each session (F131); no write between a press and its release. **Three fader events carry a previous byte one or three counts off the last wire value of their index** (seq 21 367 index 0x04: 0x1D after 0x1A; seq 78 433 index 0x07: 0x0F after 0x0D; seq 81 351 index 0x0E: 0x55 after 0x52): SmartSoft's own coalescing, as E9's one exception (F132: the console applied them like the rest) |
| Placement (`write_rules.py`) | After an idle reply 224, after a list 734, after a fetched object 1 897 (SmartSoft's placement, as E9c); poll submit → header 29.6–47.8 ms; the last inbound completion → header 22.2–43.4 ms; payload → next poll or fetch 13.0–16.7 ms; header to header ≥ 44.9 ms (median 62.3); press → release 46 ms … 4.67 s |
| Cadence | 37 558 polls (idle median 31.6 ms), 13 765 fetches, 10 578 lists |
| The connect phases | S1: the power-on pair pending (seq 10), System `09 00 00` (t = 47.461, pseq 0), ConsoleVersion listed, `07 00 00` (48.725, pseq 1), the **158-record download** (seq 178; the same ids as E9c, F177); S2: nothing pending, the pair (811.585, 813.132), 158 records again (seq 57 979). F58's thirteenth and fourteenth sessions **Note 2026-11-02, evening (SC-4 groundwork; the text above is kept):** S2's "nothing pending" is **contradicted**: SmartSoft's first poll (seq 57 702, t = 810.028) was answered at once by a list [PlaybackInfo] (seq 57 704, pseq 211), and PlaybackInfo lists and objects followed at nearly every reply until the pair (a sequence was playing, F184); it holds at most for the show and state objects (F177 and H65 are annotated alike). The pair's payloads are seq 92 / 175 (S1) and 57 840 / 57 976 (S2), and these are SmartSoft's seventeenth and eighteenth sessions with the pair, not the "thirteenth and fourteenth" (F58, F181) |

## What the two passes showed (`e9d/check.py`: 228 steps, every index and code as expected)

| Part | Steps per pass | Index or code sent | The console's answer (both passes) | Reading |
|---|---|---|---|---|
| **A** channel faders 1–24, bank 1-24 | 24 | **index N − 1 for every N** (0x00–0x17; 87–124 events per index over the capture) | FaderInfo record N byte 0, ChannelLevels[N], OutputLevels DMX N, LED block N, the LCD cell, every time (48 of 48 steps OK) | F171's channel-mode clause in a second and third session: **VERIFIED** proposed for all 24 indices |
| **B** FADERS 25-48 (0x37), faders labelled 25–48, the bump labelled 25, FADERS 1-24 (0x36) | 1 + 24 + 1 + 1 | **index N − 25 for the fader labelled N** (the same 0x00–0x17); the bump labelled 25 = **code 0x00** held 1.19–2.65 s | ConsoleStatus body[4] 0 → 1 then back; the fader labelled N moved FaderInfo record N − 24, **ChannelLevels[N] and DMX N** (N = 25 … 48; **channel 47 moved no DMX address: unpatched in this show**, both passes), LED block N − 24; the bump labelled 25 put **channel 25 and DMX 25 at 0xFF** from the press to the release (48 of 48 fader steps as expected, the DMX exception explained; the bump 2 of 2) | the index names the physical fader in bank 25-48 for all 24, twice; the bump too |
| **C** FADERS MEMS (0x39), faders 1–24 as memory faders of page 1, memory bumps 1–24, FADERS 1-24 | 1 + 24 + 24 + 1 | **index N − 1** for memory fader N; **code N − 1** for memory bump N, held 0.80–1.61 s | memory fader N moved FaderInfo record N **bytes 0 and 1** and LED block N; the recorded memories (1–8, 13, 21) moved their channels and DMX, the fourteen empty ones **only their record** (48 of 48 as expected); memory bump N: record N at 0xFF while held, block N, the recorded ones' channels (48 of 48) | the index and the code name the physical fader and bump in memory mode for all 24, twice |
| **D** bump 7 held, the S4a case | 1 + 6 | bump 7 (0x06) held **4.67 s** (pass 1) and **3.42 s** (pass 2); the drags of index 0x00 around FADERS MEMS / 1-24 | channel 7 at 0xFF from the press to the release; the S4a case **byte for byte as E9c**: channel 1 at 0x8A (pass 1) / 0x97 (pass 2) from the PC; at FADERS MEMS FaderInfo record 1 → 0x00 and block 1 `46 00 00 / 0A 00 00` (blinking; owner: "blinking, and no light from memory 1"); the drag in memory mode moved nothing but ended the blink at zero (block 1 phase B 0A → 46); at FADERS 1-24 record 1 → 0x8A / 0x97 again (owner: "half as before"); the cleanup drag picked the level up and brought it to zero | F172's mode case in a second and third session; a hold of 4.67 s shown and ended cleanly |

One owner slip: in S2 the fader labelled 40 moved together with the fader labelled 41 (indices 0x0F and 0x10 in one step); both were
followed correctly. No other deviation.

## Reproduction and promotions (proposals for the curator; nothing is cleared by this record)

- **F171 → VERIFIED** (as SmartSoft's and the console's behaviour, never clearance): the index names the physical fader for all 24
  indices in bank 1-24 (E9c + E9d S1 + E9d S2), for all 24 in bank 25-48 (E9d S1 + S2; 0x00 and 0x01 also E9c) and for all 24 memory
  faders of the shown page (E9d S1 + S2; 0x00, 0x01, 0x02, 0x08 also E9c), with the recorded / empty distinction (F112).
- **F172 → VERIFIED in part** (the mode-switch case S4a: E9c + E9d S1 + S2); the physical-level case (S4b) and the page-change case (S4c)
  stay OBSERVED once.
- **F174 → VERIFIED** for every memory bump 1–24 (held about 1 s, two sessions), for bump 7 (E9, E9b, E9c, E9d; held up to 4.67 s) and for
  the bump of bank 25-48's first fader (code 0x00 → channel 25, two sessions); the channel bumps 2–6, 8, 9, 11–24 of bank 1-24 and 26–48
  of bank 25-48 are still unobserved from a PC (E9e).
- **H111 → CONFIRMED** (the index addresses the physical fader; the console's bank decides what it drives); **H79 / H81's matching clause
  → CONFIRMED** for the mode case (three sessions); **H65** supported (the power-on pair after the power cycle); **F58, F89 / F177**
  (the pair and the 158-record download) in two more sessions; **F131–F134** reproduced in a fifth capture; F132's coalescing
  exception seen three more times.

## Input for the SC-5 addendum

Every "after E9d" row of SC-5 now has its second session: the 21 channel-fader indices of bank 1-24 seen once in E9c; all 24 indices of
bank 25-48; all 24 memory-fader indices of the shown page; the memory bumps 1–24 as holds; the bump of bank 25-48's first fader. Not
covered (my omission in the E9d script, not the owner's): the channel bumps 2–6, 8, 9, 11–24 of bank 1-24 and the bumps 26–48 of bank
25-48 as holds; a page other than page 1 for the memory faders (E9c had page 2 for memory 1 only). The hold bound of 3 s is below the
longest holds observed (4.67 s and 3.42 s, bump 7). Channel 47 is unpatched in this show: a fader event for it moves the channel and the
LED and no DMX output, which the UI shows from the console's objects as for any channel.
