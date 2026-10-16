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
| Line endings | CRLF only, last line terminated |
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

HYPOTHESIS: `a` = sequence index (0–47 = sequence faders 12 pages × 4, 48 = main stack).
`b` = run mode / type (2 for stack). `d`,`e` = rate/fade overrides where 256 = 100 %
(Ostrov `638` ≈ 249 %?). **All UNKNOWN until E6.**

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
| Cue number | `1.0`…`14.0`, `10.1`…`10.6` (stack); `265.0`…`271.0`, `46.0`, `19.0`…`24.0` (sequences) | Stack: cue number with point cues (max 199.9, engine string "Cue number >199.9"). Sequences: the number **equals the memory number** (265 = page 12 fader 1) or the channel number (`46.0` with `CHAN 46/100`) | INFERRED |
| `$$MEMORY p f` | `0 0`…`0 12`, `11 0`…`11 6` | memory = p×24 + f + 1 → matches `GROUP` content in 26/26 cases | INFERRED (H13, strong) |
| `$$CUEPARAMS` | 0, 1, 2 | 0 = channel step (single channel, no `$$MEMORY`), 1 = memory step (has `$$MEMORY`), 2 = stack-only state (no `$$MEMORY`, own CHAN list, has Text) | HYPOTHESIS (H12) |
| `UP t d` / `DOWN t d` | t ∈ {0, 0.4, 0.5, 1.0, 2.0, 3.0, 5.0}, d always 0 | seconds, one decimal. d = delay | INFERRED |
| `FOLLOWON t` | `0`, `1.0`, `10`, `11`, `21`, `50`, `58`, `1:30` | seconds, integer or one decimal, `m:ss` ≥ 60 s. 0 = none | INFERRED (H14) |
| `CHAN c/l` | c 1–48 seen, l 7–100 | channel / level percent integer. Channels at 0 omitted | INFERRED |

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
In the 2.0.0 demo `Text` = full label and `$$Text` = the label wrapped at 6 characters
(`Backlights` → `Backli`/`ghts`). In the 3.0.2 Ostrov file `Text` = line 1 only
(`RYBAR` + `DOM`). **The relation between `Text` and `$$Text` is version-dependent.
Preserve all four values.** Trailing space after the line number is always present.

### `PATCH 1 d/c/l …`

5 triples per line, `d` 1–512 in order. `c` = channel (0 = unpatched), `l` = proportional
level % (all 100 in samples). Leading `1` probably = universe/page. Channels **49, 50**
appear (`97/49`, `98/50` in demo; `97/24`, `98/50` in Ostrov) → HYPOTHESIS H11: 49/50 =
Independents 1/2. Status: INFERRED except H11.

### `$INDEPENDENT n a b c d`

`$INDEPENDENT 1 0 255 0 1` and `$INDEPENDENT 2 0 255 0 1` in all samples, plus Text block.
Fields a–d **UNKNOWN** (candidates: mode, level 255, bump/GM flags per manual "set levels and
set bump and GM modes").

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

## Round-trip assessment

- The format is **text, ordered, and fully written by the engine** (writer format strings in
  `sfEngine.dll`), so byte-exact regeneration looks feasible. Fixed-width padding and trailing
  spaces must be reproduced exactly.
- The file appears to be the **complete** show (console SD backups use the same format), so it
  is probably lossless for the 1248's show data. It probably excludes transient state (current
  fader positions, live levels, snapshots? UNKNOWN).
- Plan: the parser keeps every line's raw text and position. A later writer can be tested
  with "parse → write → byte-compare" on every sample.

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
