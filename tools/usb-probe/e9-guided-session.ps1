# Guided operator session "E9" (2026-10-30): the passive USBPcap capture of SmartSoft WRITING to the
# console (docs/protocol/reverse-engineering.md, "E9 runbook"; static analysis in
# docs/protocol/smartsoft-analysis.md S11.15). Our own software is not involved: SmartSoft talks to
# the console exactly as when the owner uses it, USBPcap records the bus, and this script only
#   - takes the safety-rule-3 gate (the SD backup made just now),
#   - checks that interface 1 is bound to ETC's driver (SmartSoft needs it; WinUSB must be undone first),
#   - starts SmartSoft (Start-Process) after USBPcap is running,
#   - shows one instruction at a time and logs the PC clock when the operator presses Enter,
#   - asks a few zero-risk questions and logs the answers,
#   - ends with the list of files to copy into docs/protocol/captures/.
# Never in E9: the wheel, REC SEQ, Patch / Independents / Settings edits, page clicks, Open showfile,
# New show, Save As, any Erase. Steps 13-16 use the console's own REC MEM, EDIT MEM and COPY through
# the Simulator keys, only on the empty memories 9 and 10 of page 1, which are deleted again at the end.
# -LogFile is mandatory and must be a NEW file (action logs are hashed evidence).
param(
    [Parameter(Mandatory = $true)][string]$LogFile,
    [string]$SmartSoft = 'C:\Program Files (x86)\ETC\SmartSoft\Binaries\SmartSoft.exe'
)

if (Test-Path -LiteralPath $LogFile) {
    Write-Host ("Refusing to append to the existing file {0}: action logs are hashed evidence. Choose a new file name." -f $LogFile) -ForegroundColor Red
    exit 1
}
if (-not (Test-Path -LiteralPath $SmartSoft)) {
    Write-Host ("SmartSoft was not found at {0}; pass -SmartSoft <path to SmartSoft.exe>." -f $SmartSoft) -ForegroundColor Red
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
function MemoryIntegrityState {
    try {
        $dg = Get-CimInstance -Namespace 'root\Microsoft\Windows\DeviceGuard' -ClassName 'Win32_DeviceGuard' -ErrorAction Stop
        if ($dg.SecurityServicesRunning -contains 2) { return "ON (HVCI running: the ETC driver will not load)" }
        return "off"
    } catch {
        return "unknown"
    }
}

$host.UI.RawUI.WindowTitle = "E9 guided session"
Write-Host "E9 guided session: SmartSoft writes to the console while USBPcap records; our software is not involved." -ForegroundColor Green
Write-Host "Follow the yellow instructions one by one. Press Enter exactly when you START each action. Leave at least the" -ForegroundColor Green
Write-Host "waits between actions untouched: they separate the writes in time." -ForegroundColor Green
Write-Host "Never in E9: the wheel, REC SEQ, Patch / Independents / Settings edits, page clicks, Open showfile, New show," -ForegroundColor Green
Write-Host "Save As, any Erase." -ForegroundColor Green
Log "START guided E9 session (PC clock, local time)"

# --- Safety rule 3: the backup ---
Write-Host ""
Write-Host "Safety rule 3: save the show to the SD card on the console NOW (Menu > Save/Load Show > Save), before anything else." -ForegroundColor Yellow
$backup = ""
while ([string]::IsNullOrWhiteSpace($backup)) {
    $backup = Read-Host "   Type the file name the console used for the backup (e.g. SHOW07.ASC); it is required"
}
Log ("backup taken: " + $backup.Trim() + " (copy it into smartfade-sd-card-backup/ after the session)")
Ask "Is the show on the console still the same as smartfade-sd-card-backup/SHOW06.ASC as far as you know? (y / n / unsure)" "show equals SHOW06"
Ask "Was the console switched OFF since the last session (E8b)? (y / n / unsure)" "console powered off since E8b"

# --- The driver: SmartSoft needs ETC's driver on interface 1 ---
Log ("memory integrity: " + (MemoryIntegrityState))
$svc = Interface1Service
while ($svc -ne 'ETCHSP') {
    Write-Host ""
    Write-Host ("Interface 1 is bound to '{0}'. SmartSoft needs ETC's driver (service ETCHSP)." -f $svc) -ForegroundColor Red
    Write-Host "Device Manager > the console's interface-1 device > Update driver > Browse my computer > Let me pick >" -ForegroundColor Yellow
    Write-Host "'USB High-Speed-Pipe' (or uninstall the WinUSB device and scan for hardware changes). Memory Integrity must be off." -ForegroundColor Yellow
    Read-Host "   Switch the driver, then press Enter to check again" | Out-Null
    $svc = Interface1Service
}
Log "interface 1 driver service: ETCHSP (ETC's driver)"

# --- USBPcap before SmartSoft ---
Write-Host ""
Write-Host "In an ADMIN PowerShell window start USBPcap on the console's root hub, into a NEW file, e.g.:" -ForegroundColor Yellow
Write-Host '   & "C:\Program Files\USBPcap\USBPcapCMD.exe" -d \\.\USBPcap1 -A --inject-descriptors -o e9.pcap' -ForegroundColor White
Confirm-Done "When USBPcap is running." "usbpcap running (owner)"
Write-Host ""
Write-Host "Starting position on the console: all channel faders at ZERO, MASTER at FULL, BUMPS at FULL, faders in CHANNEL mode (1-24)," -ForegroundColor Yellow
Write-Host "BLACKOUT off, SOLO off, IND 1 and IND 2 off, STACK off, no sequence running, page 1. Memories 9 and 10 of page 1 must be EMPTY." -ForegroundColor Yellow
Confirm-Done "Set that up now." "starting position set (owner)"
Ask "Are memories 9 and 10 of page 1 empty (their keys unlit in MEMS mode)? (y / n)" "memories 9 and 10 empty"

# --- SmartSoft Online ---
Write-Host ""
Write-Host "Starting SmartSoft ..." -ForegroundColor Yellow
Start-Process -FilePath $SmartSoft -WorkingDirectory (Split-Path $SmartSoft) | Out-Null
Log "smartsoft start (Start-Process by the script)"
Confirm-Done "Wait until SmartSoft shows ONLINE." "smartsoft online (owner)"
Wait-Idle 60 "baseline: SmartSoft online, nothing touched"

# --- 1. the Console Simulator ---
Step "Open SmartSoft's CONSOLE SIMULATOR window (the on-screen console). Touch nothing in it afterwards." "simulator opened"
Wait-Idle 30 "simulator open, untouched"

# --- 2. a channel fader from the Simulator ---
Step "SIMULATOR channel fader 1 (bank 1-24): drag it slowly ALL THE WAY UP." "sim fader 1 up start"
Confirm-Done "When it is at the TOP." "sim fader 1 at full"
Ask "Did anything happen on the console? (the fixture, the bump LED under fader 1 blinking, the LCD)" "console reaction to sim fader 1 up"
Wait-Idle 15 "sim fader 1 at full"
Step "SIMULATOR channel fader 1: drag it slowly back to ZERO." "sim fader 1 down start"
Confirm-Done "When it is at ZERO." "sim fader 1 at zero"
Wait-Idle 15 "after sim fader 1"

# --- 3. a channel bump from the Simulator ---
Step "SIMULATOR bump button under channel 7: press and HOLD it (mouse button down)." "sim bump 7 press"
Confirm-Done "Hold it for 2 seconds, then RELEASE it." "sim bump 7 release"
Ask "What did the lights do during that bump?" "lights during sim bump 7"
Wait-Idle 15 "after sim bump 7"

# --- 4. MASTER from the Simulator ---
Step "SIMULATOR MASTER fader: drag it slowly down to about HALF." "sim master to half start"
Confirm-Done "When it is at about half." "sim master at half"
Wait-Idle 15 "sim master at half"
Step "SIMULATOR MASTER fader: drag it slowly back to FULL." "sim master to full start"
Confirm-Done "When it is at FULL." "sim master at full"
Wait-Idle 15 "after sim master"

# --- 5. BUMPS from the Simulator ---
Step "SIMULATOR BUMPS fader: drag it slowly to ZERO." "sim bumps to zero start"
Confirm-Done "When it is at ZERO." "sim bumps at zero"
Wait-Idle 15 "sim bumps at zero"
Step "SIMULATOR BUMPS fader: drag it slowly back to FULL." "sim bumps to full start"
Confirm-Done "When it is at FULL." "sim bumps at full"
Wait-Idle 15 "after sim bumps"

# --- 6. BLACKOUT from the Simulator ---
Step "SIMULATOR BLACKOUT: click it ON." "sim blackout on"
Wait-Idle 15 "sim blackout on"
Ask "Is the console's BLACKOUT key lit? (y / n)" "console blackout led after sim on"
Step "SIMULATOR BLACKOUT: click it OFF." "sim blackout off"
Wait-Idle 15 "after sim blackout"

# --- 7. SOLO from the Simulator ---
Step "SIMULATOR SOLO: click it ON." "sim solo on"
Wait-Idle 15 "sim solo on"
Step "SIMULATOR SOLO: click it OFF." "sim solo off"
Wait-Idle 15 "after sim solo"

# --- 8. a memory bump from the Simulator ---
Step "SIMULATOR: click FADERS MEMS (memory mode), then press and HOLD the bump under memory 1." "sim mems and memory bump 1 press"
Confirm-Done "Hold it for 2 seconds, then RELEASE it." "sim memory bump 1 release"
Wait-Idle 15 "after sim memory bump"
Step "SIMULATOR: click FADERS 1-24 (channel mode again)." "sim channel mode again"
Wait-Idle 15 "after sim channel mode"

# --- 9. GO with STACK unlit ---
Step "SIMULATOR GO (the play key under crossfader A) ONCE, with the STACK key unlit." "sim go stack unlit"
Confirm-Done "When whatever it did is over." "sim go done"
Ask "What happened on the console and the lights?" "reaction to sim go"
Wait-Idle 15 "after sim go"

# --- 10. Playback tab: all faders to zero ---
Step "SmartSoft PLAYBACK tab: click the 'all faders to zero' down-arrow button once." "playback all faders to zero"
Wait-Idle 15 "after all faders to zero"

# --- 11. a label with a non-ASCII character (H40) ---
Ask "What is channel 7's label right now in SmartSoft? (type it exactly; '-' if none)" "channel 7 label before"
Step "SmartSoft: rename channel 7's label to  Caj  spelled with the Czech C-with-caron (type it with the caron) and confirm." "channel 7 label to Caj with caron"
Wait-Idle 15 "label changed"
Ask "Does the console's LCD or SmartSoft show the caron correctly? (describe)" "caron shown"
Step "SmartSoft: rename channel 7's label back to what it was and confirm." "channel 7 label restored"
Wait-Idle 15 "label restored"

# --- 12. optional: Mem Edit ---
Ask "Optional: change channel 7's level in memory 1 in SmartSoft's Mem Edit and restore it afterwards? (y / n)" "mem edit decision"
Write-Host "   If n: press Enter through the next two prompts without doing anything." -ForegroundColor Yellow
Step "SmartSoft MEM EDIT: set channel 7 of memory 1 (page 1) to 50 % and confirm (or Enter to skip)." "mem edit memory 1 channel 7 to 50"
Wait-Idle 15 "after mem edit change"
Step "SmartSoft MEM EDIT: set channel 7 of memory 1 back to its old value and confirm (or Enter to skip)." "mem edit memory 1 channel 7 restored"
Wait-Idle 15 "after mem edit restore"

# --- 13. REC MEM through the Simulator (console function, empty memory 9) ---
Step "On the CONSOLE (physical): channel fader 5 to about HALF." "console fader 5 to half"
Confirm-Done "When it is at about half." "console fader 5 at half"
Wait-Idle 10 "fader 5 at half"
Step "SIMULATOR REC MEM: click it once (the console's LCD should say Record Memory / Select Bump or Stack)." "sim rec mem"
Wait-Idle 10 "rec mem screen"
Step "SIMULATOR: click the bump under memory 9 (records the look into memory 9 of page 1)." "sim rec mem bump 9"
Wait-Idle 15 "after record into memory 9"
Ask "Is memory 9's key now lit dimmed red in MEMS mode on the console (something stored)? (y / n)" "memory 9 recorded"

# --- 14. EDIT MEM through the Simulator ---
Step "SIMULATOR EDIT MEM: click it once." "sim edit mem"
Wait-Idle 10 "edit mem screen"
Step "SIMULATOR: click the bump under memory 9." "sim edit mem bump 9"
Wait-Idle 10 "memory 9 in edit"
Step "On the CONSOLE: channel fader 5 slowly to FULL (the edit)." "console fader 5 to full in edit"
Confirm-Done "When it is at FULL." "console fader 5 at full in edit"
Wait-Idle 10 "edited"
Step "SIMULATOR CLEAR: click it once to leave EDIT MEM." "sim clear leaves edit mem"
Wait-Idle 15 "after edit mem"

# --- 15. COPY through the Simulator ---
Step "SIMULATOR COPY: click it once." "sim copy"
Wait-Idle 10 "copy screen"
Step "SIMULATOR: click the bump under memory 9 (source)." "sim copy source 9"
Wait-Idle 10 "copy source chosen"
Step "SIMULATOR: click the bump under memory 10 (target)." "sim copy target 10"
Wait-Idle 15 "after copy"
Ask "Are the keys of memories 9 AND 10 now lit dimmed red in MEMS mode? (y / n)" "memories 9 and 10 recorded"

# --- 16. delete memories 9 and 10 again (all-zero content deletes, F107) ---
Step "On the CONSOLE: channel fader 5 to ZERO (all channel faders at zero now)." "console fader 5 to zero"
Confirm-Done "When it is at ZERO." "console fader 5 at zero"
Step "SIMULATOR EDIT MEM, then the bump under memory 9, then (all faders at zero) SIMULATOR CLEAR: memory 9 is deleted." "delete memory 9"
Confirm-Done "When the LCD is back to normal." "memory 9 deleted"
Wait-Idle 15 "after delete 9"
Step "SIMULATOR EDIT MEM, then the bump under memory 10, then SIMULATOR CLEAR: memory 10 is deleted." "delete memory 10"
Confirm-Done "When the LCD is back to normal." "memory 10 deleted"
Wait-Idle 15 "after delete 10"
Ask "Are memories 9 and 10 empty again (keys unlit in MEMS mode)? (y / n)" "memories 9 and 10 empty again"

# --- End ---
Wait-Idle 30 "end of actions"
Step "Close SmartSoft now (the close button of its window). Do NOT save anything if it asks." "smartsoft close start"
Confirm-Done "When SmartSoft is closed." "smartsoft closed"
Confirm-Done "Stop USBPcap (Ctrl+C in its window)." "usbpcap stopped (owner)"
Log "END guided E9 session"
Write-Host ""
Write-Host "Files to copy into docs/protocol/captures/ (with the date prefix ...-e9-...):" -ForegroundColor Green
Write-Host ("   action log:   " + $LogFile) -ForegroundColor White
Write-Host "   USBPcap file: the .pcap you named when starting USBPcapCMD (it is split to the console before it is committed)" -ForegroundColor White
Write-Host ("   SD backup:    " + $backup.Trim() + " into smartfade-sd-card-backup/") -ForegroundColor White
