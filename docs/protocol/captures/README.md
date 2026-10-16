# Captures

No captures yet.

Each capture consists of:

- `YYYY-MM-DD-<slug>.pcapng`: raw USBPcap/Wireshark capture (unmodified evidence)
- `YYYY-MM-DD-<slug>.jsonl`: normalized capture (format in [../reverse-engineering.md](../reverse-engineering.md#capture-format-proposal))
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
