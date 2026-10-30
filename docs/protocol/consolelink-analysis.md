# ConsoleLink (third-party, SmartFade ML) compared with our 1248 evidence

Created 2026-10-29 (protocol-researcher). **Evidence class 5 of CLAUDE.md: "other sources,
clearly identified".** ConsoleLink is a third-party Python program for the ETC SmartFade **ML**
(USB PID 0x0201). The ML is a sibling console: it shares the Peperoni USB stack with our
SmartFade 1248 (`etchsp.inf` binds PIDs 0200 and 0201, F11), but it runs its own firmware image
(F17) and SmartSoft serves it with a different engine (`sfmlEngine.dll`, not the `sfEngine.dll`
of [smartsoft-analysis.md](smartsoft-analysis.md)). **Nothing here is OBSERVED or VERIFIED for
our 1248. Nothing here changes a status by itself, and nothing here may be sent to a console.**
The source can corroborate, contradict or extend our hypotheses.

Method: ConsoleLink's code, README, CONTRIBUTING and test fixtures were read as data. ConsoleLink
was not run and the console was not touched. Our numbers come from the committed E2 capture
(`captures/2026-10-28-e2-smartsoft-connect.jsonl`). They can be re-derived with
`py -3.13 -I tools/protocol-analyzer/hsp_summary.py <jsonl> payloads --id N`.

Conventions:

- `CL-ML:` marks a ConsoleLink claim about the ML. `fN (seqN)` is our E2 pcap frame and JSONL
  record, as in the [E2 record](captures/2026-10-28-e2-smartsoft-connect.md).
- `body[n]` is byte n after the 5-byte inbound payload header. ConsoleLink's `data[n]` is the
  same position.
- File references point to the ConsoleLink checkout. `protocol.py` and `app.py` are under
  `src/consolelink/`, and `*.log` files are under `tests/fixtures/`.
- ConsoleLink writes object types in hex, and these are our decimal IDs of the sfEngine name
  table (S11.6): 0x00 = 0 Memory, 0x09 = 9 ChannelText, 0x0c = 12 Independents,
  0x0d = 13 OutputLevels, 0x0e = 14 FaderInfo, 0x0f = 15 ChannelLevels, 0x10 = 16 OutputPalettes,
  0x11 = 17 PlaybackInfo, 0x14 = 20 ConsoleEvent, 0x15 = 21 DisplayContent, 0x16 = 22 LedStatus,
  0x17 = 23 ConsoleStatus, 0x18 = 24 DeviceStatus, 0x27 = 39 System, 0x28 = 40 DirtyObjects.
  The names come from `sfEngine.dll`. The ML engine's table was not checked.
- ConsoleLink's terms map to ours: "msgType" = w0, "payloadLen" = w1, "state[0..3]" = w2..w5,
  "announce" = DirtyObjects list, "ack" = Fetch.

## Result in brief

- **Agrees.** The framing, the session model and the System 9 / System 7 start-up are the same as
  what E2 showed on the 1248.
- **Agrees, object layouts.** The ML layouts of FaderInfo, OutputLevels, LedStatus and
  DisplayContent predict exactly the bytes that moved in E2: fader 5 at FaderInfo body[17], DMX 5
  at body[6], the G bytes of LED block 5 at body[26] and body[29], and the bar glyph at body[7].
- **Agrees, catalog.** The ML catalog list has the same first five and last four records as our
  157-record list f384.
- **Contradicts.** ConsoleLink reads inbound payload bytes 0–1 as a 16-bit sequence. Our sequence
  wrap from f88 to f218 rules that out.
- **Undecided.** ConsoleLink reads labels as a pad byte plus UTF-16**LE** (12/12/11 bytes). That
  fits the same 38-byte block as our UTF-16**BE** reading (H40), so the byte order is UNDECIDED.
- **New hypotheses**, all passive tests (§3.2):
  - N1, Grand Master in FaderInfo body[194]: 0xAA in E2.
  - N2, GM scaling and a blinking BlackOut LED.
  - N3, LedStatus block map.
  - N4, LCD bar graph.
  - N5, PlaybackInfo crossfader record.
  - N6, System a = 10 as page select.

## 1. Provenance

| Item | Value | Source |
|---|---|---|
| Project | ConsoleLink, "Web interface and Art-Net output for ETC SmartFade ML lighting consoles, over USB" | pyproject.toml:6-8 |
| Repository | https://github.com/gaeljaffrain/consolelink | checkout `.git/config` (origin) |
| Commit | `09b3c266757c4d4a614151fa9326e25143caaa1f` ("Bump version: 0.1.7 → 0.1.8"), 2026-10-20 15:00:56 −07:00, annotated tag `v0.1.8`; version string `0.1.8` | `git log`, tag object; protocol.py:90 |
| Author, licence | Gaël Jaffrain; MIT | LICENSE:1-3; pyproject.toml:11-13 |
| Console | **SmartFade ML only**: "ConsoleLink was tested with SmartFade ML" (README.md:11); VID 0x14D5 / PID 0x0201 (protocol.py:92-94; README.md:95) | |
| How the protocol was obtained | "The general process to establish the protocol details was to capture USB traces using the original software and consoles, and study the traces to understand the wire protocol. For the SmartFade ML case in particular, the traces were obtained running the original SmartSoft software under Windows 10 against an actual SmartFade ML console. USB traffic was captured with Wireshark. Each capture isolated a single step (initial connection, moving one fader, pressing one button, etc.) so the resulting traces could be matched to a specific action." | CONTRIBUTING.md:33-39 |
| Unstated | SmartSoft version, ML firmware version, the OS ConsoleLink itself was run on for the fixtures: UNKNOWN | whole repository |
| Published evidence | No SmartSoft capture is published. The fixtures are ConsoleLink's own `--capture` logs. They hold object data only (after the 5-byte payload header), with no 12-byte headers and no sequence bytes. Their kinds (`requested`, `acked`, `announce`, `payload`) are traffic that **ConsoleLink** drove, not SmartSoft. Statements about what "real SmartSoft" sends (protocol.py:164-167, 203-206, 650-651; tests/test_buttons.py:1-2) are the author's reading of unpublished traces | CONTRIBUTING.md:75-89; app.py:490-494; tests/conftest.py:5-9 |
| USB access | Python ≥ 3.13, pyusb + libusb (`libusb-package` bundles libusb on macOS, Windows non-ARM64 and Linux x86_64 / aarch64; elsewhere the system libusb). Claims only the interface with a bulk IN and a bulk OUT endpoint (interface 1) | pyproject.toml:10, 28-34; protocol.py:7-8, 117-132; app.py:443-478 |
| Windows | "Switch the console to the generic WinUSB driver, once: libusb can't open it through the ETC driver installed with SmartSoft. Use Zadig: Options → List All Devices. The console shows up as two ETC SmartFade entries, interface 0 and interface 1: select each in turn, choose WinUSB and click Replace Driver. Then replug the console; no reboot needed. SmartSoft won't see the console while it uses WinUSB. To switch back, uninstall the device in Device Manager (tick "delete the driver software") and replug, or reinstall SmartSoft." | README.md:119-126 |
| macOS, Linux | "USB access works out of the box on macOS"; Linux / Raspberry Pi need a udev rule for `14d5:0201` | README.md:67, 83-107 |
| Local checkout | `analysis/extracted/consolelink-09b3c26/` (git-ignored, shallow clone, read-only). The line numbers in this document refer to the commit; the Windows working copy has CRLF line endings and the same line numbers | |

SHA-256 of the committed (LF) content, computed as `git show HEAD:<path> | sha256sum` on 2026-10-29:

| File | SHA-256 |
|---|---|
| `src/consolelink/protocol.py` | `952adc78a19958a083271f891f2fdfbe53f7125772a4c8021f20a3d6620e67a2` |
| `src/consolelink/app.py` | `0853ab521be9f4834616e21ad748b02f7be0dc983497dc2a4cf64919e6fa2487` |
| `README.md` | `a93818734b58a03098ff91f693b456cdadba171eeef267bcaf977784275bf77c` |
| `CONTRIBUTING.md` | `262f38c8f288b35b6de1eeee1599041568b41396fb3f7b41bb61d7d6dbfdc073` |
| `LICENSE` | `277f5cb2eff40f1793a1c3312afe93a52265dca0eecadd77b86e2f1e553f3fc9` |
| `tests/fixtures/faders.log` | `5f53596ab78787881db28eb688618225b7cac84e3b4b3b0bad2f5f33a9fe18ec` |
| `tests/fixtures/output_scaling.log` | `fbdfb0c893e5ea2b33cc6b6a4d1d73033eb0a814fb84d4c81098b4aa36a86878` |
| `tests/fixtures/crossfaders.log` | `f966ac2da3179864b7dd332682bb4712759df55d8a11e6251dd2a33ace4fc0a4` |
| `tests/fixtures/lcd.log` | `d6081f24e0a3fe110ad712bb7efee0f7b3b0105b0cdd3acc92e43cfa598cbfce` |
| `tests/fixtures/indicators.log` | `68b1a4f69731a6622affb2b892e7a8f5ae6065968e2d3b41a0e749379f24de07` |
| `tests/fixtures/bump_leds.log` | `c2c1d4734ed024ef656b564154294ecda705339fbcd41e98b7efdf6d11cb3c28` |
| `tests/fixtures/labels.log` | `5ef41711754f991f8c7c8d78d29376744904287391443f6507ee8dff28b073c7` |
| `tests/fixtures/mems_names.log` | `7be8678805fdd150afd54d8128b60f826393af9cdb9d51ce61a9af69f270fa34` |
| `tests/fixtures/independents.log` | `e192f09a91b4c8ce494d8eda8f23fa0d5d135716ff0d36dc191cb321086d84ef` |
| `tests/fixtures/dmx.log` | `b7b26780bd79da5be403b836b5d6a03c82c1602f9ceea83d9092f60d17af6ddf` |
| `tests/fixtures/fader_modes.log` | `e6e76daf5af8943742e932b0dd3cd6a1fa90b778e69c061619ad7921e5381d75` |

**Caveat: ML, not 1248.** The ML is a different product: a moving-light console with 24 faders,
two LCDs, two DMX universes and device / palette objects. It has a different firmware image and a
different PC engine. The comparison below shows that the transport stack and the object framework
are shared. A match with our E2 bytes raises confidence in how we read those bytes. It never
promotes a 1248 status (promotion rules: [README.md](README.md)). ML numbers are not 1248
numbers (§4).

## 2. Comparison

Verdicts:

- **AGREES**: ConsoleLink's ML claim and our 1248 evidence say the same.
- **DIFFERS**: the readings differ but are compatible with all bytes seen, or the difference is in
  host-side behaviour.
- **EXTENDS**: the claim is ML-only or untested on the 1248.
- **CONTRADICTS**: our 1248 bytes rule ConsoleLink's reading out for the 1248.

### 2.1 Transport and framing

| # | Element | ConsoleLink (ML) | Our 1248 evidence | Verdict |
|---|---|---|---|---|
| T1 | Interface | Claims "the bulk vendor interface (interface 1)", found as the first interface with a bulk IN and a bulk OUT endpoint; interface 0 is never opened (protocol.py:7-8, 117-132; app.py:444, 467) | Session on interface 1 only, bulk 0x04 / 0x83 (F51); HID interface unused (F63, H38) | AGREES |
| T2 | 12-byte header | 6 × u16 LE (msgType, payloadLen, state[4]) (protocol.py:10, 191-192, 606-616) | Six LE words w0..w5 (F52, F53; H15, H16) | AGREES |
| T3 | Poll | msgType 0, payloadLen 0. ConsoleLink's idle poll has state = 0 (protocol.py:10-11; app.py:577). Its poll after an ack repeats the ack's state words (app.py:626; protocol.py:725), although the docstring says "plain" poll (protocol.py:19) | w0 = 0, w1 = 0; SmartSoft's w2..w5 are stale, 3 002 / 3 002 (e.g. poll f19 (seq10) repeats fetch f17's ID 0x11); the first poll f11 (seq6) is all zero (F52) | AGREES on w0 / w1. DIFFERS on idle w2..w5 (host behaviour; that the ML ignores them is ML-only) |
| T4 | Data header | msgType 1, payloadLen = 4 + objLen (protocol.py:70-71, 660, 672) | w0 = 1, w1 = 7 for a 3-byte body: f209 (seq105) and f375 (seq188) `01 00 07 00 00 00 00 00 00 00 00 00` (F52, F58) | AGREES |
| T5 | Fetch = "ack" | msgType 2, state = (type, first selector, second selector, 0) (protocol.py:18-19, 723; app.py:620-621) | w0 = 2, w2 = ID, w3..w5 = a, b, c of the DirtyObjects record (F52, F57; H20); fetch f17 (seq9) `02 00 00 00 11 00 00 00 00 00 00 00` | AGREES (identical bytes while c = 0, see S1) |
| T6 | Only polls are answered | "a client that only sends msgType=2 gets zero replies". The data arrives after the following poll (protocol.py:11-12, 18-21) | All 3 001 polls are followed directly by a 12-byte IN header. All 228 fetch headers and both data headers are followed by another OUT transfer, never by a reply of their own. The object comes with the next poll's reply (F53; E2 record "Structure") | AGREES with what E2 shows. "A fetch-only client gets nothing" EXTENDS (never tried on the 1248, and must not be tried) |
| T7 | Reply types | Object data arrives "wrapped in an IN header with msgType=3" (protocol.py:20-21). **msgType 4 is not described anywhere.** ConsoleLink ignores the reply msgType and the echo and uses only payloadLen (app.py:600, 637; protocol.py:730) | Type 3 = fetched object, echoes (ID, a, b, c) 228 / 228, e.g. f22 (seq11) `03 00 56 00 11 00 …`. Type 4 = poll reply: idle with w1 = 0, f92 (seq46) `04 00 00 00 00 00 00 00 00 00 00 00`, or a DirtyObjects list (16 / 16) (F53) | AGREES for type 3. ConsoleLink is silent on type 4. Design note: ConsoleLink cannot detect a wrong object; SmartSoft checks the echo (S11.2) |
| T8 | Header and payload as two transfers | Header and payload are written separately (protocol.py:595-604, 635-647). A second read of exactly payloadLen bytes follows the header read (protocol.py:13-14, 618-623) | One URB per WriteFile / ReadFile, header and payload separate; INs up to 792 bytes are single URBs (F51) | AGREES |
| T9 | OUT payload | `[seq:1][objLen:2 LE][type:1][data]` (protocol.py:71, 635-647) | `[seq][u16 LE len][id][body]`, len = w1 − 4: f211 (seq106) `00 03 00 27 09 00 00`, f377 (seq189) `01 03 00 27 07 00 00` (F54; H18) | AGREES |
| T10 | IN payload | `[seq:2 LE][objLen:2 LE][type:1][data]` (protocol.py:625-633); the sequence value is never used (app.py:606) | `[seq][byte1][u16 LE len][id][body]`, byte1 = 0x00 in 244 / 244 and len = payload − 5 in 244 / 244 (F54). Bytes 2–4 and the body agree. **Byte 1 is not the high byte of a 16-bit counter.** At the only wrap, f88 (seq44) starts `FF 00 3D 00 24` and the next payload, f218 (seq109), starts `00 00 07 00 28`. A carrying 16-bit counter would have given `00 01` | AGREES on bytes 0 and 2–4. CONTRADICTS the 16-bit sequence reading (one wrap seen). The meaning of byte 1 stays UNKNOWN |
| T11 | OUT sequence | A per-link counter. SmartSoft uses 0 and 1 for its two connect-time 0x27 requests and continues from 2 (protocol.py:71-72, 592, 666-668) | SmartSoft: 0x00 (f211), 0x01 (f377), no later write (F55). Static: one global counter, post-incremented per queued message (S11.4, S11.14.3) | AGREES as far as seen. The continuation EXTENDS (ML; INFERRED for sfEngine) |
| T12 | Integers inside bodies | Big-endian counts and first selector (protocol.py:202, 215, 427, 437) | Numeric u16 body fields BE (F54; H36) | AGREES |
| T13 | Product ID per console | ML = 0x0201 (protocol.py:93; README.md:95) | Ours = 0x0200 (F40). `etchsp.inf` lists both (F11). sfEngine selects 0x0200 (F14) | AGREES with the PID-per-product reading (S11.10). Class-5 answer to S11 open lead 5 (the static check of `sfmlEngine.dll` is still not done) |

### 2.2 Session

| # | Element | ConsoleLink (ML) | Our 1248 evidence | Verdict |
|---|---|---|---|---|
| S1 | DirtyObjects = "announce" (0x28) | One entry per ready object, `[0x00][count][type][u16 BE][u16 LE]`. ConsoleLink reads the count as one byte (data[1]) and the last two record bytes as one LE word (protocol.py:15-17, 195-218). Its example: a page-3 / slot-5 memory is announced with `02 04 00` after the page's high byte and acked by SmartSoft with state[2] = 4 (protocol.py:202-206) | Body `[u16 BE count]` + count × `[id][u16 BE a][b][c]`, c = 0 in all 228 records (F57; H20). First list f16 (seq8) `F6 00 2F 00 28 00 09 11 00 00 00 00 0E 00 00 00 00 …` | AGREES on the values. ConsoleLink's LE word = b + 256·c and its 1-byte count give the same values while c = 0 and the count is below 256 (max 157 here, 193 in mems_names.log:8; c = 0 in all ML entries). DIFFERS only in reading. The 1248 engine parses a 16-bit count (S11.7) |
| S2 | Ack obligatory | "This ack step is NOT optional -- skipping it means the announce repeats forever and the real value is never delivered" (protocol.py:22-23) | SmartSoft fetched every record of all 16 lists, 228 / 228 (F57); a skipped record never occurred | EXTENDS (ML). No passive test with SmartSoft is possible |
| S3 | Unprompted announce at connect | The console sends its own announce at connect, which ConsoleLink "loses [in] a race against macOS's automatic USB HID driver probing almost every time" (protocol.py:717-720; app.py:506-509; CONTRIBUTING.md:23-26) | The first poll reply was a 9-record list pending before any write: f14 / f16 (seq7 / seq8): PlaybackInfo, FaderInfo, LedStatus, OutputLevels, ChannelLevels, ConsoleStatus × 2, DisplayContent, FileName (F56) | AGREES (a pending list exists at connect). The race EXTENDS (ML on macOS; not seen with SmartSoft on Windows) |
| S4 | Unsolicited fetch, `request_type()` | A fetch (msgType 2, state[0] = type) without a preceding announce returns current data. The docstring names 0x0e, 0x17, 0x0f, 0x0c and 0x16 (protocol.py:24-26, 713-736); app.py also requests 0x15, 0x11 and 0x0d (app.py:496-536). Fixtures of kind `requested` exist for 0x0e, 0x0f, 0x0c and 0x11 (output_scaling.log:6, :8; faders.log:7; independents.log:5; crossfaders.log:5). **This is ConsoleLink's own technique; ConsoleLink does not say that SmartSoft does it** | Every SmartSoft fetch followed the list that named it (F57); no unsolicited fetch was seen | EXTENDS (ML only, UNTESTED on the 1248). Must not be tried on the 1248 unless a capture shows SmartSoft doing it (§4) |
| S5 | System = "GUI request" (0x27) | Right after connecting, SmartSoft sends subtype 0x09 (version query), then 0x07 (full show-catalog request). 0x07 is answered by "a single huge type=0x28 announce (191 entries)" (protocol.py:649-662) | System body `09 00 00` (f211, seq106) → list [ConsoleVersion] (f218, seq109) → "3.0.2.9.0.43" (f226, seq113). Then `07 00 00` (f377, seq189) → one 157-record list (f384, seq192) (F58, F59; H4, H30) | AGREES (same bodies, same order, same effect) |
| S6 | Catalog list skeleton | mems_names.log:8 holds 193 entries, BE count `00 C1`. First five: `26 00 00 00 00`, `27 00 00 00 00`, `27 00 06 00 00`, `29 00 00 00 00`, `05 00 00 00 00`. Last four: `17 00 00 00 00`, `16 00 00 00 00`, `0D 00 00 00 00`, `0F 00 00 00 00` | f384 (seq192) holds 157 entries; its first five and last four records are byte-identical to the ML list. Runs by ID: ML 38, 39 × 2, 41, 5, [6 × 23, 1 × 24], 8, 0 × 15, 8, 0, 8, 0, 8 × 9, 3, 4 × 13, [7 × 11], 9 × 74, [10], 12, [27, 26 × 3], 39 (a = 1), [24], 36, 23, 22, 13, 15. Ours: 38, 39 × 2, 41, 5, 8, 0 × 9, 8 × 11, 0 × 7, 2, 3, 4 × 19, 9 × 96, 12, 39 (a = 1), 36, 23, 22, 13, 15. Brackets mark ML-only object types | AGREES: the object types the two lists share appear in the same order, and Page(a) is followed by the memories of page a. Strong ML support for H30's "System 7 lists the whole show" |
| S7 | Connecting without System 9 | ConsoleLink never sends 0x09. After 2 s of polling it sends 0x07 alone, with sequence 1, and the ML lists its catalog (app.py:595-599) | SmartSoft always sent 9 before 7 (F58) | EXTENDS (ML, non-SmartSoft client) |
| S8 | MEMS page select | System subtype 0x0a, `[0x0a][page − 1][0]`. The console answers with 0x17, then 0x0e (protocol.py:79-80, 178-181, 693-703) | Not seen. sfEngine's System handler accepts a = 10 only with b < 12 (S11.14.11) | EXTENDS → HYPOTHESIS N6 (§3.2) |

### 2.3 Objects

| # | Object | ConsoleLink (ML) | Our 1248 evidence | Verdict |
|---|---|---|---|---|
| O1 | FaderInfo (14) layout | 99 bytes = `[0x18 = 24][24 × 4-byte records][Bumps][Master]`. Fader N at data[1 + 4(N − 1)], Bumps at data[97], Master at data[98] (protocol.py:220-246). faders.log:5 starts `18 BB 00 00 00 5E 00 00 00 …` and ends `FF FF` | 195 bytes: body[0] = 0x30 = 48, then 48 × 4 bytes and two trailing bytes (F67). body[17] moves (F66) | AGREES on structure. 1 + 4n + 2 gives 99 for n = 24 and 195 for n = 48. Fader 5 = body[1 + 4·4] = body[17], which matches F66's INFERRED fader 5. The ML layout predicts body[193] = Bumps master and body[194] = Grand Master for the 1248. f32 (seq16) body[189..194] = `00 00 00 00 00 AA`, constant in all 14 FaderInfo payloads → HYPOTHESIS N1. Record bytes 1–3 are 0 in all four ML FaderInfo fixtures (faders.log:5; output_scaling.log:6, :12, :14). On the 1248, f32 body[81..84] = `00 00 01 00` and body[97..100] = `00 00 01 00` (records 20 and 24, byte 2 = 0x01): meaning UNKNOWN, ConsoleLink gives none |
| O2 | FaderInfo values are outputs | "output levels, not lever positions: level x Crossfader Live (type=0x11) x Master" (protocol.py:33-36; app.py:65-67). Faders at full read `FF` (output_scaling.log:6), `98` with Live at 60 % (:12), and `5C` with Master `9A` as well (:14) | Fader 5 never exceeded 0xAB and returned to exactly 0xAB twice: f32 (seq16), f2354 (seq1177), f2592 (seq1296) (F66). 0xAB = 67 % in memory data (F72) | EXTENDS → HYPOTHESIS N2: the 0xAB ceiling is a fader at full scaled by a Grand Master at about 67 % (body[194] = 0xAA). Caveat: 255 × 170 / 255 = 170 (0xAA), not 171 (0xAB). The ML data show no such offset (255 × 152 / 255 = 152, output_scaling.log:12) |
| O3 | ChannelLevels (15) | 75 bytes = `[0x4A = 74][72 = 3 × 24 intensity banks][2 bytes]`, with "+2 more bytes, likely Bumps/Master" (protocol.py:37-40, 407-419; faders.log:7) | 51 bytes = `[0x32 = 50][48 channel levels][2]`; sfEngine compares the last two with the Independents (S11.14.11, H26). f56 (seq28) body[0..6] = `32 00 00 00 00 AB 00`; body[49..50] = `00 00` in all 15 payloads | AGREES on `[n][n − 2 levels][2]`; the bank layout is ML-only. ConsoleLink's "Bumps/Master" guess fails on its own fixture. In output_scaling.log:16 the trailing bytes are `00 00`, 62 ms after output_scaling.log:14 showed Bumps `FF` and Master `9A`. This is consistent with H26 (Independents), which is still untested on the 1248 |
| O4 | OutputLevels (13) | 1026 bytes = `[u16 BE 1024][1024 levels]`. DMX N of universe 1 is at data[1 + N] (protocol.py:41-43, 424-441). In dmx.log:11 fader 1 → DMX 1 at data[2]. The console sends it only when a patched output changes (protocol.py:42-43; README.md:246) | 514 bytes = `[02 00][512]`, one universe (F2). f48 (seq24) body[0..7] = `02 00 00 00 00 00 AB 00`, so DMX 5 is at body[6] = body[1 + 5] (F66, F70). OutputLevels was dirty only during the fader activity (F66) | AGREES on the index rule and on change-driven reporting. 1024 channels is ML-only |
| O5 | PlaybackInfo (17) | 209 bytes = `[0x0D = 13][13 × 16-byte records]`. Crossfader Live and Next are at data[195] and data[196], bytes 2 and 3 of the last record. After a completed crossfade the console resets them to Live 255, Next 0 (protocol.py:44-46, 248-265). The last record is `01 01 94 71 00 00 01 00 01 00 00 32 00 32 00 00` in crossfaders.log:5 and `01 01 FF 00 00 00 01 00 01 00 00 32 00 32 00 00` in output_scaling.log:8. The other records are `00 00 FF 00 00 00 01 00 01 00 00 00 00 00 00 00` | 81 bytes = `[0x05][5 × 16]` (S11.6 parser: a count of 16-byte records). f24 (seq12) body[1..16] = `00 00 FF 00 00 00 01 00 01 00 00 0A 00 0A 00 0A`. Records 1 and 3 equal the ML's idle record byte for byte. The last record, body[65..80], is `00 00 FF 00 00 00 01 00 01 00 00 32 00 32 00 00` | AGREES on structure. Our last record matches the ML crossfader record of output_scaling.log:8 in 14 of 16 bytes; only bytes 0–1 differ (`00 00` vs `01 01`) → HYPOTHESIS N5 |
| O6 | DisplayContent (21) | 81 bytes: a marker, then 80 characters = 2 LCDs × 2 lines × 20 (protocol.py:47-52, 284-293). The marker is 'P' = 0x50 = 80 (lcd.log:2). Bytes 0x80–0x87 are bar glyphs from 1/8 to 8/8 (protocol.py:268-282). The INT screen shows "IntA: 1" followed by one cell per fader 1–12, and "13" followed by 13–24 on the second line (protocol.py:268-272; lcd.log:10) | 41 bytes = `[0x28 = 40]` + 40 characters (F67). f80 (seq40) body = `28 20 31 20 20 20 20 84 20 20 20 20 20 20 20 20 55 20 35 2E 30 31 33 20 20 20 20 20 20 20 20 20 20 20 20 20 44 20 35 2E 30` | AGREES: the marker is the character count (0x50 on the ML, 0x28 here; S11.6 parser "body[0] = n"). 40 characters fit one 2 × 20 LCD. Decoded with ConsoleLink's glyph table, line 1 is " 1    ▅        U 5.0" and line 2 is "13             D 5.0". That reads as row labels 1 and 13, twelve bar cells per row at columns 2–13, 0x84 = a 5/8 bar at column 6 = fader 5, and the times on the right: the ML INT screen compressed into one LCD → HYPOTHESIS N4. *Correction to the task brief:* these bytes are in f80 (seq40), not f86 (f86 is the FileName reply header, seq43), and four `20` bytes, not five, sit between `31` and `84` |
| O7 | DisplayContent glyph vs level | A fader brought down to 0 steps 0x85, 0x84 … 0x80, then shows a space (protocol.py:268-272) | body[7] over E2: 0x84 at 0xAB (f80, f2332, f2378, f2562, f2608); 0x83 at levels of about 0x81–0x8A (f2176 (seq1088), f2708 (seq1354)) and while rising through 0x7B–0x9E (f2516 (seq1258)); 0x82 at 0x7F (f2246 (seq1123)); 0x20 at 0x00 (f2424 (seq1212)); 0x80 while rising from 0 (f2470 (seq1235)) | AGREES (monotonic, space at 0). Exact thresholds are UNKNOWN, because DisplayContent is fetched after the level objects of the same list |
| O8 | LedStatus (22) layout | 637 bytes = `[0x6A = 106][106 × 6]` (indicators.log:5). Each LED is a 6-byte block of two RGB triples, one per blink phase. Fader N's (bump) LED is at 1 + 6(N − 1). In INT / PARAM modes it is green `(0, v, 0)` at the fader's output, with both triples equal when not blinking (protocol.py:53-59, 515-536; bump_leds.log:11 block 1 `00 CD 00 00 CD 00`) | 451 bytes = `[0x4B = 75][75 × 6]` (F67). Only body[26] and body[29] change (F66). With 1 + 6 · (5 − 1) = 25, block 5 is body[25..30]: body[26] = G of phase A, body[29] = G of phase B. f40 (seq20) body[25..30] = `00 AB 00 00 AB 00`; f2746 (seq1373) = `00 81 00 00 81 00` | AGREES. The ML layout predicts exactly the two offsets that moved, only the G bytes move, and their values equal FaderInfo body[17]. This strongly supports H32's "fader 5" and the LedStatus layout. The block map is HYPOTHESIS N3 |
| O9 | LedStatus indicators | Solo at 517, BlackOut at 523, Go Mode at 361, Int Only at 373 (protocol.py:539-550). Idle is `0A 0A 0A`. BlackOut blinks `37 37 FF` / `0A 0A 0A` when "BlackOut off but Master pulled below full" (indicators.log:10-11) | The ML offsets 517 / 523 lie beyond the 1248 body (451 bytes). f40 (seq20) block 53, body[313..318], is `37 37 FF 0A 0A 0A`, unchanged in all 14 payloads. Block 52, body[307..312], is `FF FF FF FF FF FF` | EXTENDS → HYPOTHESIS N2: block 53 is the 1248's BlackOut LED, blinking because the Grand Master is below full. The offsets are ML-only; the identical 6-byte pattern is the only link |
| O10 | ConsoleStatus (23) | 0x17, 7 bytes = fader-mode selector (INT / MEMS / PARAM families, sub-modes, MEMS page) (protocol.py:60-63, 299-338). Example: INT B is `00 01 00 00 01 01 00` (fader_modes.log:5) | 7 bytes `00 00 00 00 00 01 00` in f64 (seq32), f72 (seq36) and f1616 (seq808) (F67) | AGREES on ID and length. The semantics EXTENDS: the ML fader modes have no counterpart in [../hardware/smartfade-1248.md](../hardware/smartfade-1248.md), and byte 1 is 0x01 on the ML but 0x00 here |
| O11 | ChannelText (9) | 39-byte records `[index][03 06][pad 00][UTF-16LE 12 + 12 + 11 bytes]`, 74 entries (protocol.py:349-404; labels.log:4-77) | 39 bytes = `[a][03 06][36 bytes]`, a = 0..95 (96 objects) (F67, F73), read as 18 UTF-16BE code units (H40) | AGREES on the 39-byte record and on a in body[0]. DIFFERS in byte order (§2.6). The index mapping is ML-only (§4) |
| O12 | Memory (0) | `[page][slot]…`; the name is tail-anchored in the last 38 bytes as `[03 06][pad 00][12][12][11]`, UTF-16LE. Length varies from 323 to 1047 bytes (protocol.py:444-479; mems_names.log:9-25) | `[a][b][01][60][96 levels][03 06 + 36]` = 138 bytes (F71, H39). (a, b) = (page, slot) as qualifiers (H25) | AGREES on (page, slot) in body[0..1], on body[2] = 0x01 (17 / 17 ML records, 16 / 16 ours) and on the tail-anchored 38-byte text block. DIFFERS on byte order. ML body[3] ∈ {0, 1}, ours is 0x60: nothing to transfer |
| O13 | Independents (12) | 85 bytes. Each entry is `[clicked 0/1][value 0–255][00 01 03 06 00][35-byte name]` = 42 bytes (protocol.py:482-512). independents.log:5 starts `02 00 49 00 01 03 06 00 57 00 6F …`; with IND 1 pressed, :7 starts `02 01 49 …` | 85 bytes. f1592 (seq796) body[0..9] = `02 00 FF 00 01 03 06 00 00 00`, and record 2 at body[43..48] = `00 FF 00 01 03 06`. This equals `$INDEPENDENT n 0 255 0 1` (F76, H44) | AGREES on layout: count 2, 42-byte records, and the constant `00 01` before `03 06`. EXTENDS the meaning: a = latched / pressed, b = level 0–255 on the ML. For the 1248 that would read as not latched, level 255 (proposed annotation for H44) |
| O14 | Types 0x00, 0x10, 0x18 | 0x10 (16 OutputPalettes): 1026 bytes, `[u16 BE 1024]` + flag-like values, undecoded. 0x18 (24 DeviceStatus): a selection `[count][ids]`. 0x00: "constant ~195-byte payload … seen live, not yet in any pcap capture" (protocol.py:64-65, 99-114, 341-346) | IDs 16 and 24 never seen (F67). Memory (0) is 138 bytes | EXTENDS (ML only). The "constant ~195-byte 0x00" note conflicts with ConsoleLink's own variable-length Memory decoder (protocol.py:444-479) and is not evaluated here |

### 2.4 Writes (PC → console), recorded for comparison only

| # | Element | ConsoleLink (ML) | Our 1248 evidence | Verdict |
|---|---|---|---|---|
| W1 | ConsoleEvent (20), kind 1 | `[1][code][pressed]`; the release follows the press after 0.12–0.17 s. ML button codes are listed (protocol.py:73-75, 135-163, 664-674, 705-711) | S11.14.4 builder, kind 1: `[1][button][pressed]`, 3 bytes (H24). No ID 20 in E2 (F67) | AGREES with S11.14.4 on the layout (ML). The codes are ML-only (§4) |
| W2 | ConsoleEvent, kind 0 | `[0][id][value][prev]`, where prev is "the last value sent for that id (0 for the first)", sent about every 0.09 s while dragging. Ids 0–23 = faders, 24 Master, 25 Bumps, 26 / 27 Crossfader Live / Next (protocol.py:76-78, 164-177, 676-691) | S11.14.4 builder, kind 0: `[0][fader][new][previous]`. previous comes from a per-index cache that every event updates, and coalescing keeps the first previous (H24) | AGREES with S11.14.4, including the previous-value semantics (ML SmartSoft per the author). The 1248 id ranges are UNKNOWN (`sub_10016970` not decompiled) |
| W3 | ConsoleEvent, kind 2 | Not mentioned | S11.14.4: `[2][index][value]` | UNKNOWN on both |
| W4 | Write gate | `--allow-write` gates the web page's buttons and faders (app.py:14-24; protocol.py:70). The connect-time System 0x07 is sent **without** that gate (app.py:595-599) | — | See §4: ConsoleLink's default mode is not write-free |
| W5 | Latched state | "The console never releases a button by itself (a bump stays on if the USB cable is pulled mid-press)" (app.py:190-195) | — | EXTENDS (ML). Relevant to safety (§4) |

### 2.5 Host-side behaviour

| # | Element | ConsoleLink (ML) | Our 1248 evidence | Verdict |
|---|---|---|---|---|
| X1 | Exclusive access, "console busy" | If the claim fails, ConsoleLink sets `console_busy` and retries every 2 s; "Only one program can use the console at a time" (app.py:466-478; README.md:227-228; tests/test_console_busy.py:1, 32-55) | sfEngine opens the HSP interface with share mode 0, `CreateFileW(path, 0xc0000000, 0, …)` (S11.14.10) | AGREES in kind (one client at a time) |
| X2 | Stalled bulk pipe | More than 20 consecutive OUT write failures "usually mean a previous session left the bulk pipe stalled". A USB port reset (`dev.reset()`) clears that, a re-claim does not (app.py:577-589, 665-672). Non-daemon thread so that the interface is released (app.py:955-957, 981-982) | No failure in E2: 6 483 URBs, all status 0 (F65) | EXTENDS (ML). A design note for us; a port reset is never automatic (§4) |
| X3 | Timeouts and cadence | 200 ms for header writes and reads, 1000 ms for payload reads (protocol.py:97, 595-623). Polls run back to back without a sleep (app.py:545-602) | SmartSoft polls about every 31 ms when idle; reply headers complete in a median 1.92 ms, max 6.97 ms (F62) | EXTENDS (ML; a host choice). 200 ms is about 29 × the E2 maximum |
| X4 | SET_CONFIGURATION | `dev.set_configuration()` on every connect, errors ignored (app.py:462-465) | No SET_CONFIGURATION on the bus in E2; the only one is the synthetic f5 / f6 that USBPcap injects | DIFFERS (host behaviour) |
| X5 | HID / interface 0 | Never used; the macOS HID probing race at connect (app.py:506-509) | F63 (no URB on 0x81 / 0x02 in 0–280.54 s), H38 | AGREES (the session needs no HID) |

### 2.6 Label byte order, shown with our bytes

f504 (seq252, `GROUP 13`, "NOC" / "BLESKY"), body[100..137]:

```
03 06 00 4E 00 4F 00 43 00 00 00 00 00 00 00 42 00 4C 00 45 00 53 00 4B 00 59 00 00 00 00 00 00 00 00 00 00 00 00
```

- **Reading A (ours, H40 / F73):** after `03 06` come 18 UTF-16BE code units, 3 lines × 6.
  - Line 1 = `00 4E` `00 4F` `00 43` `00 00` `00 00` `00 00` = "NOC".
  - Line 2 = `00 42` `00 4C` `00 45` `00 53` `00 4B` `00 59` = "BLESKY".
  - Line 3 = 12 zero bytes.
- **Reading B (ConsoleLink, protocol.py:349-404, 456-461):** after `03 06` comes one pad byte
  `00`, then UTF-16LE lines of 12, 12 and 11 bytes.
  - Line 1 = `4E 00` `4F 00` `43 00` `00 00` `00 00` `00 00` = "NOC".
  - Line 2 = `42 00` `4C 00` `45 00` `53 00` `4B 00` `59 00` = "BLESKY". Its last `00` is
    reading A's high byte of the first line-3 unit.
  - Line 3 = 11 bytes, that is 5 units plus one dangling byte, = "".
- **Comparison.** Both readings give the same text. For characters below U+0100 they always agree.
  They differ only in where the odd zero byte sits:
  - B's pad byte is A's high byte of the first unit.
  - A's last low byte is B's dangling byte.

Counts:

- **E2, 1248.** All 128 label blocks decode identically under A and B: 16 Memory, 1 Sequence,
  1 SequenceMain, 12 Page, 96 ChannelText and 2 Independents blocks. The last block byte is 0x00
  in 128 / 128. All 2 304 bytes that A reads as high bytes are 0x00. No third line is used.
- **ML fixtures.** All 74 ChannelText labels (labels.log:4-77), 17 Memory names
  (mems_names.log:9-25) and the Independents labels (independents.log:5-9) also decode identically
  under both readings. The 6-character first line "Window" (labels.log:13) is fine in both.
  ConsoleLink says the ML console truncates a 6-character third line ("Orange" → "Orang",
  protocol.py:354-358). That is consistent with labels.log:8-9, where the last byte is 0x00, so
  the sixth character is not on the wire.
- **The owner's SD shows** (read-only check of `smartfade-sd-card-backup/`). The 7 console-written
  `SHOWnn.ASC` files (LF) never use `$$Text 3`: all 875 such lines are empty. Only one CRLF file of
  unknown origin, `KAIMO final 13-06-19.asc`, has third lines of 6 characters (× 2) and of 11
  characters (× 3).

**Conclusion: the byte order is UNDECIDED.** The structural argument of F73 / H40 ("the
little-endian alternative would need a byte beyond the body") excludes only LE with three full
12-byte lines. ConsoleLink's LE reading, with a leading pad byte and an 11-byte third line, also
fills exactly 38 bytes. Every label seen on either console is ASCII.

Two console-side owner actions, each followed by an E2-type capture (E6 probe 5), decide it:

- **A label with "č" (U+010D).** Reading A predicts `01 0D` at an even offset after `03 06`.
  Reading B predicts `0D 01` after the pad byte.
- **A 6-character third line.** If the last block byte carries the sixth character, the 1248 stores
  6 characters on line 3 and reading A holds with three full lines. If it is 0x00, the 1248
  truncates like the ML, and both readings stay possible.

The same question covers the Memory name block (O12) and the Independents label (O13). A
zero-risk static alternative remains: the sfEngine text helpers `sub_10013970` / `sub_100139F0`
(S11 open lead 1) show how SmartSoft's 1248 engine reads the block.

## 3. What this changes for us

### 3.1 Proposed annotations (for the curator; not applied here)

| Item | Proposed annotation | Status change |
|---|---|---|
| H3 | CL-ML: the same poll / announce / ack (fetch) model on the ML (protocol.py:10-23; T2–T7) | none (class-5 support) |
| H4 | CL-ML: ML SmartSoft also sends System 9, then 7 (protocol.py:650-651). A non-SmartSoft client got the ML catalog with 7 alone (app.py:595-599) | none; the mismatch part stays OPEN |
| H5 | CL-ML: on the ML, SmartSoft's virtual controls are ConsoleEvent writes (protocol.py:135-138, 164-167) | none for the 1248 (the ConsoleEvent part stays OPEN) |
| H6 | CL-ML: no "set level" object on the ML either; control is ConsoleEvent fader moves and button presses | none |
| H7 | see §3.3 | none (priority unchanged) |
| H17, H18, F54, F55 | CL-ML reads inbound bytes 0–1 as a u16 LE sequence (protocol.py:626-629). E2's wrap f88 → f218 contradicts a carrying 16-bit counter (T10). Byte 1 stays UNKNOWN | none |
| H19, F15 | CL-ML uses the same IDs for 0, 9, 12–15, 17, 20–23, 39 and 40. Its catalog also lists IDs not seen on the 1248: 1, 6, 7, 10, 24, 26, 27 (mems_names.log:8), and it names 16 (protocol.py:103-106). The name table looks shared across engines as far as it is visible | none (IDs not seen on the 1248 stay untested) |
| H20, F57 | CL-ML's `[type][u16 BE][u16 LE]` equals ours while c = 0 (c = 0 in all 193 ML entries and all 228 E2 records); ConsoleLink reads the count as one byte (S1) | none |
| H24 | CL-ML corroborates the S11.14.4 layouts of kinds 0 and 1 and the previous-value semantics (ML SmartSoft traffic per the author; W1, W2) | none (UNTESTED on the 1248) |
| H26 | ConsoleLink's own fixture refutes its "Bumps/Master" guess for the two trailing bytes; consistent with IND1 / IND2 (O3) | none |
| H30, F58 | CL-ML: System 7 is answered by one catalog announce (protocol.py:651-653). The ML list has the same first five and last four records as f384 (S6) | none (still one 1248 observation; causality stays INFERRED) |
| H32, F66 | The ML layouts predict four E2 bytes, and all four match: FaderInfo is 195 bytes for 48 records with fader 5 at body[17], LedStatus has fader 5's G bytes at body[26] and body[29], and OutputLevels has DMX 5 at body[6]. On "0xAB is the top or a limit", CL-ML suggests the limit is Master scaling (N2) | none; wording "layout supported by CL-ML (class 5)" |
| H36, H40, F73; packet-format.md, responses.md, asc-format.md | Byte order: the structural argument does not exclude ConsoleLink's pad + LE (12 / 12 / 11) reading (§2.6) | **Byte-order part of F73: INFERRED → HYPOTHESIS, two competing readings (A: BE, B: pad + LE).** Not REJECTED; the text match with the file stays OBSERVED, because both readings give the same text. Add the 6-character third-line check to H40's test |
| H38, F63 | CL-ML never opens interface 0 either (T1, X5) | none |
| H44, F76 | CL-ML meaning: a = latched (0 / 1), b = level 0–255, c d = constant `00 01` (independents.log:5-9). That would make `00 FF 00 01` "not latched, level 255" | none (the meaning stays HYPOTHESIS) |
| H45, F77 | The same three u16 values (5.0, 5.0, 0) also appear in PlaybackInfo's last record, f24 body[75..80], and in the ML crossfader record, crossfaders.log:5 (O5) | none (supports the "times" reading) |
| H31, H33 | no information (ConsoleLink ignores the inbound sequence) | none |
| S11 open lead 5 | ML = PID 0x0201 (README.md:95), class 5; the static check of `sfmlEngine.dll` is still open | none |

*(Curator note 2026-10-29: the proposals of this table were applied as "CL-ML" annotations in
[hypotheses.md](hypotheses.md) and [known-facts.md](known-facts.md) and as the column CL-ML of
[evidence-matrix.md](evidence-matrix.md); the byte-order part of F73 / H36 / H40 was downgraded from
INFERRED to HYPOTHESIS as proposed. Section 3.5 lists the E3 outcomes.)*

### 3.2 New hypotheses (IDs to be assigned by the curator)

All tests are passive: owner actions on the console with SmartSoft Online and a USBPcap
capture. Nothing is sent by our software. Safety rules 1–6 of
[reverse-engineering.md](reverse-engineering.md) apply, including the backup before the session.

| N | Hypothesis (1248) | Basis | Passive test | Status |
|---|---|---|---|---|
| N1 | FaderInfo = `[48][48 × 4][Bumps master][Grand Master]`: body[193] is the Bumps master level and body[194] the Grand Master level. In E2, Bumps = 0x00 and GM = 0xAA | CL-ML layout (protocol.py:221, 229-232; output_scaling.log:14 shows Master `9A`). E2 f32 (seq16) body[193..194] = `00 AA`, constant in 14 / 14 payloads. The 1248 has a GM and a Bumps master fader (DOCUMENTED, [hardware doc](../hardware/smartfade-1248.md) "Masters") | **E3(c)**: move the GM full → 0 → full with typed times; expect only body[194] to change within FaderInfo. Bumps master: an extra E3 single action (ID to be assigned); expect body[193] to change | HYPOTHESIS |
| N2 | The live level objects (FaderInfo records, ChannelLevels, OutputLevels, LedStatus G bytes) carry post-GM outputs. In E2 the GM at about 67 % capped fader 5 at 0xAB. LedStatus block 53 (body[313..318] = `37 37 FF 0A 0A 0A`) is the BlackOut LED, blinking because the GM is below full | CL-ML scaling (protocol.py:33-36; output_scaling.log:12-16) and its BlackOut blink pattern (indicators.log:10-11); E2 f32 (seq16) and f40 (seq20) | **E3(c)**: with a channel fader at full, raise the GM to full. Expect body[194] to go to 0xFF, the channel's level bytes to rise towards 0xFF, and block 53 to stop blinking (pattern change). Caveat: 255 × 170 / 255 = 170 ≠ 0xAB, so the arithmetic is open | HYPOTHESIS |
| N3 | LedStatus = `[75][75 × (RGB phase A, RGB phase B)]`. Blocks 1–24 are the LEDs of faders 1–24: green at the fader's output and `00` when the fader is at 0. Blocks 25–48 sit at `00 0A 00 00 0A 00` (dim). Blocks 49–75 are other indicators | CL-ML block format (protocol.py:53-59, 515-536; LED_IDLE_MAX = 0x0A, protocol.py:566-569); E2 f40 (seq20) block view (O8, O9) | **E3(a)**: fader 1 → expect body[2] and body[5] (G bytes of block 1) to follow FaderInfo body[1]. **E3(b)**: press a memory bump and see which block changes | HYPOTHESIS |
| N4 | DisplayContent = `[40][line 1: 20][line 2: 20]` of one 2 × 20 LCD, with bytes 0x80–0x87 as bar glyphs from 1/8 to 8/8. The E2 screen shows row labels 1 / 13, cells for faders 1–12 / 13–24 at columns 2–13, and the fade times at columns 15–19 | CL-ML (protocol.py:268-293; lcd.log:10); E2 f80 (seq40) and the body[7] history (O6, O7) | **E3(a)**: fader 1's cell is body[3] (line 1, column 2). Expect 0x20 at 0 and rising glyphs from 0x80; 0x87 at full only if the GM is at full (N2) | HYPOTHESIS |
| N5 | PlaybackInfo = `[5][5 × 16]`. Record 4 (body[65..80]) is the main playback with the crossfader pair: body[67] = Live and body[68] = Next scene level (E2: 0xFF / 0x00). body[75..80] = three u16 BE times (E2: 5.0, 5.0, 0). Records 0–3 might be the four sequence faders of the page (F3; untested) | CL-ML (protocol.py:44-46, 248-265; crossfaders.log:5-17; output_scaling.log:8-10); E2 f24 (seq12) (O5) | **E3(d)** (GO on a safe stack), or a manual crossfade as an extra E3 single action: expect body[67] / body[68] to move and settle at 0xFF / 0x00 | HYPOTHESIS |
| N6 | System (39) body `[0x0A][page 0..11][0x00]` selects the memory page | CL-ML subtype 0x0a (protocol.py:79-80, 178-181). sfEngine accepts System a = 10 only with b < 12 (S11.14.11). The 1248 has 12 pages (F3) | Capture while the owner changes the memory page on the console, and separately in SmartSoft if it offers a page control (that is a SmartSoft write: owner decision). Look for System a = 10 in a DirtyObjects list or in SmartSoft's outbound data. **Never sent by us** | HYPOTHESIS |

Open questions without a passive test (stay UNKNOWN; do not probe):

- Does the 1248 answer a fetch for an object it did not list (S4)? Only a capture showing
  SmartSoft doing it would allow this question to be studied.
- Does the 1248 keep re-listing a record that is never fetched (S2)? SmartSoft always fetches,
  so a capture cannot show it.

### 3.3 Implication for H7 (user-mode USB)

- **Class-5 support for H7.** ConsoleLink drives the ML session over libusb (pyusb) on macOS, on
  Linux / Raspberry Pi and on Windows, with no ETC driver (pyproject.toml:15-34;
  README.md:54-126). This supports H7's premise that the HSP pipe is ordinary bulk I/O, which a
  user-mode stack can drive.
- **No IOCTL is needed on the bus.** The `etchsp.sys` IOCTLs (0x222000 / 4 / 8, S11.10) are
  private to the driver. In E2 only the product-string IOCTL reached the bus, as a standard
  GET_DESCRIPTOR(STRING 2) (f8 / f10, F41). A user-mode client can therefore read the same string
  with a standard request (INFERRED).
- **What the README requires on Windows.**
  - The README requires WinUSB, installed with Zadig, on **both** interface 0 and interface 1,
    because "libusb can't open it through the ETC driver installed with SmartSoft".
  - Afterwards SmartSoft no longer sees the console. To revert, uninstall the device together with
    its driver software, or reinstall SmartSoft (README.md:119-126).
  - The README gives no reason for replacing the HID driver on interface 0, and ConsoleLink never
    opens that interface.
- **For us.** H7's test stays as planned:
  - WinUSB goes on `MI_01` only, and the HID class driver stays on `MI_00`.
  - The binding is a deliberate, reversible **owner** action.
  - ConsoleLink adds two checks to that test. Can libusb / WinUSB claim `MI_01` while `MI_00`
    stays with HidUsb? Is a Zadig-style binding of the inbox `winusb.sys` accepted with Memory
    Integrity on? F46 covers only `etchsp.sys`, and ConsoleLink says nothing about HVCI. Both
    checks are untested.
- **Consequence for captures.** While WinUSB is bound, SmartSoft cannot reach the console. E3
  captures of SmartSoft therefore still need `etchsp.sys`, which means Memory Integrity off on
  the capturing PC. The two setups cannot coexist on one PC without switching drivers
  (README.md:125-126).

### 3.4 Design notes (not protocol facts)

- ConsoleLink ignores the reply type and the type-3 echo, while SmartSoft checks the echo (S11.2).
  Our decoder should check it (F53).
- The stalled-pipe recovery by port reset and the 200 ms timeouts are ML host choices. For us,
  any recovery that re-enumerates the console needs its own safety review.
- **Reading state without writing.** The initial pending list (F56) needed no write, but the full
  show appeared only after System 7 (H30). ConsoleLink's alternative, unsolicited fetches, is not
  SmartSoft behaviour. Neither is sendable today.

### 3.5 Outcomes of the E3 test and where the proposals went (curator, 2026-10-29)

*Added by the curator; the researcher's text above is unchanged.* The new hypotheses N1–N6 of §3.2 got the
IDs **H46–H51** in [hypotheses.md](hypotheses.md). E3 ([record](captures/2026-10-29-e3-console-actions.md))
tested five of the six; the owner's answers of the same evening
([statements](captures/2026-10-29-e3-owner-statements.txt)) and the console manual
([known-facts.md](known-facts.md) F5–F8) were added. Nothing here turns a ConsoleLink claim into a 1248
fact by itself: every outcome rests on our own capture, and the manual and the owner are named where they
were used.

| N | ID | Outcome (E3) | Status now |
|---|---|---|---|
| N1 | H46 | FaderInfo body[194] moved with the MASTER fader only (0xFF → 0x83 → 0xFF, 31 changes; F82); body[193] = 0x00 in all 80 payloads while the BUMPS fader stood at 0 % (owner) | MASTER part CONFIRMED (wire position OBSERVED in one session, time-correlated with the logged moves and the owner's panel label; the Master/Blackout behaviour is DOCUMENTED); BUMPS part HYPOTHESIS, consistent, not yet seen moving |
| N2 | H47 | LedStatus block 53 blinks `37 37 FF` / `0A 0A 0A` exactly while the MASTER is below full (F85), and the owner saw the Blackout LED blink; the scaling of live levels was not tested (no channel up); arithmetic: H57 | LED part CONFIRMED; scaling HYPOTHESIS (the behaviour is DOCUMENTED, manual p.56) |
| N3 | H48 | LedStatus block map confirmed for faders 1 and 5, blocks 25–48 and the memory-mode colours (F84); the manual's colours (red memories, yellow sequences, green channels) match | CONFIRMED (OBSERVED); extension to all faders INFERRED |
| N4 | H49 | Layout confirmed; "0x87 at full only if the MASTER is at full" is contradicted: the top state is 0x86, 0x87 never occurs (F86) | layout CONFIRMED; sub-prediction REJECTED |
| N5 | H50 | PlaybackInfo was never listed in E3 (F89) | UNTESTED |
| N6 | H51 | No System `[0x0A][page][0]` write and no such object; the owner stayed on page 1 | UNTESTED |

Other ConsoleLink claims touched by E3: **T10** (inbound bytes 0–1 as a 16-bit sequence) is contradicted
again, the wrap f10942 → f10950 keeps byte 1 = 0x00; **O10** (ConsoleStatus byte 0 = fader-mode family)
is consistent, 0x00 → 0x01 at the switch to memory mode (F87, H53); **S3** (a pending list at connect) is
consistent but the content differs, 1 record (InputLevels) after a power cycle (F80); **O9** (the BlackOut
LED pattern) is matched by block 53 (F85); **§2.6** (label byte order) is unchanged, still undecided, no
label changed. Names: the 1248 panel labels are MASTER and BUMPS (owner, Q5), the manual says "Master
fader" and "BUMPS fader" (printed pp.56, 12), the knowledge base writes **MASTER (Grand Master)** and
**BUMPS (bump master)**.

*(Update 2026-10-30: the E3b outcomes for N1, N2, N3 and N5, which supersede the rows "BUMPS part HYPOTHESIS",
"scaling HYPOTHESIS" and "UNTESTED" above, are in §3.6.)*

### 3.6 Outcomes of the E3b test and where the proposals went (curator, 2026-10-30)

*Added by the curator; the researcher's text and §3.5 are unchanged.* E3b
([record](captures/2026-10-30-e3b-live-objects.md)) tested the parts of N1, N2, N3 and N5 that E3 left open;
the owner's statements of the same day ([E3b statements](captures/2026-10-30-e3b-owner-statements.txt)) explain
the GO crossfade. Nothing here turns a ConsoleLink claim into a 1248 fact by itself: every outcome rests on
our own capture, and the owner is named where he was used.

| N | ID | Outcome (E3b) | Status now |
|---|---|---|---|
| N1 | H46 | FaderInfo body[193] moved 0x00 ↔ 0xFF 104 times, in four groups that coincide with the owner's four logged BUMPS moves; nothing else changed in those windows (F95) | BUMPS part CONFIRMED (OBSERVED); MASTER part as in §3.5 |
| N2 | H47 | With fader 5 at full all level objects followed the MASTER (0xFF → 0x7F → 0xFF) and read 0x7F together at rest at 'half'; the owner saw fixture 1 dim (F96). ConsoleLink's 'output levels incl. Master' (O2) fits. The arithmetic of H57 (the E2 fit) is rejected | Scaling part CONFIRMED (OBSERVED); the exact rule is open (H60) |
| N3 | H48 | Extended: blocks 1–24 follow the physical faders in every mode and bank; blocks 25–48 constant; SOLO is white in block 52 (ConsoleLink 'Solo on = white', O9, fits); the bump LED 'FF while the memory is up, 0x46 recorded, 0x0A empty' reproduced (F84, F100) | CONFIRMED (OBSERVED) for the blocks tested |
| N4 | H49 | Layout reproduced; row labels '25' / '37' in bank 25-48; 0x87 still never occurs (F86, F98) | layout CONFIRMED; unchanged |
| N5 | H50 | One GO crossfade (pressed by the owner, STACK button unlit, E3b-A1): Live / Next at body[67] / [68] moved in opposite directions and were reset to `FF/00`, as ConsoleLink says for the ML crossfader (O5); body[75..78] counted down 5.0 s in 1-s steps (F101) | layout CONFIRMED (OBSERVED once) |
| N6 | H51 | No page change and no System `[0x0A][page][0]` | UNTESTED |

Other ConsoleLink claims touched by E3b: **T10** (inbound bytes 0–1 as a 16-bit sequence) is contradicted four
more times (six wraps in all; byte 1 stays 0x00; F93). **O5**: the same three times 5.0, 5.0, 0 in PlaybackInfo's
last record and, in E2, in SystemSettings, which the curator note under H45 read as support for the 'times'
reading, is **weakened**: after the crossfade PlaybackInfo read 5.0, 5.0, 0 (f104238) while SystemSettings read
5.1, 5.1, 0 (F101, F102), so the E2 equality was a coincidence of two 5.0 values (INFERRED: PlaybackInfo carries
a stack step's times). The default-times reading of SystemSettings now rests on the LCD correlation and the
owner's wheel move (H45). The ML record bytes 0–1 of that last record are `01 01` (O5), ours `00 00` in E2 and
`06 06` / `04 04` in E3b (H63). ConsoleLink's 'mode-agnostic' FaderInfo (type 0x0e, app.py:57, 118) fits: records
1–24 follow the physical faders in bank 25-48 and in memory mode (F99). **O10** (ConsoleStatus byte 0 = fader-mode
family, `data[4]` = intensity sub-mode) fits body[0] and body[4] (F98). **S3** (a pending list at connect) fits,
but the content differs again (LedStatus and DisplayContent after E3b's power cycle, nothing after a restart;
F94, H65). §2.6 (label byte order) is unchanged: no label changed. Names: the panel labels MASTER and BUMPS
(owner, Q5); SOLO and the GO ('play') button as the owner calls them (E3b-A1).

## 4. What must NOT be inferred

CLAUDE.md, verbatim:

- "**Never invent hardware protocol behaviour.** No guessed commands, byte sequences, checksums,
  IDs or timings, including in mocks, examples and tests."
- "Only **VERIFIED** protocol elements may be used by code that can talk to real hardware."
- "Never send unverified data to the real console."

Safety rule 2 of [reverse-engineering.md](reverse-engineering.md): "Never send captured or
hand-built bytes to the console until the message is understood and its effect is known to be
harmless".

1. **ML numbers.** None of these applies to the 1248:
   - 24 faders (protocol.py:168).
   - Control ids 24–27 = Master, Bumps, Live, Next (protocol.py:169-177). Even on the ML this
     order (Master before Bumps) differs from FaderInfo's Bumps-then-Master order
     (protocol.py:221), so ids cannot be derived from object offsets.
   - Button codes (protocol.py:139-163) and button timing (release after 0.12–0.17 s, presses
     under about 0.15 s ignored; protocol.py:149, 158-162).
   - Two LCDs / 80 characters (protocol.py:47-52).
   - 1024 DMX channels in two universes (protocol.py:41-43, 422-441).
   - 106 LED blocks and the LED offsets 361 / 373 / 517 / 523 (protocol.py:539-550).
   - 13 PlaybackInfo records.
   - The fader-mode families of 0x17 (protocol.py:299-338).
   - The 74 ChannelText entries and their family mapping (protocol.py:359-363).
   - The MEMS page semantics, and the 0x10 / 0x18 decodes.
2. **Writes.** ConsoleLink sends ConsoleEvent (20) button and fader messages and System (39)
   requests to an ML console (protocol.py:70-80, 649-711; app.py:555-575, 595-599).
   - For us every PC → console message stays non-sendable until it has been observed from
     SmartSoft on the 1248, understood, shown to be harmless and cleared by
     `/hardware-safety-check`. That includes the two System bodies SmartSoft itself sent
     ([commands.md](commands.md)).
   - Agreement between ConsoleLink's ML bytes and our static S11.14.4 reading does not make a
     1248 ConsoleEvent known. The index translation (`sub_10016970`, `sub_10016800`) and the
     console's reaction are unobserved.
3. **Behaviour of a non-SmartSoft client.** These are all experiments a third-party client made
   on the ML; none was seen from SmartSoft on the 1248 (F52, F55, F57, F58, F62):
   - unsolicited fetches (`request_type()`)
   - idle polls with zeroed state words
   - skipping System 9
   - starting the outbound sequence at 1
   - back-to-back polling without a sleep
   - `set_configuration()` on connect
   - an automatic USB port reset

   Do not try them on the console. A fetch is also a transmission with console-side effects
   (safety rule 2, E2 note).
4. **ConsoleLink's default mode is not write-free.** It sends System `07` with sequence 1 on every
   connect (app.py:595-599), outside the `--allow-write` gate (protocol.py:70). A "read-only" label
   on a third-party tool is no evidence that a message is harmless.
5. **Writes can leave state behind.** ConsoleLink reports that the ML never releases a held button
   by itself if the cable is pulled mid-press (app.py:190-195).
6. **ConsoleLink fixtures are not our captures.** Golden tests may use only bytes from a committed
   capture in `docs/protocol/captures/` (CLAUDE.md). The fixtures also lack headers and sequence
   bytes. No ConsoleLink code or data is copied into this repository, only short quotes and
   file:line references (MIT licence).
7. **"Agrees with ConsoleLink" never promotes a 1248 status.** Promotion needs evidence on our
   unit ([README.md](README.md)).

## 5. For the curator: evidence-matrix cells to extend

*(Curator note 2026-10-29: applied. The matrix got a column "CL-ML" after "SFML", the legend entry below,
and the notes of this table in the matching rows; no status was changed because of them. A row "Memory
bumps (read side)" was added for E3.)*

**Legend (proposal).** Add "**CL-ML** = ConsoleLink 0.1.8, a third-party Python client for the
SmartFade **ML** (commit `09b3c26`, MIT; evidence class 5;
[consolelink-analysis.md](consolelink-analysis.md)). Not 1248 evidence."

The matrix has no column for this source. Either add a column "CL-ML" (for example after
"SFML"), or append the sentence to the row's **Status** cell without changing the status.
Proposed notes:

| Row (Feature) | Proposed "CL-ML: …" note |
|---|---|
| USB link to PC | ML = PID 0x0201; the session runs over libusb / WinUSB without `etchsp.sys` on macOS, Linux and Windows (README.md:54-126; §3.3); class-5 support for H7 |
| HID interface (`MI_00`) | Never opened by ConsoleLink; its README still binds WinUSB to interface 0 without giving a reason; the connect-time announce is lost to macOS HID probing (app.py:506-509) |
| USB message framing | Same 12-byte header, types 0 / 1 / 2 out and 3 in (4 not described), same payload layouts. ConsoleLink reads inbound bytes 0–1 as a u16 sequence, which our wrap f88 → f218 contradicts (T1–T13) |
| Version handshake | System = "GUI request": 9 = version query, 7 = catalog request. The ML catalog list has the same first five and last four records as f384 (S5, S6) |
| Channel level (write) | ML control = ConsoleEvent kinds 0 / 1, as in S11.14.4; ML-only, nothing sendable (W1–W3) |
| Channel / output level readback | OutputLevels DMX N at body[1 + N], which matches body[6] = DMX 5. ChannelLevels `[n][n − 2][2]`. On the ML the values are post-Master (O2–O4) |
| Fader positions | FaderInfo `[n][n × 4][Bumps][Master]` → 195 bytes for 48 records and fader 5 at body[17], both matching E2; on the ML the values are outputs (O1, O2) |
| Grand master | Master = the last FaderInfo byte on the ML. On the 1248, body[194] = 0xAA in E2 (N1, N2) |
| Blackout | On the ML the BlackOut LED blinks `37 37 FF` / `0A 0A 0A` when Master is below full. The same 6 bytes sit in the 1248's LedStatus block 53 (N2) |
| Bumps master | On the ML, Bumps = the byte before Master. On the 1248, body[193] = 0x00 (N1) |
| Crossfader faders | Live / Next = bytes 2 / 3 of PlaybackInfo's last record on the ML. On the 1248, record 4 = `00 00 FF 00 …` (N5) |
| Cue/stack GO / PAUSE | PlaybackInfo `[count][count × 16]`; on the ML, Live 255 / Next 0 after a completed crossfade (N5) |
| Memories (288) | Memory (page, slot) in body[0..1], body[2] = 0x01, tail text block. MEMS page select on the ML = System a = 10 (N6) |
| Independents | `[latched][level][00 01][label]` (O13; H44) |
| Labels / text | Alternative reading: a pad byte plus UTF-16LE 12 / 12 / 11. Same text for ASCII; byte order UNDECIDED (§2.6) |
| LEDs / LCD mirror | LedStatus 6-byte two-phase RGB blocks, fader N at 1 + 6(N − 1), which matches body[26] / [29]. DisplayContent `[n]` + n characters with bar glyphs 0x80–0x87, which matches f80 (O6–O9) |
| Load/save show | On the ML too, FileName (36) and the whole show catalog are listed after System 7 (S6) |

Other documents the curator may want to touch:

- `packet-format.md` "Byte order" row and `responses.md` "Text block" / "Label text": UTF-16BE is
  INFERRED from structure, and the structure also fits a LE reading.
- `asc-format.md` "Text block": same point. Also note the one CRLF show with 11-character third
  lines (§2.6).
- `smartsoft-analysis.md` open lead 5: ML = PID 0x0201, class 5.
- `docs/source-materials.md`: record the ConsoleLink checkout as a class-5 source with the commit
  and the hashes above.
- `docs/protocol/README.md`: add this file to the documents table.
