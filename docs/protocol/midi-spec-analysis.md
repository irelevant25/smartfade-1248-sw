# MIDI implementation analysis

Sources:
- **[M15]** `SmartFade_v1.5__MIDI_Spec.pdf`: "SmartFade MIDI specification Version 0.12",
  describes **SmartFade firmware 1.50**, dated 27 April 2005 / 06/07/2005, 7 pages.
- **[M30]** `SmartFade_v3.0_User_Manual_revA.pdf` (bundled in the SmartSoft installer),
  pages 26–28 "MIDI Setup". Firmware 3.0, released 2010-11.
- **[ASC]** `SET MIDIChannel`, `SET MIDIActive`, `SET MSCDeviceId`, `SET MSCActive` in show files.

**Nothing in this document is VERIFIED.** "DOCUMENTED" means stated in the source named.

## Key conclusions

1. MIDI is a documented, standard (non-proprietary) control path into the console. It needs
   a PC MIDI interface (USB-MIDI adapter) wired to the console's MIDI IN, and to MIDI OUT
   for monitoring.
2. **There is no evidence that SmartSoft uses MIDI** (see smartsoft-analysis.md S4). The MIDI
   spec describes console↔console sync and show control, not the SmartSoft link.
3. MIDI covers only a **subset**: GO/PAUSE, stack step jump, memory page, blackout, GM, bumps
   master, crossfader faders, memory masters 1–48 (MEM mode), memory bumps. There is no
   channel-level control, no patch, no programming and no state readback. A console in
   master mode transmits its own actions, which gives an *event* stream.
4. **[M15] and [M30] contradict each other** on Program Change and Control Change values
   (tables below). Firmware 3.x is almost certainly what matters, but **each value must be
   tested** on the user's firmware before use.
5. [M15]: "No system-real-time or system-exclusive commands are used." [M30] adds MIDI Show
   Control (MSC), which *is* SysEx (`F0 7F <id> 02 01 <cmd> … F7` per the MIDI Show Control
   standard; SmartFade-specific details are UNKNOWN). So [M15] is outdated on this point.

## General

| Feature | [M15] | [M30] | Status |
|---|---|---|---|
| Transmit/receive channel | One TX and one RX channel, default 1, set in menu | One channel for Basic MIDI (1–16 or Off). Menu *Setup > MIDI > Midi Channel* | DOCUMENTED |
| ASC encoding of channel | — | `SET MIDIChannel 0` in files where default is presumably ch 1 → **HYPOTHESIS: 0-based** | HYPOTHESIS |
| Master/slave | Master sends key presses and fader moves on OUT, ignores IN. Slave obeys IN, echoes IN→OUT (thru), sends nothing of its own | Connect OUT of A to IN of B, main playback "mirrored". Default ON | DOCUMENTED |
| OMNI | Removed in doc 0.11 | — | DOCUMENTED |
| Running status | Slave must accept. Master uses running status, re-sends status byte on change or after 1 s continuous | not mentioned | DOCUMENTED ([M15] only) |
| Note Off | May be sent as Note On velocity 0 | — | DOCUMENTED |
| Level encoding | "All levels are coded as 7 bit unsigned", value 0–127 "top 7 bits of level" | — | DOCUMENTED |
| Matching | CC only sent for *matched* faders on master | Faders synchronised "in MEM mode" | DOCUMENTED |
| MSC | none | Basic set, "sent and received at all times regardless of MIDI channel". Device ID 0–126 or All, or Off. Commands listed: Go, Stop, Resume | DOCUMENTED ([M30]) |
| MSC ↔ Basic MIDI | — | Use one or the other. Both on causes "double stepping" | DOCUMENTED |

## Control Change `Bn cc vv` (n = channel − 1)

| cc | [M15] fw 1.50 (TX master & RX slave) | [M30] fw 3.0 | Agreement | Direction | Range | Verified |
|---|---|---|---|---|---|---|
| 0 | reserved (future) | — | — | — | — | No |
| 1–48 | "Paged Virtual fader level, 1-48" (memory faders of current page) | "Master 1-48 (only in MEM mode)" | Consistent | TX+RX | vv 0–127 | No |
| 49–123 | reserved | — | — | — | — | No |
| 124 | **XF down** | **Live fader** | Probably same physical crossfader pair, different naming | TX+RX | 0–127 | No |
| 125 | **XF up** | **Next fader** | as above | TX+RX | 0–127 | No |
| 126 | Bumps master | Bump Master | Consistent | TX+RX | 0–127 | No |
| 127 | Grand master | Grand Master | Consistent | TX+RX | 0–127 | No |

Note: 1–48 memory faders against a page of 24 memory faders. On the 1248 the CC range 25–48
is **UNKNOWN** (maybe the 2496's second row). Test it.

## Program Change `Cn pp`

| pp | [M15] fw 1.50 | [M30] fw 3.0 | Agreement |
|---|---|---|---|
| 0 | Goto Stack step zero (dead state) | Reset the Stack to step 0 | Consistent |
| 1–99 | Stack step number | GOTO step 1–99 in the Stack | Consistent. Steps 100–199 and point cues unreachable |
| 100–123 | Memory page number **1–24** | 100–111 = Memory pages **1–12** | Consistent for 100–111. 112–123 differ (fw 3.0 has 12 pages) |
| 124 | **GO** | **Pause** | **CONTRADICTION** |
| 125 | **PAUSE** | **Start (GO)** | **CONTRADICTION** |
| 126 | **Black Out ON** | **B.O. not active** (= blackout OFF) | **CONTRADICTION** |
| 127 | **Black Out OFF** | **B.O. active** (= blackout ON) | **CONTRADICTION** |

[M15]: "As the user turns the dial on a Master to jump to a step no data is sent… When the
user releases the Stack button the Master sends the message."

**Safety note:** because blackout polarity is contradictory, the first PC-to-console MIDI test
must be done with nothing important patched and the outputs watched.

## Note On / Off `9n kk vv` / `8n kk vv`

| kk | [M15] | [M30] | Meaning |
|---|---|---|---|
| 0–47 | Mems Bump 1–48 | MEM bumps 1–48 (only in MEM mode) | Consistent. Momentary: Note On at press, Note Off at release. vv "normally any non-zero value" |
| 48–127 | reserved | — | — |

[M15]: if SOLO is on in the slave, a received bump On kills all other sources except other
bumped memories. [M30]: SOLO mode is synchronised for MEM bumps (mechanism UNKNOWN).

## Not supported over MIDI (per both documents)

Sequence stepping ([M15]: "not sequence stepping and some other functions"), channel levels,
recording, patch, labels, independents, snapshots, reading stored show data.

## Timing requirements

None documented beyond running-status refresh (1 s). No acknowledgement or response
messages exist, so MIDI control is **open loop**. The only feedback is the console acting as
master, or the DMX output.

## Test plan (see reverse-engineering.md, experiment E4/E5)

1. Console as MIDI master → PC MIDI IN. Record GM, bumps master, crossfaders, a memory
   fader, a bump, GO, PAUSE, blackout, page change. This observes real firmware values
   **with zero risk to the rig** and settles the contradictions for TX.
2. Only afterwards: PC → console MIDI IN, one message at a time, DMX output monitored.
