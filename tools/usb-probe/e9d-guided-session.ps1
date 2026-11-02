# Guided operator session "E9d" (written 2026-11-02 from the E9c one, not yet run): the passive repeat that SC-5
# (docs/protocol/safety-checks.md) names for the classes it leaves "after E9d". SmartSoft's Console Simulator does the
# writing in TWO SmartSoft sessions (a restart between them), USBPcap records, this script only logs and starts SmartSoft;
# our own software is not involved. In each session: every channel fader 1-24 once in bank 1-24; FADERS 25-48, every fader
# there once (the Simulator labels them 25-48) and one held bump there; FADERS MEMS, every fader 1-24 once as a memory fader
# of page 1 (an empty memory moves only its record) and every memory bump once; FADERS 1-24; a held bump of 5 s; the S4a
# case of E9c (a mode switch with a PC level in force) once more.
# Never in E9d: REC MEM, EDIT MEM, COPY, REC SEQ, MAGIC, SNAPSHOT, RATE, the wheel, GO, STACK, NEXT, PAUSE, CLEAR, Patch /
# Independents / Settings edits, page clicks in SmartSoft, Open showfile, New show, Save As, any Erase. Nothing of the show
# is changed on purpose: the after-session SD save must equal the backup byte for byte.
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
    return $a.Trim()
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

# One full pass of the live controls in every bank; $s names the SmartSoft session (1 or 2) in every tag.
function Run-Pass([int]$s) {
    $p = "s" + $s + " "
    Step "Open SmartSoft's CONSOLE SIMULATOR window. Touch nothing in it afterwards." ($p + "simulator opened")
    Wait-Idle 15 ($p + "simulator open, untouched")

    Write-Host ""
    Write-Host ("Pass {0}, part A: each SIMULATOR channel fader 1 to 24 once (bank 1-24): up to about HALF, hold 1 s, back to ZERO." -f $s) -ForegroundColor Green
    foreach ($n in 1..24) {
        Step ("A: SIMULATOR channel fader {0}: up to about HALF, hold 1 s, back to ZERO." -f $n) ($p + "a sim fader " + $n + " half and back")
        Wait-Idle 2 ($p + "after fader " + $n)
    }
    Wait-Idle 5 ($p + "after part A")

    Write-Host ""
    Write-Host ("Pass {0}, part B: bank 25-48. The Simulator labels its faders 25 to 48 there: the fader labelled N is the (N - 24)th fader." -f $s) -ForegroundColor Green
    Step "B: SIMULATOR FADERS 25-48." ($p + "b sim faders 25-48")
    Wait-Idle 5 ($p + "b bank 25-48")
    foreach ($n in 25..48) {
        Step ("B: SIMULATOR fader labelled {0}: up to about HALF, hold 1 s, back to ZERO." -f $n) ($p + "b sim fader labelled " + $n + " half and back")
        Wait-Idle 2 ($p + "after fader labelled " + $n)
    }
    Step "B: SIMULATOR bump under the fader labelled 25: press, HOLD 2 s, release." ($p + "b sim bump labelled 25 held 2 s")
    Wait-Idle 5 ($p + "after b bump")
    Step "B: SIMULATOR FADERS 1-24." ($p + "b sim faders 1-24")
    Wait-Idle 5 ($p + "after part B")

    Write-Host ""
    Write-Host ("Pass {0}, part C: memory mode, page 1. Each SIMULATOR fader 1 to 24 once as a memory fader (an empty memory moves nothing but its record), then each memory bump once." -f $s) -ForegroundColor Green
    Step "C: SIMULATOR FADERS MEMS." ($p + "c sim faders mems")
    Wait-Idle 5 ($p + "c memory mode")
    foreach ($n in 1..24) {
        Step ("C: SIMULATOR fader {0} (memory {0} of page 1): up to about HALF, hold 1 s, back to ZERO." -f $n) ($p + "c sim memory fader " + $n + " half and back")
        Wait-Idle 2 ($p + "after memory fader " + $n)
    }
    foreach ($n in 1..24) {
        Step ("C: SIMULATOR bump under memory {0}: press, HOLD 1 s, release." -f $n) ($p + "c sim memory bump " + $n + " held 1 s")
        Wait-Idle 2 ($p + "after memory bump " + $n)
    }
    Step "C: SIMULATOR FADERS 1-24." ($p + "c sim faders 1-24")
    Wait-Idle 5 ($p + "after part C")

    Write-Host ""
    Write-Host ("Pass {0}, part D: a long hold and the mode switch with a PC level in force (E9c S4a)." -f $s) -ForegroundColor Green
    Step "D: SIMULATOR bump under channel 7: press, HOLD 5 s, release." ($p + "d sim bump 7 held 5 s")
    Wait-Idle 5 ($p + "after d bump 7")
    Step "D: SIMULATOR channel fader 1 to about HALF and LEAVE it there." ($p + "d sim fader 1 half, left")
    Wait-Idle 5 ($p + "d channel 1 at half from the PC")
    Step "D: SIMULATOR FADERS MEMS (a mode switch with the PC's channel 1 level in force)." ($p + "d sim faders mems with pc level")
    Wait-Idle 5 ($p + "d memory mode with pc level")
    Ask "D: memory fader 1's bump LED now (steady / blinking / off), and any light from memory 1? (describe)" ($p + "d memory fader 1 after switch") | Out-Null
    Step "D: SIMULATOR fader 1 (now memory 1): drag it to FULL, hold 2 s, then to ZERO." ($p + "d sim memory fader 1 full and zero")
    Wait-Idle 5 ($p + "after d memory fader 1")
    Step "D: SIMULATOR FADERS 1-24." ($p + "d sim faders 1-24 back")
    Wait-Idle 5 ($p + "d channel mode again")
    Ask "D: channel 1 now: its level on the LCD / bump LED (half as before the switch, zero, or else)? (describe)" ($p + "d channel 1 after round trip") | Out-Null
    Step "D: SIMULATOR channel fader 1: drag it to FULL and back to ZERO (note where the light reacted)." ($p + "d sim fader 1 full and zero cleanup")
    Wait-Idle 10 ($p + "after part D")
}

$host.UI.RawUI.WindowTitle = "E9d guided session"
Write-Host "E9d guided session: SmartSoft's Simulator drives every fader and bump of every bank, twice (two SmartSoft sessions), while USBPcap records; our software is not involved." -ForegroundColor Green
Write-Host "Follow the yellow instructions one by one. Press Enter exactly when you START each action. Leave the waits untouched." -ForegroundColor Green
Write-Host "Never: the record keys, RATE, the wheel, GO, STACK, NEXT, PAUSE, CLEAR, any SmartSoft edit, page clicks in SmartSoft, show load or erase." -ForegroundColor Green
Log "START guided E9d session (PC clock, local time)"

# --- Safety rule 3 and the open questions ---
Write-Host ""
Write-Host "Safety rule 3: save the show to the SD card on the console NOW (Menu > Save/Load Show > Save), before anything else." -ForegroundColor Yellow
$backup = ""
while ([string]::IsNullOrWhiteSpace($backup)) {
    $backup = Read-Host "   Type the file name the console used for the backup (e.g. SHOW26.ASC); it is required"
}
Log ("backup taken: " + $backup.Trim() + " (copy it into smartfade-sd-card-backup/ after the session)")
Ask "Was the console switched OFF since E9c (2026-11-02, 01:20)? (y / n / unsure)" "console powered off since e9c" | Out-Null
Ask "What is on your rig today (fixtures and their DMX addresses)? (describe)" "rig today" | Out-Null

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

# --- USBPcap before SmartSoft, into a new file per attempt ---
$pcapName = 'e9d-' + (Get-Date -Format 'yyyyMMdd-HHmm') + '.pcap'
Write-Host ""
Write-Host "In an ADMIN PowerShell window start USBPcap on the console's root hub, into a NEW file:" -ForegroundColor Yellow
Write-Host ('   & "C:\Program Files\USBPcap\USBPcapCMD.exe" -d \\.\USBPcap1 -A --inject-descriptors -o "' + (Join-Path (Get-Location) $pcapName) + '"') -ForegroundColor White
Log ("usbpcap file name suggested to the owner: " + $pcapName)
Confirm-Done "When USBPcap is running." "usbpcap running (owner)"
Write-Host ""
Write-Host "Starting position on the console: all channel faders at ZERO, MASTER at FULL, BUMPS at FULL, CHANNEL mode (FADERS 1-24 lit)," -ForegroundColor Yellow
Write-Host "BLACKOUT off, SOLO off, IND 1 and IND 2 off, STACK off, NEXT off (unlit), no sequence running, page 1, nothing armed." -ForegroundColor Yellow
Confirm-Done "Set that up now." "starting position set (owner)"
Ask "Which memories of page 1 are recorded (their keys dim red in MEMS mode)? (list the numbers)" "page 1 recorded memories" | Out-Null

# --- Session 1 ---
Start-SmartSoft 1
Wait-Idle 60 "baseline: SmartSoft online, nothing touched (the System pair and the download)"
Run-Pass 1
Close-SmartSoft 1
Wait-Idle 10 "between sessions 1 and 2"

# --- Session 2: the same again after a restart ---
Start-SmartSoft 2
Wait-Idle 30 "session 2 download"
Run-Pass 2

# --- End ---
Wait-Idle 30 "end of actions"
Close-SmartSoft 2
Confirm-Done "Stop USBPcap (Ctrl+C in its window)." "usbpcap stopped (owner)"
Ask "Physical fader positions at the end (channel faders, MASTER, BUMPS) and the LEDs of SOLO, BLACKOUT, IND 1 / 2, STACK, NEXT? (describe)" "end positions" | Out-Null
Write-Host ""
Write-Host "Save the show to the SD card on the console NOW, to another FREE number (the after-session file; it must equal the backup byte for byte: no record function was used)." -ForegroundColor Yellow
$after = ""
while ([string]::IsNullOrWhiteSpace($after)) {
    $after = Read-Host "   Type the file name the console used for this save (e.g. SHOW27.ASC); it is required"
}
Log ("after-session save: " + $after.Trim() + " (copy it into smartfade-sd-card-backup/ with the backup)")
Log "END guided E9d session"
Write-Host ""
Write-Host "Files to copy into docs/protocol/captures/ (with the date prefix ...-e9d-...):" -ForegroundColor Green
Write-Host ("   action log:   " + $LogFile) -ForegroundColor White
Write-Host "   USBPcap file: the .pcap you named when starting USBPcapCMD (split to the console before it is committed)" -ForegroundColor White
Write-Host ("   SD files:     " + $backup.Trim() + " (backup) and " + $after.Trim() + " (after) into smartfade-sd-card-backup/; they must be byte-identical") -ForegroundColor White
