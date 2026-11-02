# Guided operator session "E9e" (written 2026-11-02 after E9d, cut to a spot check the same day, not yet run): the channel
# bumps the E9d script left out are cleared by the SC-5 addendum on the physical-control rule (VERIFIED for every fader of
# three banks and every memory bump); this session is only the control of that inference: FOUR bumps per SmartSoft session,
# about one minute each. SmartSoft's Console Simulator does the writing in TWO SmartSoft sessions (a restart between them),
# USBPcap records, this script only logs and starts SmartSoft; our own software is not involved. In each session: the bumps
# under the channel faders 2, 12 and 24 of bank 1-24 held about 1 s; FADERS 25-48, the bump under the fader labelled 48
# held about 1 s; FADERS 1-24.
# Never in E9e: the faders, REC MEM, EDIT MEM, COPY, REC SEQ, MAGIC, SNAPSHOT, RATE, the wheel, GO, STACK, NEXT, PAUSE,
# CLEAR, Patch / Independents / Settings edits, page clicks in SmartSoft, Open showfile, New show, Save As, any Erase.
# Nothing of the show is changed on purpose: the after-session SD save must equal the backup byte for byte.
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

# One pass of the channel bumps in both banks; $s names the SmartSoft session (1 or 2) in every tag.
function Run-Pass([int]$s) {
    $p = "s" + $s + " "
    Step "Open SmartSoft's CONSOLE SIMULATOR window. Touch nothing in it afterwards." ($p + "simulator opened")
    Wait-Idle 15 ($p + "simulator open, untouched")

    Write-Host ""
    Write-Host ("Pass {0}, part A: three SIMULATOR bumps of bank 1-24 (under faders 2, 12 and 24): press, HOLD about 1 s, release." -f $s) -ForegroundColor Green
    foreach ($n in 2, 12, 24) {
        Step ("A: SIMULATOR bump under fader {0}: press, HOLD about 1 s, release." -f $n) ($p + "a sim bump " + $n + " held 1 s")
        Wait-Idle 3 ($p + "after bump " + $n)
    }
    Wait-Idle 5 ($p + "after part A")

    Write-Host ""
    Write-Host ("Pass {0}, part B: bank 25-48, one bump. The Simulator labels its faders 25 to 48 there." -f $s) -ForegroundColor Green
    Step "B: SIMULATOR FADERS 25-48." ($p + "b sim faders 25-48")
    Wait-Idle 5 ($p + "b bank 25-48")
    Step "B: SIMULATOR bump under the fader labelled 48: press, HOLD about 1 s, release." ($p + "b sim bump labelled 48 held 1 s")
    Wait-Idle 3 ($p + "after bump labelled 48")
    Step "B: SIMULATOR FADERS 1-24." ($p + "b sim faders 1-24")
    Wait-Idle 10 ($p + "after part B")
}

$host.UI.RawUI.WindowTitle = "E9e guided session"
Write-Host "E9e guided session (a spot check, about one minute per pass): SmartSoft's Simulator presses four channel bumps per session, in two SmartSoft sessions, while USBPcap records; our software is not involved." -ForegroundColor Green
Write-Host "Follow the yellow instructions one by one. Press Enter exactly when you START each action. Leave the waits untouched." -ForegroundColor Green
Write-Host "Never: the faders, the record keys, RATE, the wheel, GO, STACK, NEXT, PAUSE, CLEAR, any SmartSoft edit, page clicks in SmartSoft, show load or erase." -ForegroundColor Green
Log "START guided E9e session (PC clock, local time)"

# --- Safety rule 3 ---
Write-Host ""
Write-Host "Safety rule 3: save the show to the SD card on the console NOW (Menu > Save/Load Show > Save), before anything else." -ForegroundColor Yellow
$backup = ""
while ([string]::IsNullOrWhiteSpace($backup)) {
    $backup = Read-Host "   Type the file name the console used for the backup (e.g. SHOW28.ASC); it is required"
}
Log ("backup taken: " + $backup.Trim() + " (copy it into smartfade-sd-card-backup/ after the session)")
Ask "Was the console switched OFF since E9d (2026-11-02, 10:14)? (y / n / unsure)" "console powered off since e9d" | Out-Null
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
$pcapName = 'e9e-' + (Get-Date -Format 'yyyyMMdd-HHmm') + '.pcap'
Write-Host ""
Write-Host "In an ADMIN PowerShell window start USBPcap on the console's root hub, into a NEW file:" -ForegroundColor Yellow
Write-Host ('   & "C:\Program Files\USBPcap\USBPcapCMD.exe" -d \\.\USBPcap1 -A --inject-descriptors -o "' + (Join-Path (Get-Location) $pcapName) + '"') -ForegroundColor White
Log ("usbpcap file name suggested to the owner: " + $pcapName)
Confirm-Done "When USBPcap is running." "usbpcap running (owner)"
Write-Host ""
Write-Host "Starting position on the console: all channel faders at ZERO, MASTER at FULL, BUMPS at FULL, CHANNEL mode (FADERS 1-24 lit)," -ForegroundColor Yellow
Write-Host "BLACKOUT off, SOLO off, IND 1 and IND 2 off, STACK off, NEXT off (unlit), no sequence running, page 1, nothing armed." -ForegroundColor Yellow
Confirm-Done "Set that up now." "starting position set (owner)"

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
    $after = Read-Host "   Type the file name the console used for this save (e.g. SHOW29.ASC); it is required"
}
Log ("after-session save: " + $after.Trim() + " (copy it into smartfade-sd-card-backup/ with the backup)")
Log "END guided E9e session"
Write-Host ""
Write-Host "Files to copy into docs/protocol/captures/ (with the date prefix ...-e9e-...):" -ForegroundColor Green
Write-Host ("   action log:   " + $LogFile) -ForegroundColor White
Write-Host "   USBPcap file: the .pcap you named when starting USBPcapCMD (split to the console before it is committed)" -ForegroundColor White
Write-Host ("   SD files:     " + $backup.Trim() + " (backup) and " + $after.Trim() + " (after) into smartfade-sd-card-backup/; they must be byte-identical") -ForegroundColor White
