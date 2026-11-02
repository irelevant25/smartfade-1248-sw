# Guided operator session "E11b" (written 2026-11-02, after SC-5 and its addenda; not run): the SECOND live-control session of
# our own software and the first with the classes SC-5 cleared conditionally (docs/protocol/safety-checks.md "SC-5"): every channel
# fader of both banks through the AUTOMATIC BANK SWITCH (the device presses FADERS 25-48 / FADERS 1-24 / FADERS MEMS itself,
# waits for the console to report the bank, then sends the target's reported level first), memory faders of the page shown,
# held bumps bounded to 3 s, the bank keys, IND 1 / IND 2, plus the SC-2 controls. Nothing else is sent; nothing in this script
# talks to the console. The script
#   - refuses to go on unless the protocol-safety-reviewer pass over the SC-5 implementation is closed and the owner gives the
#     written acceptances SC-5 lists,
#   - takes the safety-rule-3 gate (the SD backup's name), records the versions (backend commit, Windows build, firmware, cable),
#   - wants USBPcap running from BEFORE the WinUSB bind and interface 1 bound to WinUSB before the backend starts,
#   - starts the backend with hardware mode AND live control confirmed for today, and the UI, in their own windows,
#   - shows one instruction at a time, logs the PC clock when the operator presses Enter, asks for the console's answer after
#     every control, and applies the abort rules (an Error or a refusal ends the step; a fader the console does not follow ends
#     the session),
#   - ends with Disconnect at an idle point, the SD save for the byte comparison and the list of files to copy.
# Abort rules (SC-1 / SC-2 / SC-5): any Error in the UI ends the session (Disconnect; read the backend log before anything else;
# never reconnect before); anything the console does that you did not cause ends it (note the time); a key reported as still held
# is pressed ON THE PANEL only after reading the log; never power-cycle the console to recover; if INCOMPLETE-WRITE.txt appears in
# the capture folder, nothing connects again until you have read the log and removed the file yourself.
# -LogFile is mandatory and must be a NEW file (action logs are hashed evidence).
param(
    [Parameter(Mandatory = $true)][string]$LogFile,
    [string]$Repo = ""
)

# $PSScriptRoot is not usable in a parameter default under -File on Windows PowerShell 5.1.
if ([string]::IsNullOrWhiteSpace($Repo)) {
    $scriptPath = $MyInvocation.MyCommand.Path
    $Repo = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $scriptPath))
}
if (-not (Test-Path -LiteralPath (Join-Path $Repo "SmartFade.sln"))) {
    Write-Host ("The repository root was not found at {0}; pass -Repo <path to the checkout>." -f $Repo) -ForegroundColor Red
    exit 1
}
if (Test-Path -LiteralPath $LogFile) {
    Write-Host ("Refusing to append to the existing file {0}: action logs are hashed evidence. Choose a new file name." -f $LogFile) -ForegroundColor Red
    exit 1
}
$enc = New-Object System.Text.UTF8Encoding $false
$LogFile = [IO.Path]::GetFullPath((Join-Path (Get-Location) $LogFile))
function Log([string]$text) {
    $line = "{0:yyyy-MM-dd HH:mm:ss.fff} {1}" -f (Get-Date), $text
    [IO.File]::AppendAllText($LogFile, $line + "`r`n", $enc)
    Write-Host ("   logged: " + $line) -ForegroundColor DarkGray
}
function Step([string]$instruction, [string]$tag) {
    Write-Host ""
    Write-Host $instruction -ForegroundColor Yellow
    Read-Host "   Press Enter at the moment you START doing this" | Out-Null
    Log $tag
}
function Confirm-Done([string]$question, [string]$tag) {
    Read-Host ("   " + $question + " Then press Enter") | Out-Null
    Log $tag
}
function Ask([string]$question, [string]$tag) {
    Write-Host ""
    Write-Host $question -ForegroundColor Magenta
    $a = Read-Host "   answer"
    Log ("owner answer: " + $tag + " = " + $a)
    return $a
}
function Require-Yes([string]$question, [string]$tag) {
    $a = Ask $question $tag
    if ($a.Trim().ToLowerInvariant() -ne 'y') {
        Log ("STOP: '" + $tag + "' was not answered y; E11b does not start (SC-2)")
        Write-Host "E11b does not start without this answer (SC-2). Nothing was sent. Run a read-only session (e8b-guided-session.ps1) instead if you want." -ForegroundColor Red
        exit 2
    }
}
function Wait-Idle([int]$seconds, [string]$why) {
    Write-Host ("   Now do not touch anything for {0} seconds ({1}) ..." -f $seconds, $why) -ForegroundColor Cyan
    Start-Sleep -Seconds $seconds
    Log ("wait {0} s done ({1})" -f $seconds, $why)
    Write-Host "   OK." -ForegroundColor Cyan
}
function Interface1Service {
    $dev = Get-PnpDevice -PresentOnly -ErrorAction SilentlyContinue | Where-Object { $_.InstanceId -like 'USB\VID_14D5&PID_0200&MI_01*' } | Select-Object -First 1
    if ($null -eq $dev) { return "(console interface 1 not present)" }
    $svc = (Get-PnpDeviceProperty -InstanceId $dev.InstanceId -KeyName 'DEVPKEY_Device_Service' -ErrorAction SilentlyContinue).Data
    if ([string]::IsNullOrWhiteSpace($svc)) { return "(no driver service bound)" }
    return [string]$svc
}
# W1's answer as one of three cases: 'refused' (a, or any text with 'refus'), 'followed' (b, y, yes, followed) or 'not followed'
# (c, n, no, or anything else). A stop case is confirmed once, because it ends the session: attempt 2 (2026-11-01) ended on a
# mistyped letter while the console had followed every event.
function W1-Case([string]$raw) {
    $a = $raw.Trim().ToLowerInvariant()
    if ($a -eq 'a' -or $a -match 'refus') { return 'refused' }
    if ($a -eq 'b' -or $a -eq 'y' -or $a -eq 'yes' -or $a -eq 'followed') { return 'followed' }
    $sure = Ask ("You typed '" + $raw.Trim() + "': this means the console did NOT follow a sent event, and the session ENDS without the other controls (H66 stop). Is that what you saw? (y = yes, end the session / n = the level followed, go on)") "w1 stop confirmation"
    if ($sure.Trim().ToLowerInvariant() -eq 'n' -or $sure.Trim().ToLowerInvariant() -eq 'no') { return 'followed' }
    return 'not followed'
}

# After every control: the console's answer as the UI shows it, and the abort rule for an Error or a refusal.
function After-Control([string]$what, [string]$tag) {
    $a = Ask ("After " + $what + ": what did the UI show, in this order: the console's reported change (level or LED), any REFUSAL text, any ERROR text? (describe, or 'followed')") $tag
    if ($a -match '(?i)error') {
        Log ("STOP: an Error was reported after " + $what + "; the session ends (abort rule)")
        Write-Host "An Error ends the session: press DISCONNECT in the UI now, read the backend log before anything else, never reconnect before. Then finish this script's end steps." -ForegroundColor Red
        return $false
    }
    return $true
}
function Optional-Step([string]$instruction, [string]$tag, [bool]$enabled) {
    if ($enabled) { Step $instruction $tag } else { Log ("skipped (optional): " + $tag) }
}

$host.UI.RawUI.WindowTitle = "E11b guided session"
Write-Host "E11b guided session: the second live-control session of our own software, the first with SC-5's classes (the automatic bank switch, every fader of both banks and the memory faders, held bumps, the bank keys, IND 1 / 2)." -ForegroundColor Green
Write-Host "Follow the yellow instructions one by one. Press Enter exactly when you START each action. Leave the waits untouched." -ForegroundColor Green
Write-Host "Abort rules: an Error in the UI ends the session (Disconnect, read the backend log, never reconnect before); anything the" -ForegroundColor Green
Write-Host "console does that you did not cause ends it (note the time); a key reported as held is pressed on the panel only after reading" -ForegroundColor Green
Write-Host "the log; never power-cycle the console to recover; INCOMPLETE-WRITE.txt in the capture folder is removed only after reading the log." -ForegroundColor Green
Write-Host "Never save to or load from the SD card while our software is connected: during E12b's save the console stopped answering USB for about 0.85 s (H109); our session would end with a fault." -ForegroundColor Green
Log "START guided E11b session (PC clock, local time)"
Log ("repository: " + $Repo)

# --- Gate 1: the SC-5 review and the written acceptances (SC-5 "Conditions for building the automatic switch and the holds") ---
Write-Host ""
Write-Host "SC-5 gate. Read docs/protocol/safety-checks.md, section 'SC-5' with its addenda and the 'Implementation' section, before answering." -ForegroundColor Yellow
Require-Yes "Review: is the protocol-safety-reviewer pass over the SC-5 implementation closed (every blocker and major fixed, the minors fixed or accepted in the SC-5 review table)? (y / n)" "sc5 review closed"
Require-Yes "Acceptance 1: the automatic bank switch presses the console's FADERS key before a fader of another bank is moved from the UI: the console's 24 physical faders then control that bank, for anyone at the console, until the next switch; the UI shows the bank. (y / n)" "accept automatic switch"
Require-Yes "Acceptance 2: after a switch the console ignores PC events for a re-assigned fader until one equals the new content's level (F172); the device sends the reported level first; a wrong first event moves nothing and the bump LED blinks. (y / n)" "accept matching rule"
Require-Yes "Acceptance 3: the device asks for a held bump's release 2.75 s after the press (Acceptance 7 for the overrun) or before any Disconnect; the lost-release risk (a process killed mid-hold leaves the bump down until the panel is pressed) was accepted on 2026-11-02. (y / n)" "accept bounded hold"
Require-Yes "Acceptance 4: the last mover wins (F141): the console's level and a physical fader can disagree until someone moves it; MASTER changes the whole live output. (y / n)" "accept last mover and master"
Require-Yes "Acceptance 5 (SC-5 addendum 3): channel bump 1 of bank 1-24 is cleared as a hold like the other channel bumps (code 0x00, VERIFIED as memory bump 1 and bank 25-48's first bump, held once in bank 1-24 in E9c); you acknowledge the addendum. (y / n)" "accept addendum 3 channel bump 1"
Require-Yes "Acceptance 6 (review MAJOR 1, residual risk): while a bump is held ON THE PANEL, the console reports that fader at the bump's level and the device cannot tell it from a fader at full; a fader of the same channel or memory moved from the UI at that moment may leave the content at the bump's level when the bump ends (what the console does is UNKNOWN); with SOLO on, a panel bump may do the same to every other fader (F6; unobserved, INFERRED). The device guards its own holds (under SOLO, every fader); the panel case cannot be refused (F178: no object marks a press) and was accepted as a residual risk by the assistant at your delegation on 2026-11-02, like F141; you acknowledge it here. This session never moves a fader during or right after a bump of the same physical fader, and SOLO is off at every bump. (y / n)" "accept panel bump residual risk"
Require-Yes "Acceptance 7 (review MINOR 2): the device asks for a bump's release 2.75 s after the press and sends it at the next write slot; a console replying near the 500 ms timeout (never seen: every reply in every capture came within 12 ms) could carry the release to about 3.8 s, which is logged, not prevented; you accepted this in chat on 2026-11-02, confirm it here. (y / n)" "accept hold overrun with a slow console"
Require-Yes "The rig: memory 3 of page 1 (W5) and channels 1, 2, 5 and 30 may light briefly on whatever is patched to them; IND 1 is unpatched and IND 2 drives DMX 47 in this show (E8b), nothing on the rig today (W8); is that safe? (y / n)" "rig decision"
$optIndependents = (Ask "Optional W8: IND 1 on and off, then IND 2 on and off from the UI? (y / n)" "opt independents") -eq 'y'
$optMaster = (Ask "Optional W10 (last): MASTER to about half and back from the UI? (y / n)" "opt master") -eq 'y'

# --- Gate 2: safety rule 3, the backup; the versions ---
Write-Host ""
Write-Host "Safety rule 3: save the show to the SD card on the console NOW (Menu > Save/Load Show > Save), to a FREE number." -ForegroundColor Yellow
$backup = ""
while ([string]::IsNullOrWhiteSpace($backup)) {
    $backup = Read-Host "   Type the file name the console used for the backup (e.g. SHOW15.ASC); it is required"
}
Log ("backup taken: " + $backup.Trim() + " (copy it into smartfade-sd-card-backup/ after the session)")
$commit = ""
try { $commit = (& git -C $Repo rev-parse --short HEAD 2>$null) } catch { $commit = "" }
if ([string]::IsNullOrWhiteSpace($commit)) { $commit = "(git not available)" }
Log ("backend commit: " + $commit)
Log ("windows build: " + [System.Environment]::OSVersion.Version.ToString())
Ask "Firmware as the console's Menu > Diagnostics shows it now (e.g. 3.0.2.9.0.43)?" "firmware"
Ask "Cable: which USB cable and port (e.g. the A-B cable straight into the PC's rear port)?" "cable"
Ask "Did you power-cycle the console today before this session? (y / n)" "power cycled"

# --- Gate 3: USBPcap before the WinUSB bind (SC-2 item 8; H68) ---
Write-Host ""
Write-Host "USBPcap must record from BEFORE the WinUSB bind. In an ADMIN PowerShell window start it on the console's root hub, into a NEW file (an absolute path; an admin window starts in C:\Windows\System32):" -ForegroundColor Yellow
# A new file per attempt (a raw capture must never be overwritten).
$pcapName = 'e11b-' + (Get-Date -Format 'yyyyMMdd-HHmm') + '.pcap'
Write-Host ('   & "C:\Program Files\USBPcap\USBPcapCMD.exe" -d \\.\USBPcap1 -A --inject-descriptors -o "' + (Join-Path (Get-Location) $pcapName) + '"') -ForegroundColor White
Log ("usbpcap file name suggested to the owner: " + $pcapName)
Confirm-Done "When USBPcap is running." "usbpcap running (owner)"
Log ("interface 1 driver service before the bind: " + (Interface1Service))
Write-Host ""
Write-Host "Now bind WinUSB to the console's interface 1 with Zadig: Options > List All Devices, pick the entry with" -ForegroundColor Yellow
Write-Host "USB ID 14D5 0200 01 (never the one ending in 00), driver WinUSB, Replace Driver." -ForegroundColor Yellow
Step "Start Zadig's Replace Driver now (skip with Enter if interface 1 is already on WinUSB)." "zadig bind start"
$svc = Interface1Service
while ($svc -ne 'WinUSB') {
    Write-Host ("   Interface 1 is bound to '{0}', not to WinUSB yet." -f $svc) -ForegroundColor Red
    Read-Host "   Finish the Zadig bind, then press Enter to check again" | Out-Null
    $svc = Interface1Service
}
Log "interface 1 driver service after the bind: WinUSB"
$captureDir = Join-Path $env:LOCALAPPDATA "SmartFade\captures"
if (Test-Path -LiteralPath (Join-Path $captureDir "INCOMPLETE-WRITE.txt")) {
    Log "STOP: INCOMPLETE-WRITE.txt exists in the capture folder; E11b does not start"
    Write-Host ("INCOMPLETE-WRITE.txt exists in " + $captureDir + ": a Data write of an earlier session did not complete. Read that session's transfer log first; the backend refuses to connect while the file exists. E11b does not start.") -ForegroundColor Red
    exit 3
}

# --- the backend with live control for today, and the UI ---
$today = "{0:yyyy-MM-dd}" -f (Get-Date)
Write-Host ""
Write-Host ("Starting the backend in hardware mode WITH LIVE CONTROL (both confirmed for {0}) and the UI in two new windows ..." -f $today) -ForegroundColor Yellow
Start-Process -FilePath "dotnet" -ArgumentList @("run", "--project", "src/backend/SmartFade.Api", "--", "--Device:Mode=Usb", ("--Device:Usb:ConfirmDate=" + $today), ("--Device:Usb:LiveControlDate=" + $today)) -WorkingDirectory $Repo | Out-Null
Log ("backend started by the script: dotnet run --project src/backend/SmartFade.Api -- --Device:Mode=Usb --Device:Usb:ConfirmDate=" + $today + " --Device:Usb:LiveControlDate=" + $today)
Start-Process -FilePath "cmd.exe" -ArgumentList @("/k", "npm start") -WorkingDirectory (Join-Path $Repo "src/frontend") | Out-Null
Log "ui dev server started by the script: npm start in src/frontend"
Write-Host "   Open http://localhost:4200 when the UI window says it is ready. BEFORE you press Connect the bar must show HARDWARE and" -ForegroundColor Yellow
Write-Host "   LIVE CONTROL, the panels 'Not reported by the console yet'; with live control every channel fader, every memory fader of the page shown," -ForegroundColor Yellow
Write-Host "   the bumps, MASTER, BUMPS, SOLO, BLACKOUT, IND 1 / 2, GO and the FADERS keys may be enabled; the record keys, STACK, PAUSE, NEXT, RATE and CLEAR must be read-only." -ForegroundColor Yellow
Confirm-Done "When the UI shows HARDWARE and LIVE CONTROL and only those controls are enabled." "ui ready, hardware and live control badges (owner)"
Ask "Before Connect every fader and key is disabled; after Connect only the live controls get enabled. Is anything other than the bar's CONNECT button enabled now? (n / what)" "ui enabled controls"
Write-Host ""
Write-Host "Starting position on the console: CHANNEL mode (FADERS 1-24 lit), all channel faders at ZERO, MASTER at FULL, BUMPS at FULL," -ForegroundColor Yellow
Write-Host "STACK unlit, BLACKOUT off, SOLO off, IND 1 / IND 2 off, REC MEM / EDIT MEM / COPY / REC SEQ off, no sequence running, page 1." -ForegroundColor Yellow
Confirm-Done "Set that up now." "starting position set (owner)"

# --- Connect and the read-only minute ---
Write-Host "   (From here until Disconnect: no SD save or load on the console, no menu that writes the card.)" -ForegroundColor Yellow
Step "Press CONNECT in the UI." "connect pressed"
Confirm-Done "When the UI shows Connected." "connected (owner)"
Wait-Idle 60 "idle after connect: the read side as in E8, nothing is sent but the poll and the listed fetch"
Ask "During the idle minute: did anything on the console change by itself, and does the UI show the console's state (levels, LEDs, the LCD in the backend log)? (describe)" "idle observation"

# --- W0: the console must report its state before any control (attempt 1 of 2026-11-01: after a power cycle with nothing touched the
# console listed nothing in 192 s, so conditions 5, 6 and 8 refused the first control; a read-only session never asks for an unlisted object) ---
Write-Host ""
Write-Host "W0: the console reports LEDs, its fader mode, the MASTER and the channel levels only when they change. Make it report them from the PANEL now (nothing is sent by us):" -ForegroundColor Yellow
Step "W0: on the CONSOLE press FADERS MEMS (memory mode)." "w0 faders mems on panel"
Wait-Idle 5 "w0 memory mode"
Step "W0: on the CONSOLE press FADERS 1-24 (back to channel mode)." "w0 faders 1-24 on panel"
Wait-Idle 5 "w0 channel mode"
Step "W0: on the CONSOLE press SOLO (on), then SOLO again (off)." "w0 solo on off on panel"
Wait-Idle 5 "w0 solo toggled"
Step "W0: on the CONSOLE move the MASTER down a little and back to FULL." "w0 master wiggle on panel"
Wait-Idle 5 "w0 master back at full"
Step "W0: on the CONSOLE move CHANNEL FADER 1 up a little and back to ZERO (the console lists its channel levels only when one changes; a channel fader from the PC is refused until it has)." "w0 channel fader 1 wiggle on panel"
Wait-Idle 10 "w0 channel levels listed"
$w0 = Ask "W0: does the UI now show the console's key LEDs (the Record and Crossfader panels show LED readings), the MASTER at full, channel mode and channel 1 at zero, and are the channel faders, the bumps, MASTER, BUMPS, SOLO, BLACKOUT and IND 1 / 2 enabled? (y / what is missing)" "w0 console state reported"
if ($w0.Trim().ToLowerInvariant() -ne 'y') {
    Step "W0: repeat the five panel actions above once more (FADERS MEMS, FADERS 1-24, SOLO on and off, MASTER down and back, channel fader 1 up and back)." "w0 repeated"
    Wait-Idle 10 "w0 repeated"
    $w0 = Ask "W0: and now? (y / what is missing)" "w0 console state reported 2"
    if ($w0.Trim().ToLowerInvariant() -ne 'y') {
        Log "STOP: the console's state is not reported after W0; no control is tried (the interlocks would refuse it)"
        Write-Host "Without the console's reported state every control is refused by design. Press DISCONNECT and finish the end steps; tell the assistant what the UI showed." -ForegroundColor Red
    }
}
$ok = $w0.Trim().ToLowerInvariant() -eq 'y'

# --- The controls, one at a time, 10 s between steps, the console's answer watched in the UI ---
if ($ok) {
    Step "W1: in the UI, drag CHANNEL FADER 1 slowly to about 20 % and release it (the SC-2 control first: the sanity check; with SC-5 the first event of a fader carries its reported level, 0, then your drag)." "w1 channel fader 1 to 20"
    Wait-Idle 10 "after w1 up"
    $h66 = Ask "W1: three cases. (a) The UI shows 'Refused: ...': type a, or the refusal text (nothing was sent). (b) The fader went out and the UI's channel 1 level followed within a second: type b or y. (c) The fader went out (no refusal) and the level did NOT follow: type c or no." "h66 channel 1 followed"
    $answer = W1-Case $h66
    if ($answer -eq 'refused') {
        Log "W1 refused by the device, nothing sent; the cause is in the refusal text"
        Step "W1: a refusal means the console's state is not reported or not as required: repeat W0's five panel actions (FADERS MEMS, FADERS 1-24, SOLO on and off, MASTER down and back to full, channel fader 1 up and back to zero), wait 10 s, then drag CHANNEL FADER 1 to about 20 % once more." "w1 retry after w0"
        Wait-Idle 10 "after w1 retry"
        $h66 = Ask "W1 retry: (a) refused again: type a or the text; (b) sent and followed: b or y; (c) sent and not followed: c or no." "h66 channel 1 followed retry"
        $answer = W1-Case $h66
    }
    if ($answer -eq 'refused') {
        Log "STOP: W1 refused twice; no event was sent; the session ends without a write (the cause goes to the assistant)"
        Write-Host "Refused twice: nothing was sent. Press DISCONNECT and finish the end steps; the refusal texts go to the assistant." -ForegroundColor Red
        $ok = $false
    } elseif ($answer -ne 'followed') {
        Log "STOP: the console did not follow channel fader 1 (H66 for writes); the session ends, nothing is retried"
        Write-Host "The console did not follow a sent event: press DISCONNECT in the UI now (the session ends, nothing is retried), then finish the end steps below." -ForegroundColor Red
        $ok = $false
    }
}
if ($ok) {
    Step "W1: in the UI, drag CHANNEL FADER 1 back to ZERO." "w1 channel fader 1 to zero"
    Wait-Idle 10 "after w1 down"
    $ok = After-Control "channel fader 1" "w1 result"
}
if ($ok) {
    Step "W2: in the UI, drag CHANNEL FADER 5 slowly to about 20 % (a new index of bank 1-24, SC-5 class A2) and release it." "w2 channel fader 5 to 20"
    Wait-Idle 10 "after w2 up"
    Step "W2: in the UI, drag CHANNEL FADER 5 back to ZERO." "w2 channel fader 5 to zero"
    Wait-Idle 10 "after w2 down"
    $ok = After-Control "channel fader 5 (did channel 5 follow on the console: its bump LED, the LCD?)" "w2 result"
}
if ($ok) {
    Step "W3: in the UI, drag CHANNEL FADER 30 slowly to about 20 % and release it: the AUTOMATIC SWITCH to bank 25-48 (the device presses FADERS 25-48 first; the console's FADERS 25-48 LED lights, its LCD rows read 25 / 37, the UI's FADERS column lights 25-48), then the first event at channel 30's reported level, then your drag." "w3 channel fader 30 to 20 (automatic switch to 25-48)"
    Wait-Idle 10 "after w3 up"
    Ask "W3: did the console switch to bank 25-48 by itself (its FADERS 25-48 LED), did channel 30 follow (bump LED 6 of the second bank, the LCD), and what does the UI's FADERS column show? (describe)" "w3 switch observation" | Out-Null
    Step "W3: in the UI, drag CHANNEL FADER 30 back to ZERO." "w3 channel fader 30 to zero"
    Wait-Idle 10 "after w3 down"
    $ok = After-Control "channel fader 30 and the switch" "w3 result"
}
if ($ok) {
    Step "W4: in the UI, drag CHANNEL FADER 2 slowly to about 20 % and release it: the automatic switch BACK to bank 1-24 (FADERS 1-24 pressed by the device first)." "w4 channel fader 2 to 20 (automatic switch to 1-24)"
    Wait-Idle 10 "after w4 up"
    Step "W4: in the UI, drag CHANNEL FADER 2 back to ZERO." "w4 channel fader 2 to zero"
    Wait-Idle 10 "after w4 down"
    $ok = After-Control "channel fader 2 and the switch back (is the console in bank 1-24 again: its LED, the LCD rows 1 / 13?)" "w4 result"
}
if ($ok) {
    Step "W5: in the UI, drag MEMORY FADER 3 of page 1 slowly to about 20 % and release it: the automatic switch to MEMORY MODE (FADERS MEMS pressed by the device first; memory 3's look comes up on the rig at 20 %)." "w5 memory fader 3 to 20 (automatic switch to mems)"
    Wait-Idle 10 "after w5 up"
    Ask "W5: did the console switch to memory mode by itself (its FADERS MEMS LED, the red bump LEDs), did memory fader 3 follow (bump LED 3, memory 3's channels on the rig)? (describe)" "w5 switch observation" | Out-Null
    Step "W5: in the UI, drag MEMORY FADER 3 back to ZERO." "w5 memory fader 3 to zero"
    Wait-Idle 10 "after w5 down"
    $ok = After-Control "memory fader 3 and the switch" "w5 result"
}
if ($ok) {
    Step "W6: in the UI, press and HOLD the BUMP of MEMORY 1 (page 1) for about 2 seconds, then release it (a held bump: memory 1's look at the BUMPS level while held; the device releases at 3 s at the latest)." "w6 memory bump 1 held 2 s"
    Wait-Idle 10 "after w6"
    $ok = After-Control "the held memory bump 1 (did the look stay up while you held, go out when you released, and did the UI's key come back up?)" "w6 result"
}
if ($ok) {
    Step "W7: in the UI, press and HOLD the BUMP of CHANNEL 7 for about 2 seconds, then release it: the automatic switch back to bank 1-24 first (FADERS 1-24 by the device), then the hold (channel 7 at the BUMPS level while held)." "w7 channel bump 7 held 2 s (automatic switch to 1-24)"
    Wait-Idle 10 "after w7"
    $ok = After-Control "the switch back and the held bump 7" "w7 result"
}
if ($ok -and $optIndependents) {
    Step "W8: in the UI, click IND 1 (ON), wait 5 s, click IND 1 (OFF)." "w8 ind 1 on off"
    Wait-Idle 10 "after w8 ind 1"
    $ok = After-Control "IND 1 (its LED on the console)" "w8 ind 1 result"
}
if ($ok -and $optIndependents) {
    Step "W8: in the UI, click IND 2 (ON), wait 5 s, click IND 2 (OFF) (DMX 47 in this show)." "w8 ind 2 on off"
    Wait-Idle 10 "after w8 ind 2"
    $ok = After-Control "IND 2" "w8 ind 2 result"
}
if ($ok) {
    Step "W9: in the UI, click the 25-48 key of the FADERS column (FADERS 25-48 as a tap), wait 5 s, then click the 1-24 key." "w9 faders 25-48 and 1-24 from ui"
    Wait-Idle 10 "after w9"
    $ok = After-Control "the two bank keys (the console's FADERS LEDs and the UI's FADERS column)" "w9 result"
}
if ($ok -and $optMaster) {
    Step "W10 (last, the whole output): in the UI, drag MASTER slowly to about HALF." "w10 master to half"
    Wait-Idle 10 "after w10 half"
    Step "W10: in the UI, drag MASTER back to FULL." "w10 master to full"
    Wait-Idle 10 "after w10 full"
    $ok = After-Control "MASTER (the physical MASTER stays where it is: last mover wins, F141)" "w10 result"
}
if ($ok) { Log "E11b controls covered: channel faders 1, 5, 30 (switch to 25-48), 2 (switch back), memory fader 3 (switch to MEMS), memory bump 1 held, channel bump 7 held (switch back), IND 1 / 2 (optional), the bank keys from the UI, MASTER (optional)" } else { Log "E11b stopped before the controls were covered (see the STOP line above)" }
Wait-Idle 10 "end of actions, an idle point"

# --- End: disconnect at an idle point, the checks, the save ---
Step "Press DISCONNECT in the UI (at an idle point: nothing pending)." "disconnect pressed"
Confirm-Done "When the UI shows Disconnected." "disconnected (owner)"
Ask "Does the UI or the backend window show a message about a key still held, a release that could not be sent, or a write that did not complete? Type n if there is no such message, else its text." "end-of-session report"
if (Test-Path -LiteralPath (Join-Path $captureDir "INCOMPLETE-WRITE.txt")) {
    Log "INCOMPLETE-WRITE.txt exists after the session"
    Write-Host ("INCOMPLETE-WRITE.txt exists in " + $captureDir + ": read the transfer log of this session before anything else; remove the file yourself only after that.") -ForegroundColor Red
}
Confirm-Done "Close the backend window (Ctrl+C in it) and stop USBPcap (Ctrl+C in its window)." "backend and usbpcap stopped (owner)"
Ask "Physical fader positions at the end (channel 1, BUMPS, MASTER) and the LEDs of SOLO and BLACKOUT? (describe)" "end positions"
Write-Host ""
Write-Host "Save the show to the SD card on the console NOW, to another FREE number (the after-session file; it must equal the backup byte for byte: no show data may have changed)." -ForegroundColor Yellow
$after = ""
while ([string]::IsNullOrWhiteSpace($after)) {
    $after = Read-Host "   Type the file name the console used for this save (e.g. SHOW16.ASC); it is required"
}
Log ("after-session save: " + $after.Trim() + " (copy it into smartfade-sd-card-backup/ with the backup)")
Log "END guided E11b session"
Write-Host ""
Write-Host "If you start the backend again after this point, that run is OUTSIDE this session's conditions: start USBPcap first (a new file name) and save the show to the SD card afterwards, or leave the console alone. (Attempt 3, 2026-11-01: two later runs had no USBPcap and no SD pair.)" -ForegroundColor Yellow
Write-Host ""
Write-Host "Files to copy into docs/protocol/captures/ (with the date prefix ...-e11b-...):" -ForegroundColor Green
Write-Host ("   action log:   " + $LogFile) -ForegroundColor White
Write-Host ("   backend log:  " + $captureDir + " (the newest .jsonl: the evidence of every transfer our software made)") -ForegroundColor White
Write-Host "   USBPcap file: the .pcap you named when starting USBPcapCMD (split to the console before it is committed)" -ForegroundColor White
Write-Host ("   SD files:     " + $backup.Trim() + " (backup) and " + $after.Trim() + " (after) into smartfade-sd-card-backup/; they must be byte-identical") -ForegroundColor White
Write-Host "Afterwards put the ETC driver back on interface 1 if you want SmartSoft again (E8 runbook step 2)." -ForegroundColor Green
