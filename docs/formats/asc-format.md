# SmartFade ASC show file format

> Kept in `docs/formats/`, separate from `docs/protocol/`, because the show file format is a
> different subject from the hardware communication protocol (project brief §88).

## Basis

- **DOCUMENTED:** SmartFade console manual v3.0 p.6: "The file format is ASCII Text
  Representation for Lighting Console Data. This format is compatible with all systems that
  follow this standard. Although there may be discrepancies between systems… See www.usitt.org."
- The file header `IDENT 3.0` matches USITT ASCII version 3.0 (published 1992).
  **PROBABLE.** We do not yet have the text of the standard. Keyword semantics below that cite
  USITT are unverified until the spec is obtained.
- USITT ASCII allows manufacturer-specific keywords with a `$` prefix (and `$$` for sub-keywords).
  SmartFade uses this heavily. The prefix convention is INFERRED from the files, and the
  standard text is not checked yet.

## Samples analysed

| File | Origin | `$SOFTWAREVERSION` | `CONSOLE` | Lines |
|---|---|---|---|---:|
| `example lighting output file - Ostrov - 16.09.2026.asc` | User, real production | `3.0.2 43` | `SMARTFADE` | 1 160 |
| `Demoshow-SmartFade1248-1296.asc` | ETC, in installer | `2.0.0b20` | `SMARTFADE` | 965 |
| `Demoshow-SmartFade2496.asc` | ETC, in installer | `2.0.0b20` | `SMARTFADE WIDE` | — |
| `Demoshow-SmartFadeML.asc` | ETC, in installer | `2.1.0b20` | `SmartFadeML` | — (different dialect, out of scope) |
| `smartfade-sd-card-backup/SHOW04.ASC` | Console, saved to SD 2026-10-28 (fw 3.0.2.9.0.43), SHA-256 `559636399c1d82e0f95e737c961bfee31f88962fe8c486bbc6eba7f7d2177a90`, 15 944 bytes | `3.0.2 43` | `SMARTFADE` | 1 153 |
| `smartfade-sd-card-backup/SHOW05.ASC` | Console, saved to SD 2026-10-30 (fw 3.0.2.9.0.43), the owner's backup before E3b; **byte-identical to SHOW04.ASC** (same SHA-256 `559636399c1d82e0f95e737c961bfee31f88962fe8c486bbc6eba7f7d2177a90`, 15 944 bytes, `cmp` finds no difference) | `3.0.2 43` | `SMARTFADE` | 1 153 |
| `smartfade-sd-card-backup/SHOW01/02/03/10/11/99.ASC` | Console (SD card, owner) | `3.0.2 43` | `SMARTFADE` | 828 / 1 079 / 1 276 / 1 192 / 898 / 923 |
| 23 named `*.asc` shows in the same folder (+ `SMRTFADE.SRC`) | Owner's older shows (origin per file UNKNOWN) | `3.0.2 43` (3 files `3.0.1 41`) | `SMARTFADE` | 804 to 1 436 |

SHOW04.ASC is the only file correlated with live console objects (E2 capture, see
"Cross-check against the E2 capture" below). The other SD files are used here only for keyword
inventory and line-ending comparison. *(Update 2026-10-30: the E3b capture was cross-checked against SHOW05.ASC,
which is byte-identical to SHOW04.ASC, so it is the same show and not a second one.)*

The 1248/1296 demo and 2496 demo have identical keyword counts and differ in the `CONSOLE`
line. **The 1248 and 1296 cannot be told apart from the file** (both `SMARTFADE`, both list
96 `$CHANNEL` entries).

Additional keywords that the SmartFade engine can write or read, found in `sfEngine.dll`
strings but **absent from all samples**: `$BACKUPLOOK`, `PRONTO`, `$$TEXT` (upper case),
`CONSOLE SMARTFADE WIDE`. The parser must accept unknown keywords (see §Unknown data).

## Physical format (OBSERVED in Ostrov + 1248 demo)

| Property | Value |
|---|---|
| Encoding | 7-bit ASCII (no bytes ≥ 0x80 in samples). **Encoding of non-ASCII labels (e.g. Czech diacritics) is UNKNOWN.** Test with a label containing "č" |
| Line endings | CRLF only, last line terminated, in the Ostrov and demo samples. **Not universal, see next row** |
| Line endings, console-written files | **LF only**. SHOW04.ASC: 0 CR, 1 153 LF. The same holds for SHOW01/02/03/10/11/99.ASC (0 CR each). Contrast: all 23 named `*.asc` shows in `smartfade-sd-card-backup/` and `SMRTFADE.SRC` are CRLF (CR count = LF count; all 7-bit ASCII, 0 bytes >= 0x80). Pattern: 8.3-style `SHOWnn.ASC` names = LF, long-name `.asc` = CRLF. **OBSERVED**, cause UNKNOWN (console writer vs SmartSoft writer vs copy path). The earlier "CRLF only" was true for the samples then at hand and is **annotated, not retracted**. Parser: accept CRLF and LF, preserve which one per file |
| Tabs | none |
| Max line length | 58 |
| Tokens | space-separated. Several lines have **trailing spaces** and fixed-width padding (`UP 5.0   0    `, `ENDDATA `) |
| Comments | lines starting with `!` act as section banners (`! Main Sequence`, `! Sequences`, `! Memories`, `! Patch`, `! Independents`, `! Channel Info`, `! Pages`, `! Settings`) |
| Blank lines | used as record separators, not significant (INFERRED) |
| Case | keywords upper case except `Text`, `$$Text` |
| Terminator | `ENDDATA` (with trailing space in both samples) |

## Structure (in file order, OBSERVED)

```
MANUFACTURER AVAB
CONSOLE SMARTFADE
$SOFTWAREVERSION <major.minor.patch> <build>
IDENT 3.0
CLEAR ALL
! Main Sequence
$SEQUENCE 48 2 0 256 256          ← stack header
  CUE <n.n> … (step records)
! Sequences
$SEQUENCE <s> 0 0 <x> 256         ← one per non-empty sequence? (1 in each sample)
  Text/$$Text (sequence label)
  $CUE <n.n> … (step records)
! Memories
  GROUP <memory#> / CHAN … / Text / $$Text 1..3
! Patch
  PATCH 1 <dmx>/<channel>/<level%> × 5 per line, 512 entries
! Independents
  $INDEPENDENT <n> <a> <b> <c> <d> + Text block   (n = 1, 2)
! Channel Info
  $CHANNEL <n> + Text block        (n = 1..96)
! Pages
  $PAGE <n> + Text block           (n = 1..12)
! Settings
  SET <Key> <int>
ENDDATA
```

## Records

### Header

| Line | Fields | Status |
|---|---|---|
| `MANUFACTURER AVAB` | constant in samples. AVAB is an ETC brand (ML uses `ETC`) | OBSERVED |
| `CONSOLE SMARTFADE` / `SMARTFADE WIDE` | console family. SmartSoft rejects a file from another console type ("This show file originates from another type of console") | OBSERVED + INFERRED |
| `$SOFTWAREVERSION 3.0.2 43` | version string + build number (demo: `2.0.0b20`, single token) | OBSERVED |
| `IDENT 3.0` | USITT ASCII version | PROBABLE |
| `CLEAR ALL` | USITT "clear before load" | PROBABLE |

### `$SEQUENCE a b c d e`

| Sample | a | b | c | d | e |
|---|---|---|---|---|---|
| Ostrov main | 48 | 2 | 0 | 256 | 256 |
| Ostrov seq | 0 | 0 | 0 | 638 | 256 |
| Demo main | 48 | 2 | 0 | 256 | 256 |
| Demo seq | 0 | 0 | 0 | 256 | 256 |

Cross-check (SHOW04, E2): the wire objects SequenceMain / Sequence carry b, c, d, e as raw
fields (`$SEQUENCE 48 2 0 256 256` = SequenceMain body `13 02 00 00 01 00 01 00`; Sequence (0,0)
`0 0 0 256 256` likewise) plus the step counts (19, 7). **OBSERVED** (one show). d and e are
not always 256 in other console-written files: `$SEQUENCE 48 2 0 256 54` (SHOW02.ASC),
`... 256 258` (SHOW03), `... 256 200` (SHOW99); sequence rows with d = 2559 (SHOW03) and 246
(SHOW99). So "256 = 100 %" stays a HYPOTHESIS and the meaning of d, e is UNKNOWN.

HYPOTHESIS: `a` = sequence index (0–47 = sequence faders 12 pages × 4, 48 = main stack).
`b` = run mode / type (2 for stack). `d`,`e` = rate/fade overrides where 256 = 100 %
(Ostrov `638` ≈ 249 %?). **All UNKNOWN until E6.**

**Update 2026-10-31 (static pass, `docs/protocol/smartsoft-analysis.md` S11.16 rows 3 and 7; INFERRED, no status change):** the
researcher reads SmartSoft's run-mode popup as setting `b` to 0 Loop, 1 One Shot, 2 Manual (the console's meaning is HYPOTHESIS,
H95), so `b` = 2 on the stack would read "Manual" and `b` = 0 on KOTLIK "Loop". The console manual (printed p.51, known-facts F149)
lists the sequence run modes as Manual, One Shot, Loop (default) and says the Stack's run mode "is determined by the wait times
recorded in the Stack steps", so the stack's `b` = 2 may be something else. The researcher's addressing of the step commands (a = page × 4 + slot for a
sequence and 48 for the stack, row 3) agrees with the HYPOTHESIS on `a` above. Both readings are tested by the planned capture
E12 (reverse-engineering.md), not by an SD save; nothing here is a reason to write `b` in a file other than as read.

Sequence label: `Text KOTLIK` + `$$Text 1 KOTLIK` / `$$Text 2 ` / `$$Text 3 ` directly after the header.

### Step record (`CUE` in main stack, `$CUE` in sequences)

```
CUE 10.1                  ← main stack; "CUE  1.0" (number right-aligned in width 4)
$$MEMORY 0 12             ← optional: referenced memory, 0-based page, 0-based fader
$$CUEPARAMS 1             ← step type (H12)
UP 0     0                ← up time, up delay
DOWN 0     0              ← down time, down delay
FOLLOWON 1.0              ← follow-on / wait (H14)
CHAN 15/8 46/100          ← channel/level% pairs, max 8 per line, continued on more CHAN lines
Text …/$$Text 1..3        ← optional (present in demo stack steps)
```

| Field | Observed values | Interpretation | Status |
|---|---|---|---|
| Cue number (see also "Sequence step numbers" below) | `1.0`…`14.0`, `10.1`…`10.6` (stack); `265.0`…`271.0`, `46.0`, `19.0`…`24.0` (sequences) | Stack: cue number with point cues (max 199.9, engine string "Cue number >199.9"). Sequences: the number **equals the memory number** (265 = page 12 fader 1) or the channel number (`46.0` with `CHAN 46/100`) | INFERRED. Update 2026-10-28: for memory-referencing sequence steps `$CUE` = 24p + f + 1 of `$$MEMORY p f` in 348 of 348 steps over SHOW01..99 and the 23 named shows (SHOW04: 265.0..271.0 = `$$MEMORY 11 0..6`; the 2 remaining steps in Husle-04.asc have no `$$MEMORY`). **But the console sends 1.0..7.0 for SHOW04's sequence on the wire** (OBSERVED, one capture), so the file number is not what the protocol carries |
| `$$MEMORY p f` | `0 0`…`0 12`, `11 0`…`11 6` | memory = p×24 + f + 1 → matches `GROUP` content in 26/26 cases | INFERRED (H13, strong). Update: **OBSERVED** for the numbering (E2 cross-check: 16/16 memories, 26/26 step records); 0-based page/fader as the console's physical page and fader is **OPEN** (E6 probe 1) |
| `$$CUEPARAMS` | 0, 1, 2 | 0 = channel step (single channel, no `$$MEMORY`), 1 = memory step (has `$$MEMORY`), 2 = stack-only state (no `$$MEMORY`, own CHAN list, has Text) | HYPOTHESIS (H12) |
| `UP t d` / `DOWN t d` | t ∈ {0, 0.4, 0.5, 1.0, 2.0, 3.0, 5.0}, d always 0 | seconds, one decimal. d = delay | INFERRED |
| `FOLLOWON t` | `0`, `1.0`, `10`, `11`, `21`, `50`, `58`, `1:30` | seconds, integer or one decimal, `m:ss` ≥ 60 s. 0 = none | INFERRED (H14). E2: on the wire FOLLOWON is the third u16 BE time after UP and DOWN, in tenths of a second, 26/26 steps (SHOW04 line 75 `FOLLOWON 1:30` = 900 tenths = `03 84`; `58` = `02 44`; `50` = `01 F4`). **OBSERVED** for position and unit; that the console calls it "Wait" is **UNKNOWN** (H14 name part, E6 probe 3) |
| `CHAN c/l` | c 1–48 seen, l 7–100 | channel / level percent integer. Channels at 0 omitted | INFERRED. E2: the console holds the level as an 8-bit value, 0xFF = 100 %; across all 53 non-zero levels of SHOW04 raw = ceil(pct × 255 / 100) fits 53/53 (round(pct × 2.55) fails 7/53, e.g. 17 % = 0x2C = 44). The percentage is therefore a **derived** value (raw to pct by rounding or truncation, both fit 53/53; **rounding direction UNKNOWN**). A reader cannot recover the raw byte beyond ceil(pct × 2.55) and a writer must derive pct from raw. **OBSERVED** (one show) |

Note: a memory-referencing step **duplicates** the memory's channel levels in its `CHAN`
lines. The parser must keep both and report inconsistencies. Never silently prefer one.

### Memory: `GROUP n`

`GROUP 1`…`GROUP 271`, followed by `CHAN` lines and a Text block. `n` = memory number 1–288.
In Ostrov, memories 265–271 (page 12) are referenced only by sequence steps, and the others by
stack steps. Only non-empty memories are written. Status: INFERRED.

### Text block

```
Text RYBAR
$$Text 1 RYBAR
$$Text 2 DOM
$$Text 3
```

Three label lines (SmartSoft "select how many lines of text are displayed").
E2 (SHOW04, **OBSERVED**): on the console side a label is a text block of 3 lines × 6 UTF-16
code units (`03 06` + 36 bytes), so `Text` / `$$Text n` hold at most **6 characters per line,
3 lines**. The byte order of the code units is UTF-16BE, **INFERRED** from block boundaries
(all SHOW04 characters are below U+0100, so ASCII-only evidence). **Update 2026-10-29: the byte
order is now HYPOTHESIS, not INFERRED** (the text match with the file stays OBSERVED): the structural
argument excludes only little-endian with three full 12-byte lines. The third-party ConsoleLink
(SmartFade ML, evidence class 5) reads the same 38-byte block as a pad byte `00` followed by UTF-16LE
lines of 12 / 12 / 11 bytes, which fills the block exactly and gives the same text for every label seen
([known-facts.md](../protocol/known-facts.md) F73, [hypotheses.md](../protocol/hypotheses.md) H40,
[consolelink-analysis.md](../protocol/consolelink-analysis.md) §2.6). A "č" label and a
6-character third line decide it. Note: in the 30 SD shows only one CRLF file of unknown origin
(`KAIMO final 13-06-19.asc`) has third lines at all (6 characters twice, 11 characters three times);
the 7 console-written `SHOWnn.ASC` files never use `$$Text 3` (all 875 such lines are empty). The label on the wire equals
`$$Text 1..3` in 16/16 memories, 1 sequence, 12 pages, 96 channels and 2 independents. In
SHOW04 `Text` equals `$$Text 1` (lines 170-171 `Text KOTLIK` / `$$Text 1 KOTLIK`). A parser
must not truncate labels it reads; a writer targeting the console should validate 3 × 6.
In the 2.0.0 demo `Text` = full label and `$$Text` = the label wrapped at 6 characters
(`Backlights` → `Backli`/`ghts`). In the 3.0.2 Ostrov file `Text` = line 1 only
(`RYBAR` + `DOM`). **The relation between `Text` and `$$Text` is version-dependent.
Preserve all four values.** Trailing space after the line number is always present.

### `PATCH 1 d/c/l …`

5 triples per line, `d` 1–512 in order. `c` = channel (0 = unpatched), `l` = proportional
level % (all 100 in samples). Leading `1` probably = universe/page. Channels **49, 50**
appear (`97/49`, `98/50` in demo; `97/24`, `98/50` in Ostrov) → HYPOTHESIS H11: 49/50 =
Independents 1/2. Status: INFERRED except H11.

E2 cross-check (SHOW04, **OBSERVED**, one show): the console's Patch object (u16 BE count 512
+ 512 channel bytes, 0 = unpatched) equals the file's channel per DMX address, 512/512. The
object has **no room for a level**, so the file's `l` field comes from elsewhere (**UNKNOWN**).
All 512 entries of SHOW04, and of all other files in the SD folder, carry level 100 (0 entries
with another level in 30 files), so no non-100 level has been seen yet. SHOW04's patch is
unusual: line 374 `46/46/100 47/50/100 48/48/100 49/0/100 50/0/100` (DMX 47 → channel 50),
line 384 `96/0/100 97/24/100 98/0/100` (DMX 97 → channel 24, which is also on DMX 24); channel
49 is not patched at all. This **does not support** H11's "channels 49/50 = independents at DMX
97/98": channel 50 sits at DMX 47 and DMX 98 is unpatched. H11 stays OPEN (the file stores no
independent levels, so no contradiction is proven either).

### `$INDEPENDENT n a b c d`

`$INDEPENDENT 1 0 255 0 1` and `$INDEPENDENT 2 0 255 0 1` in all samples, plus Text block.
Fields a–d **UNKNOWN** (candidates: mode, level 255, bump/GM flags per manual "set levels and
set bump and GM modes"). E2 (SHOW04, **OBSERVED**): the Independents object body is
`02 | 00 FF 00 01 + empty text block | 00 FF 00 01 + empty text block`: each record is the
4 bytes `00 FF 00 01` followed by the 38-byte text block, matching `$INDEPENDENT 1 0 255 0 1`
and `$INDEPENDENT 2 0 255 0 1`. The fields are single bytes on the wire; still no meaning.

### `$CHANNEL n`, `$PAGE n`

Label containers. 96 channels, 12 pages. Status: OBSERVED.

### `SET key value`

| Key | Ostrov | Demo | Meaning |
|---|---|---|---|
| `Language` | 0 | 0 | 0 = English? |
| `CrossfadeFadeMode` | 0 | 0 | Crossfade faders: Upwards only / Both ways? |
| `MIDIChannel` | 0 | 0 | H10: 0-based |
| `MIDIActive` | 1 | 1 | Basic MIDI on |
| `MSCDeviceId` | 127 | 127 | 127 = "All"? (menu offers 0–126 or All) |
| `MSCActive` | 0 | 1 | MSC on/off |

E2 (SHOW04, **OBSERVED**): the six values (0, 0, 0, 1, 127, 0 in file order) occur as one
contiguous run in the console's SystemSettings object at body[6..11]; only MIDIActive
(body[9] = 1) and MSCDeviceId (body[10] = 0x7F) are distinctive, the zero-valued keys cannot be
assigned one by one, so H10 (0- vs 1-based MIDI channel) stays undecidable. The object has 12
further bytes with no `SET` line (body[0..5], body[12..17]; the latter possibly default times,
HYPOTHESIS, see the E2 record). The file's `SET` block is therefore not the whole settings
object.

**E3b (2026-10-30, OBSERVED):** body[13] and body[15] read 0x33 instead of 0x32 in all three sessions and the
LCD's times read 'U 5.1' / 'D 5.1' instead of 'U 5.0' / 'D 5.0': body[12..15] are the console's default Up /
Down times (strongly supported; known-facts F102, H45), changed by the owner moving the wheel by accident
(E3b-A2, [owner statements](../protocol/captures/2026-10-30-e3b-owner-statements.txt)). `SHOW05.ASC` equals `SHOW04.ASC` byte for byte, and the file has no `SET` line for these times, so
they are **probably not stored in the file** (INFERRED, with the limit stated in F102: the console's value at
the moment of the SHOW05 save is not recorded).

## Cross-check against the E2 capture (SHOW04.ASC)

Source: `docs/protocol/captures/2026-10-28-e2-smartsoft-connect.md`, section "Cross-check
against SHOW04.ASC" (tool `tools/protocol-analyzer/e2_asc_crosscheck.py`). The file was saved
by the console minutes before the capture; no memory or address was changed afterwards
(owner-reported). Everything below is one show and one session: **OBSERVED** or **INFERRED**,
nothing VERIFIED. *(Update 2026-10-29: the E3 session returned the same bodies byte for byte, so
"the console returns these bodies" is VERIFIED for the objects below (known-facts F70-F77); the
comparison with the file is still one show and one file, so the rows below keep their statuses.)*

| Item | Result | Status |
|---|---|---|
| Line endings | SHOW04.ASC is LF-only (0 CR, 1 153 LF) | OBSERVED |
| Patch | 512/512 DMX addresses match; the object has no level | OBSERVED; level source UNKNOWN |
| Memory and `GROUP` | 16/16 memories, 768/768 level bytes (channels 1-48; 49-96 all 0), labels 16/16 | OBSERVED |
| `GROUP n` numbering | n = 24a + b + 1, with (a, b) = `$$MEMORY p f` = memory body[0..1] (H13, H25 CONFIRMED as numbering) | OBSERVED; physical page/fader meaning OPEN |
| Level scale | 0xFF = 100 %, raw = ceil(pct × 255 / 100) in 53/53; 0xAB = 171 = 67 % (`GROUP 3`, `CHAN 9/67`) | OBSERVED / INFERRED; rounding direction UNKNOWN |
| Labels | 3 × 6 UTF-16 code units, BE by structure | OBSERVED (match), INFERRED (byte order) *(update 2026-10-29: byte order HYPOTHESIS, two readings, see "Text block" above; the console returns the same label blocks in E3, F73)* |
| Sequence step numbers | wire 1.0..7.0, file `$CUE 265.0..271.0` (lines 175, 183, 191, 199, 207, 215, 223) | OBSERVED, see below |
| Step records | cue, CUEPARAMS, memory index, UP, DOWN, FOLLOWON match 19/19 (main) and memory index, CUEPARAMS, UP, DOWN, FOLLOWON 7/7 (sequence). Wire memory index = 24p + f (264 for `$$MEMORY 11 0`), the file shows p and f separately | OBSERVED |
| FOLLOWON | third time, tenths of a second | OBSERVED; name "Wait" UNKNOWN |
| `$INDEPENDENT` | 4-byte record `00 FF 00 01` | OBSERVED |
| `SET` | six values at SystemSettings body[6..11] | OBSERVED (position only) |

**E3b (2026-10-30).** `e2_asc_crosscheck.py` run on the E3b JSONL against `SHOW05.ASC` (byte-identical to
SHOW04.ASC) repeated every result in all three sessions (known-facts F104): Patch 3 × 512 of 512, Memory 16
`GROUP`s with 2 304 of 2 304 level bytes, labels 36 of 36 pages and 288 of 288 channels, StepContent 57 of 57,
Sequence records 21 of 21 differing only in the cue number, `SET` values at SystemSettings body[6..11],
Independents `00 FF 00 01` × 6. **Two saves of the same show, made two days and two console power cycles
apart, are byte-identical** (OBSERVED, one pair of saves). The live state at the two save moments is not
recorded (E2's session start had the MASTER at 0xAA and fader 5 at full, E3b's starting position was set after
the SHOW05 save), so the limit is: nothing that differed in the console's history between the saves reached the
file; INFERRED, not shown, that fader positions, MASTER, BUMPS, SOLO, modes and the default fade times are not
stored. The console's FileName object follows the last SD save: 'SHOW04.ASC' in E2 and E3, 'SHOW05.ASC' in E3b
(F103).

### Sequence step numbers: two number sets

- File: `$CUE 265.0` .. `271.0` under `$SEQUENCE 0 0 0 256 256` (line 168, label KOTLIK), each
  with `$$MEMORY 11 0` .. `11 6`, so `$CUE` = memory number 24 × 11 + f + 1.
- Wire (E2, f656, Sequence (0,0)): the cue field of the 7 step records is `00 0A` .. `00 46`
  = 1.0 .. 7.0 (u16 BE tenths).
- In every other file with sequences in the folder `$CUE` is also the memory number (SHOW10.ASC:
  `$CUE 1.0` with `$$MEMORY 0 0`; SHOW02.ASC: `$CUE 25.0` with `$$MEMORY 1 0`), 348/348 memory
  steps. So the file number is the memory number; the wire number is 1..n in SHOW04. Which one
  the console displays is **UNKNOWN**, and what 264 (= 24 × 11) means beyond that arithmetic is
  **UNKNOWN**. HYPOTHESIS (not claimed further): the writer puts the memory number where the
  step number would go. E6 probe 2 decides.
- Parser: keep `$CUE` text verbatim; never renumber.

## Proposed E6 probes

Each is one deliberate console action, SD save, diff. With the console connected to SmartSoft
in an E2-style capture the wire side can be compared too.

1. **Page/fader meaning of `$$MEMORY p f` and `GROUP n`**: record one memory on a known
   physical memory fader (e.g. page 2, fader 3, one channel at a known level), save, diff.
   Expected if H13 holds with 0-based p, f: `GROUP 27` (24 × 1 + 2 + 1), `$$MEMORY 1 2`.
2. **Sequence step numbering shown on the console**: build one sequence with 3 steps, write down
   the numbers the console display shows per step, save; compare with `$CUE` (memory numbers
   expected) and, if connected, the wire cue field (1.0, 2.0, 3.0).
3. **Wait mapping**: set one step's Wait (as the console labels it) to a unique value such as
   1:30 with distinct Up and Down; save; confirm that `FOLLOWON` changes (H14 name).
4. **Non-100 % patch level**: patch one channel with a proportional level other than 100 %
   (e.g. 50 %) in the console patch menu, save; see which `PATCH` field changes and whether
   any wire object carries it.
5. **Label with "č"**: label one memory `čaj`, save; check how the file encodes it (7-bit ASCII
   is violated or replaced) and, with a capture, the UTF-16 byte order (reading A, BE, predicts
   `01 0D` at an even offset after `03 06`; reading B of ConsoleLink predicts `0D 01` after a pad byte, H40). Also try
   a 7th character on a line and a 4th line to confirm the 3 × 6 limit.
6. **Rounding direction of CHAN percentages**: choose a raw level where round and truncate give
   different percentages (read raw from the wire), compare with the file.
7. **Line endings**: save the same show from the console to SD and from SmartSoft to the PC;
   compare bytes (LF vs CRLF) to find the cause of the LF-only `SHOWnn.ASC` files.
8. **`$INDEPENDENT a..d`**: change one independent's mode or bump/GM setting, save, diff.

## Round-trip assessment

- The format is **text, ordered, and fully written by the engine** (writer format strings in
  `sfEngine.dll`), so byte-exact regeneration looks feasible. Fixed-width padding and trailing
  spaces must be reproduced exactly.
- The file appears to be the **complete** show (console SD backups use the same format), so it
  is probably lossless for the 1248's show data. It probably excludes transient state (current
  fader positions, live levels, snapshots? UNKNOWN). *(Update 2026-10-30: two saves of the same show are
  byte-identical although the console's history between them differed (F104); see the limit in "Cross-check".)*
- Plan: the parser keeps every line's raw text and position. A later writer can be tested
  with "parse → write → byte-compare" on every sample. *(Done 2026-10-31: see "Writer rules learned" above; 33 of 33 files
  byte-identical.)*

## Writer rules learned from the files (2026-10-31, OBSERVED on 33 files)

`SmartFade.ShowFile.Asc` (`src/backend/SmartFade.ShowFile.Asc`, reader and writer, no hardware
dependency; CLI `tools/asc-converter`) reads a file into a model that keeps numbers and times as text
and renders it again. On 2026-10-31 the committed Ostrov sample and all 32 `.asc` files in the owner's
`smartfade-sd-card-backup/` (7 console-written LF files, 25 SmartSoft-written CRLF files, 3.0.1 and
3.0.2) rendered **byte-identical** after a read, so the rules below describe the engine's writer for
every file seen. They come from the files, not from a specification.

- Bytes are read and written as Latin-1 (one byte, one character), so any non-ASCII label passes
  through unchanged; the line ending (LF or CRLF) is detected and preserved per file.
- Header: `MANUFACTURER`, `CONSOLE`, `$SOFTWAREVERSION`, `IDENT`, blank, `CLEAR ALL`, blank.
- `! Main Sequence`, the `$SEQUENCE` line, a blank, then each step followed by one blank; an
  optional label block (`Text`, `$$Text 1..3`) sits between the header's blank and the steps, itself
  followed by a blank. `! Sequences` follows directly; each sequence as the main stack; the section
  ends with one extra blank.
- Step: `CUE` / `$CUE` + the number right-aligned in 4 characters (`CUE  1.0`, `$CUE 265.0`);
  `$$MEMORY p f`; `$$CUEPARAMS n`; `UP` and `DOWN` with the time left-aligned in 6 and the delay in 5
  characters (`UP 5.0   0    `); `FOLLOWON` with the time in 5 (`FOLLOWON 1:30 `); `CHAN` lines of at
  most 8 `c/l ` pairs, each pair followed by a space; the optional label block.
- `! Memories`: `GROUP n`, `CHAN` lines, the label block, then **two** blanks per memory; one extra
  blank after the section.
- `! Patch`: `PATCH 1 ` + five `d/c/l ` triples per line (two on the last), one blank after the section.
- `! Independents`: `$INDEPENDENT n a b c d`, the label block, one blank per record; one extra blank
  after the section.
- `! Channel Info`: `$CHANNEL n`, the label block, one blank per record; **no** extra blank.
- `! Pages`: `$PAGE n`, the label block, **two** blanks per page; no extra blank.
- `! Settings`: the `SET` lines, a blank, `ENDDATA ` (trailing space), a final line ending.
- Label block: `Text ` + text, `$$Text 1 ` + line 1, `$$Text 2 ` + line 2, `$$Text 3 ` + line 3,
  each verbatim (empty labels leave the trailing space).

The reader keeps every unrecognised line as an `UnknownRecord` with a warning (none occurred in the
33 files); the writer cannot place unknown records, so a file with any is not guaranteed to round-trip.

## Step rules learned from the files (2026-10-31, OBSERVED on 34 files)

Counted over the 34 console-written files available on 2026-10-31 (`smartfade-sd-card-backup/`, SHOW01–SHOW11,
SHOW99 and the 22 named shows; the Ostrov sample is byte-identical to one of them) by a script over the raw
lines; the show-file page's editor and `ShowFileRules` (docs/api.md) keep an edited file inside these shapes:

| Rule | Count | Status |
|---|---|---|
| Every main-stack step (`CUE`) references a memory and carries `$$CUEPARAMS 1` | 525 of 525 | OBSERVED |
| Stack cue numbers have one decimal (`d.d`, 1–3 digits before the point) and rise strictly through the stack | 525 of 525 (no file has a lower or equal number after a higher one); the engine string "Cue number >199.9" gives the ceiling | OBSERVED (the ceiling DOCUMENTED by the string only) |
| A sequence step (`$CUE`) with a memory carries `$$CUEPARAMS 1` and is numbered by its memory as `n.0`, n = 24p + f + 1 of `$$MEMORY p f` | 383 of 383 | OBSERVED (extends the 348 of 348 above) |
| A sequence step without a memory is a channel step, `$$CUEPARAMS 0`, with its own `CHAN` line | 2 of 2 (Husle-04.asc, `$CUE 41.0`, `42.0`) | OBSERVED |
| A memory step whose memory is in the file repeats the memory's `CHAN` list exactly (same pairs, same order) | 894 of 894 | OBSERVED |
| A memory step whose memory is not in the file (never recorded or emptied) has no `CHAN` line | 14 of 14 | OBSERVED |
| Most common time triples (UP, DOWN, FOLLOWON) | stack: `5.0 0`, `5.0 0`, `0` (204 of 525); sequences: `1.0 0`, `1.0 0`, `0` (234 of 385) | OBSERVED (the editor copies a neighbour's times and uses these only for an empty stack or sequence) |

What the console does with a file that breaks one of these shapes is UNKNOWN (the SD load path is the only
way such a file reaches it; E6 is the test), so the editor never produces one.

## Label and memory rules learned from the files (2026-10-31, OBSERVED on 34 files)

Counted like the step rules above, over the same 34 console-written files; the show-file page's editor and
`ShowFileRules` keep an edited file inside these shapes:

| Rule | Count | Status |
|---|---|---|
| The `Text` line of a label block is derived from its three `$$Text n` lines: line 1, then line 2 only when line 1 has 6 characters, then line 3 only when line 2 has 6 too (`NOC 3` / `MORE` → `Text NOC 3`; `LAFIAO` / `GULA` → `Text LAFIAOGULA`; `BLUE 2` / `HVIRZD` → `Text BLUE 2HVIRZD`) | 4 282 of 4 282 blocks (memories, sequences, steps, channels, pages, independents) | OBSERVED; consistent with H40's reading of the 18-unit block as three lines read up to the first 0 |
| A label line has at most 6 characters | 12 843 of 12 846 lines; the 3 others (KAIMO final 13-06-19.asc, lines 545, 563, 581, a memory label's line 3) read `&#268;ERVEN`: the console writes a character outside ASCII as an HTML numeric entity (`&#268;` = Č), which counts as one character on the console | OBSERVED (the entity form in one file; how the console reads an entity back from the SD card is UNTESTED) |
| Label bytes are printable ASCII (0x20–0x7E); no other byte occurs in any label line | 12 846 of 12 846 lines | OBSERVED |
| A memory's `CHAN` pairs name channels 1..48 only, in rising order, each once, at 1..100 % (a channel at 0 is not written) | 4 860 of 4 860 pairs, 496 of 496 memories | OBSERVED |

The editor writes labels of printable ASCII without `&` (so it never produces an entity and never breaks one), and
memory channel lists in the files' form; what the console does with a file outside these shapes is UNKNOWN (E6).

## Unknown data policy

The parser must never drop data:
- Unknown keyword → `UnknownRecord { Keyword, RawLine, LineNumber, ContextSection }` + warning.
- Known keyword with unexpected field count/format → keep raw + warning.
- Fatal only for: invalid (non-ASCII/undecodable) bytes if strict, missing `ENDDATA`
  (truncated file), structurally impossible content.

## Relationship to other sources

| ASC element | Console | MIDI | SmartSoft/USB | Status |
|---|---|---|---|---|
| `GROUP n` (memory) | Memory fader page ⌈n/24⌉, fader ((n−1) mod 24)+1 | CC 1–48 + PC 100–111 page select (MEM mode) | `Console::Memory`, `consoleCacheMemoryUpdated` | HYPOTHESIS |
| `CUE` in `$SEQUENCE 48` | Stack step | PC 1–99 jump | `SequenceMain`, `StepContent` | HYPOTHESIS |
| `CHAN c/l` | Channel c (1–48), level % | none | `ChannelLevels` | HYPOTHESIS |
| `PATCH` | Patch menu | none | `consoleCachePatchUpdated` | HYPOTHESIS |
| `SET MIDI*`/`MSC*` | MIDI menu | configuration of MIDI | "MIDI MSC on", "MIDI channel" | DOCUMENTED + INFERRED |
| SFML / PERSLIB | not applicable to 1248 | — | — | — |
