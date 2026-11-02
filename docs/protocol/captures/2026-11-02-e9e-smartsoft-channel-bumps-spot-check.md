# 2026-11-02 E9e: four channel bumps per session from SmartSoft's Console Simulator, the spot check of SC-5 addendum 2 (USBPcap, passive, guided script)

- **Date:** 2026-11-02, 10:57–11:06 local time (CET, UTC+1). USBPcap ran 10:58:33.496–11:05:39.907 (the raw capture's first and last
  frames); the committed console-only split runs from 10:58:33.496 (f1) to 11:05:35 (f70890). The guided script
  `tools/usb-probe/e9e-guided-session.ps1` (e2c5dd4, the four-bump version) ran 10:57:26–11:06:05, started SmartSoft twice (10:58:43,
  11:03:09) and logged every step.
- **Experiment:** **E9e**, the control of [SC-5 addendum 2](../safety-checks.md): the channel bumps the E9d script had left out were
  cleared on the physical-control rule, and this session checks four of them in two SmartSoft sessions: the bumps under faders 2, 12 and
  24 of bank 1-24, and the bump under the fader labelled 48 in bank 25-48, each held about 1 s. SmartSoft Online, its Console Simulator
  driven by the owner; **our own software was not involved and sent nothing.** The owner's refusal of the first, 96-press version of
  E9e is in the owner-statements file; the omission of the channel bumps from E9d was the analyst's.
- **Console:** ETC SmartFade 1248, USB device address **46** (as E9d; not switched off since, owner 10:57:42). Firmware "3.0.2.9.0.43".
  The inbound counter stood at 23 at the first payload (no power-on).
- **Host:** the owner's PC; Memory Integrity off; interface 1 on ETC's driver (ETCHSP); SmartSoft 3.0.43 started by the script; USBD
  status 0 non-zero over 35 445 transfers.
- **Show backup (safety rule 3): taken.** `SHOW28.ASC` before (10:57:40), `SHOW29.ASC` after (11:06:04): **byte-identical** (15 944 bytes,
  SHA-256 `559636399c1d82e0f95e737c961bfee31f88962fe8c486bbc6eba7f7d2177a90`, the same bytes as every save since `SHOW23.ASC`).
- **Rig:** one twelve-channel fixture at DMX 5–16 (owner); nothing on DMX 2, 12, 24 or 48.
- **Action log:** `2026-11-02-e9e-actions.txt` (55 lines). **Owner statements:** `2026-11-02-e9e-owner-statements.txt`.

## Evidence files

| File | SHA-256 | Notes |
|---|---|---|
| `2026-11-02-e9e-smartsoft-channel-bumps-spot-check.pcap` | `f813d6b2544c31825b5a600e823b7896899b91a7ea408f8cdf1bb0521359d8be` | console-only split of device address 46, 70 890 frames, 3 862 366 bytes |
| `2026-11-02-e9e-smartsoft-channel-bumps-spot-check.jsonl` | `64aebc524e6bda6abc48746d26610ec47c4c9c1f910bddf4306932e34ed22a04` | the split normalized with the action log merged as 56 notes: 35 445 transfers (15 386 bulk OUT, 20 054 bulk IN, 5 control) |
| `2026-11-02-e9e-actions.txt` | `948c789bfc53b0d646170114e2d1d7f48a1bddd01d1f244db69711ada560835e` | the guided script's log |
| `2026-11-02-e9e-owner-statements.txt` | (hash in `README.md`) | the owner's refusal of the first E9e, the answers |
| not committed | `787ad0b506ee519fbd05e7196daba88f9a81cb153a6af02a282b2c4ffa7588b8` | the raw root-hub capture `e9e-20261102-1058.pcap`, 234 480 494 bytes, 278 574 frames (other devices) |

Tools (read-only): `hsp_summary.py`, `write_rules.py`, `join_reads.py`, `e9d/check.py` (extended for the channel-bump steps).

## Health and the writes

0 framing errors; **28 Data writes: 4 System (the pair, twice) and 24 ConsoleEvents** (12 key pairs: the four bumps per session and
the two bank keys per session); the counter from 0x00 in each session (F131); the header's stale words the last reply's 28 of 28 (F52);
no write between a press and its release; press → release 1.01–1.56 s for the bumps, 47–60 ms for the bank keys; the placement as
SmartSoft's (F131). The System pair with the ConsoleVersion listing and the 158-record download in both sessions (F58, F177). *(Note 2026-11-02, evening, SC-4 groundwork: E9e's two downloads had 159 records, PlaybackInfo appended last (F182, F183), and the sessions are the nineteenth and twentieth with the pair (F58, F181).)*

## The spot check (`e9d/check.py`: 14 steps, 0 unexpected)

| Step (both sessions) | Code sent, hold | The console's answer | Reading |
|---|---|---|---|
| bump under fader 2, bank 1-24 | **0x01**, 1.42 s / 1.23 s | ChannelLevels[2], DMX 2 (body[3]), FaderInfo record 2 and LED block 2 at 0xFF while held, back at the release | code = bump − 1; channel 2 |
| bump under fader 12 | **0x0B**, 1.56 s / 1.15 s | ChannelLevels[12], DMX 12, record 12, block 12 | channel 12 |
| bump under fader 24 | **0x17**, 1.37 s / 1.11 s | ChannelLevels[24], DMX 24 **and DMX 97** (this show's patch, as E9c S1), record 24, block 24 | channel 24 |
| FADERS 25-48 (0x37), bump under the fader labelled 48, FADERS 1-24 (0x36) | **0x17**, 1.42 s / 1.01 s | ChannelLevels[48], DMX 48 (body[49]), record 24 (the physical fader's record) | the same code in bank 25-48 lights channel 48 |

Every step as the physical-control rule predicts, in both sessions: the inference of SC-5 addendum 2 (the channel bumps 2–6, 8, 9, 11–24
and 26–48 behave like the bumps seen before) is confirmed at the four points chosen (the first, a middle and the last of bank 1-24, the
last of bank 25-48), and nothing in the capture suggests an exception. The codes 0x01, 0x0B and 0x17 are now OBSERVED twice as channel
bumps (0x17 in both banks); the other 17 channel-mode codes remain inferred, as the addendum states.

## For the curator

- **F174:** note the spot check (the channel bumps 2, 12, 24 and 48 as holds in two sessions; the hold 1.0–1.6 s; DMX 97 with channel 24).
- **SC-5 addendum 2:** the control was run and passed; the owner did not decline it; nothing else changes.
- No new fact, no status change beyond F174's note; nothing is cleared by this record.
