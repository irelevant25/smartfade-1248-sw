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

**Update 2026-10-30 (E8,** [record](captures/2026-10-30-e8-first-read-only-session.md)**).** Our own read-only
session (polls and listed fetches only, **no USBPcap**, so cited by the JSONL `seq` of the backend's transfer log
and with no pcap frame numbers) received eight live objects (IDs 12, 13, 14, 15, 17, 21, 22, 23), each with the
body length of F67, and no show object. It added readings of FaderInfo record bytes 1–3, ConsoleStatus body[2],
the Independents record byte 0, ChannelLevels body[49] / [50], OutputLevels body[48], the LED blocks 53, 57, 60,
63–67, 72 and 73, the LCD texts of the page view, STACK and the record functions, and PlaybackInfo records 1 and
5 (section "Live objects (E8)" below; F110–F120). Blinking is phase A different from phase B inside one LedStatus
payload. A row below that still says what it said earlier carries the E8 update in its Status cell. VERIFIED
never clears a message for sending.

**Update 2026-10-30 (E8b,** [record](captures/2026-10-30-e8b-second-read-only-session.md)**).** The second read-only session of our own
software (polls and listed fetches only, after a power cycle, **with USBPcap**; cited by the backend log's JSONL `seq` and, where the
bus was looked at, by the frame `fN` of the committed console-only split) received nine objects (IDs 3, 12, 13, 14, 15, 17, 21, 22,
23), each with the body length of F67, and no other show object. It added readings of FaderInfo (memory-mode records pre-MASTER,
byte 3 per fader across a page change), of the level objects under BLACKOUT, of PlaybackInfo records 1 and 5 under a running
sequence, under GO and under the manual crossfaders, of SequenceMain at a GO, of the LED blocks 50, 60, 63–67, 72, 73 and the
constant colour of blocks 25–48, and of the LCD texts of the stack steps and the sign `+` (section "Live objects (E8b)" below;
F122–F130). WinUSB splits an IN read longer than 64 bytes and not a multiple of 64 into two URBs on the bus; the decoders see the
concatenated record (F122). A row below that still says what it said earlier carries the E8b update in its Status cell. VERIFIED
never clears a message for sending.

**Update 2026-10-30, late evening (E9,** [record](captures/2026-10-30-e9-smartsoft-writes.md)**).** A SmartSoft session driven through its
Console Simulator (passive capture, our software sent nothing; cited by the JSONL `seq` and, where the bus was looked at, by the frame
`fN` of the committed console-only split) received 20 object ids in 630 fetches, each with the body length of F67, and brought **the
first Memory objects listed by the console itself after a recording** (four, 138 bytes each), the first System records with c ≠ 0
(F137) and the FileName of the owner's latest SD save (F103). It added readings of how the console's state objects follow PC events
(the same objects with the same bytes as for the physical controls, F134), of the record functions' screens and lists (F136) and of
the matching of PC faders (F135) (section "Live objects (E9)" below; F131–F140). A row below that still says what it said earlier
carries the E9 update in its Status cell. VERIFIED never clears a message for sending.

**Update 2026-10-31 (E9b,** [record](captures/2026-10-31-e9b-smartsoft-writes-repeat.md)**).** The repeat of E9 (three SmartSoft sessions through
the Console Simulator after a console power cycle, passive; our software sent nothing; cited by the JSONL `seq` and, where the bus was
looked at, by the frame `fN` of the committed console-only split) received the same object ids with the body lengths of F67, and brought
the first **System record from the console's own EDIT MEM** (5, 5, 0), **Memory objects listed after recordings made with the console's
own keys** and after a deletion (byte 2 = 0), the **post-MASTER level in a stored memory**, and the console's **verbatim return of a
ChannelText write** in the next download (section "Live objects (E9b)" below; F141–F147). A row below that still says what it said
earlier carries the E9b update in its Status or Fact cell. VERIFIED never clears a message for sending.

**Update 2026-10-31, evening (E12,** [record](captures/2026-10-31-e12-smartsoft-patch-stack-edits.md)**).** SmartSoft editing the patch, the independents, the stack, a sequence,
a memory and page names (three sessions after a console power cycle, passive; our software sent nothing; cited by the JSONL `seq`
and the frame `fN` of the committed console-only split; decoded by the `capture-analyst` on Opus 5.5 at the owner's request)
brought the console's answers to SmartSoft's **step commands** (lists of the affected objects, among them **Sequence (2, 0, 0, 6)
with c ≠ 0**), **Sequence and StepContent bodies of other lengths** (144 and 148 bytes), the stored results of SmartSoft's object
writes in a 161-record download, and the console's **own menu edits listed at every wheel detent** (section "Objects after edits
(E12)" below; F151–F159). F143 and F147 are now VERIFIED and F144 and F146 VERIFIED in part (E9b + E12). VERIFIED never clears a
message for sending.

## Reply types

| Reply | Meaning | Fact |
|---|---|---|
| Type 4, w1 = 0 | Nothing pending (idle) | F53 |
| Type 4, w1 > 0 | DirtyObjects list (ID 40), the only content seen in type-4 payloads (16 of 16; E9: 264 of 264, four lists naming a record with c ≠ 0, F137; E12: 287 of 287, two naming Sequence (2, 0, 0, 6), F156) | F57 |
| Type 3 | Object payload requested by a fetch, header echoes (id, a, b, c) | F53 |

## Objects seen

| ID | Name | Body bytes | What is known | Status |
|---|---|---|---|---|
| 0 | Memory | 138 | body[0..1] = (a, b) qualifiers echoed; text field after `03 06` | OBSERVED; rest UNKNOWN (H25) *Update: body layout decoded against SHOW04.ASC, see below (F71, H39)* **E9 2026-10-30 (late evening):** 20 payloads, 16 in the download and **4 listed by the console itself after a recording** (REC MEM and a bump: (a, b) = (0, 8), seq 74007; EDIT MEM stores: (0, 0), seq 67552, and (0, 6), seq 68832; COPY: (0, 9), seq 84668), each `[a][b][01][60][96 levels][text block]`, 138 bytes (F67, F136, F139) **E12 2026-10-31:** listed after the console's own REC MEM (seq 38719, channel 5 = 0x85) and its deletion (seq 188544, byte 2 = 0); download 2's body is SmartSoft's W46 in this layout (seq 139072; F143 and F147 VERIFIED, F146 in part) |
| 2 | Sequence | 132 | body[0..1] = (0, 0); text after `03 06` | OBSERVED *Update: header + 7 step records, see below (F74, H42)* **E12 2026-10-31:** **144 bytes** with 8 steps (seq 87591, 139152) besides 132 with 7; listed with **c = 6** after SmartSoft's InsertStep and DeleteStep on KOTLIK (seq 87587, 169123); body[3] = the run mode as SmartSoft writes it, stored (F154, F156) |
| 3 | SequenceMain | 46 | body[0] = 0x13 = 19 | OBSERVED; meaning **CONFIRMED (OBSERVED) 2026-10-28** = step count (H35, F74) **E8b:** listed by the console at each GO (seq 58654, 59236) and fetched by our host; body `13 02 00 00 01 00 01 00 03 06` + 36 zero bytes, byte-identical to E2's, E3's and E3b's (seq 58662; F130) **E12 2026-10-31:** body[0] followed the stack's step count 0x13 → 0x14, 0x15, 0x16 and back after SmartSoft's step commands; listed twice before each StepContent in the console's own Step Timing lists (F151, F155) |
| 4 | StepContent | 13 | body[0] = step index a | OBSERVED; body[1..12] = step record, 19/19 match the file (H41, F74) **E12 2026-10-31:** **148 bytes** for a type-2 "State" step (seq 139240, 175932); the console's default step after InsertStep (type 0, memory index 0, seq 30132); a record of zeros at the old last index after a delete (seq 30477); indices 19–21 in download 2 (F152, F153, F156) |
| 5 | Patch | 514 | `02 00` + 512 bytes (F68) | OBSERVED; meaning **CONFIRMED (OBSERVED) 2026-10-28** against SHOW04.ASC (H34, F70) **E12 2026-10-31:** listed by the console at every wheel detent of its own Blind Patch by dimmer (53 times in 22 lists) and at Unpatch dimmer; download 2 = SmartSoft's last write (F153, F155) |
| 8 | Page | 39 | body[0] = page a; text after `03 06` | OBSERVED **E12 2026-10-31:** download 2's Page 0 = SmartSoft's write ("T", seq 139036; F153) |
| 9 | ChannelText | 39 | body[0] = a = 0..95 (96 objects); text after `03 06` | OBSERVED **E9:** 96 payloads in the download; the first object SmartSoft wrote (three writes of channel 7's label, F138); nothing was listed back **E12 2026-10-31:** ChannelText 6 still carries E9's residue in all three downloads after another power cycle (seq 422, 139272, 194516; F144 VERIFIED in part) |
| 12 | Independents | 85 | body[0] = 2 | OBSERVED; layout OBSERVED once against SHOW04.ASC (H44, F76); field meaning UNKNOWN **E8 2026-10-30:** record byte 0 (body[1], body[43]) = the independent's on state, 0 ↔ 1 with the IND keys; bytes 1–3 `FF 00 01` constant (F115; H44 supported, still HYPOTHESIS) **E8b:** the on state named with timed keys: record 1 = IND 1, record 2 = IND 2 (seq 15616, 16383; F115) **E12 2026-10-31:** fields c, d = IND 1's button mode, GM in SmartSoft's writes (F154); the console's reaction unobserved |
| 13 | OutputLevels | 514 | `02 00` (BE 512) + 512 level bytes; DMX 5 changed in the fader movement | OBSERVED; index mapping INFERRED (H32). **E3 2026-10-29:** DMX d = body[1 + d] (DMX 1 = body[2], DMX 5 = body[6]); length VERIFIED (F67); index mapping OBSERVED for DMX 1 and 5 (F83) **E3b 2026-10-30:** body[0..1] = `02 00` in all 73 payloads; mapping seen for DMX 5, 7, 8 and 25; the values follow the MASTER and bumps (F83 VERIFIED in part, F96, F97, F99) **E8:** body[48] = DMX 47 followed IND 2 (the show's patch: channel 50 → DMX 47, F115); the memory faders drive the stored content scaled by the fader (F112) **E8b:** body[48] = DMX 47 with the named IND 2 key; BLACKOUT zeroes DMX 5 (body[6]) with channel 5 at full (F125); on the bus a 519-byte read is two URBs, 512 + 7 bytes (F122) |
| 14 | FaderInfo | 195 | body[0] = 48; 48 × 4-byte records + 2 trailing bytes; body[17] changed | OBSERVED; layout HYPOTHESIS (H32). **E3:** layout CONFIRMED (OBSERVED) for faders 1 and 5, body[194] = MASTER fader, body[193] = BUMPS (HYPOTHESIS, 0x00 so far) (F82, H46); length VERIFIED (F67) **E3b:** layout VERIFIED in part (F82); BUMPS = body[193] OBSERVED (F95); records belong to the physical faders, records 25–48 never change (F99); record byte 1 moved only with a memory bump (F97, H62) **E8:** records 1–24 are also the memory faders (bytes 0 and 1 equal in memory mode, byte 1 = 0 in channel mode: H62 REJECTED, successor H71); byte 3 of records 1–24 = page − 1 (H69); record 21 byte 2 (body[83]) = 0x00 on page 12 against 0x01 on page 1 (H70); BUMPS = body[193] VERIFIED (F95); records 25–48 never changed (F99, F112, F113) **E8b:** in memory mode the record's bytes 0 and 1 are equal (87 of 87 non-zero samples) and **pre-MASTER** while ChannelLevels and OutputLevels are post-MASTER (F126); byte 3 is per fader across a page change (H69 CONFIRMED, F127); record 21 byte 2 = 1 on page 1 and 0 on page 12 in four transitions (H70); record byte 0 of a channel at full goes to 0 under BLACKOUT (F125); BUMPS also at 0x81 (F95); records 25–48 never changed; on the bus a 200-byte read is two URBs, 192 + 8 bytes (F122) |
| 15 | ChannelLevels | 51 | body[0] = 50; body[5] changed; body[49], [50] = 0 | OBSERVED (length part H26 confirmed); Independents part open. **E3:** channel c = body[c] (channels 1 and 5 seen, F83); body[49], [50] = 0 in all 48 payloads; length VERIFIED (F67) **E3b:** body[0] = 50 and body[49], [50] = 0 in all 75 payloads; channels 5, 7, 8 and 25 seen; the values are MASTER-scaled (F96, F97, F99) **E8:** body[49], [50] = the independents' levels (0x00 ↔ 0xFF with the IND keys, F115; H26 Independents part CONFIRMED in capture); memory content scaled by the memory fader (F112) **E8b:** body[49] = IND 1 and body[50] = IND 2 with named, timed keys (F115); channel 5 goes to 0 under BLACKOUT (F125); channels 37, 38, 45 with the sequence on fader 21 (F129); channels 7, 8, then `GROUP 2`, then channel 2 under the stack (F130) |
| 17 | PlaybackInfo | 81 | body[0] = 5; 81 = 1 + 5 × 16 | OBSERVED; layout HYPOTHESIS. **E3:** never listed or fetched (F89); E2's f24 is the only sample; H50 **E3b:** seen again, 66 payloads during a GO crossfade, 81 bytes: layout CONFIRMED in capture (F101, H50); length VERIFIED (F67) **E8:** 2 340 payloads: record 1 = a running 7-step sequence (step index in bytes 0–1, the Live / Next pair, three u16 times; H63 CONFIRMED in capture for record 1), record 5 bytes 0–1 changed at the STACK press, bytes 4–9 `00 00 01 00 01 00` in all five records (F117, F118, H74); F101 VERIFIED in part **E8b:** 1 305 payloads (an 86-byte read is two URBs on the bus, 64 + 22 bytes, F122); record 1 = the sequence on fader 21 (start timed, keeps stepping at fader zero, F129; F118 VERIFIED in part); **record 5 = the stack under GO and the manual crossfaders** (bytes 0–1 count the steps `00 00` … `03 03`, the Live / Next pair sums to 255 in timed fades only, 18 manual samples with sums 0 … 510; F130, H76, H80); bytes 4–9 `00 00 01 00 01 00` in all five records (H74) |
| 19 | InputLevels | 515 | `00 02 00` + 512 zero bytes (E3 f28): `[00][u16 BE 512][512 levels]` by analogy with OutputLevels | OBSERVED (E3, once; bytes); HYPOTHESIS (meaning, H52); F81 **E3b:** not listed or fetched (F105) |
| 21 | DisplayContent | 41 | body[0] = 40, then 40 text-range bytes | OBSERVED; LCD reading HYPOTHESIS. **E3:** `[0x28][2 × 20]`, fader N's cell at body[2 + N], glyphs 0x20 and 0x80–0x86 (F86); bar-graph reading INFERRED (H49); length VERIFIED (F67) **E3b:** 49 payloads; row labels '25' / '37' in bank 25-48 and times 'U 5.1' / 'D 5.1' (F86 VERIFIED in part, F98, F102) **E8:** 139 payloads; new texts for the page view, STACK and the record functions; 'U 5.0' / 'D 5.0' again (F86, F102, F113, F116, F117) **E8b:** 150 payloads; the stack steps "Live: 1.0 Next: 2.0" … "Live: 3.0 Next: 4.0" with "Mem:01/02", "Mem:01/04", the crossfade bars of line 2 (body[21], body[31]) and the sign `+` at body[15] (F86, H77); the cell of a channel at full goes blank under BLACKOUT (F125) |
| 22 | LedStatus | 451 | body[0] = 75; 451 = 1 + 75 × 6 | OBSERVED; layout HYPOTHESIS (H32). **E3:** block map decoded for blocks 1–48 and the indicator blocks 53 / 55 / 58 (F84, F85; H48, H55, H56); length VERIFIED (F67) **E3b:** blocks 50 (blue during the GO crossfade), 52 (SOLO), 55 / 56 (bank) and 58 (memory mode) added (F98, F100); F84 and F85 VERIFIED in part **E8:** block 53 steady blue = BLACKOUT active (INFERRED, F114); blocks 72 / 73 = IND 1 / IND 2 (F115); blocks 57, 60, 63–67 = the key LEDs of CLEAR, STACK, COPY, REC SEQ, MAGIC, REC MEM, EDIT MEM (INFERRED, H72; F116, F117); the held-MEMS page view on blocks 1–12 and the page-12 map (F113); block 52 `FF`×6 / `0A`×6 with SOLO a second time (F100); blinking = phase A ≠ phase B inside one payload **E8b:** block 53 steady blue (VERIFIED pattern, F114) also with a level up (F125); blocks 60, 63, 64, 66, 67, 72, 73 with timed single-key presses (H72 CONFIRMED in capture; 57 and 65 INFERRED); block 50 steady blue in timed GO fades and blinking `37 37 FF 0A 0A 0A` in incomplete manual crossfades (H64, F130); block 21 `FF FF 00 FF FF 00` while fader 21 runs the sequence (F129); **blocks 25–48 `0A 00 00 0A 00 00` in all 97 payloads** (E2–E8: `00 0A 00 00 0A 00`; H78); a 456-byte read is two URBs on the bus, 448 + 8 bytes (F122) |
| 23 | ConsoleStatus | 7 | `00 00 00 00 00 01 00` in all 3 fetches | OBSERVED; meaning UNKNOWN. **E3:** body[0] changed 0x00 → 0x01 at the switch to memory mode (F87); reading "fader mode" HYPOTHESIS (H53) **E3b:** body[0] = mode (five transitions in E3 + E3b), body[4] = fader bank (F98); F87 VERIFIED in part **E8:** body[2] = memory page − 1 (0x0B on page 12; once, two transitions, F113); body[0] three more transitions (F87); body[4] not exercised **E8b:** first delivered at the first mode switch, 21.9 s (seq 1118), not at power-on; body[0] nine more transitions; body[2] = page − 1 in four timed transitions (0x0B at seq 11976 and 29992, back at seq 13733 and 34858; F113); body[4] not exercised; re-listed unchanged at the page-view openings and closings, the EDIT MEM exit and the COPY entry (F119) **E9:** `01 00 00 00 00 01 00` at connect (the console was in memory mode); body[0] 1 / 0 with the PC's FADERS MEMS / 1-24 keys, → 0 at an EDIT MEM selection and back at CLEAR; re-listed unchanged at each EDIT MEM selection and store (23 of 33 payloads equal their predecessor; F119, H53) |
| 36 | FileName | 61 | length byte 30, then UTF-16LE "SHOW04.ASC" (F60) | OBSERVED **E3b:** the value is "SHOW05.ASC", the owner's last SD save (F103) **E9:** the value is "SHOW07.ASC" (seq 72, f144), the owner's latest SD save (F103 VERIFIED, E3b + E9) **E12 2026-10-31:** "SHOW12.ASC" in all three downloads (F103) |
| 37 | ConsoleVersion | 13 | length byte 12, then ASCII "3.0.2.9.0.43" (F59) | OBSERVED (value VERIFIED, F48) |
| 38 | SystemSizes | 8 | `30 02 00 18 00 0C 04 01` (F61) | OBSERVED; H27 confirmed, H33 open |
| 39 | System | 3 | body[0] = a (0, 6, 1 seen); `00 00 00`, `06 00 00`, `01 00 00` | OBSERVED; meaning UNKNOWN **E9:** also `05 05 00`, `05 05 06`, `05 05 08` (twice) and `05 05 09`: (a, b, c) = (5, 5, slot − 1), listed when EDIT MEM selects a memory, with **c ≠ 0** in four records (bodies at seq 68349, 77862, 91079, 95553; F137); the body equals the fetch qualifiers; the meaning of a = b = 5 is UNKNOWN (H83) |
| 40 | DirtyObjects | 2 + 5n | see [packet-format.md](packet-format.md) | OBSERVED **E9:** 264 lists, 630 records, four with c ≠ 0 (F57 annotation, F137) **E12 2026-10-31:** 287 lists, 973 records = 973 fetches; two Sequence records with c ≠ 0 (F156); the full list 157 / 161 / 157 records (F157) |
| 41 | SystemSettings | 18 | `00 64 5A 3C 00 00 00 00 00 01 7F 00 00 32 00 32 00 00` | OBSERVED; body[6..11] = the six `SET` values (position only, H45, F77); rest UNKNOWN **E3b:** body[13] and [15] = 0x33, the default Up / Down times 5.1 s (E2 / E3: 0x32 = 5.0 s; F102, H45) |

IDs 1, 6, 7, 10, 11, 16, 18, 19, 20, 24–35 and 42 were never seen (F67). *(Update 2026-10-29: ID 19
InputLevels was seen in E3, row above; still unseen in E2 and E3: 1, 6, 7, 10, 11, 16, 18, 20, 24–35,
42.)* *(E12, 2026-10-31: ids 32, 34, 35 and 42 were seen PC → console only, as SmartSoft's step commands (F151); none of 24–35 or 42 was ever listed or fetched.)*

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
| Memory (0) | `[a][b][01][60][96 levels][text block]`; level raw = ceil(pct x 255 / 100), 0xFF = 100 %, 0xAB = 67 %; memory number = 24a + b + 1; body[2] meaning UNKNOWN *(E9b, 2026-10-31: body[2] = 0x01 in 86 of 86 console bodies of E2, E9 and E9b with a recorded memory and 0x00 in the 2 bodies of an emptied memory, `00 08 00 60` + 96 zero levels, seq 84076, 98692: reading it as a "recorded" flag is H87, HYPOTHESIS. The Memory body SmartSoft writes is a different layout, 90 bytes with body[3] = 0x30 and 48 levels, F143.)* | `GROUP`, `CHAN`, `$$MEMORY p f`: 16/16 memories, 768/768 levels (channels 1-48; 49-96 are 0) | f440-f504, f600-f648 (example f448) | H39, H13, H25 | F71, F72, F143, F146 |
| Text block | `[03][06]` + 18 UTF-16 code units (byte order BE is INFERRED from structure, H40 / H36; **update 2026-10-29: downgraded to HYPOTHESIS**: reading A = 18 UTF-16BE code units, reading B (CL-ML, class 5) = one pad byte then UTF-16LE lines of 12 / 12 / 11 bytes, same text for every label seen) = 3 lines x 6 characters, each line cut at its first 0; in Memory, Sequence, SequenceMain, Page, ChannelText, Independents | `$$Text 1..3` and `Text` (= `$$Text 1`): 16 memories, 1 sequence, 12 pages, 96 channels, 2 independents | f504, f656 | H40, H36 | F73 |
| Patch (5) | `[u16 512][512 x channel]`, entry d - 1 = channel at DMX address d, 0 = unpatched; no proportional level | `PATCH 1 d/c/l`: 512/512, 49 patched (incl. DMX 47 -> 50, 48 -> 48, 97 -> 24) | f424 | H34 | F70 |
| Step record (12 bytes) | `[cue x 10][CUEPARAMS][memory index 24p+f][x = 0][UP][DOWN][FOLLOWON]`, cue and times u16 *(E12: the third time is the console's "W", H14 CONFIRMED; CUEPARAMS 2 = "State", F154)* | `CUE`/`$CUE`, `$$CUEPARAMS`, `$$MEMORY`, `UP`, `DOWN`, `FOLLOWON`: 19/19 + 7/7 | f672-f816, f656 body[48..] | H41, H14, H12 | F74 F154 |
| StepContent (4) | `[a = step index][step record]` *(E12, 2026-10-31: a type-2 step is `[a][record, byte 2 = 2][60][96 levels][03 06][36 label bytes]`, 148 bytes, the console's "State" (seq 139240); F153, F154)* | as above | f672-f816 (f728, f736, f744) | H41 | F74 F153, F154 |
| SequenceMain (3) | `[steps][b][c][d][e][text block]` (8 + 38) | `$SEQUENCE 48 2 0 256 256`, 19 `CUE` | f664 | H42, H35 | F74 |
| Sequence (2) | `[page][seq][steps][b][c][d][e][text block][steps x step record]` (10 + 38 + 84) *(E12: 144 bytes = 10 + 38 + 8 × 12 with 8 steps, seq 87591; b = body[3] = the run mode as SmartSoft writes it, 0 Loop, 1 One Shot, F154)* | `$SEQUENCE 0 0 0 256 256`, 7 `$CUE`, label KOTLIK | f656 | H42 | F74 F154, F156 |
| Sequence step numbers | wire: ordinal 1.0-7.0; file: `$CUE` = memory number; console display UNKNOWN | `$CUE 265.0-271.0` (30 SD shows: 348/348, asc-format.md) | f656 | H43 | F75 |
| Independents (12) | `[2]` + 2 x `[a][b][c][d][text block]`; here `00 FF 00 01` *(E12: c = button mode, d = GM in SmartSoft's writes, F154)* | `$INDEPENDENT n 0 255 0 1` | f1592 | H44 | F76 F154 |
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
  DMX IN is UNKNOWN, F105). *(Update 2026-10-30: a page change was seen on the read side in E8, F113; the
  page-select write was not.)*

## Live objects (E8, 2026-10-30)

Source: [E8 record](captures/2026-10-30-e8-first-read-only-session.md) (JSONL `seq` of the backend's transfer
log; **no pcap frame numbers and no USBPcap capture**) and the owner's untimed statements
([captures/2026-10-30-e8-owner-statements.txt](captures/2026-10-30-e8-owner-statements.txt)). **OBSERVED (one
session) unless stated; the key identities are INFERRED; nothing here may be sent to a console, and E8 sent only
SC-1's poll and listed fetch.** Notation as in the E3 and E3b sections: "block k" = LedStatus
body[1 + 6(k − 1) .. 6k], phase A the first three bytes and phase B the last three (steady: A = B; blinking:
A ≠ B); "record N" = FaderInfo body[1 + 4(N − 1) .. 4N] with its bytes numbered 0–3. The rig is one LED bar at
DMX 5–16, nothing on DMX IN.

| Object | Layout / change (body) | Seen with | seq (no pcap frames) | Hyp. | Fact | Status |
|---|---|---|---|---|---|---|
| FaderInfo (14) | `[48][48 × 4][BUMPS][MASTER]` unchanged. **Memory mode:** record k byte 0 = byte 1 = the level of memory fader k (equal in 26 of 26 non-zero samples; byte 1 = 0 in all 15 non-zero channel-mode samples). **Byte 3 of records 1–24 = page − 1** (`00 00 00 0B` on page 12). **Record 21 byte 2 (body[83])** = 0x01 on page 1, 0x00 on page 12. BUMPS body[193] 0x00 → 0xFF → 0x00 (10 changes); MASTER body[194] moved twice. Records 25–48 never changed (record 25 stays `00 00 01 00`). Record 1 dropped `FF FF` → `00 00` in one step at 372.420 s | memory faders 1 and 2; a page change; BUMPS; MASTER; CLEAR (INFERRED) | 4168 … 4613, 18007, 18106, 7158 … 7278, 5971 … 6197, 20846 … 20918, 24706 | H46, H62 (REJECTED), H69, H70, H71, H73 | F95, F99, F112, F113, F120 | OBSERVED; BUMPS body[193] VERIFIED (F95); records 1–24 = the physical faders VERIFIED in part (F99); bytes 1–3 of a record HYPOTHESIS |
| ConsoleStatus (23) | 7 bytes. **body[2] = memory page − 1** (`01 00 0B 00 00 01 00` on page 12); body[0] three more transitions; body[4] stayed 0; body[1], [3], [5], [6] constant | a page change (page 12 and back); mode switches | 18003, 18102; 4095, 18142, 18314 | H51, H53 | F87, F98, F113 | OBSERVED (once, two transitions); meaning HYPOTHESIS, strongly supported |
| Independents (12) | `[2]` + 2 × `[a][b][c][d][text block]`: **a (body[1] for record 1, body[43] for record 2) = the on state**, 0x00 ↔ 0x01; b, c, d = `FF 00 01` constant | the IND keys (12 changes) | 6300, 6421 | H44 | F115 | OBSERVED; the reading HYPOTHESIS (supported); which key is which INFERRED |
| ChannelLevels (15), OutputLevels (13) | ChannelLevels **body[49]** (record 1) and **body[50]** (record 2) 0x00 ↔ 0xFF with the IND keys; OutputLevels **body[48] = DMX 47** with record 2 only (the show's patch: channel 50 → DMX 47, channel 49 unpatched). With a memory fader up, the memory's channels carry its stored levels scaled by the fader (`GROUP 2` at full: 48 of 48 channel bytes) | the IND keys; memory faders 1 and 2 | 6308, 6312, 6429; 4463, 4467, 4176, 4180 | H11, H26 | F83, F112, F115 | OBSERVED; H26 Independents part CONFIRMED in capture |
| LedStatus (22) | **Block 53** `00 00 FF 00 00 FF` (steady blue) while BLACKOUT is on, over the MASTER blink. **Blocks 72 / 73** `00 0A 00 00 0A 00` ↔ `00 FF 00 00 FF 00` with IND 1 / IND 2. **Block 60** `46 46 00 46 46 00` → `FF FF 00 FF FF 00` at the STACK press and `C8 C8 00 0A 0A 00` (blinking) in REC MEM and REC SEQ. **Record functions:** block 57 `FF FF FF 0A 0A 0A` blinking in all four; block 65 blinking in REC MEM only; blocks 66, 64, 63 steady in REC MEM, REC SEQ, COPY; block 67 `FF 37 37 0A 0A 0A` blinking in EDIT MEM; the bump LEDs 1–24 per function (F116). **Page view:** blocks 1–12 = the pages (current page steady full red, pages with content blinking `C8 00 00 0A 00 00`); the page-12 map has blocks 1–7 recorded. Block k `FF 00 00 FF 00 00` while memory fader k is up. Block 52 `FF`×6 with SOLO. Blocks 25–48 never changed | BLACKOUT; the IND keys; STACK; the record keys; held MEMS; memory faders; SOLO | 5820, 6165, 7483, 20754; 6304, 6425; 24971; 26861 …; 17728, 18015, 18083; 7377, 7437 | H48, H55, H56, H61, H72 | F100, F112–F117 | OBSERVED; key identities INFERRED (H72); blinking = phase A ≠ phase B inside one payload (28 of 28 screens) |
| DisplayContent (21) | Same layout. New texts: "Memory page:1" / "Memory page:12" with "Bump 1-12 to change"; "Live:14.0 Next: 1.0" with "Mem:01/01 Mem:01/01"; "Jump to step:> 1" / "Mem:01"; "Record Memory Pg:1" / "Select Bump or Stack"; "Edit Memory"; "Record Sequence" / "Select Sequence"; "Copy Memory/Seq" / "Select source". 'U 5.0' / 'D 5.0' in 139 of 139 frames | the page view; STACK; the record keys | 17732, 18087, 24967, 25057, 26861, 27116, 29110, 29266 | H49 | F86, F102, F113, F116, F117 | OBSERVED; layout VERIFIED in part (F86) |
| PlaybackInfo (17) | `[5][5 × 16]`. **Record 1 (body[1..16])** = a running 7-step sequence: bytes 0–1 the 0-based step index (0 … 6 cyclic, 207 changes), bytes 2 / 3 the Live / Next pair summing to 255 (2 340 of 2 340), bytes 10–15 three u16 times `00 0A` counting to 0 in every step; the stop at seq 17739 with `03 03` and `FF 00`. **Record 5** bytes 0–1 `01 01` → `00 00` at the STACK press. Bytes 4–9 `00 00 01 00 01 00` in all five records; records 2–4 never changed | a running sequence (before our first poll, stopped at 237.519 s); STACK | 51 … 17739, 24963 | H50, H63, H74 | F101, F117, F118 | OBSERVED; layout VERIFIED in part (F101); H63 CONFIRMED in capture for record 1 |

What E8 shows about the live objects:

- **A read-only session receives no Memory object and no ConsoleVersion** (the console lists them only after
  SmartSoft's System writes, F89, H58), which is why the memories panel stayed empty while the memory faders
  moved: their levels were in FaderInfo records 1–24 all along (F112).
- **Being listed is not being changed** (F119): 48 of 139 DisplayContent payloads, 82 of 2 340 PlaybackInfo
  payloads and 20 of the 25 later ConsoleStatus payloads equal their predecessor.
- **Blinking is one payload** (F85, F116): phase A differs from phase B inside a single LedStatus payload; no
  alternating payloads were seen in 28 screens.
- **Not exercised in E8:** BUMPS between 0x00 and 0xFF (H54), several stable MASTER positions (H60), a bump (F97),
  a level up during a MASTER move (F96), the 25-48 bank, GO with the STACK button lit, the page-select write
  (H51), InputLevels with a DMX source (H52), BLACKOUT's effect on the level objects. *(Update 2026-10-30, E8b: BLACKOUT's effect on
the level objects was tested, F125.)*

## Live objects (E8b, 2026-10-30)

Source: [E8b record](captures/2026-10-30-e8b-second-read-only-session.md) (JSONL `seq` of the backend's transfer log, `fN` of the
committed console-only pcap split where the bus was looked at), the timed action log and the owner's statements
([captures/2026-10-30-e8b-owner-statements.txt](captures/2026-10-30-e8b-owner-statements.txt)). **OBSERVED (one session) unless
stated; the key identities are OBSERVED (IND 1, IND 2, BLACKOUT, STACK) or INFERRED (the record keys); nothing here may be sent to a
console, and E8b sent only SC-1's poll and listed fetch.** Notation as in the E3, E3b and E8 sections. The rig is one LED bar at DMX
5–16, nothing on DMX IN; the show is `SHOW05.ASC` (`SHOW06.ASC` byte-identical).

| Object | Layout / change (body) | Seen with | seq / frames | Hyp. | Fact | Status |
|---|---|---|---|---|---|---|
| FaderInfo (14) | `[48][48 × 4][BUMPS][MASTER]` unchanged. **Memory mode:** record k bytes 0 and 1 equal (87 of 87 non-zero samples) and **pre-MASTER** (record 1 `94 94 00 00` at MASTER 0x76); channel mode: byte 0 post-MASTER, byte 1 = 0 (16 of 16). **Byte 3 per fader across a page change:** with fader 1 at full, page 12 selected, record 1 `FF FF 00 00` while records 2–24 read `00 00 00 0B`; at fader zero record 1 `00 00 00 0B`. Record 21 byte 2 = 1 on page 1, 0 on page 12. **BLACKOUT:** record 5 byte 0 `FF` → `00` and back. A bump held at BUMPS 0x81: record 2 `81 81 00 00`. BUMPS 0x00 → 0x81 → 0x00. Records 25–48 never changed | MASTER at half, a page change with a fader up, BLACKOUT with channel 5 at full, bump 2 at BUMPS 0x81, memory faders 1, 2, 21 | 36521, 29996, 33741, 26987, 27762, 40318, 40074 … 40144 | H46, H69, H70, H71, H73 | F95, F99, F125, F126, F127 | OBSERVED; H69 CONFIRMED; H71 HYPOTHESIS (narrowed); F112, F113 VERIFIED for their structure |
| ChannelLevels (15), OutputLevels (13) | Memory-mode levels are **post-MASTER** (0x45 at fader 0x94 and MASTER 0x76, 0x94 at MASTER 0xFF). **BLACKOUT:** ChannelLevels[5] and OutputLevels[6] `FF` → `00` and back. **Stored × BUMPS / 255** with a held bump at 0x81. ChannelLevels body[49] / [50] = IND 1 / IND 2 and OutputLevels[48] = DMX 47 with IND 2 (named keys). Channels 37, 38, 45 (OutputLevels[38], [39], [46]) with the sequence on fader 21; channels 7, 8, then `GROUP 2`, then channel 2 under the stack | MASTER at half, BLACKOUT, bump 2, the IND keys, fader 21, GO and the crossfaders | 36536, 37591, 26991, 26995, 40326, 15624, 16387, 16391, 50154, 50158, 58670 … 60849 | H47, H54, H60 | F96, F97, F115, F125, F126, F129, F130 | OBSERVED; the MASTER scaling and bump output CONFIRMED in capture (H47, H54 also at 0x81) |
| LedStatus (22) | Block 53 `00 00 FF 00 00 FF` steady with BLACKOUT (also with a level up). Blocks 60 (STACK), 63 (COPY), 64 (REC SEQ), 66 (REC MEM), 67 (EDIT MEM), 72 / 73 (IND 1 / 2) change with the key pressed alone. **Block 50** steady blue in timed GO fades, `37 37 FF 0A 0A 0A` while a manual crossfade is incomplete. Block 21 `FF FF 00 FF FF 00` while fader 21 runs the sequence. Blocks 1 and 5 blink while their fader is out of step (memory mode `46 00 00 0A 00 00`, or `C8 00 00 0A 00 00` over the page-12 map; channel mode `00 46 00 00 0A 00`; H79). **Blocks 25–48 `0A 00 00 0A 00 00` in all 97 payloads** (E2–E8 `00 0A 00 00 0A 00`) | BLACKOUT, the IND keys, STACK, the record keys, GO, fader 21, CLEAR, a page change | 14512, 26999, 15620, 16395, 56728, 17961, 18923, 19943, 20871, 58666 … 60305, 50150, 43217, 45109, 48211, 30022 | H56, H61, H64, H72, H78, H79 | F114, F115, F116, F117, F124, F128, F129, F130 | OBSERVED; block 53 pattern VERIFIED (F114); H72 CONFIRMED in capture for 60, 63, 64, 66, 67, 72, 73 (identity INFERRED for 63, 64, 66, 67; 57, 65 INFERRED) |
| DisplayContent (21) | New texts: "Live: 1.0 Next: 2.0" / "Mem:01/01 Mem:01/02", "Live: 2.0 Next: 3.0" / "Mem:01/02 Mem:01/04", "Live: 3.0 Next: 4.0" / "Mem:01/04 Mem:01/02" (the stack's cues and memories as `$SEQUENCE 48` of `SHOW06.ASC`); the crossfade bars of line 2 (body[21] 0x86 → 0x80 Live, body[31] 0x80 → 0x86 Next); the sign **`+` at body[15]** while the sequence on fader 21 put out channels 37, 38, 45; "25" / "37" row labels at power-on; 'U 5.0' / 'D 5.0' throughout | GO, the crossfaders, fader 21, the power-on state | 59043, 59622, 60293, 50162, 52081, 13 | H49, H77 | F86, F102, F124, F129, F130 | OBSERVED; the `+` HYPOTHESIS (H77) |
| ConsoleStatus (23) | 7 bytes. First delivered at the first mode switch (21.9 s), not at power-on. body[0] nine more transitions; body[2] = page − 1 in four timed transitions; body[4] untouched. Re-listed unchanged at the page-view openings and closings, the EDIT MEM exit and the COPY entry | mode switches and page changes | 1118, 1649, 11976, 13733, 29992, 34858 | H53 | F87, F113, F119 | OBSERVED; meaning HYPOTHESIS, strongly supported |
| PlaybackInfo (17) | `[5][5 × 16]`. **Record 1** = the sequence on fader 21: started at step 0 by raising the fader (`05 05 FF 00 …` → `00 00 E0 1F …`), 129 step changes in order, the pair summing to 255 in 1 305 of 1 305 payloads, still stepping at fader zero until the end of the session. **Record 5** = the stack: bytes 0–1 `00 00` → `01 01` → `02 02` → `03 03` at each completed fade, reset of the pair to `FF 00`, times `00 32` → `00 0A` in 1-s steps in timed fades; under the manual crossfaders byte 2 (Live) falls with A and byte 3 (Next) rises with B independently (18 samples, sums 0 … 510). Bytes 4–9 `00 00 01 00 01 00` in all five records | fader 21 up and down, STACK lit, GO × 2, the crossfaders | 11984, 50146, 61110, 58658, 59036, 59603, 59880 … 60289 | H50, H63, H74, H76, H80 | F101, F118, F129, F130 | OBSERVED; record 1 and the layout VERIFIED in part (F101, F118); H76, H80 HYPOTHESIS |
| SequenceMain (3) | 46 bytes `13 02 00 00 01 00 01 00 03 06` + 36 zero bytes, **listed by the console at each GO** together with PlaybackInfo (lists [17, 3, 22, 13, 15, 21] and [3, 17, 22, 15, 13, 21]); byte-identical to the SmartSoft downloads' | GO with STACK lit | 58654, 58662, 59236, 59240 | H58 | F74, F130 | OBSERVED (a second sighting outside SmartSoft's download: E3b listed it at its GO crossfade) |
| Independents (12) | Record byte 0 (body[1] / body[43]) `00` ↔ `01` with IND 1 / IND 2, bytes 1–3 `FF 00 01` constant; first payload of the session at the first IND press | the IND keys, named and timed | 15616, 16383 | H44 | F76, F115 | OBSERVED; key identities OBSERVED |

What E8b shows about the live objects:

- **The power-on list is 17 bytes and the other objects wait** (F124): `00 00 0C 00 28 00 02 16 00 00 00 00 15 00 00 00 00` (LedStatus,
  DisplayContent, pseq 0x00), byte-identical to E3b session 1's; ConsoleStatus, FaderInfo, the level objects, PlaybackInfo,
  Independents and SequenceMain arrive with the first action that changes them.
- **WinUSB splits long reads, and no read was a multiple of 64** (F122): a payload of 86, 90, 200, 456 or 519 bytes is two URBs on
  the bus (64 + 22, 64 + 26, 192 + 8, 448 + 8, 512 + 7); a decoder works on the concatenated record; H75 stays untested.
- **Being listed is not being changed** (F119): 38 of 150 DisplayContent, 35 of 1 305 PlaybackInfo and 12 of 26 ConsoleStatus payloads
  equal their predecessor; every CLEAR series re-listed DisplayContent unchanged.
- **Not exercised in E8b:** BUMPS at levels other than 0x81, the 25-48 bank, SOLO, a bump below the fader's level (H71), a known source
  on DMX IN (H52), RATE and the Fade % (H74), the page-select write (H51), PAUSE (the owner's description is a statement; no PAUSE press
  exists in any capture), and what stops a running sequence.

## Live objects (E9, 2026-10-30, late evening)

Source: [E9 record](captures/2026-10-30-e9-smartsoft-writes.md) (JSONL `seq` of the normalized capture, `fN` of the committed
console-only pcap split), the guided script's timed action log and the owner's statements
([captures/2026-10-30-e9-owner-statements.txt](captures/2026-10-30-e9-owner-statements.txt)). SmartSoft Online, driven through its Console
Simulator, **passive: our software sent nothing; the PC events are SmartSoft's.** **OBSERVED (one session) unless stated; nothing here
may be sent to a console.** Notation as in the E3, E3b, E8 and E8b sections. The rig is one LED bar at DMX 5–16, nothing on DMX IN;
the show is the one of `SHOW07.ASC` (= `SHOW04.ASC` … `SHOW06.ASC`) at the start.

| Object | Layout / change (body) | Seen with | seq / frames | Hyp. | Fact | Status |
|---|---|---|---|---|---|---|
| FaderInfo (14) | `[48][48 × 4][BUMPS][MASTER]` unchanged. **Record 1 byte 0 (body[1])** = the PC's channel fader 1 (0x1F, 0x5D, 0xAA, 0xE7, 0xFF) with the physical fader 1 at zero; **body[194] = MASTER** 0xFF → 0x7E → 0xFF (event index 0x30); **body[193] = BUMPS** 0xFF → 0x00 → 0xFF (index 0x31); a held bump 7 → body[25] 00 → FF; memory bump 1 → body[1], [2] 00 → FF. Not listed while a fader edited a memory inside EDIT MEM (LED block and LCD only) | the Simulator's fader 1, MASTER, BUMPS; bumps 7 and 1; EDIT MEM | seq 9684 … 9830, 24124 … 24457, 29443 … 29674, 19066, 39603, 78905 … 79676 | H46, H81 | F95, F97, F132, F134, F135 | OBSERVED; BUMPS = body[193] and the bump bytes VERIFIED (F95, F97) |
| ChannelLevels (15), OutputLevels (13) | Channel 1 follows the PC's fader 1 (ChannelLevels[1], OutputLevels[2] = DMX 1); a held bump 7 sets ChannelLevels[7] / OutputLevels[8] to FF and memory bump 1 sets [7], [8] / [8], [9], all back to 00 at release; a PC fader moved inside EDIT MEM or after leaving it changed no level object | fader 1; bumps 7 and 1; fader 7 in and after EDIT MEM | seq 9690, 19062, 19070, 39614, 39618, 69078 … 69101 | H47, H54, H81 | F97, F134, F135 | OBSERVED; the bump bytes VERIFIED for BUMPS 0xFF (F97) |
| LedStatus (22) | Block 1 G steady `00 FF 00 00 FF 00` with the PC's fader 1 at full (not blinking although the physical fader is at zero). Blocks 50, 52, 53, 55, 57, 58, 63, 66, 67 and the bump blocks 1, 7, 9, 10 change with the named PC key codes (code = block − 1, F133). Block 5 `46 00 00 0A 00 00` blinking in memory mode with channel fader 5 up; block 7 `00 46 00 00 0A 00` blinking while the PC's fader 7 came down unmatched; block 57 blinking inside a record function and steady when CLEAR left it. **Blocks 25–48 `0A 00 00 0A 00 00` in all 101 payloads** | the Simulator's keys and faders | seq 9812, 33551, 35238, 36395, 37736, 43881, 44335, 73019, 75740, 69078 … 69101 | H56, H64, H72, H78, H79, H82 | F100, F114, F116, F133, F135 | OBSERVED; H72 supported and extended; key identities as in H72 |
| DisplayContent (21) | New texts: "Edit:" / "01/01", "01/07", "01/09", "01/10" after EDIT MEM and a bump; "Copy 01/09" / "Select target" after COPY and the source bump; "SmartFade Menu" on line 2 for about 1.4 s at an EDIT MEM store; the cell of channel 1 (body[3]) 0x84 → 0x86 with the PC's fader 1 at full; the bump cells 0x86 while held | EDIT MEM, COPY, fader 1, bumps | seq 67124, 68364, 77878, 91087, 95568, 83198, 67562 → 67655, 9801, 19078 | H49 | F86, F116, F136 | OBSERVED; layout VERIFIED in part (F86) |
| ConsoleStatus (23) | 7 bytes. body[0] 1 / 0 with FADERS MEMS (0x39) / FADERS 1-24 (0x36); 0 at an EDIT MEM selection and back at CLEAR; `01 00 00 00 00 01 00` at connect; re-listed unchanged at each EDIT MEM selection and store (23 of 33 payloads equal their predecessor) | MEMS, 1-24, EDIT MEM, CLEAR | seq 76, 39479, 39485, 41458, 41462, 77858, 80947; re-listings 67105 … 67113, 68345 … 68353, 77866, 91075 … 91083, 95549 … 95557 (selections), 67558, 68838 (stores) | H53 | F87, F119 | OBSERVED; meaning HYPOTHESIS, strongly supported |
| System (39) | Body `05 05 c` = (a, b, c) = (5, 5, slot − 1) of the memory selected for editing, **listed by the console at the EDIT MEM selection** and fetched with those qualifiers; **c ≠ 0 in four records** (`05 05 06`, `05 05 08` twice, `05 05 09`), `05 05 00` for memory 1; the meaning of a = b = 5 is UNKNOWN | EDIT MEM and a bump (memories 1, 7, 9, 9, 10) | seq 67101 / 67109, 68341 / 68349, 77854 / 77862, 91071 / 91079, 95545 / 95553 | H83 | F137 | OBSERVED once (PC keys only) |
| Memory (0) | Listed by the console itself after a recording: [Memory, 22, 21] (REC MEM and bump 9), [Memory, 23, 21, 22] (EDIT MEM stores), [Memory, 22, 21] (COPY target); body `[a][b][01][60][96 levels][text block]`, 138 bytes: channel 5 = 0x79 for memories 9 and 10; memory 1 with channel 7 = 0x7F; memory 7 with channel 7 = 0xFF added to channel 15 = 0x15 | REC MEM + bump 9, EDIT MEM stores (memories 1, 7), COPY to bump 10 | seq 74003 / 74007, 67548 / 67552, 68828 / 68832, 84664 / 84668 | H25, H58 | F136, F139 | OBSERVED once |
| PlaybackInfo (17), SequenceMain (3) | GO with STACK unlit from the PC's GO key: list [3, 22], SequenceMain unchanged (46 bytes), PlaybackInfo record 5 Live / Next `FF 00` → `F7 08` … `02 FD` → `FF 00` with the times counting down from 5.0 s, bytes 0–1 `02 02` throughout, block 50 blue | the Simulator's GO key | seq 43873, 43877, 43888 … 44324 | H50, H64, H76 | F101, F130 | OBSERVED; layout VERIFIED in part (F101) |
| FileName (36) | "SHOW07.ASC" (61 bytes) | the owner's SD save at 22:18:26 | seq 72 (f144) | | F103 | **VERIFIED** (E3b + E9) |
| ChannelText (9) | 96 objects in the download; SmartSoft's three writes of channel 7's label (the whole 39-byte object) were not listed back | the label step | seq 468 (f936); writes seq 57437, 57881, 60605 | H40, H84 | F138 | OBSERVED once; the console's stored block after a non-ASCII write unobserved (H84) |

What E9 shows about the console's objects:

- **Writes are not echoed** (F131): ConsoleEvent (20) was never listed or fetched, a written ChannelText was not listed back, and the
  reply to the poll after a write is an ordinary idle reply or list; the effect of a write shows only in the state objects.
- **A PC event produces the same objects and bytes as the physical control** (F134): FaderInfo, the level objects, an LED block, an LCD
  cell, ConsoleStatus or PlaybackInfo, 16–65 ms after the event; the physical faders do not move.
- **The console lists a recorded memory itself** (F136): the Memory object, 138 bytes, after REC MEM and a bump, an EDIT MEM store and a
  COPY target (in SmartSoft's session; untested for a host that never sent the System pair).
- **System (5, 5, slot − 1) at an EDIT MEM selection carries c ≠ 0** (F137, H83).
- **Being listed is not being changed** (F119): ConsoleStatus is re-listed unchanged at every EDIT MEM selection and store.
- **Not exercised in E9:** the MASTER scaling with a level up and BUMPS at an intermediate level (all levels were 0 at those steps),
  STACK, the IND keys, REC SEQ, the wheel, a page change, the crossfaders, PAUSE, a known source on DMX IN (H52), the Mem Edit tab.

## Live objects (E9b, 2026-10-31)

Source: [E9b record](captures/2026-10-31-e9b-smartsoft-writes-repeat.md) (JSONL `seq` of the normalized capture, `fN` of the committed
console-only pcap split), the guided script's timed action log and the owner's statements
([captures/2026-10-31-e9b-owner-statements.txt](captures/2026-10-31-e9b-owner-statements.txt)). SmartSoft Online, three sessions through its Console
Simulator after a console power cycle, **passive: our software sent nothing; the PC events are SmartSoft's.** **OBSERVED (one session) unless
stated; nothing here may be sent to a console.** Notation as in the E3, E3b, E8, E8b and E9 sections. The rig is one LED bar at DMX 5–16,
nothing on DMX IN; the show at the start is the one of `SHOW08.ASC`, which still carried E9's edit of memory 7 (F139).

| Object | Layout / change (body) | Seen with | seq / frames | Hyp. | Fact | Status |
|---|---|---|---|---|---|---|
| FaderInfo (14) | `[48][48 × 4][BUMPS][MASTER]` unchanged. **Record 5 byte 0 (body[17])** = the PC's channel fader 5 (index 0x04) or, after a takeover, the physical fader 5; in channel mode the record is the console's level after a PC event or a physical takeover, post-MASTER (fader 0x7C at MASTER 0x86 gave 0x41); body[194] = the MASTER (the PC's 0xFF → 0x80 → 0xFF; the console's own 0xFF → 0x86), body[193] = BUMPS. Memory mode: bytes 0 and 1 equal in 11 of 11 records; one channel-mode exception, record 9 `00 FF 00 00` with record 5 `FF 00 00 00` (seq 95014) | the Simulator's fader 5 (index 0x04), MASTER (0x30), BUMPS (0x31); the physical faders 1 and 5 and the MASTER | seq 32850 … 32999, 39747, 42968 … 43271, 45477, 76489 … 76687, 82071 … 82161, 95014 | H71, H81, H90 | F96, F112, F141, F145 | OBSERVED (the layout VERIFIED earlier) |
| ChannelLevels (15), OutputLevels (13) | Channel 5 follows the PC's fader 5 (ChannelLevels[5], OutputLevels[6] = DMX 5) and, after a takeover, the physical fader; post-MASTER 0x41 at fader 0x7C and MASTER 0x86; a held bump 7 and memory bump 1 as in E9 (F97); IND 1 / IND 2: ChannelLevels body[49] / [50] 00 ↔ FF, and DMX 47 = 0xFF (OutputLevels body[48]) with IND 2 only; memory fader 9 raised to full drove ChannelLevels[5] and DMX 5 to 0xFF after the Memory write | fader 5, the MASTER, bumps 7 and memory bump 1, the IND keys, memory fader 9 | seq 42968, 76672, 76676, 11401 … 11417, 24896 … 24915, 49365, 50187, 94919 / 94923 | H60 | F96, F97, F115, F143, F145 | OBSERVED |
| LedStatus (22) | Block 56 `00 0A 00 …` → `00 FF 00 00 FF 00` with FADERS 25-48 (block 55 dim); block 60 `46 46 00 46 46 00` → `FF FF 00 FF FF 00` with STACK; blocks 72 / 73 `00 0A 00 …` → `00 FF 00 00 FF 00` with IND 1 / IND 2; block 53 `00 00 FF 00 00 FF` with BLACKOUT; block 52 `FF`×6 with SOLO; block 50 blue during a GO fade; memory 9's key `46 00 00 46 00 00` (recorded) and `0A 00 00 0A 00 00` (empty); the console's EDIT MEM screen (recorded memories `C8 00 00 0A 00 00` blinking, empty slots `46 00 00 46 00 00`, block 57 blinking, block 67 `FF 37 37 0A 0A 0A`) and the REC MEM screen as in E8b and E9. **Blocks 25–48 `0A 00 00 0A 00 00` (dim red) in every payload** after a channel-mode power-on | FADERS 25-48, STACK, IND 1, IND 2, BLACKOUT, SOLO, GO, REC MEM and EDIT MEM (PC keys and the console's own); the power-on | seq 51634, 47249, 48208, 49371, 50195, 19750, 22172, 28421, 85061, 54611, 78980, 18 | H64, H72, H78, H82 | F84, F98, F100, F114–F117 | OBSERVED (block 56 identified once with a named PC key) |
| DisplayContent (21) | New texts: "Edit:" / "12/01" (page 12, seq 58747) beside "01/01" (seq 54810); "Memory page:12" / "Bump 1-12 to change" (seq 58420); the rows "25" / "37" with the 25-48 bank (seq 51630); the stack view "Live:14.0 Next: 1.0" / "<6>Mem:01/01 Mem:01/01" (seq 47245), as E8b's; "Record Memory Pg:1" / "Select Bump or Stack" (seq 78974, 87401, 98620) and "Edit Memory" / "Select Bump or Stack" (seq 54604, 58578) | the console's EDIT MEM and a page change on the console, FADERS 25-48, STACK, REC MEM | seq 58747, 54810, 58420, 51630, 47245, 78974, 54604 | | F113, F116, F117, F142 | OBSERVED |
| ConsoleStatus (23) | 7 bytes. **body[4] 0 → 1** (`00 00 00 00 01 01 00`, seq 51626) with the 25-48 bank from a PC key and back with FADERS 1-24; body[2] = 0x0B at the page change to page 12 on the console (`01 00 0B 00 00 01 00`, seq 58408) and, in channel mode for the edit, `00 00 0B 00 00 01 00` (seq 58735); body[0] 1 / 0 with FADERS MEMS / 1-24 (seq 24824 / 24832, 26924 / 26930); re-listed unchanged at the console's EDIT MEM selection on page 1 | FADERS 25-48 and 1-24 from the PC, the page change and EDIT MEM on the console | seq 51626, 58408, 58735, 24824, 26924 | H53 | F87, F98, F113, F119 | OBSERVED |
| System (39) | Body `05 05 00` = (a, b, c) = (5, 5, 0), **listed by the console at its own EDIT MEM selection of memory 1**, on page 1 (seq 54790 / 54798) and on page 12 (seq 58731 / 58739): the record does not carry the page; 11 System records in all (9 in the three downloads, 2 at EDIT MEM selections), none with c ≠ 0 | EDIT MEM and the bump under memory 1 on the console | seq 54790 / 54798 (f109580 / f109596), 58731 / 58739 (f117462 / f117478) | H83 | F137, F142 | OBSERVED (twice) |
| Memory (0) | Listed by the console after a recording made with the PC's keys (seq 79062 / 79066, 84072 / 84076) and with its own keys (seq 87480 / 87484, 98688 / 98692): body `[page − 1][slot − 1] [flag] 60` + 96 levels + the label block, 138 bytes; flag 0x01 for a recorded memory (channel 5 = 0x41, the post-MASTER level, after the PC's REC MEM; 0x7F after the console's), **0x00 and 96 zero levels for an emptied memory** (`00 08 00 60` + zeros). The body SmartSoft writes is a different layout (90 bytes, body[3] = 0x30, 48 levels; F143) | REC MEM and bump 9 from the PC and from the console; REC MEM of an empty look | seq 79066 (f158132), 84076 (f168152), 87484 (f174968), 98692 (f197384) | H85 (REJECTED), H86, H87 | F143, F145, F146, F147 | OBSERVED |
| PlaybackInfo (17), SequenceMain (3) | GO with STACK unlit from the PC's GO key: list [3, 22], SequenceMain unchanged (46 bytes, seq 28417), PlaybackInfo record 5 `02 02 F5 0A` … → `02 02 FF 00` (seq 28428 … 28869); **STACK from the PC: record 5 bytes 0–1 `02 02` → `00 00`** (seq 47241), and they stayed `00 00` after STACK off; the first PlaybackInfo after the power-on read `02 02` (seq 14) and was pending at connect | the PC's GO and STACK keys | seq 28417, 28428 … 28869, 47241, 14 | H76 | F101, F117, F124 | OBSERVED |
| Independents (12) | Record byte 0 (body[1] for IND 1, seq 49365; body[43] for IND 2, seq 50181) followed the PC's IND keys 0x47 / 0x48 like the console's own keys | IND 1 and IND 2 from the PC | seq 49365, 50181 | H72 | F115 | OBSERVED |
| FileName (36) | "SHOW08.ASC" in all three downloads (the owner's SD save of 08:35:31, before the capture) | the backup logged as "backup taken: SHOW08.ASC" | seq 792 (f1584), 65461 (f130922), 70848 (f141696) | | F103 | **VERIFIED** (E3b + E9, a third capture in E9b) |
| ChannelText (9) | 96 objects in each download. Channel 7's label (ChannelText 6) came back as written after each of SmartSoft's two writes and after E9's: `06 03 06 00 00 00 61 00 6A` + zeros (seq 424, f848), `06 03 06 01 0C 00 61 00 6A` + zeros (seq 65093, f130186), the residue again (seq 70480, f140960); nothing was listed after the writes | SmartSoft's "Čaj" write and its restore, across two restarts and a power cycle | seq 424, 65093, 70480 | H40, H84 | F138, F144 | OBSERVED (three round trips) |

What E9b shows about the console's objects:

- **Writes are not echoed, the Memory write included** (F131, F143): ConsoleEvent (20) was never listed or fetched, and neither the
  Memory (0) write nor the two ChannelText writes produced a listing; the effect of a write shows only in the state objects, or in the next download.
- **A PC event produces the same objects and bytes as the physical control, and the last mover wins** (F134, F141): a PC fader sets the
  console's record at once, a physical fader takes over at once from a PC-set level, and nothing in any object tells the two apart.
- **The console stores a label write verbatim** (F144): the next download returns the 39 bytes as written, across SmartSoft restarts and a
  power cycle; the SD file does not carry a label block's residue.
- **The console lists a recorded memory itself, whoever recorded** (F136, F147): after the PC's REC MEM and bump and after the console's
  own; an emptied memory is listed with byte 2 = 0 and 96 zero levels (F146). **System (5, 5, 0) at the console's own EDIT MEM** (F142).
- **Being listed is not being changed** (F119): ConsoleStatus was re-listed unchanged at the console's EDIT MEM selection on page 1.
- **Not exercised in E9b:** the MASTER at several stable positions and BUMPS at intermediate levels (H60, H54), the crossfaders and PAUSE, a
  known source on DMX IN (H52), the page-select write (H51), EDIT MEM store, CLEAR and COPY from the PC, the Playback tab, the wheel, a
  bump in bank 25-48 (H82), and an EDIT MEM selection of a memory other than memory 1 (c ≠ 0 from the console's own key, H83).

## Objects after edits (E12, 2026-10-31)

Source: [E12 record](captures/2026-10-31-e12-smartsoft-patch-stack-edits.md) (JSONL `seq` of the normalized capture, `fN` of the committed console-only pcap split), the
guided script's timed action log and the owner's statements ([captures/2026-10-31-e12-owner-statements.txt](captures/2026-10-31-e12-owner-statements.txt)).
SmartSoft Online, three sessions after a console power cycle, **passive: our software sent nothing; the writes are SmartSoft's.**
**OBSERVED (one capture) unless stated; nothing here may be sent to a console.** Notation as in the sections above; Wn = the n-th data
write of the capture.

| Object | Layout / change (body) | Seen with | seq / frames | Hyp. | Fact | Status |
|---|---|---|---|---|---|---|
| DirtyObjects (40) | After each SmartSoft step command, on the next poll: [3, 4:index …] for the stack; **(2, 0, 0, 6), c = 6**, for KOTLIK (list body `00 01 02 00 00 00 06`); nothing after any of the 46 object writes; at every wheel detent of the console's own patch and step-timing edits Patch (5) or [3, 3, 4:21]; the 161-record list after the edits | SmartSoft's InsertStep, DeleteStep, MoveStep, id 42; the console's menus | seq 30124, 65959, 79658, 87587 (f175174), 169123 (f338246), 175914, 175943, 148220, 155735, 139012 (f278024) | H101, H102, H106 | F151, F155, F156, F157 | OBSERVED |
| Sequence (2) | 144 bytes (8 steps) after the KOTLIK insert: record 6 = the console's default (a copy of the displaced step's memory, 0x010E, 5.0 / 5.0), record 7 = the old 7th step renumbered 8.0; 132 bytes after the delete = download 1's; body[3] = 1 (One Shot) and the label KOTLIX in download 2 | InsertStep, DeleteStep, SmartSoft's Sequence writes | seq 87591 (f175182), 169127 (f338254), 139152 (f278304) | H35, H42, H43, H95, H103 | F152, F154, F156 | OBSERVED |
| SequenceMain (3) | body[0] = the step count, 0x13 → 0x14, 0x15, 0x16 and back; listed twice before each StepContent in the console's own Step Timing lists | step commands; the console's Step Timing | seq 30128, 61498, 65963, 79662; 155735 … 155998 | H35 | F151, F155 | OBSERVED |
| StepContent (4) | Default step after InsertStep `[index] [cue × 10] 00 00 00 00 00 32 00 32 00 00` (type 0, memory 0, 5.0 / 5.0); a record of zeros at the old last index after a delete; **148 bytes for a type-2 "State" step** `[index][record][60][96 levels][03 06][36]`; the console's own Step Timing changed the Up time (body[8]) in 11 steps | InsertStep, DeleteStep, the Direct cue, the console's Step Timing | seq 30132 (f60264), 30477 (f60954), 139240 (f278480), 175932 (f351864), 155747 … 155935 (f311870) | H12, H41, H98 | F152, F153, F154, F155, F156 | OBSERVED |
| Patch (5) | DMX 100 = 47 and DMX 101 = 49 (IND 1) in download 2 (= W10); DMX 102 through 39 channel values in the console's Blind Patch by dimmer, 0 after Unpatch dimmer; download 3 = download 1 | SmartSoft's Patch writes; the console's patch menu | seq 139032 (f278064), 148137 … 148462 (f296924), 151667 (f303334), 194292 (f388584) | H34, H91, H92 | F153, F154, F155 | OBSERVED |
| Memory (0) | Recorded by the console's REC MEM in B1: `00 08 01 60` + channel 5 = 0x85; in download 2 = SmartSoft's W46 in this layout (channel 5 = 0xFF, label "T"); emptied in I3: `00 08 00 60` + zeros, and absent from download 3's list | REC MEM on the console; SmartSoft's Memory writes | seq 38719 (f77438), 139072 (f278144), 188544 (f377088), 194272 (f388544) | H87, H99 | F143, F146, F147, F157 | VERIFIED (F143, F147; F146 in part; E9b + E12) |
| Page (8) | Page 0 = "T" in download 2 (= W47, the owner's slip), empty again in download 3 | SmartSoft's Page writes | seq 139036 (f278072) | H99 | F153 | OBSERVED |
| ChannelText (9) | ChannelText 6 = `06 03 06 00 00 00 61 00 6A` + zeros (the block of E9b's last write) in all three downloads, after another power cycle | — | seq 422 (f844), 139272 (f278544), 194516 (f389032) | H40, H84 | F144 | VERIFIED in part (F144: kept across a power cycle and returned byte for byte) |
| ConsoleStatus (23), LedStatus (22) | Memory mode from SmartSoft's FADERS MEMS (W25) to the end of the capture: `01 00 00 00 00 01 00`, block 58 lit; blocks 25–48 dim green `00 0A 00 00 0A 00` in every payload after a channel-mode power-on (INFERRED mode) | FADERS MEMS from SmartSoft's edit tab (unlogged); the power-on | seq 77596, 77600; `blk2548.py` | H53, H78 | F158 | OBSERVED |
| DisplayContent (21) | The console's menus: "Patch by Dimmer", "Patch mode:>Blind", "D:>101 C: i1", "Unpatch dimmer", "Playback: XF", "Step Timing", "XF Step:>14.1 State", "U 3.0 D 4.0 W 2.0" | the console's patch and step-timing menus | seq 145414 … 156014 | H14, H49 | F148, F149, F154, F155 | OBSERVED |
| FileName (36) | "SHOW12.ASC" in all three downloads (the backup saved before the session) | the backup | seq 790 (f1580), 139640 (f279280), 194884 (f389768) |  | F103 | **VERIFIED** (E3b + E9; E12 again) |

What E12 shows about the console's objects:

- **Object writes are not echoed; step commands are answered by lists of what they changed** (F151, F153): no list followed any of
  the 46 object writes, and every step command was followed on the next poll by the affected SequenceMain / StepContent records or
  by Sequence (2, 0, 0, 6); no list names a command.
- **The console stores what SmartSoft writes, and its full list follows the show** (F153, F157): download 2 equals the last writes
  in the console's layout and holds 161 records (memory 9 and three stack steps added); after the restores the list is the 157-record
  one again, and an emptied memory is not named.
- **Two ids have content-dependent lengths, and a Sequence record can carry c ≠ 0** (F156): a decoder must take the length from the
  object and must not assume c = 0 (SC-1's read session faults instead, a safe stop, INFERRED from the code).
- **The console lists its own menu edits at every wheel detent** (F155): a Blind patch changes the patch, and lists it, while the
  wheel scrolls.
- **Not exercised in E12:** a channel step (type 0), Manual, a console-side insert / delete / move (H106), the console's reaction to
  the Independents values (H93), a "č" in a label, the SD form of any edited object.
## Label text

Labels follow two constant bytes `03 06` as `00 xx` pairs; byte order was undetermined (H36).
*Update (SHOW04.ASC cross-check): UTF-16BE, 3 x 6 characters, INFERRED from structure and matched to the
file (F73, H40); see the text block row above.* **Update 2026-10-29:** the byte order is **HYPOTHESIS**, not
INFERRED: a second reading (CL-ML, class 5) fits the same 38 bytes (one pad byte, then UTF-16LE lines of
12 / 12 / 11 bytes) and gives the same text for every label seen; the text match with the file stays
OBSERVED. A "č" label and a 6-character third line decide it (H40).
