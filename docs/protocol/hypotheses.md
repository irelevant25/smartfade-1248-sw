# Hypotheses

Each hypothesis has an ID, the evidence behind it, and a concrete test. Close a hypothesis
by marking it CONFIRMED (and move the fact to known-facts.md with evidence) or REJECTED
(keep the text, add why).

| ID | Hypothesis | Evidence for | Test | Status |
|---|---|---|---|---|
| H1 | Our console enumerates as a USB composite device `VID_14D5`, with `MI_00` = HID and `MI_01` = vendor "High-Speed-Pipe" interface | etchsp.inf binds MI_01. HID strings in sfEngine/SmartSoft | E1: descriptor dump (read-only) | OPEN |
| H2 | 0x14D5 is Peperoni-Light's USB vendor ID; ETC contracted the USB stack | INF authorship, driver version comments | Check usb.ids / USB-IF listing | OPEN |
| H3 | The HSP protocol is poll-based: PC polls, console answers with a dirty-object list, PC fetches objects by name/ID + 3 qualifiers | sfEngine strings (S6) | E2: USBPcap capture of SmartSoft session | OPEN |
| H4 | The session starts with a version exchange (`ConsoleVersion`), and the session is refused on mismatch | S7 strings | E2 | OPEN |
| H5 | Front-panel state (faders, LEDs, LCD) is mirrored to the PC as `FaderInfo`, `LedStatus`, `DisplayContent` objects, and PC-side control is sent as `ConsoleEvent` | S6 strings; SmartSoft mimics buttons/faders | E2/E3: move one fader, press one button, compare capture | OPEN |
| H6 | There is no direct "set channel level" message. Channel control is via events or level objects | Absence of such a name in object list | E3 | OPEN |
| H7 | The same USB protocol works with a user-space driver (WinUSB/libusb) instead of `etchsp.sys`, so our backend needs no ETC kernel driver | Generic USB bulk use via ReadFile/WriteFile | After protocol understood: bind WinUSB to MI_01 on a test PC (reversible) | OPEN, later |
| H8 | The engine DLL is the console firmware logic compiled for PC (simulator) | Full LCD menu text in sfEngine.dll; manual "run as part of the console application" | Dynamic observation in VM | OPEN, low priority |
| H9 | MIDI values in the console manual v3.0 are correct for firmware 3.0.2, and those in spec v1.5 are outdated | Document dates | E4: console as MIDI master, record TX | OPEN |
| H10 | In ASC, `SET MIDIChannel` is 0-based (0 = MIDI channel 1) | Default channel 1 documented, file value 0 | Change channel on console, save show to SD, diff | OPEN |
| H11 | Channels 49 and 50 in the ASC patch are Independents 1 and 2 (default DMX 97/98) | Demo patch `97/49`, `98/50`. Ostrov `98/50` | Check patch on console (Patch Independents menu) | OPEN |
| H12 | `$$CUEPARAMS` = step type: 0 channel step, 1 memory step, 2 stack-only memory state | Correlates with `$$MEMORY` presence and single-channel `$CUE 46.0`. Manual: "Each step can be a single intensity channel, a memory or a unique Stack memory state" | Create each step type on console, save, inspect | OPEN (see asc-format.md) |
| H13 | `GROUP n` = memory n = page×24 + fader + 1, and `$$MEMORY p f` is 0-based page/fader | 26/26 references in Ostrov consistent | Record memory on known page/fader, save, inspect | OPEN (strong) |
| H14 | ASC `FOLLOWON` corresponds to the console's step **Wait** time | USITT keyword semantics. Console steps have Up/Down/Wait | Set a known wait on console, save, inspect | OPEN |
