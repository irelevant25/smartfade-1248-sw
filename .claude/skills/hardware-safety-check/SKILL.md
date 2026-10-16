---
name: hardware-safety-check
description: >
  Pre-flight checklist before anything touches the real SmartFade 1248: enabling real-hardware mode,
  sending a message, running an active experiment (E3, E5), replaying a capture, or merging code on
  the device path. Use when the user says "can we send this", "enable hardware mode", "ready for the
  console", "is this safe", "before we plug in", or before any commit touching SmartFade.Devices.Usb,
  SmartFade.Protocol or SmartFade.Communication.
---

# Hardware safety check

Authoritative rules: CLAUDE.md and the "Safety rules" section of
`docs/protocol/reverse-engineering.md`. Answer each item with evidence. A single "no" or "unknown"
on items 1 to 6 means **do not proceed**.

## Checklist

1. **Evidence status.** Every message, field, ID and timing involved is **VERIFIED** in
   `docs/protocol/known-facts.md` with a capture reference. (DOCUMENTED and OBSERVED are not enough
   for writes. Reads/polls may proceed at OBSERVED only with the owner's explicit go-ahead.)
2. **Side effects understood.** The effect of the message on the console is known from a capture
   where SmartSoft sent the same thing. Edit/record/load messages are excluded until the owner
   explicitly accepts the risk for that specific message.
3. **Backup.** The show is backed up to SD card before the session, file name recorded.
4. **Rig.** Dimmers/fixtures on a safe rig, or DMX output disconnected or monitored.
5. **Firmware path untouched.** Nothing involves SFDownloader, self-test "Firmware Upgrade",
   DPInst/driver installers or any bootloader interface.
6. **Mode gating.** Real-hardware mode is off by default, enabled by explicit configuration for this
   run only, and visibly indicated in the UI. The simulated device is still the default build.
7. **No blind retries.** Side-effecting commands are not retried without confirming the previous
   outcome. Timeouts are logged, not looped.
8. **Logging on.** Transport-level frame logging is enabled so the session produces a capture
   (`docs/protocol/captures/` record afterwards).
9. **Versions recorded.** Console firmware (Menu > Diagnostics), SmartSoft version if involved,
   OS, cable/adapter.
10. **Review done.** `protocol-safety-reviewer` has reviewed the exact code/bytes to be sent.
11. **Passive first.** For a new message type, a passive observation (E2/E3/E4) of SmartSoft or the
    console producing it exists before we produce it.

## Output

```
## Decision: GO / NO-GO
| # | Item | Status | Evidence |
## Conditions (if GO)
## What to record during the session
```
