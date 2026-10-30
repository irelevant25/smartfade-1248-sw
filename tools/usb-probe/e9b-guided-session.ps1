# Guided operator session "E9b" (2026-10-31): the repeat of E9 (passive USBPcap capture of SmartSoft
# writing to the console; docs/protocol/captures/2026-10-30-e9-smartsoft-writes.md, "Next capture to
# take", item B). It reproduces E9's live steps with a timed log, so that F131-F134 can become VERIFIED,
# and adds the tests of H81-H86. Our own software is not involved: SmartSoft talks to the console as for
# any user, USBPcap records, this script only logs and starts SmartSoft.
# Never in E9b: the wheel, REC SEQ, Patch / Independents / Settings edits, page clicks in SmartSoft,
# Open showfile, New show, Save As, any Erase. Memory 9 of page 1 is recorded and deleted again (REC MEM
# of an empty look); nothing else of the show is changed on purpose.
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
function Start-SmartSoft([int]$n) {
    Write-Host ""
    Write-Host ("Starting SmartSoft (session #{0}) ..." -f $n) -ForegroundColor Yellow
    Start-Process -FilePath $SmartSoft -WorkingDirectory (Split-Path $SmartSoft) | Out-Null
    Log ("smartsoft start #" + $n + " (Start-Process by the script)")
    Confirm-Done "Wait until SmartSoft shows ONLINE." ("smartsoft online #" + $n + " (owner)")
}
function Close-SmartSoft([int]$n) {
    Step "Close SmartSoft now (the close button of its window). Do NOT save anything if it asks." ("smartsoft close #" + $n + " start")
    Confirm-Done "When SmartSoft is closed." ("smartsoft closed #" + $n)
}

$host.UI.RawUI.WindowTitle = "E9b guided session"
Write-Host "E9b guided session: the repeat of E9. SmartSoft writes to the console while USBPcap records; our software is not involved." -ForegroundColor Green
Write-Host "Follow the yellow instructions one by one. Press Enter exactly when you START each action. Leave the waits untouched." -ForegroundColor Green
Write-Host "Never: the wheel, REC SEQ, Patch / Independents / Settings edits, page clicks in SmartSoft, Open showfile, New show, Save As, Erase." -ForegroundColor Green
Log "START guided E9b session (PC clock, local time)"

# --- Safety rule 3 and the open questions of E9 ---
Write-Host ""
Write-Host "Safety rule 3: save the show to the SD card on the console NOW (Menu > Save/Load Show > Save), before anything else." -ForegroundColor Yellow
$backup = ""
while ([string]::IsNullOrWhiteSpace($backup)) {
    $backup = Read-Host "   Type the file name the console used for the backup (e.g. SHOW08.ASC); it is required"
}
Log ("backup taken: " + $backup.Trim() + " (copy it into smartfade-sd-card-backup/ after the session)")
Ask "E9 question 1: memory 7 of page 1 was edited by mistake in E9 (channel 7 to full). Did you check and restore it? (y / n / what you found)" "memory 7 state"
Ask "E9 question 2: in E9's Mem Edit step, did you use the Console Simulator window or the Mem Edit tab? (simulator / tab)" "e9 mem edit window"
Ask "E9 question 3: at about 22:24:39 in E9 channel 1 dropped to zero with no write from SmartSoft. Did you touch the physical fader 1 or a key then? (y / n / unsure)" "e9 drop at 279 s"
Ask "E9 question 4: at about 22:24:50 in E9 the Simulator's fader 1 went down and up again. Was that you? (y / n / unsure)" "e9 fader move at 290 s"
Ask "Was the console switched OFF since E9? (y / n / unsure)" "console powered off since E9"

# --- The driver: SmartSoft needs ETC's driver on interface 1 ---
Log ("memory integrity: " + (MemoryIntegrityState))
$svc = Interface1Service
while ($svc -ne 'ETCHSP') {
    Write-Host ""
    Write-Host ("Interface 1 is bound to '{0}'. SmartSoft needs ETC's driver (service ETCHSP)." -f $svc) -ForegroundColor Red
    Write-Host "Device Manager > the console's interface-1 device > Update driver > Browse my computer > Let me pick > 'USB High-Speed-Pipe'." -ForegroundColor Yellow
    Read-Host "   Switch the driver, then press Enter to check again" | Out-Null
    $svc = Interface1Service
}
Log "interface 1 driver service: ETCHSP (ETC's driver)"

# --- USBPcap before SmartSoft ---
Write-Host ""
Write-Host "In an ADMIN PowerShell window start USBPcap on the console's root hub, into a NEW file, e.g.:" -ForegroundColor Yellow
Write-Host '   & "C:\Program Files\USBPcap\USBPcapCMD.exe" -d \\.\USBPcap1 -A --inject-descriptors -o e9b.pcap' -ForegroundColor White
Confirm-Done "When USBPcap is running." "usbpcap running (owner)"
Write-Host ""
Write-Host "Starting position on the console: all channel faders at ZERO, MASTER at FULL, BUMPS at FULL, CHANNEL mode (FADERS 1-24 lit)," -ForegroundColor Yellow
Write-Host "BLACKOUT off, SOLO off, IND 1 and IND 2 off, STACK off, no sequence running, page 1. Memory 9 of page 1 must be EMPTY." -ForegroundColor Yellow
Confirm-Done "Set that up now." "starting position set (owner)"
Ask "Is memory 9 of page 1 empty (its key unlit in MEMS mode)? (y / n)" "memory 9 empty"

# --- SmartSoft Online, baseline, Simulator ---
Start-SmartSoft 1
Wait-Idle 60 "baseline: SmartSoft online, nothing touched"
Step "Open SmartSoft's CONSOLE SIMULATOR window. Touch nothing in it afterwards." "simulator opened"
Wait-Idle 20 "simulator open, untouched"

# --- B1: E9's live steps again (F131-F134) ---
Step "SIMULATOR channel fader 1: drag it slowly ALL THE WAY UP." "sim fader 1 up start"
Confirm-Done "When it is at the TOP." "sim fader 1 at full"
Wait-Idle 15 "sim fader 1 at full"
Step "SIMULATOR channel fader 1: drag it slowly back to ZERO." "sim fader 1 down start"
Confirm-Done "When it is at ZERO." "sim fader 1 at zero"
Wait-Idle 15 "after sim fader 1"
Step "SIMULATOR bump under channel 7: press and HOLD it." "sim bump 7 press"
Confirm-Done "Hold it for 2 seconds, then RELEASE it." "sim bump 7 release"
Wait-Idle 15 "after sim bump 7"
Step "SIMULATOR MASTER: drag it slowly down to about HALF." "sim master to half start"
Confirm-Done "When it is at about half." "sim master at half"
Wait-Idle 15 "sim master at half"
Step "SIMULATOR MASTER: drag it slowly back to FULL." "sim master to full start"
Confirm-Done "When it is at FULL." "sim master at full"
Wait-Idle 15 "after sim master"
Step "SIMULATOR BUMPS: drag it slowly to ZERO." "sim bumps to zero start"
Confirm-Done "When it is at ZERO." "sim bumps at zero"
Wait-Idle 15 "sim bumps at zero"
Step "SIMULATOR BUMPS: drag it slowly back to FULL." "sim bumps to full start"
Confirm-Done "When it is at FULL." "sim bumps at full"
Wait-Idle 15 "after sim bumps"
Step "SIMULATOR BLACKOUT: click it ON." "sim blackout on"
Wait-Idle 15 "sim blackout on"
Step "SIMULATOR BLACKOUT: click it OFF." "sim blackout off"
Wait-Idle 15 "after sim blackout"
Step "SIMULATOR SOLO: click it ON." "sim solo on"
Wait-Idle 15 "sim solo on"
Step "SIMULATOR SOLO: click it OFF." "sim solo off"
Wait-Idle 15 "after sim solo"
Step "SIMULATOR: click FADERS MEMS, then press and HOLD the bump under memory 1." "sim mems and memory bump 1 press"
Confirm-Done "Hold it for 2 seconds, then RELEASE it." "sim memory bump 1 release"
Wait-Idle 15 "after sim memory bump"
Step "SIMULATOR: click FADERS 1-24." "sim channel mode again"
Wait-Idle 15 "after sim channel mode"
Step "SIMULATOR GO (the play key under crossfader A) ONCE, STACK unlit." "sim go stack unlit"
Confirm-Done "When whatever it did is over." "sim go done"
Wait-Idle 15 "after sim go"

# --- B2: H81, the PC fader against the physical fader ---
Step "H81: SIMULATOR channel fader 1 to FULL and leave it there (the physical fader 1 stays at zero)." "h81 sim fader 1 full"
Confirm-Done "When it is at FULL." "h81 sim fader 1 at full"
Wait-Idle 10 "h81 sim fader at full, physical at zero"
Step "H81: on the CONSOLE move the physical channel fader 1 slowly up to about HALF." "h81 physical fader 1 to half"
Confirm-Done "When it is at about half." "h81 physical fader 1 at half"
Wait-Idle 10 "h81 physical half"
Ask "What did the channel 1 level and the bump LED under fader 1 do while you moved the physical fader? (describe)" "h81 physical takes over?"
Step "H81: on the CONSOLE move the physical fader 1 slowly to FULL, then back to ZERO." "h81 physical fader 1 full then zero"
Confirm-Done "When it is at ZERO." "h81 physical fader 1 at zero"
Wait-Idle 10 "after h81 physical"
Step "H81: SIMULATOR channel fader 1 to ZERO." "h81 sim fader 1 to zero"
Confirm-Done "When it is at ZERO." "h81 sim fader 1 at zero"
Wait-Idle 10 "h81 both at zero"
Step "H81: on the CONSOLE physical fader 5 to about HALF; then in the SIMULATOR drag fader 5 slowly from ZERO to FULL." "h81 physical 5 half, sim 5 from zero"
Confirm-Done "When the Simulator's fader 5 is at FULL." "h81 sim fader 5 at full"
Ask "At which point did the Simulator's fader 5 take effect on the light: at once, or only when it reached the physical fader's level? (describe)" "h81 pickup"
Step "H81: SIMULATOR fader 5 to ZERO, then the CONSOLE's physical fader 5 to ZERO." "h81 cleanup"
Confirm-Done "When both are at zero." "h81 cleanup done"
Wait-Idle 15 "after h81"

# --- B3: H82, keys not yet seen from the PC (safe for the rig) ---
Step "H82: SIMULATOR STACK: click it ON." "sim stack on"
Wait-Idle 10 "sim stack on"
Step "H82: SIMULATOR STACK: click it OFF." "sim stack off"
Wait-Idle 10 "after sim stack"
Step "H82: SIMULATOR IND 1: click it ON, wait 5 s, click it OFF." "sim ind 1 on off"
Wait-Idle 10 "after sim ind 1"
Step "H82: SIMULATOR IND 2: click it ON, wait 5 s, click it OFF (IND 2 drives DMX 47 in your patch)." "sim ind 2 on off"
Wait-Idle 10 "after sim ind 2"
Step "H82: SIMULATOR FADERS 25-48, wait 5 s, then FADERS 1-24." "sim bank 25-48 and back"
Wait-Idle 15 "after sim bank"

# --- B4: H83, the console's own EDIT MEM selection (nothing stored) ---
Step "H83: on the CONSOLE press EDIT MEM, then the bump under memory 1, wait 3 s, then CLEAR (nothing is changed)." "console edit mem select 1 clear"
Confirm-Done "When the LCD is back to normal." "console edit mem left"
Wait-Idle 15 "after console edit mem page 1"
Step "H83: on the CONSOLE change to page 12 (hold MEMS, bump 12, release), then EDIT MEM, bump 1, wait 3 s, CLEAR." "console page 12 edit mem select 1 clear"
Confirm-Done "When the LCD is back to normal." "console edit mem left page 12"
Wait-Idle 10 "after console edit mem page 12"
Step "H83: on the CONSOLE back to page 1 (hold MEMS, bump 1, release)." "console page 1"
Wait-Idle 15 "after page 1"

# --- B5: H84, the label and a SmartSoft restart ---
Ask "What is channel 7's label right now in SmartSoft? ('-' if none)" "channel 7 label before"
Step "SmartSoft: rename channel 7's label to  Caj  with the Czech C-with-caron and confirm." "channel 7 label to Caj with caron"
Wait-Idle 15 "label changed"
Close-SmartSoft 1
Wait-Idle 10 "between sessions 1 and 2"
Start-SmartSoft 2
Wait-Idle 30 "session 2 download"
Ask "What does SmartSoft show as channel 7's label after the restart? (type it exactly)" "label after restart"
Step "SmartSoft: rename channel 7's label back to what it was ('-' = empty) and confirm." "channel 7 label restored"
Wait-Idle 15 "label restored"
Close-SmartSoft 2
Wait-Idle 10 "between sessions 2 and 3"
Start-SmartSoft 3
Wait-Idle 30 "session 3 download"
Ask "What does SmartSoft show as channel 7's label now? (type it exactly)" "label after second restart"
Step "Open the CONSOLE SIMULATOR window again. Touch nothing." "simulator opened again"
Wait-Idle 15 "simulator open again"

# --- B6: H85 / H86, REC MEM with the MASTER at half, and the deletion method ---
Step "H85: on the CONSOLE physical fader 5 to about HALF; SIMULATOR MASTER to about HALF." "h85 fader 5 half, master half"
Confirm-Done "When both are at about half." "h85 set"
Wait-Idle 10 "h85 set"
Step "H85: SIMULATOR REC MEM, then the bump under memory 9 (records the look into memory 9 of page 1)." "h85 rec mem bump 9"
Wait-Idle 15 "after record into memory 9"
Ask "Is memory 9's key lit dimmed red in MEMS mode now? (y / n)" "memory 9 recorded"
Step "H85: SIMULATOR MASTER back to FULL; on the CONSOLE physical fader 5 to ZERO." "h85 reset"
Confirm-Done "When MASTER is at full and fader 5 at zero." "h85 reset done"
Wait-Idle 10 "h85 reset"
Step "H86: SIMULATOR REC MEM, then the bump under memory 9 again: an empty look is recorded, which deletes memory 9 (your method)." "h86 delete 9 by rec mem"
Wait-Idle 15 "after delete 9"
Ask "Is memory 9 empty again (key unlit in MEMS mode)? (y / n)" "memory 9 empty again"

# --- B7: optional, the Mem Edit TAB (a Memory object write?) ---
Ask "Optional: record memory 9 once more and change one cell of it in SmartSoft's MEM EDIT TAB (not the Simulator), then delete it again? (y / n)" "mem edit tab decision"
Write-Host "   If n: press Enter through the next four prompts without doing anything." -ForegroundColor Yellow
Step "B7: on the CONSOLE physical fader 5 to about HALF; SIMULATOR REC MEM, bump 9 (or Enter to skip)." "b7 record memory 9"
Wait-Idle 15 "b7 recorded"
Step "B7: in SmartSoft's MEM EDIT TAB open memory 9 of page 1 and set channel 5 to 100 % in the table cell, confirm (or Enter to skip)." "b7 mem edit tab cell change"
Wait-Idle 15 "b7 cell changed"
Ask "Did the console react (LCD, keys, light)? (describe, or '-')" "b7 console reaction"
Step "B7: on the CONSOLE physical fader 5 to ZERO; SIMULATOR REC MEM, bump 9: memory 9 deleted again (or Enter to skip)." "b7 delete 9"
Wait-Idle 15 "b7 deleted"
Ask "Is memory 9 empty again? (y / n / skipped)" "b7 memory 9 empty"

# --- End ---
Wait-Idle 30 "end of actions"
Close-SmartSoft 3
Confirm-Done "Stop USBPcap (Ctrl+C in its window)." "usbpcap stopped (owner)"
Log "END guided E9b session"
Write-Host ""
Write-Host "Files to copy into docs/protocol/captures/ (with the date prefix ...-e9b-...):" -ForegroundColor Green
Write-Host ("   action log:   " + $LogFile) -ForegroundColor White
Write-Host "   USBPcap file: the .pcap you named when starting USBPcapCMD (split to the console before it is committed)" -ForegroundColor White
Write-Host ("   SD backup:    " + $backup.Trim() + " into smartfade-sd-card-backup/") -ForegroundColor White
