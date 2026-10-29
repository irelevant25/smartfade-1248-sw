# Captures

## Index

| Date | Experiment | Record | Evidence files (SHA-256 listed in the record) |
|---|---|---|---|
| 2026-10-28 | E1 USB identification (read-only) + passive HID listen | [2026-10-28-e1-usb-identification.md](2026-10-28-e1-usb-identification.md) | `2026-10-28-e1-pnputil.txt`, `2026-10-28-e1-descriptors.txt`, `2026-10-28-e1-hid-probe.txt`, `2026-10-28-e1-driver-events.txt`, `2026-10-28-e1-hid-listen-60s-inconclusive.txt`, `2026-10-28-e1-hid-listen-owner-30s.txt`, `2026-10-28-e1-owner-statements.txt`, `2026-10-28-e1-pnp-properties.txt`, `2026-10-28-e1-smartsoft-install.txt`, `2026-10-28-e1-driver-loaded.txt` |
| 2026-10-28 | E2 SmartSoft session capture (USBPcap, passive): connect, full download, one fader moved, close | [2026-10-28-e2-smartsoft-connect.md](2026-10-28-e2-smartsoft-connect.md) | `2026-10-28-e2-smartsoft-connect.pcap` (SHA-256 `8F350496438DE66E4AEA70B6BE5206ADBBFEC4D8A61F8C3782FA6A122580CB82`, console-only split, the committed evidence), `2026-10-28-e2-smartsoft-connect.jsonl` (normalized, `808834C94D42CEFB9B3F930F267C4D8714A4FCADE757B73A0CE5A12502AA4E8B`). Not committed: `2026-10-28-e2-smartsoft-connect.raw-all-devices.pcap` (`7ADCCDD8B379F6159B976108DD30031984897538DE104B71617667E06333531D`), git-ignored because it holds every device on the root hub, including the owner's keyboard and mouse (keystrokes); kept locally only to prove the split complete (the raw file was deleted on 2026-10-29 after the completeness checks; its hash stays recorded) |
| 2026-10-29 | E3 console actions while SmartSoft is Online (USBPcap, passive, after a console power cycle): faders 1 and 5, memory-mode bump, Grand Master; also the second E2-type start-up | [2026-10-29-e3-console-actions.md](2026-10-29-e3-console-actions.md) | `2026-10-29-e3-console-actions.pcap` (SHA-256 `0DCC3E1DCA277F97F471D6DDF12F5A39AADF7898A126471606A2579888058340`, console-only split of device address 7, the committed evidence), `2026-10-29-e3-console-actions.log` (owner's action log, `EF10EBC2F3949A37B3F76EC881D43EE2C2C3B0FF37A61CEE2A79AB69A2B1645B`), `2026-10-29-e3-console-actions.jsonl` (normalized with the log merged as notes, normalizer version 2, `3B2B6861C25CE7B90BDF4212E20445F7B53EF8DE1A44E308F75B2C43E9AE59CF`), `2026-10-29-e3-owner-statements.txt` (the owner's answers Q1–Q5 / A1–A4 given in the chat the same evening after the capture, transcribed by Claude, `d3e0ee5b995e3e419d2fa96568644d94cb4a9599309a16f7b73f6c4d5dbde328`; owner statements, never promoting by themselves). Not committed: `2026-10-29-e3-console-actions.raw-all-devices.pcap` (`E11B5AA5E32D1E2BCC51F7E3A505CAC91D651E3EE23D7F1BE7C2067857E26D04`, 234 903 216 bytes), git-ignored for the same reason as E2's (every device on the root hub, including keyboard and mouse); kept locally only to prove the split complete (the raw file was deleted on 2026-10-29 after the completeness checks; its hash stays recorded) |

E1 produced text evidence (tool output), not a packet capture. **E2 is the first USB traffic
capture** and **E3 the second** (an independent session after a power cycle); golden-test
candidates are listed in each record (by JSONL `seq` and pcap frame). Action logs written by
`tools/usb-probe/e3-guided-session.ps1` or `action-log.ps1` are committed next to the capture
and merged into the JSONL as `note` records (`--notes-log`, see the tool README).

Raw USBPcap captures (`*.raw-all-devices.pcap`) are never committed: USBPcap records a whole
root hub, so they contain other devices' traffic. The committed evidence is the console-only
split made with `tshark -r <raw> -Y "usb.device_address == <console>" -F pcap -w <split>`; the
experiment record states how the split was checked for completeness against the raw file.
Normalizer and analysis tools: [`tools/protocol-analyzer/`](../../../tools/protocol-analyzer/README.md).

Each capture consists of:

- `YYYY-MM-DD-<slug>.pcap` / `.pcapng`: USBPcap/Wireshark capture (unmodified evidence; for
  USB, the console-only split)
- `YYYY-MM-DD-<slug>.jsonl`: normalized capture (proposal in [../reverse-engineering.md](../reverse-engineering.md#capture-format-proposal); keys actually written by the normalizer in [Capture format as implemented](../reverse-engineering.md#capture-format-as-implemented-normalizer-version-1-e2-2026-10-28))
- `YYYY-MM-DD-<slug>.md`: experiment record (template below)

Golden protocol tests may only reference bytes that exist in a committed capture.

## Experiment record template

```markdown
# <slug>

- Date:
- Experiment ID: E?
- Console: SmartFade 1248, serial …, firmware … (read from Menu > Diagnostics)
- Host: OS …, SmartSoft version …, driver …
- Connection: USB cable / MIDI interface …
- Show backup taken: yes (file …)
- Action log (with capture-relative times):
  - 00.0 s start capture
  - …
- Observed result on console / DMX output:
- Conclusions (with status changes to hypotheses):
```
