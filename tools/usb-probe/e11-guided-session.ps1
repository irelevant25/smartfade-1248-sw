# Guided operator session "E11" (planned 2026-10-31, evening; not run): the FIRST LIVE-CONTROL session of our own software,
# as safety check SC-2 conditions it (docs/protocol/safety-checks.md, "Conditions for E11 (the first write session) and what to
# record", with the "E11 script adjustments to the implementation"). The backend sends the poll and the listed fetch (SC-1) and,
# with the write switch set for today, the whitelisted ConsoleEvents of SC-2 classes A and B1 that the owner's implementation
# exposes to the UI: channel fader 1, MASTER, BUMPS, channel bump 7, memory bump 1 of the page shown, SOLO and BLACKOUT (GO,
# FADERS MEMS and FADERS 1-24 have no port method yet and are NOT part of E11). Nothing else is sent; nothing in this script
# talks to the console. The script
#   - refuses to go on unless the SC-2 re-check's open items are closed and the owner gives every written acceptance SC-2 lists,
#   - takes the safety-rule-3 gate (the SD backup's name), records the versions (backend commit, Windows build, firmware, cable),
#   - wants USBPcap running from BEFORE the WinUSB bind and interface 1 bound to WinUSB before the backend starts,
#   - starts the backend with hardware mode AND live control confirmed for today, and the UI, in their own windows,
#   - shows one instruction at a time, logs the PC clock when the operator presses Enter, asks for the console's answer after
#     every control, and applies the abort rules (an Error or a refusal ends the step; a fader the console does not follow ends
#     the session),
#   - ends with Disconnect at an idle point, the SD save for the byte comparison and the list of files to copy.
# Abort rules (SC-1 / SC-2): any Error in the UI ends the session (Disconnect; read the backend log before anything else; never
# reconnect before); anything the console does that you did not cause ends it (note the time); a key reported as still held is
# pressed ON THE PANEL only after reading the log; never power-cycle the console to recover; if INCOMPLETE-WRITE.txt appears in the
# capture folder, nothing connects again until you have read the log and removed the file yourself.
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
        Log ("STOP: '" + $tag + "' was not answered y; E11 does not start (SC-2)")
        Write-Host "E11 does not start without this answer (SC-2). Nothing was sent. Run a read-only session (e8b-guided-session.ps1) instead if you want." -ForegroundColor Red
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

$host.UI.RawUI.WindowTitle = "E11 guided session"
Write-Host "E11 guided session: the first live-control session of our own software (SC-2 classes A and B1, the owner's implementation)." -ForegroundColor Green
Write-Host "Follow the yellow instructions one by one. Press Enter exactly when you START each action. Leave the waits untouched." -ForegroundColor Green
Write-Host "Abort rules: an Error in the UI ends the session (Disconnect, read the backend log, never reconnect before); anything the" -ForegroundColor Green
Write-Host "console does that you did not cause ends it (note the time); a key reported as held is pressed on the panel only after reading" -ForegroundColor Green
Write-Host "the log; never power-cycle the console to recover; INCOMPLETE-WRITE.txt in the capture folder is removed only after reading the log." -ForegroundColor Green
Log "START guided E11 session (PC clock, local time)"
Log ("repository: " + $Repo)

# --- Gate 1: SC-2's re-check and the written acceptances (SC-2 "Conditions for E11") ---
Write-Host ""
Write-Host "SC-2 gate. Read docs/protocol/safety-checks.md, sections 'Conditions for E11' and 'Review (SC-2 item 10)', before answering." -ForegroundColor Yellow
Require-Yes "Re-check: are the second pass's MINOR A, MINOR B and MINOR C fixed or accepted in the SC-2 review table (not merely open)? (y / n)" "sc2 re-check items closed"
Require-Yes "Acceptance 1: H66 for writes is UNKNOWN: the console may ignore our events; if channel fader 1's first event is not followed by the console, the session ends and nothing is retried. (y / n)" "accept h66 unknown"
Require-Yes "Acceptance 2: the last mover wins: the console's level and a physical fader can disagree until someone moves it (F141). (y / n)" "accept last mover"
Require-Yes "Acceptance 3: the lost-release risk: a process killed between a press and its release leaves the console with a held key until it is pressed on the panel (condition 4). (y / n)" "accept lost release"
Require-Yes "Acceptance 4: MASTER and BLACKOUT events change the whole live output like the panel does; BUMPS and bump taps flash channels or a memory on the rig. (y / n)" "accept live output"
Require-Yes "Acceptance 5 (review point a): our first ConsoleEvent carries the counter 0x00, where SmartSoft's first carried 0x02 or more; whether the console checks it is UNKNOWN. (y / n)" "accept counter 0x00"
Require-Yes "Acceptance 6 (review point b): after a session fault, an owed key release is still sent when no exchange is open, also when the fault was a LedStatus, ConsoleStatus or FaderInfo report that did not decode; a press whose own transfer failed gets no release and is reported as not completed. (y / n)" "accept release after fault"
Require-Yes "The rig: is every fixture on DMX 5-16 safe to flash or dim briefly (channel 7 and memory 1's look at the BUMPS level; MASTER to half)? (y / n)" "rig decision"
$optMemoryBump = (Ask "Optional: memory bump 1 of page 1 tapped from the UI in memory mode (flashes memory 1's look on the rig at the BUMPS level)? (y / n)" "opt memory bump") -eq 'y'

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
Write-Host ('   & "C:\Program Files\USBPcap\USBPcapCMD.exe" -d \\.\USBPcap1 -A --inject-descriptors -o "' + (Join-Path (Get-Location) 'e11.pcap') + '"') -ForegroundColor White
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
    Log "STOP: INCOMPLETE-WRITE.txt exists in the capture folder; E11 does not start"
    Write-Host ("INCOMPLETE-WRITE.txt exists in " + $captureDir + ": a Data write of an earlier session did not complete. Read that session's transfer log first; the backend refuses to connect while the file exists. E11 does not start.") -ForegroundColor Red
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
Write-Host "   LIVE CONTROL, the panels 'Not reported by the console yet', and only channel fader 1, bump 7, MASTER, BUMPS, SOLO, BLACKOUT" -ForegroundColor Yellow
Write-Host "   (and memory bump 1 in memory mode) may be enabled; every other fader and key must be read-only." -ForegroundColor Yellow
Confirm-Done "When the UI shows HARDWARE and LIVE CONTROL and only those controls are enabled." "ui ready, hardware and live control badges (owner)"
Ask "Which controls are enabled in the UI (list them exactly)? Is anything else enabled?" "ui enabled controls"
Write-Host ""
Write-Host "Starting position on the console: CHANNEL mode (FADERS 1-24 lit), all channel faders at ZERO, MASTER at FULL, BUMPS at FULL," -ForegroundColor Yellow
Write-Host "STACK unlit, BLACKOUT off, SOLO off, IND 1 / IND 2 off, REC MEM / EDIT MEM / COPY / REC SEQ off, no sequence running, page 1." -ForegroundColor Yellow
Confirm-Done "Set that up now." "starting position set (owner)"

# --- Connect and the read-only minute ---
Step "Press CONNECT in the UI." "connect pressed"
Confirm-Done "When the UI shows Connected." "connected (owner)"
Wait-Idle 60 "idle after connect: the read side as in E8, nothing is sent but the poll and the listed fetch"
Ask "During the idle minute: did anything on the console change by itself, and does the UI show the console's state (levels, LEDs, the LCD in the backend log)? (describe)" "idle observation"

# --- The controls, one at a time, 10 s between steps, the console's answer watched in the UI ---
$ok = $true
Step "W1: in the UI, drag CHANNEL FADER 1 slowly to about 20 % and release it (the first write of our software; channel 1 is outside the rig)." "w1 channel fader 1 to 20"
Wait-Idle 10 "after w1 up"
$h66 = Ask "H66: did the UI's channel 1 level follow within a second (the console reported FaderInfo / ChannelLevels), yes or no? If no, type what the UI shows." "h66 channel 1 followed"
if ($h66.Trim().ToLowerInvariant() -ne 'y' -and $h66.Trim().ToLowerInvariant() -ne 'yes' -and $h66.Trim().ToLowerInvariant() -ne 'followed') {
    Log "STOP: the console did not follow channel fader 1 (H66 for writes); the session ends, nothing is retried"
    Write-Host "The console did not follow: press DISCONNECT in the UI now (SC-2: the session ends, nothing is retried), then finish the end steps below." -ForegroundColor Red
    $ok = $false
}
if ($ok) {
    Step "W1: in the UI, drag CHANNEL FADER 1 back to ZERO." "w1 channel fader 1 to zero"
    Wait-Idle 10 "after w1 down"
    $ok = After-Control "channel fader 1" "w1 result"
}
if ($ok) {
    Step "W2: in the UI, click the BUMP of channel 7 once (a tap: press and release from the same code path; channel 7 flashes at the BUMPS level)." "w2 bump 7 tap"
    Wait-Idle 10 "after w2"
    $ok = After-Control "the bump 7 tap (did channel 7 flash on the console and in the UI, and did the key come back up?)" "w2 result"
}
if ($ok) {
    Step "W3: in the UI, click SOLO once (ON)." "w3 solo on"
    Wait-Idle 10 "after w3 on"
    $ok = After-Control "SOLO on (its LED on the console and in the UI)" "w3 on result"
}
if ($ok) {
    Step "W3: in the UI, click SOLO once more (OFF)." "w3 solo off"
    Wait-Idle 10 "after w3 off"
    $ok = After-Control "SOLO off" "w3 off result"
}
if ($ok) {
    Step "W4: in the UI, click BLACKOUT once (ON; with every fader at zero the rig stays dark anyway)." "w4 blackout on"
    Wait-Idle 10 "after w4 on"
    $ok = After-Control "BLACKOUT on (its LED)" "w4 on result"
}
if ($ok) {
    Step "W4: in the UI, click BLACKOUT once more (OFF)." "w4 blackout off"
    Wait-Idle 10 "after w4 off"
    $ok = After-Control "BLACKOUT off" "w4 off result"
}
if ($ok) {
    Step "W5: in the UI, drag the BUMPS fader to about HALF." "w5 bumps to half"
    Wait-Idle 10 "after w5 half"
    Step "W5: in the UI, drag the BUMPS fader back to FULL." "w5 bumps to full"
    Wait-Idle 10 "after w5 full"
    $ok = After-Control "the BUMPS fader (body[193] in the UI's masters panel)" "w5 result"
}
if ($ok -and $optMemoryBump) {
    Step "W6: on the CONSOLE press FADERS MEMS (memory mode; the page shown stays 1)." "w6 memory mode on panel"
    Wait-Idle 10 "after w6 mode"
    Step "W6: in the UI, click the BUMP of MEMORY 1 (page 1) once (a tap: memory 1's look flashes on the rig at the BUMPS level)." "w6 memory bump 1 tap"
    Wait-Idle 10 "after w6 tap"
    $ok = After-Control "the memory bump 1 tap" "w6 result"
    Step "W6: on the CONSOLE press FADERS 1-24 (back to channel mode)." "w6 channel mode on panel"
    Wait-Idle 10 "after w6 back"
}
if ($ok) {
    Step "W7 (last, the whole output): in the UI, drag MASTER slowly to about HALF." "w7 master to half"
    Wait-Idle 10 "after w7 half"
    Step "W7: in the UI, drag MASTER back to FULL." "w7 master to full"
    Wait-Idle 10 "after w7 full"
    $ok = After-Control "MASTER (the physical MASTER stays where it is: last mover wins, F141)" "w7 result"
}
Log "GO, FADERS MEMS and FADERS 1-24 from the UI: not in E11 (no port method yet; SC-2 'E11 script adjustments')"
Wait-Idle 10 "end of actions, an idle point"

# --- End: disconnect at an idle point, the checks, the save ---
Step "Press DISCONNECT in the UI (at an idle point: nothing pending)." "disconnect pressed"
Confirm-Done "When the UI shows Disconnected." "disconnected (owner)"
Ask "Does the UI or the backend log report a key still held, a release that could not be sent, or a write that did not complete? (n / the text)" "end-of-session report"
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
Log "END guided E11 session"
Write-Host ""
Write-Host "Files to copy into docs/protocol/captures/ (with the date prefix ...-e11-...):" -ForegroundColor Green
Write-Host ("   action log:   " + $LogFile) -ForegroundColor White
Write-Host ("   backend log:  " + $captureDir + " (the newest .jsonl: the evidence of every transfer our software made)") -ForegroundColor White
Write-Host "   USBPcap file: the .pcap you named when starting USBPcapCMD (split to the console before it is committed)" -ForegroundColor White
Write-Host ("   SD files:     " + $backup.Trim() + " (backup) and " + $after.Trim() + " (after) into smartfade-sd-card-backup/; they must be byte-identical") -ForegroundColor White
Write-Host "Afterwards put the ETC driver back on interface 1 if you want SmartSoft again (E8 runbook step 2)." -ForegroundColor Green
