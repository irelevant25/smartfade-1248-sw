# SmartFade 1248 control software

An independent, modern PC application to control and monitor an **ETC SmartFade 1248**
lighting console, plus tooling to understand how the console communicates and to convert
SmartSoft/console `.asc` show files.

**Status:** Phase 0 (discovery). Source materials audited; experiments E1 (USB identification)
and E2 (first USB capture of a SmartSoft session: poll / dirty-list / fetch framing) done on
2026-10-28, E3 (console actions while SmartSoft is Online, after a console power cycle) on
2026-10-29 and E3b (three more SmartSoft sessions after another power cycle: BUMPS, bumps, SOLO, the MASTER
scaling, the 25-48 bank, a GO crossfade) on 2026-10-30, and E8 (the first read-only session of our own software,
861 s over WinUSB) the same day. 39 facts are VERIFIED as observed console / SmartSoft
behaviour (USB framing, start-up, show objects, front-panel mirroring, product string, firmware version; twelve
of them only in part; before E3 only the last two were). VERIFIED never
clears a message for sending: no protocol message is cleared, so no code may talk to a console yet
*(superseded in part on 2026-10-30 by safety check SC-1, which clears the poll and the listed fetch for
a read-only session; see below)*.
**Phase 1 is built (2026-10-30):** the first vertical slice Angular → SignalR → .NET → simulated
console, with protocol decoders tested against the committed captures. **Safety check SC-1
(2026-10-30)** cleared the poll and the listed fetch for a read-only session: a WinUSB device
exists behind `--Device:Mode=Usb`, replay-tested against every capture; its first live session
(E8, 2026-10-30) read the console for 861 s with polls and listed fetches only (the console kept reporting its live
state; H66 and H67 CONFIRMED for what was used) and cleared nothing further.

## Run it

```
dotnet build SmartFade.sln && dotnet test SmartFade.sln      # .NET 10 SDK (global.json)
dotnet run --project src/backend/SmartFade.Api              # backend, http://localhost:5160, simulated console
cd src/frontend && npm install && npm start                 # UI, http://localhost:4200 (proxies to 5160)
```

The simulator is the default device. The read-only hardware mode
(`dotnet run --project src/backend/SmartFade.Api -- --Device:Mode=Usb --Device:Usb:ConfirmDate=<today>`)
is for read-only hardware sessions only (E8 done 2026-10-30, E8b next), after the preparation in
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
