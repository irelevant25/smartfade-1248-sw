# Guided operator session "E8b" (2026-10-30): the second read-only session of our own software,
# proposed by the E8 analysis (docs/protocol/captures/2026-10-30-e8-first-read-only-session.md,
# "Next capture to take", item B; runbook in docs/protocol/reverse-engineering.md). Nothing here
# talks to the console: the backend does (only the poll and the listed fetch cleared by SC-1), and
# the operator works the console's panel. The script
#   - takes the safety-rule-3 gate: the name of the SD backup made just now ("backup taken"),
#   - checks that USBPcap is meant to run from BEFORE the WinUSB bind (H68) and that interface 1 is
#     bound to WinUSB before the backend starts,
#   - starts the backend (hardware mode, confirmed for today) and the UI in their own windows,
#   - shows one instruction at a time and logs the PC clock when the operator presses Enter,
#   - ends with the list of files to copy into docs/protocol/captures/.
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

$host.UI.RawUI.WindowTitle = "E8b guided session"
Write-Host "E8b guided session: the second read-only session of our own software (SC-1: poll and listed fetch only)." -ForegroundColor Green
Write-Host "Follow the yellow instructions one by one. Press Enter exactly when you START each action." -ForegroundColor Green
Write-Host "Abort rules: an Error in the UI ends the session (do not reconnect before the log is read); anything the console" -ForegroundColor Green
Write-Host "does that you did not cause ends it (note the time); never power-cycle the console to recover, just Disconnect." -ForegroundColor Green
Log "START guided E8b session (PC clock, local time)"
Log ("repository: " + $Repo)

# --- Safety rule 3: the backup ---
Write-Host ""
Write-Host "Safety rule 3: save the show to the SD card on the console NOW (Menu > Save/Load Show > Save), before anything else." -ForegroundColor Yellow
$backup = ""
while ([string]::IsNullOrWhiteSpace($backup)) {
    $backup = Read-Host "   Type the file name the console used for the backup (e.g. SHOW06.ASC); it is required"
}
Log ("backup taken: " + $backup.Trim() + " (copy it into smartfade-sd-card-backup/ after the session)")
Ask "Is the show on the console the same as smartfade-sd-card-backup/SHOW05.ASC as far as you know? (y / n / unsure)" "show equals SHOW05"
Ask "Was the console switched OFF at any time since E8 this afternoon? (y / n / unsure)" "console powered off since E8"

# --- H65: the power-on pending list (optional) ---
Ask "Do you want to power-cycle the console now, so that the first list after Connect is the power-on list (H65)? (y / n)" "power cycle before E8b"
Write-Host "   If you answered y: switch the console OFF, wait 5 s, switch it ON, wait until it is ready, and touch nothing." -ForegroundColor Yellow
Confirm-Done "When the console is ready (or if you skipped this)." "console ready for E8b"

# --- H68: USBPcap before the WinUSB bind ---
Write-Host ""
Write-Host "USBPcap must record from BEFORE the WinUSB bind (H68: what the driver does on the control endpoint)." -ForegroundColor Yellow
Write-Host "In an ADMIN PowerShell window start it on the console's root hub, as for E3b, into a NEW file, e.g.:" -ForegroundColor Yellow
Write-Host '   & "C:\Program Files\USBPcap\USBPcapCMD.exe" -d \\.\USBPcap1 -A --inject-descriptors -o e8b.pcap' -ForegroundColor White
Confirm-Done "When USBPcap is running." "usbpcap running (owner)"
Log ("interface 1 driver service before the bind: " + (Interface1Service))
Write-Host ""
Write-Host "Now bind WinUSB to the console's interface 1 with Zadig: Options > List All Devices, pick the entry with" -ForegroundColor Yellow
Write-Host "USB ID 14D5 0200 01 (never the one ending in 00), driver WinUSB, Replace Driver." -ForegroundColor Yellow
Step "Start Zadig's Replace Driver now." "zadig bind start"
$svc = Interface1Service
while ($svc -ne 'WinUSB') {
    Write-Host ("   Interface 1 is bound to '{0}', not to WinUSB yet." -f $svc) -ForegroundColor Red
    Read-Host "   Finish the Zadig bind, then press Enter to check again" | Out-Null
    $svc = Interface1Service
}
Log "interface 1 driver service after the bind: WinUSB"

# --- the backend and the UI ---
$today = "{0:yyyy-MM-dd}" -f (Get-Date)
Write-Host ""
Write-Host ("Starting the backend in hardware mode (confirmed for {0}) and the UI in two new windows ..." -f $today) -ForegroundColor Yellow
Start-Process -FilePath "dotnet" -ArgumentList @("run", "--project", "src/backend/SmartFade.Api", "--", "--Device:Mode=Usb", ("--Device:Usb:ConfirmDate=" + $today)) -WorkingDirectory $Repo | Out-Null
Log ("backend started by the script: dotnet run --project src/backend/SmartFade.Api -- --Device:Mode=Usb --Device:Usb:ConfirmDate=" + $today)
Start-Process -FilePath "cmd.exe" -ArgumentList @("/k", "npm start") -WorkingDirectory (Join-Path $Repo "src/frontend") | Out-Null
Log "ui dev server started by the script: npm start in src/frontend"
Write-Host "   Open http://localhost:4200 when the UI window says it is ready. The badge must read HARDWARE and the panels" -ForegroundColor Yellow
Write-Host "   'Not reported by the console yet' / 'Not delivered ...' BEFORE you press Connect." -ForegroundColor Yellow
Confirm-Done "When the UI shows HARDWARE and everything is read-only." "ui ready, hardware badge (owner)"
Write-Host ""
Write-Host "Starting position on the console: all channel faders at ZERO, MASTER at FULL, BUMPS at ZERO, faders in CHANNEL mode (1-24)," -ForegroundColor Yellow
Write-Host "BLACKOUT off, SOLO off, IND 1 and IND 2 off, no sequence running, page 1." -ForegroundColor Yellow
Confirm-Done "Set that up now." "starting position set (owner)"

# --- Connect and idle ---
Step "Press CONNECT in the UI." "connect pressed"
Confirm-Done "When the UI shows Connected." "connected (owner)"
Wait-Idle 60 "idle after connect"
Ask "Did anything on the console change by itself during the idle minute? (n, or what)" "idle observation"

# --- B1: repeat E8's readings with times ---
Step "Switch the faders to MEMORY mode (press the FADERS MEMS key; the bump LEDs turn red)." "memory mode on"
Wait-Idle 5 "memory mode"
Step "MEMORY FADER 1: slowly ALL THE WAY UP." "memory fader 1 up start"
Confirm-Done "When it is at the TOP (hands off)." "memory fader 1 at full"
Wait-Idle 5 "memory fader 1 at full"
Step "MEMORY FADER 1: slowly back to ZERO." "memory fader 1 down start"
Confirm-Done "When it is at ZERO." "memory fader 1 at zero"
Step "MEMORY FADER 2: slowly ALL THE WAY UP, then after 5 s slowly back to ZERO." "memory fader 2 up start"
Confirm-Done "When it is back at ZERO." "memory fader 2 at zero"
Wait-Idle 5 "after memory fader 2"
Step "PAGE CHANGE: hold FADERS MEMS, press bump 12, release MEMS (page 12). All faders stay at zero." "page 12 start"
Confirm-Done "When the LCD shows page 12 (or the keys show it)." "page 12 selected"
Wait-Idle 5 "page 12"
Step "PAGE CHANGE back: hold FADERS MEMS, press bump 1, release MEMS (page 1)." "page 1 start"
Confirm-Done "When page 1 is back." "page 1 selected"
Wait-Idle 5 "page 1"
Step "BLACKOUT: press it ON." "blackout on"
Wait-Idle 5 "blackout on"
Step "BLACKOUT: press it OFF." "blackout off"
Wait-Idle 5 "blackout off"
Step "IND 1: press it ON." "ind 1 on"
Wait-Idle 5 "ind 1 on"
Step "IND 2: press it ON." "ind 2 on"
Wait-Idle 5 "ind 2 on"
Step "IND 1: press it OFF." "ind 1 off"
Wait-Idle 5 "ind 1 off"
Step "IND 2: press it OFF." "ind 2 off"
Wait-Idle 5 "ind 2 off"
Step "REC MEM: press it once (the screen 'Record Memory'). Press NO bump." "rec mem on"
Wait-Idle 5 "rec mem screen"
Step "CLEAR: press it once to leave REC MEM." "rec mem left with clear"
Wait-Idle 5 "after rec mem"
Step "EDIT MEM: press it once. Press NO bump." "edit mem on"
Wait-Idle 5 "edit mem screen"
Step "CLEAR: press it once to leave EDIT MEM." "edit mem left with clear"
Wait-Idle 5 "after edit mem"
Step "REC SEQ: press it once. Press NO bump." "rec seq on"
Wait-Idle 5 "rec seq screen"
Step "CLEAR: press it once to leave REC SEQ." "rec seq left with clear"
Wait-Idle 5 "after rec seq"
Step "COPY: press it once. Press NO bump." "copy on"
Wait-Idle 5 "copy screen"
Step "CLEAR: press it once to leave COPY." "copy left with clear"
Wait-Idle 5 "after copy"
Step "STACK: press and HOLD it." "stack press"
Confirm-Done "Hold it for 3 seconds, then RELEASE it." "stack release"
Wait-Idle 5 "after stack"
Ask "Is the STACK key lit, dimmed or off now?" "stack led after press"

# --- B2: new tests ---
Step "Switch the faders back to CHANNEL mode (FADERS 1-24)." "channel mode on"
Wait-Idle 5 "channel mode"
Step "CHANNEL FADER 5: slowly ALL THE WAY UP (the fixture lights)." "channel fader 5 up start"
Confirm-Done "When it is at the TOP." "channel fader 5 at full"
Wait-Idle 5 "channel fader 5 at full"
Step "BLACKOUT: press it ON while channel fader 5 stays at full." "blackout on with level"
Wait-Idle 5 "blackout on with level"
Ask "What did the fixture do when BLACKOUT went on? (e.g. went dark)" "fixture at blackout"
Step "BLACKOUT: press it OFF." "blackout off with level"
Wait-Idle 5 "blackout off with level"
Step "CHANNEL FADER 5: slowly back to ZERO." "channel fader 5 down start"
Confirm-Done "When it is at ZERO." "channel fader 5 at zero"
Step "Switch to MEMORY mode again (FADERS MEMS)." "memory mode on again"
Wait-Idle 5 "memory mode again"
Step "H69: MEMORY FADER 1 slowly to FULL and leave it there." "h69 memory fader 1 up"
Confirm-Done "When it is at FULL." "h69 memory fader 1 at full"
Step "H69: PAGE CHANGE to page 12 with fader 1 still at full (hold MEMS, bump 12, release)." "h69 page 12 with fader up"
Confirm-Done "When page 12 is selected." "h69 page 12 selected"
Wait-Idle 5 "h69 page 12, fader 1 up"
Ask "Is the bump LED under fader 1 blinking now? (y / n)" "h69 bump 1 blinking"
Step "H69: MEMORY FADER 1 slowly to ZERO." "h69 memory fader 1 down"
Confirm-Done "When it is at ZERO." "h69 memory fader 1 at zero"
Wait-Idle 5 "h69 fader 1 at zero on page 12"
Step "PAGE CHANGE back to page 1 (hold MEMS, bump 1, release)." "page 1 again"
Confirm-Done "When page 1 is back." "page 1 selected again"
Wait-Idle 5 "page 1 again"
Step "H71: MASTER to about HALF, then MEMORY FADER 1 to about HALF." "h71 master half, fader 1 half"
Confirm-Done "When both are at about half." "h71 set"
Wait-Idle 5 "h71 fader 1 half, master half"
Step "H71: MASTER back to FULL, MEMORY FADER 1 to ZERO." "h71 reset"
Confirm-Done "When MASTER is at full and fader 1 at zero." "h71 reset done"
Step "H71: BUMPS fader to about HALF, MEMORY FADER 2 to about a QUARTER, then press and HOLD the bump under fader 2." "h71 bump 2 press"
Confirm-Done "Hold it for 3 seconds, then RELEASE it." "h71 bump 2 release"
Wait-Idle 5 "after h71 bump"
Step "BUMPS fader to ZERO and MEMORY FADER 2 to ZERO." "h71 bumps and fader 2 to zero"
Confirm-Done "When both are at zero." "h71 cleanup done"
Step "H73: switch to CHANNEL mode, CHANNEL FADER 5 to FULL; then MEMORY mode and MEMORY FADER 1 to FULL (both faders stay up)." "h73 setup"
Confirm-Done "When both are up and the faders are in MEMORY mode." "h73 setup done"
Wait-Idle 5 "h73 setup"
Step "H73: CLEAR once." "clear x1"
Wait-Idle 5 "after clear x1"
Step "H73: CLEAR twice (two presses within a second)." "clear x2"
Wait-Idle 5 "after clear x2"
Step "H73: CLEAR three times." "clear x3"
Wait-Idle 5 "after clear x3"
Step "H73: CLEAR four times." "clear x4"
Wait-Idle 5 "after clear x4"
Ask "After the CLEAR presses: which bump LEDs blink, and are the faders still physically up? (describe)" "h73 result"
Step "H73: bring MEMORY FADER 1 to ZERO, then CHANNEL mode and CHANNEL FADER 5 to ZERO." "h73 cleanup"
Confirm-Done "When both faders are at zero (any blinking should stop)." "h73 cleanup done"
Wait-Idle 5 "after h73"
Step "SEQUENCE: start the sequence on memory fader 21 the way you usually do (MEMORY mode, fader 21 up?)." "sequence start"
Confirm-Done "When it is running." "sequence running"
Wait-Idle 15 "sequence running"
Step "SEQUENCE: stop it the way you usually do." "sequence stop"
Confirm-Done "When it has stopped." "sequence stopped"
Wait-Idle 5 "after sequence"
Ask "How exactly did you start and stop the sequence? (which fader or key)" "sequence start/stop method"
Ask "Optional: do you want to press GO once with the STACK key lit? Only on a safe rig. (y / n)" "go with stack decision"
Write-Host "   If y: press STACK (lit), then GO once. If n: skip the next two prompts with Enter." -ForegroundColor Yellow
Step "GO with STACK lit (or Enter to skip)." "go with stack lit"
Confirm-Done "When the crossfade is over (or skipped)." "go with stack done"
Wait-Idle 10 "end of actions"

# --- End ---
Step "Press DISCONNECT in the UI." "disconnect pressed"
Confirm-Done "When the UI shows Disconnected." "disconnected (owner)"
Confirm-Done "Close the backend window (Ctrl+C in it) and stop USBPcap (Ctrl+C in its window)." "backend and usbpcap stopped (owner)"
Log "END guided E8b session"
Write-Host ""
Write-Host "Files to copy into docs/protocol/captures/ (with the date prefix 2026-10-30-e8b-...):" -ForegroundColor Green
Write-Host ("   action log:   " + $LogFile) -ForegroundColor White
Write-Host ("   backend log:  " + (Join-Path $env:LOCALAPPDATA "SmartFade\captures") + " (the newest .jsonl)") -ForegroundColor White
Write-Host "   USBPcap file: the .pcap you named when starting USBPcapCMD" -ForegroundColor White
Write-Host ("   SD backup:    " + $backup.Trim() + " into smartfade-sd-card-backup/") -ForegroundColor White
Write-Host "Afterwards put the ETC driver back on interface 1 if you want SmartSoft again (E8 runbook step 2)." -ForegroundColor Green
