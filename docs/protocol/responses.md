# Responses and objects (console → PC)

Created 2026-10-28 from E2, extended the same day with the `SHOW04.ASC` cross-check. **OBSERVED at most; contents partly decoded (see "Decoded object bodies"), the rest undecoded.** Framing:
[packet-format.md](packet-format.md). Evidence: [E2 record](captures/2026-10-28-e2-smartsoft-connect.md),
facts F53, F57, F59–F61, F66–F68, and, for the decoded bodies, F70–F79 (cross-check with `SHOW04.ASC`). Object names are from the sfEngine name table
([smartsoft-analysis.md](smartsoft-analysis.md) S11.6, INFERRED); E2 confirms the ID → name
mapping for the 21 IDs seen (H19). Body lengths are those of the one session (firmware
3.0.2.9.0.43, console with 48 channels, show `SHOW04.ASC`).

**Update 2026-10-29 (E3,** [record](captures/2026-10-29-e3-console-actions.md)**).** The second session,
after a console power cycle, returned the shared objects with the same body lengths and, for the show
objects, byte-identical bodies: the ID → name mapping and the lengths are **VERIFIED** for the 20 IDs
seen in both sessions (F67) and the decoded show bodies are VERIFIED as "the console returns these
bodies" (F70–F77); the readings against the show file stay OBSERVED (one show). The live objects were
decoded further with the owner's logged actions (section "Live objects (E3)" below; F80–F89). Where a
row below still says OBSERVED, that was its status on 2026-10-28; the Status cell of each row carries
the E3 update. "CL-ML" marks a claim of the third-party ConsoleLink program about the SmartFade **ML**
(evidence class 5, [consolelink-analysis.md](consolelink-analysis.md)), never a 1248 fact.

**Update 2026-10-30 (E3b,** [record](captures/2026-10-30-e3b-live-objects.md)**).** Three more sessions
(after another power cycle) returned the same shared objects with the same lengths and byte-identical
show bodies; PlaybackInfo is now seen in two sessions (81 bytes, F67). The live objects were decoded
further with the owner's logged actions and statements: BUMPS = FaderInfo body[193], MASTER-scaled level
objects, bump output in the level objects, ConsoleStatus body[4] = fader bank, FaderInfo per physical
fader, LED blocks 50 / 52 / 55 / 56 / 58, PlaybackInfo during a GO crossfade, the default fade times in
SystemSettings and on the LCD (section "Live objects (E3b)" below; F93–F106). A row below that still
says what it said on 2026-10-29 carries the E3b update in its Status cell. VERIFIED never clears a message
for sending.

## Reply types

| Reply | Meaning | Fact |
|---|---|---|
| Type 4, w1 = 0 | Nothing pending (idle) | F53 |
| Type 4, w1 > 0 | DirtyObjects list (ID 40), the only content seen in type-4 payloads (16 of 16) | F57 |
| Type 3 | Object payload requested by a fetch, header echoes (id, a, b, c) | F53 |

## Objects seen

| ID | Name | Body bytes | What is known | Status |
|---|---|---|---|---|
| 0 | Memory | 138 | body[0..1] = (a, b) qualifiers echoed; text field after `03 06` | OBSERVED; rest UNKNOWN (H25) *Update: body layout decoded against SHOW04.ASC, see below (F71, H39)* |
| 2 | Sequence | 132 | body[0..1] = (0, 0); text after `03 06` | OBSERVED *Update: header + 7 step records, see below (F74, H42)* |
| 3 | SequenceMain | 46 | body[0] = 0x13 = 19 | OBSERVED; meaning **CONFIRMED (OBSERVED) 2026-10-28** = step count (H35, F74) |
| 4 | StepContent | 13 | body[0] = step index a | OBSERVED; body[1..12] = step record, 19/19 match the file (H41, F74) |
| 5 | Patch | 514 | `02 00` + 512 bytes (F68) | OBSERVED; meaning **CONFIRMED (OBSERVED) 2026-10-28** against SHOW04.ASC (H34, F70) |
| 8 | Page | 39 | body[0] = page a; text after `03 06` | OBSERVED |
| 9 | ChannelText | 39 | body[0] = a = 0..95 (96 objects); text after `03 06` | OBSERVED |
| 12 | Independents | 85 | body[0] = 2 | OBSERVED; layout OBSERVED once against SHOW04.ASC (H44, F76); field meaning UNKNOWN |
| 13 | OutputLevels | 514 | `02 00` (BE 512) + 512 level bytes; DMX 5 changed in the fader movement | OBSERVED; index mapping INFERRED (H32). **E3 2026-10-29:** DMX d = body[1 + d] (DMX 1 = body[2], DMX 5 = body[6]); length VERIFIED (F67); index mapping OBSERVED for DMX 1 and 5 (F83) **E3b 2026-10-30:** body[0..1] = `02 00` in all 73 payloads; mapping seen for DMX 5, 7, 8 and 25; the values follow the MASTER and bumps (F83 VERIFIED in part, F96, F97, F99) |
| 14 | FaderInfo | 195 | body[0] = 48; 48 × 4-byte records + 2 trailing bytes; body[17] changed | OBSERVED; layout HYPOTHESIS (H32). **E3:** layout CONFIRMED (OBSERVED) for faders 1 and 5, body[194] = MASTER fader, body[193] = BUMPS (HYPOTHESIS, 0x00 so far) (F82, H46); length VERIFIED (F67) **E3b:** layout VERIFIED in part (F82); BUMPS = body[193] OBSERVED (F95); records belong to the physical faders, records 25–48 never change (F99); record byte 1 moved only with a memory bump (F97, H62) |
| 15 | ChannelLevels | 51 | body[0] = 50; body[5] changed; body[49], [50] = 0 | OBSERVED (length part H26 confirmed); Independents part open. **E3:** channel c = body[c] (channels 1 and 5 seen, F83); body[49], [50] = 0 in all 48 payloads; length VERIFIED (F67) **E3b:** body[0] = 50 and body[49], [50] = 0 in all 75 payloads; channels 5, 7, 8 and 25 seen; the values are MASTER-scaled (F96, F97, F99) |
| 17 | PlaybackInfo | 81 | body[0] = 5; 81 = 1 + 5 × 16 | OBSERVED; layout HYPOTHESIS. **E3:** never listed or fetched (F89); E2's f24 is the only sample; H50 **E3b:** seen again, 66 payloads during a GO crossfade, 81 bytes: layout CONFIRMED in capture (F101, H50); length VERIFIED (F67) |
| 19 | InputLevels | 515 | `00 02 00` + 512 zero bytes (E3 f28): `[00][u16 BE 512][512 levels]` by analogy with OutputLevels | OBSERVED (E3, once; bytes); HYPOTHESIS (meaning, H52); F81 **E3b:** not listed or fetched (F105) |
| 21 | DisplayContent | 41 | body[0] = 40, then 40 text-range bytes | OBSERVED; LCD reading HYPOTHESIS. **E3:** `[0x28][2 × 20]`, fader N's cell at body[2 + N], glyphs 0x20 and 0x80–0x86 (F86); bar-graph reading INFERRED (H49); length VERIFIED (F67) **E3b:** 49 payloads; row labels '25' / '37' in bank 25-48 and times 'U 5.1' / 'D 5.1' (F86 VERIFIED in part, F98, F102) |
| 22 | LedStatus | 451 | body[0] = 75; 451 = 1 + 75 × 6 | OBSERVED; layout HYPOTHESIS (H32). **E3:** block map decoded for blocks 1–48 and the indicator blocks 53 / 55 / 58 (F84, F85; H48, H55, H56); length VERIFIED (F67) **E3b:** blocks 50 (blue during the GO crossfade), 52 (SOLO), 55 / 56 (bank) and 58 (memory mode) added (F98, F100); F84 and F85 VERIFIED in part |
| 23 | ConsoleStatus | 7 | `00 00 00 00 00 01 00` in all 3 fetches | OBSERVED; meaning UNKNOWN. **E3:** body[0] changed 0x00 → 0x01 at the switch to memory mode (F87); reading "fader mode" HYPOTHESIS (H53) **E3b:** body[0] = mode (five transitions in E3 + E3b), body[4] = fader bank (F98); F87 VERIFIED in part |
| 36 | FileName | 61 | length byte 30, then UTF-16LE "SHOW04.ASC" (F60) | OBSERVED **E3b:** the value is "SHOW05.ASC", the owner's last SD save (F103) |
| 37 | ConsoleVersion | 13 | length byte 12, then ASCII "3.0.2.9.0.43" (F59) | OBSERVED (value VERIFIED, F48) |
| 38 | SystemSizes | 8 | `30 02 00 18 00 0C 04 01` (F61) | OBSERVED; H27 confirmed, H33 open |
| 39 | System | 3 | body[0] = a (0, 6, 1 seen); `00 00 00`, `06 00 00`, `01 00 00` | OBSERVED; meaning UNKNOWN |
| 40 | DirtyObjects | 2 + 5n | see [packet-format.md](packet-format.md) | OBSERVED |
| 41 | SystemSettings | 18 | `00 64 5A 3C 00 00 00 00 00 01 7F 00 00 32 00 32 00 00` | OBSERVED; body[6..11] = the six `SET` values (position only, H45, F77); rest UNKNOWN **E3b:** body[13] and [15] = 0x33, the default Up / Down times 5.1 s (E2 / E3: 0x32 = 5.0 s; F102, H45) |

IDs 1, 6, 7, 10, 11, 16, 18, 19, 20, 24–35 and 42 were never seen (F67). *(Update 2026-10-29: ID 19
InputLevels was seen in E3, row above; still unseen in E2 and E3: 1, 6, 7, 10, 11, 16, 18, 20, 24–35,
42.)*

## Decoded object bodies (vs `SHOW04.ASC`)

All rows: **OBSERVED once** (one capture, one show, firmware 3.0.2.9.0.43, qualifiers a / b / c in the fetch
header); nothing VERIFIED. Source: E2 record "Cross-check against SHOW04.ASC". Bytes are body offsets,
u16 are big-endian, times are tenths of a second. "Hypothesis" means the layout is a reading of this one
comparison (H39-H45), not a definition.

**Update 2026-10-29 (E3):** every body in this table was returned byte for byte again in the second
session (`startdiff`: 155 of 161 shared object instances identical; the six that differ are live
state), so the **bodies** are VERIFIED as "the console returns these bodies" (F70–F77). The
**readings** against the show file are still OBSERVED once (one show, one file) and the "Hyp." column is
unchanged. The label byte order is the exception (Text block row). "OBSERVED once" and "nothing
VERIFIED" above are the 2026-10-28 wording, kept.

| Object | Layout (body) | Checked against | Frames | Hyp. | Fact |
|---|---|---|---|---|---|
| Memory (0) | `[a][b][01][60][96 levels][text block]`; level raw = ceil(pct x 255 / 100), 0xFF = 100 %, 0xAB = 67 %; memory number = 24a + b + 1; body[2] meaning UNKNOWN | `GROUP`, `CHAN`, `$$MEMORY p f`: 16/16 memories, 768/768 levels (channels 1-48; 49-96 are 0) | f440-f504, f600-f648 (example f448) | H39, H13, H25 | F71, F72 |
| Text block | `[03][06]` + 18 UTF-16 code units (byte order BE is INFERRED from structure, H40 / H36; **update 2026-10-29: downgraded to HYPOTHESIS**: reading A = 18 UTF-16BE code units, reading B (CL-ML, class 5) = one pad byte then UTF-16LE lines of 12 / 12 / 11 bytes, same text for every label seen) = 3 lines x 6 characters, each line cut at its first 0; in Memory, Sequence, SequenceMain, Page, ChannelText, Independents | `$$Text 1..3` and `Text` (= `$$Text 1`): 16 memories, 1 sequence, 12 pages, 96 channels, 2 independents | f504, f656 | H40, H36 | F73 |
| Patch (5) | `[u16 512][512 x channel]`, entry d - 1 = channel at DMX address d, 0 = unpatched; no proportional level | `PATCH 1 d/c/l`: 512/512, 49 patched (incl. DMX 47 -> 50, 48 -> 48, 97 -> 24) | f424 | H34 | F70 |
| Step record (12 bytes) | `[cue x 10][CUEPARAMS][memory index 24p+f][x = 0][UP][DOWN][FOLLOWON]`, cue and times u16 | `CUE`/`$CUE`, `$$CUEPARAMS`, `$$MEMORY`, `UP`, `DOWN`, `FOLLOWON`: 19/19 + 7/7 | f672-f816, f656 body[48..] | H41, H14, H12 | F74 |
| StepContent (4) | `[a = step index][step record]` | as above | f672-f816 (f728, f736, f744) | H41 | F74 |
| SequenceMain (3) | `[steps][b][c][d][e][text block]` (8 + 38) | `$SEQUENCE 48 2 0 256 256`, 19 `CUE` | f664 | H42, H35 | F74 |
| Sequence (2) | `[page][seq][steps][b][c][d][e][text block][steps x step record]` (10 + 38 + 84) | `$SEQUENCE 0 0 0 256 256`, 7 `$CUE`, label KOTLIK | f656 | H42 | F74 |
| Sequence step numbers | wire: ordinal 1.0-7.0; file: `$CUE` = memory number; console display UNKNOWN | `$CUE 265.0-271.0` (30 SD shows: 348/348, asc-format.md) | f656 | H43 | F75 |
| Independents (12) | `[2]` + 2 x `[a][b][c][d][text block]`; here `00 FF 00 01` | `$INDEPENDENT n 0 255 0 1` | f1592 | H44 | F76 |
| SystemSettings (41) | body[6..11] = the six `SET` values in file order; body[12..17] = 5.0 s, 5.0 s, 0 as u16 (HYPOTHESIS: default Up / Down / Wait); body[0..5] UNKNOWN **E3b 2026-10-30:** body[12..15] = the default Up / Down times, 0x33 = 5.1 s in E3b against 0x32 = 5.0 s in E2 and E3, moving together with the LCD's 'U' / 'D' (strongly supported, F102, H45); body[16..17] untested | `SET` lines | f416 | H45 | F77 |

Not in any decoded object: the Patch proportional level, the page / fader meaning of (a, b), the
meaning of `$SEQUENCE` fields b-e and `$INDEPENDENT` fields a-d.

**Update 2026-10-30 (E3b):** every body in the table above was returned byte for byte again in three more
sessions (`startdiff` E3 versus E3b: 156 of 161 shared object keys identical); the cross-check against
`SHOW05.ASC` (byte-identical to `SHOW04.ASC`, F104) repeated every result (F70–F77). The only differing body
above is SystemSettings (body[13] and [15], F102).

## Live objects (E3, 2026-10-29)

Source: [E3 record](captures/2026-10-29-e3-console-actions.md) (JSONL `seq`, pcap `fN`), the owner's logged
actions and the owner's statements ([captures/2026-10-29-e3-owner-statements.txt](captures/2026-10-29-e3-owner-statements.txt),
A1–A4, Q5). **OBSERVED (one session) unless stated; nothing here may be sent to a console.** Panel
names: **MASTER (Grand Master)** and **BUMPS (bump master)**. Bytes are body offsets; "block k" = LedStatus
body[1 + 6(k − 1) .. 6k], numbered from 1. The ML layouts of ConsoleLink (class 5) predicted the
positions that moved (consolelink-analysis.md O1–O10); they are corroboration, not evidence.

| Object | Layout (body) | Seen with | Frames | Hyp. | Fact | Status |
|---|---|---|---|---|---|---|
| InputLevels (19) | `[00][u16 BE 512][512 levels]`, 515 bytes, all 512 levels 0; the only record of the connect-time list after a power cycle | E3 start-up | f20, f28 (seq 10, 14) | H52 | F80, F81 | bytes OBSERVED; meaning HYPOTHESIS |
| FaderInfo (14) | `[48][48 × 4][BUMPS][MASTER]`, 195 bytes: record N byte 0 = level of fader N at body[1 + 4(N − 1)] (fader 1 = body[1], fader 5 = body[17]); **body[194] = MASTER**, 0xFF → 0x83 → 0xFF; body[193] = BUMPS (0x00 so far); body[83] = body[99] = 0x01 constant | faders 1 and 5, the MASTER fader (owner's logged moves) | f9916, f16506, f37168 … f37484, f38228 … f38326 | H32, H46, H59 | F82 | layout CONFIRMED (OBSERVED); MASTER = body[194] OBSERVED (wire position, one session, time-correlated with the logged moves and the owner's panel label) with the Master/Blackout behaviour DOCUMENTED (manual p.56); BUMPS = body[193] HYPOTHESIS *(E3b 2026-10-30: BUMPS = body[193] is now OBSERVED moving, F95; layout VERIFIED in part, F82)* |
| ChannelLevels (15) | `[50][50 channel levels][IND1][IND2]`: channel c = body[c]; full = 0xFF at MASTER 0xFF; body[49], [50] = 0 so far | channels 1 and 5 | f9924, f16490 | H32, H26 | F83 | OBSERVED |
| OutputLevels (13) | `[02 00][512 levels]`: DMX d = body[1 + d]; full = 0xFF at MASTER 0xFF | DMX 1 and 5 | f9954, f16498 | H32 | F83 | OBSERVED |
| LedStatus (22) | `[75][75 × 6]`, block = two RGB triples (phase A, phase B). Blocks 1–24: channel mode G = fader level; memory mode R = 0x46 recorded memory, 0x0A empty slot, R + G (`46 46 00`) stored sequence (block 21 on page 1), 0xFF while the bump is held. Blocks 25–48 `00 0A 00 00 0A 00` constant. **Block 53 = Blackout LED**: `37 37 FF` / `0A 0A 0A` (blinks) while the MASTER is below full, `0A`×6 otherwise. Blocks 55, 58: mode LEDs (INFERRED). Other blocks 49–75 constant in E3 | channel mode, memory mode, bump 1, MASTER move | f1592, f32328, f33040, f34026, f37176, f38348 | H48, H55, H56 | F84, F85 | OBSERVED; block 53 = Blackout LED INFERRED (owner A4 + manual p.56); blocks 55, 58 HYPOTHESIS |
| DisplayContent (21) | `[0x28][line 1: 20][line 2: 20]`: " 1" + 12 cells + blank + "U 5.0" / "13" + 12 cells + blank + "D 5.0"; fader N's cell at body[2 + N]; cell = 0x20 (level 0) or 0x80–0x86 rising (8 states, **0x87 never seen**); top state 0x86 shown at 0xFF with the MASTER at 0xFF (f16522) and slightly below | faders 1 and 5 | f9624, f9878, f10732, f16522 | H49 | F86 | OBSERVED; bar-graph reading INFERRED (manual p.30) |
| ConsoleStatus (23) | 7 bytes `00 00 00 00 00 01 00`; **body[0] 0x00 → 0x01** at the switch to memory mode; body[1..6] constant | the (unlogged) memory-mode switch at 249.99 s | f1584, f32320 | H53 | F87 | OBSERVED; meaning HYPOTHESIS *(E3b: body[4] = fader bank and five transitions of body[0], F98; F87 VERIFIED in part)* |

What the live objects do **not** show or contain (E3):

- **A memory-mode bump changed no level object.** Only LedStatus block 1 changed (`46 00 00` → `FF 00 00` →
  `46 00 00`); the BUMPS fader stood at 0 %. Reading (a) "the bump flashes to the BUMPS level" is DOCUMENTED
  behaviour (manual pp.12, 56, 57) and consistent; reading (b) "level objects do not show bump output" is not
  excluded (F88, H54). *(Update 2026-10-30: with BUMPS at 0xFF a held bump does appear in the level objects,
  F97; reading (b) REJECTED.)*
- **The 157-record list omits FaderInfo, DisplayContent and PlaybackInfo** (and InputLevels); they arrive only
  when pending at connect or when they change (F89, H58).
- **A full fader reads 0xFF only with the MASTER at 0xFF;** E2's 0xAB ceiling coincided with MASTER = 0xAA
  (F90, H47, H57).
- **Not yet seen:** the BUMPS fader moving (body[193]), the 25-48 fader mode (H59), PlaybackInfo changing (H50),
  a page change (H51), DMX IN data (H52). *(Update 2026-10-30: the BUMPS fader moving, the 25-48 bank and
  PlaybackInfo changing were seen in E3b, F95, F99, F101; a page change and DMX IN data were not.)*

## Live objects (E3b, 2026-10-30)

Source: [E3b record](captures/2026-10-30-e3b-live-objects.md) (JSONL `seq`, pcap `fN`), the owner's action log
and the owner's statements ([captures/2026-10-30-e3b-owner-statements.txt](captures/2026-10-30-e3b-owner-statements.txt),
E3b-A1..A3). **OBSERVED (one capture) unless stated; nothing here may be sent to a console.** Notation as in
the E3 section; the rig is one LED bar with four cells at DMX 5–16 ([hardware doc](../hardware/smartfade-1248.md)).

| Object | Layout / change (body) | Seen with | Frames | Hyp. | Fact | Status |
|---|---|---|---|---|---|---|
| FaderInfo (14) | `[48][48 × 4][BUMPS][MASTER]` unchanged. **body[193] = BUMPS** (0x00 ↔ 0xFF, 104 changes). Records 1–24 are the 24 **physical** faders in every mode and bank (bank 25-48: record 1 = body[1] moves, record 25 = body[97] stays 0); records 25–48 never change. A held bump moves its record: channel bump 7 → byte 0 of record 7 (body[25]); memory bump 1 → bytes 0 and 1 of record 1 (body[1..2] = `FF FF`). The level bytes carry post-MASTER values (body[17] follows body[194]) | BUMPS moves, fader 5 under a MASTER sweep, bumps, bank 25-48 | f30010 … f84292, f20302 … f23188, f40368, f60332, f92020 … f93190 | H46, H47, H59 (REJECTED), H61, H62 | F95, F96, F97, F99 | BUMPS = body[193] OBSERVED; layout VERIFIED in part (F82); H61, H62 HYPOTHESIS |
| ChannelLevels (15), OutputLevels (13) | Same layouts. Post-MASTER values (all read 0x7F at rest at 'half'). Bump output at the BUMPS level: ChannelLevels[7] / OutputLevels[8] for the channel bump, channels 7 and 8 (OutputLevels[8], [9]) for memory bump 1, all 0xFF at BUMPS 0xFF. In bank 25-48 physical fader 1 moves ChannelLevels[25] / OutputLevels[26] | MASTER sweep, bumps, bank 25-48 | f20952, f20928, f40376, f40360, f60354, f60346, f92028 …, f92012 … | H47, H54, H61 | F96, F97, F99 | OBSERVED; the scaling (H47) and bump output (H54) CONFIRMED in capture |
| LedStatus (22) | Blocks 1–24 follow the physical faders in every mode; blocks 25–48 constant. **Block 52 = SOLO** (`FF`×6 while on). **Block 50** blue `00 00 FF 00 00 FF` during a GO crossfade. **Blocks 55 / 56** swap with the bank (`00 FF 00 00 FF 00` bright green = selected). **Block 58** red in memory mode. Block 53 blinks while the MASTER is below full | SOLO, GO crossfade, bank and mode switches | f71756, f79228, f103328, f104254, f88382, f94528, f46904, f20310, f23174 | H48, H56, H64 | F84, F85, F98, F100 | OBSERVED; identities INFERRED (SOLO, 55, 56, 58) or HYPOTHESIS (50) |
| DisplayContent (21) | Same layout. Row labels ' 1' / '13' → '25' / '37' in bank 25-48; times 'U 5.1' / 'D 5.1' (E2 and E3: 'U 5.0' / 'D 5.0'); bump output shows in cells 7 and 8 (0x86) | bank 25-48, bumps, the fade-time change | f88390, f94542, f36, f60370, f40392 | H49 | F86, F98, F102 | OBSERVED; layout VERIFIED in part |
| ConsoleStatus (23) | 7 bytes. **body[0]** mode (0 channel, 1 memory), **body[4]** fader bank (0 = 1-24, 1 = 25-48); the rest constant | mode and bank switches | f46896, f85826, f86336, f86984, f88374, f94520 | H53 | F87, F98 | OBSERVED; meaning HYPOTHESIS, strongly supported |
| PlaybackInfo (17) | `[5][5 × 16]`, 81 bytes. Record 5 (body[65..80]): **body[67] Live and body[68] Next** moved in opposite directions (sum 255), `FA/05` → `00/FF`, then reset to `FF/00`; body[75..78] two u16 BE times counting down from `00 32` in 1-s steps and back to `00 32`; record bytes 0–1: `06 06` (record 5), `04 04` (record 1), `00 00` in E2 | one GO crossfade (the owner's GO press, STACK unlit; E3b-A1) | f103320 … f104238 | H50, H63 | F101 | OBSERVED once; layout CONFIRMED in capture; trigger owner-explained |
| SystemSettings (41) | body[13], [15] = 0x33 (5.1 s); E2 / E3: 0x32 (5.0 s) | the owner's wheel move (E3b-A2) | f388, f5754, f10452 | H45 | F102 | OBSERVED; reading strongly supported |
| FileName (36) | "SHOW05.ASC" | the owner's backup before E3b | f1580, f6946, f11644 | | F103 | OBSERVED |

What E3b shows about the live objects:

- **A held bump appears in the level objects at the BUMPS level** (tested at 0xFF); E3's reading (b) is REJECTED.
  SOLO on and off gave the same bytes, because no other output was active (F97).
- **Level objects are MASTER-scaled;** at rest the full fader reads the MASTER byte, E2 read one more (H60).
- **FaderInfo and the LED blocks belong to the physical faders;** the channel they control is reported in
  ChannelLevels, OutputLevels and the LCD (F99, H61).
- **After a SmartSoft restart nothing is pending** and FaderInfo, DisplayContent and PlaybackInfo arrive only
  when they change; after a power-on the pending list varies (F94, H58, H65).
- **Not yet seen:** BUMPS between 0x00 and 0xFF (H54), several stable MASTER positions (H60), a GO with the
  STACK button lit (H50, H63), a page change (H51), InputLevels with a DMX source (H52; whether anything is on
  DMX IN is UNKNOWN, F105).

## Label text

Labels follow two constant bytes `03 06` as `00 xx` pairs; byte order was undetermined (H36).
*Update (SHOW04.ASC cross-check): UTF-16BE, 3 x 6 characters, INFERRED from structure and matched to the
file (F73, H40); see the text block row above.* **Update 2026-10-29:** the byte order is **HYPOTHESIS**, not
INFERRED: a second reading (CL-ML, class 5) fits the same 38 bytes (one pad byte, then UTF-16LE lines of
12 / 12 / 11 bytes) and gives the same text for every label seen; the text match with the file stays
OBSERVED. A "č" label and a 6-character third line decide it (H40).
