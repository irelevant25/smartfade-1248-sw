# SmartFade 1248 control software

An independent, modern PC application to control and monitor an **ETC SmartFade 1248**
lighting console, plus tooling to understand how the console communicates and to convert
SmartSoft/console `.asc` show files.

**Status:** Phase 0 (discovery). Source materials audited; experiments E1 (USB identification)
and E2 (first USB capture of a SmartSoft session: poll / dirty-list / fetch framing) done on
2026-10-28, E3 (console actions while SmartSoft is Online, after a console power cycle) on
2026-10-29 and E3b (three more SmartSoft sessions after another power cycle: BUMPS, bumps, SOLO, the MASTER
scaling, the 25-48 bank, a GO crossfade) on 2026-10-30, and E8 (the first read-only session of our own software,
861 s over WinUSB) and E8b (the second, 1 121 s, with USBPcap and a timed action log) the same day, and E9 (SmartSoft Online writing to
the console through its Console Simulator, recorded passively: the first observed PC -> console writes) that night, and E9b (its repeat: three more
SmartSoft sessions, 427 writes) on 2026-10-31, and E12 (SmartSoft editing the patch, the stack and a sequence, passive, 70 writes) the
same afternoon, analysed that evening, and E12b (its repeat, 62 writes, with an SD save of the edited show made on the console) the same
late evening, analysed that night. 72 facts are VERIFIED as
observed console / SmartSoft behaviour (USB framing, start-up, show objects, front-panel mirroring, memory faders and pages,
BLACKOUT, the independents, the record-function screens, the console's counter and the file name it reports, the write framing and the main
fader and key events as SmartSoft sends them, the Memory write's layout and the console's listing of its own recordings, SmartSoft's step
commands and edit writes and the console's answers to them, product string,
firmware version; several only in part, the structural parts only, never a live value; before E3 only the last two were). VERIFIED never
clears a message for sending: no protocol message is cleared, so no code may talk to a console yet
*(superseded in part on 2026-10-30 by safety check SC-1, which clears the poll and the listed fetch for
a read-only session; see below)*.
**Phase 1 is built (2026-10-30):** the first vertical slice Angular → SignalR → .NET → simulated
console, with protocol decoders tested against the committed captures. **Safety check SC-1
(2026-10-30)** cleared the poll and the listed fetch for a read-only session: a WinUSB device
exists behind `--Device:Mode=Usb`, replay-tested against every capture; its first live session
(E8, 2026-10-30) read the console for 861 s with polls and listed fetches only (the console kept reporting its live
state; H66 and H67 CONFIRMED for what was used) and cleared nothing further; its second (E8b, the same evening, 1 121 s) showed on the
bus that nothing else was sent and that no control transfer reached the console at the backend start or at Connect (H68 CONFIRMED),
tied the independents' and BLACKOUT's keys to their LEDs with timed presses, and also cleared nothing further. E9 (the same night) observed
SmartSoft's first PC -> console writes (ConsoleEvent fader and key events and a channel label, 401 writes in all) and cleared nothing: they
are OBSERVED in one session (E9b, the repeat, made the framing and the main fader and key events VERIFIED as SmartSoft's and the console's
behaviour, never clearance, and cleared nothing either), no data header is encoded anywhere in the solution *(Superseded 2026-10-31, later the same day: the owner implemented SC-2's whitelist in `HspOutbound` and `HspReadSession`, sent only by a session with live control switched on for the day (`Device:Usb:LiveControlDate`); reviewed under SC-2 item 10, NEEDS CHANGES at the first pass, see safety-checks.md; nothing is cleared for sending before E11 on the owner's go.)*, and safety check SC-2 (2026-10-31, [safety-checks.md](docs/protocol/safety-checks.md)) is the gate
before any write of ours: a conditional GO for building and reviewing a live-control encoder bounded to the fader indices 0x00 / 0x30 / 0x31 and eight live key codes, with the first write session E11 on the owner's go, and a NO-GO for the record keys from the PC (SC-3 later), ChannelText and Memory writes and the System pair; it clears nothing for sending until its conditions are met. That session also changed memory 7 of page 1 by mistake; the owner restored it by hand on 2026-10-31, and `SHOW09.ASC`, saved afterwards, equals the pre-E9 backup byte for byte. A static pass of 2026-10-31 mapped (INFERRED, never seen on the bus) the messages SmartSoft would send when the owner edits the patch, the independents, page and memory names, sequences and the stack, and the passive capture of those edits, E12, was run the same afternoon ([docs/protocol/captures/2026-10-31-e12-smartsoft-patch-stack-edits.md](docs/protocol/captures/2026-10-31-e12-smartsoft-patch-stack-edits.md): 70 writes by SmartSoft, 0 framing errors, every edit restored, `SHOW13.ASC` = `SHOW12.ASC` byte for byte). Its record holds provenance, completeness and structure only; the decoding of the writes against H91–H101 is pending (not done by the assistant, whose attempt was stopped by its safety system; the owner or a later pass does it with the committed tools). It clears nothing and our code never sends any of these messages. *(Superseded in part 2026-10-31, evening, text kept: the decoding was done by the `capture-analyst` agent on Opus 5.5 at the owner's request, with read-only tools, and applied to the knowledge base: SmartSoft's step commands (InsertStep, DeleteStep, MoveStep, id 42) and its Patch, Independents, StepContent, Sequence and Page writes are OBSERVED once (F151–F159), F143 and F147 became VERIFIED and F144 and F146 VERIFIED in part (65 VERIFIED facts); nothing is cleared, and our code still never sends any of these messages.)* The repeat, E12b, ran the same late evening ([docs/protocol/captures/2026-10-31-e12b-smartsoft-patch-stack-edits-repeat.md](docs/protocol/captures/2026-10-31-e12b-smartsoft-patch-stack-edits-repeat.md): 62 writes by SmartSoft, every edit restored, `SHOW16.ASC` = `SHOW14.ASC` byte for byte) and was analysed by the `capture-analyst` agent on Opus 5.5 at the owner's request: where it reproduced E12, SmartSoft's step commands and edit writes and the console's answers are VERIFIED as behaviour (72 VERIFIED facts; F160–F165 added: the channel step, a label the console stored and exported as UTF-16BE, the live stack addressed by index, the key codes 0x32 and 0x3A). The console stopped answering USB for about 0.85 s while it saved the show to its SD card, so no SD save or load is made on the console while our software is connected. Nothing is cleared, and our code never sends any of these messages.

## Run it

```
dotnet build SmartFade.sln && dotnet test SmartFade.sln      # .NET 10 SDK (global.json)
dotnet run --project src/backend/SmartFade.Api              # backend, http://localhost:5160, simulated console
cd src/frontend && npm install && npm start                 # UI, http://localhost:4200 (proxies to 5160)
```

The simulator is the default device. The read-only hardware mode
(`dotnet run --project src/backend/SmartFade.Api -- --Device:Mode=Usb --Device:Usb:ConfirmDate=<today>`)
is for read-only hardware sessions only (E8 and E8b done 2026-10-30), after the preparation in
`docs/protocol/reverse-engineering.md`; it is
confirmed per day and never stays switched on. See `docs/api.md` for the UI contract and
`docs/architecture.md` ("Phase 1 as built") for the layout.

| Start here | |
|---|---|
| What we know about the hardware | [docs/hardware/smartfade-1248.md](docs/hardware/smartfade-1248.md) |
| Protocol knowledge base | [docs/protocol/README.md](docs/protocol/README.md) |
| Evidence matrix | [docs/protocol/evidence-matrix.md](docs/protocol/evidence-matrix.md) |
| Next experiments | [docs/protocol/reverse-engineering.md](docs/protocol/reverse-engineering.md) |
| ASC show file format | [docs/formats/asc-format.md](docs/formats/asc-format.md) |
| Architecture (proposal + "Phase 1 as built") | [docs/architecture.md](docs/architecture.md) |
| UI ↔ backend contract (SignalR, REST, DTOs) | [docs/api.md](docs/api.md) |
| Source materials and hashes | [docs/source-materials.md](docs/source-materials.md) |
| Rules for contributors / Claude | [CLAUDE.md](CLAUDE.md) |

Third-party material in `smartsoft-source-materials/` belongs to its owners (ETC and others)
and is used for interoperability research only.
