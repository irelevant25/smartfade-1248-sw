# Guided operator session "E9c" (written 2026-11-02, not yet run): the passive capture that SC-2's "Path to a
# wider clearance" names, scoped for the owner's decision of 2026-11-02 to drive memory faders and channels
# 25-48 from the PC through an automatic bank switch, with held bumps. SmartSoft's Console Simulator does the
# writing, USBPcap records, this script only logs and starts SmartSoft; our own software is not involved.
# What it captures, each from the Simulator unless stated: every channel fader of bank 1-24 (faders 2-24, once),
# the bank 25-48 (the FADERS 25-48 key a second time, three faders and a bump there), memory mode (faders as memory
# faders on page 1 and on page 2, held memory bumps), the matching rule after a mode or page switch with a PC level
# in force (H79 / H81, the risk of an automatic switch), held bumps with their durations, the live keys seen once
# or never (IND 1, IND 2, STACK without GO, PAUSE and NEXT if the Simulator has them, CLEAR), a channel fader with
# the MASTER at half (F96), and a SmartSoft restart (the System pair and the download once more, for SC-4).
# Never in E9c: REC MEM, EDIT MEM, COPY, REC SEQ, MAGIC, SNAPSHOT, RATE (wheel-coupled), the wheel, GO with STACK
# lit, Patch / Independents / Settings edits, page clicks in SmartSoft, Open showfile, New show, Save As, any Erase.
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
function Is-Yes([string]$a) { return ($a -eq 'y' -or $a -eq 'yes') }

$host.UI.RawUI.WindowTitle = "E9c guided session"
Write-Host "E9c guided session: SmartSoft's Simulator drives the console's live controls in every bank while USBPcap records; our software is not involved." -ForegroundColor Green
Write-Host "Follow the yellow instructions one by one. Press Enter exactly when you START each action. Leave the waits untouched." -ForegroundColor Green
Write-Host "Never: REC MEM, EDIT MEM, COPY, REC SEQ, MAGIC, SNAPSHOT, RATE, the wheel, GO with STACK lit, Patch / Independents / Settings edits, page clicks in SmartSoft, Open showfile, New show, Save As, Erase." -ForegroundColor Green
Log "START guided E9c session (PC clock, local time)"

# --- Safety rule 3, the open questions, the Simulator's keys ---
Write-Host ""
Write-Host "Safety rule 3: save the show to the SD card on the console NOW (Menu > Save/Load Show > Save), before anything else." -ForegroundColor Yellow
$backup = ""
while ([string]::IsNullOrWhiteSpace($backup)) {
    $backup = Read-Host "   Type the file name the console used for the backup (e.g. SHOW24.ASC); it is required"
}
Log ("backup taken: " + $backup.Trim() + " (copy it into smartfade-sd-card-backup/ after the session)")
Ask "E11 question: was the console power-cycled between E11 attempt 2 (2026-11-01, 15:56) and attempt 3 (22:46)? (y / n / unsure)" "e11 power cycle between attempts 2 and 3" | Out-Null
Ask "E11 question: in the third backend session of 2026-11-01 (23:07-23:10), besides the refused channel fader in memory mode, the memory bump clicks, the fader 1 drags and the bump 7 clicks, did you try anything else? (describe, or '-')" "e11 session 3 other actions" | Out-Null
Ask "Was the console switched OFF since E11 attempt 3? (y / n / unsure)" "console powered off since e11" | Out-Null
$hasPause = Is-Yes (Ask "Does SmartSoft's CONSOLE SIMULATOR have a PAUSE key (the crossfader section)? (y / n)" "simulator has pause")
$hasNext = Is-Yes (Ask "Does the SIMULATOR have a NEXT key? (y / n)" "simulator has next")
Ask "Does the SIMULATOR have a RATE key? (y / n; it is NOT pressed in E9c, it is wheel-coupled)" "simulator has rate" | Out-Null

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
$pcapName = 'e9c-' + (Get-Date -Format 'yyyyMMdd-HHmm') + '.pcap'
Write-Host ""
Write-Host "In an ADMIN PowerShell window start USBPcap on the console's root hub, into a NEW file:" -ForegroundColor Yellow
Write-Host ('   & "C:\Program Files\USBPcap\USBPcapCMD.exe" -d \\.\USBPcap1 -A --inject-descriptors -o "' + (Join-Path (Get-Location) $pcapName) + '"') -ForegroundColor White
Log ("usbpcap file name suggested to the owner: " + $pcapName)
Confirm-Done "When USBPcap is running." "usbpcap running (owner)"
Write-Host ""
Write-Host "Starting position on the console: all channel faders at ZERO, MASTER at FULL, BUMPS at FULL, CHANNEL mode (FADERS 1-24 lit)," -ForegroundColor Yellow
Write-Host "BLACKOUT off, SOLO off, IND 1 and IND 2 off, STACK off, NEXT off (unlit), no sequence running, page 1, nothing armed." -ForegroundColor Yellow
Confirm-Done "Set that up now." "starting position set (owner)"
Ask "Is memory 1 of page 1 recorded (channels 7 and 8, as in E9)? (y / n)" "memory 1 recorded" | Out-Null
Ask "Is memory 2 of page 1 recorded? (y / n)" "memory 2 recorded" | Out-Null
Ask "Is memory 9 of page 1 EMPTY? (y / n)" "memory 9 empty" | Out-Null
Ask "Is memory 1 of page 2 recorded? (y / n / unsure)" "page 2 memory 1 recorded" | Out-Null

# --- S0: SmartSoft Online, baseline, Simulator ---
Start-SmartSoft 1
Wait-Idle 60 "baseline: SmartSoft online, nothing touched (the System pair and the download, for SC-4)"
Step "Open SmartSoft's CONSOLE SIMULATOR window. Touch nothing in it afterwards." "simulator opened"
Wait-Idle 20 "simulator open, untouched"

# --- S1: every channel fader of bank 1-24 (faders 2-24; fader 1 is VERIFIED, 5 and 7 OBSERVED once) ---
Write-Host ""
Write-Host "S1: each SIMULATOR channel fader 2 to 24 once: drag it up to about HALF, hold it 2 s, then back to ZERO. One fader at a time." -ForegroundColor Green
foreach ($n in 2..24) {
    Step ("S1: SIMULATOR channel fader {0}: up to about HALF, hold 2 s, back to ZERO." -f $n) ("s1 sim fader " + $n + " half and back")
    Wait-Idle 3 ("after sim fader " + $n)
}
Wait-Idle 10 "after S1"

# --- S2: bank 25-48 (the FADERS 25-48 key a second time; faders and a bump there) ---
Step "S2: SIMULATOR FADERS 25-48." "s2 sim faders 25-48"
Wait-Idle 10 "s2 bank 25-48"
Step "S2: SIMULATOR fader 1 (now the first fader of bank 25-48): up to about HALF, hold 2 s, back to ZERO." "s2 sim fader 1 in bank 25-48"
Wait-Idle 5 "after s2 fader 1"
Step "S2: SIMULATOR fader 2: up to about HALF, hold 2 s, back to ZERO." "s2 sim fader 2 in bank 25-48"
Wait-Idle 5 "after s2 fader 2"
Step "S2: SIMULATOR fader 24: up to about HALF, hold 2 s, back to ZERO." "s2 sim fader 24 in bank 25-48"
Wait-Idle 5 "after s2 fader 24"
Step "S2: SIMULATOR bump under fader 1: press, HOLD 2 s, release." "s2 sim bump 1 in bank 25-48 held"
Wait-Idle 5 "after s2 bump"
Ask "S2: which channel numbers did the console's LCD show for the faders you moved in bank 25-48? (e.g. 25, 26, 48 / unsure)" "s2 lcd channel numbers" | Out-Null
Step "S2: SIMULATOR FADERS 1-24." "s2 sim faders 1-24"
Wait-Idle 10 "after S2"

# --- S3: memory mode (faders as memory faders on page 1 and page 2, held memory bumps) ---
Step "S3: SIMULATOR FADERS MEMS." "s3 sim faders mems"
Wait-Idle 10 "s3 memory mode"
Step "S3: SIMULATOR fader 1 (memory 1 of page 1): up to about HALF, hold 2 s, back to ZERO." "s3 sim memory fader 1 half and back"
Wait-Idle 5 "after s3 memory fader 1"
Step "S3: SIMULATOR fader 2 (memory 2): up to about HALF, hold 2 s, back to ZERO." "s3 sim memory fader 2 half and back"
Wait-Idle 5 "after s3 memory fader 2"
Step "S3: SIMULATOR fader 9 (memory 9, empty): up to about HALF, hold 2 s, back to ZERO." "s3 sim memory fader 9 half and back"
Wait-Idle 5 "after s3 memory fader 9"
Step "S3: SIMULATOR bump under memory 1: press, HOLD 3 s, release." "s3 sim memory bump 1 held 3 s"
Wait-Idle 5 "after s3 memory bump 1"
Step "S3: SIMULATOR bump under memory 9 (empty): one short click." "s3 sim memory bump 9 tap"
Wait-Idle 5 "after s3 memory bump 9"
Ask "S3: what did the console's LCD and the bump LEDs show while memory fader 1 was up? (describe)" "s3 lcd and leds" | Out-Null
Step "S3: on the CONSOLE change to page 2 (hold MEMS, press bump 2, release both)." "s3 console page 2"
Wait-Idle 10 "s3 page 2"
Step "S3: SIMULATOR fader 1 (memory 1 of page 2): up to about HALF, hold 2 s, back to ZERO." "s3 sim memory fader 1 page 2 half and back"
Wait-Idle 5 "after s3 page 2 fader 1"
Step "S3: on the CONSOLE back to page 1 (hold MEMS, press bump 1, release both)." "s3 console page 1"
Wait-Idle 10 "s3 page 1 again"
Step "S3: SIMULATOR FADERS 1-24." "s3 sim faders 1-24"
Wait-Idle 10 "after S3"

# --- S4: the matching rule with a PC level in force (H79 / H81): the risk of an automatic bank switch ---
Step "S4a: SIMULATOR channel fader 1 to about HALF and LEAVE it there (the physical fader 1 stays at zero)." "s4a sim fader 1 half, left"
Wait-Idle 10 "s4a channel 1 at half from the PC"
Step "S4a: SIMULATOR FADERS MEMS (a mode switch with the PC's channel 1 level in force)." "s4a sim faders mems with pc level"
Wait-Idle 10 "s4a memory mode with pc level"
Ask "S4a: memory fader 1 now: its bump LED (steady / blinking / off) and did memory 1's light (channels 7, 8) come up? (describe)" "s4a memory fader 1 after switch" | Out-Null
Step "S4a: SIMULATOR fader 1 (now memory 1): drag it to FULL, hold 2 s, then to ZERO." "s4a sim memory fader 1 full and zero"
Wait-Idle 10 "after s4a memory fader 1"
Step "S4a: SIMULATOR FADERS 1-24 (back to channels with the Simulator's fader at zero)." "s4a sim faders 1-24 back"
Wait-Idle 10 "s4a channel mode again"
Ask "S4a: channel 1 now: its level on the LCD / bump LED (half as before the switch, zero, or else)? (describe)" "s4a channel 1 after round trip" | Out-Null
Step "S4a: SIMULATOR channel fader 1: drag it to FULL and back to ZERO (the cleanup; note where the light reacted)." "s4a sim fader 1 full and zero cleanup"
Wait-Idle 10 "after s4a"
Step "S4b: on the CONSOLE physical channel fader 2 to about HALF and leave it." "s4b physical fader 2 half"
Wait-Idle 10 "s4b physical 2 at half"
Step "S4b: SIMULATOR FADERS MEMS." "s4b sim faders mems with physical fader up"
Wait-Idle 10 "s4b memory mode"
Ask "S4b: bump LED 2 now (steady / blinking / off)? (the fader is up but memory 2's level should be zero, H79)" "s4b led 2 after switch" | Out-NULL
Step "S4b: SIMULATOR fader 2 (memory 2): from ZERO up to about HALF, hold 2 s, back to ZERO." "s4b sim memory fader 2 half and back"
Wait-Idle 5 "after s4b memory fader 2"
Ask "S4b: did the Simulator's fader 2 take effect at once or only past the physical fader's position? (at once / past / unsure)" "s4b pickup" | Out-Null
Step "S4b: SIMULATOR FADERS 1-24." "s4b sim faders 1-24 back"
Wait-Idle 10 "s4b channel mode again"
Ask "S4b: channel 2 now: level and bump LED? (describe)" "s4b channel 2 after round trip" | Out-Null
Step "S4b: on the CONSOLE physical fader 2 to ZERO." "s4b physical fader 2 zero"
Wait-Idle 10 "after s4b"
Step "S4c: SIMULATOR FADERS MEMS, then SIMULATOR fader 3 (memory 3 of page 1) to about HALF and LEAVE it." "s4c sim mems and memory fader 3 half"
Wait-Idle 10 "s4c memory 3 at half from the PC"
Step "S4c: on the CONSOLE change to page 2 (hold MEMS, bump 2): a page change with the PC's level in force." "s4c console page 2 with pc level"
Wait-Idle 10 "s4c page 2"
Ask "S4c: fader 3's bump LED and memory 3 of page 2 (any light)? (describe)" "s4c after page change" | Out-Null
Step "S4c: SIMULATOR fader 3 to ZERO; then on the CONSOLE back to page 1 (hold MEMS, bump 1); then SIMULATOR FADERS 1-24." "s4c cleanup"
Confirm-Done "When the console is on page 1 in channel mode and the Simulator's fader 3 is at zero." "s4c cleanup done"
Wait-Idle 10 "after S4"

# --- S5: held bumps, with their durations (for the SC-2 amendment the owner asked for) ---
Step "S5: SIMULATOR bump 7: press, HOLD 3 s, release." "s5 sim bump 7 held 3 s"
Wait-Idle 5 "after s5 bump 7"
Step "S5: SIMULATOR bump 10: press, HOLD 2 s, release." "s5 sim bump 10 held 2 s"
Wait-Idle 5 "after s5 bump 10"
Step "S5: SIMULATOR SOLO on; then bump 7: press, HOLD 2 s, release; then SOLO off." "s5 solo on, bump 7 held, solo off"
Wait-Idle 5 "after s5 solo bump"
Step "S5: SIMULATOR FADERS MEMS; bump under memory 1: press, HOLD 3 s, release; then FADERS 1-24." "s5 mems, memory bump 1 held 3 s, 1-24"
Wait-Idle 10 "after S5"

# --- S6: the live keys seen once or never (no GO with STACK lit; CLEAR last) ---
Step "S6: SIMULATOR IND 1: click it ON, wait 5 s, click it OFF." "s6 sim ind 1 on off"
Wait-Idle 5 "after s6 ind 1"
Step "S6: SIMULATOR IND 2: click it ON, wait 5 s, click it OFF (IND 2 drives DMX 47 in your patch)." "s6 sim ind 2 on off"
Wait-Idle 5 "after s6 ind 2"
Step "S6: SIMULATOR STACK: click it ON, wait 5 s, click it OFF. Do NOT press GO." "s6 sim stack on off"
Wait-Idle 5 "after s6 stack"
if ($hasPause) {
    Step "S6: SIMULATOR PAUSE: click it once, wait 3 s, click it again (no fade is running)." "s6 sim pause twice"
    Wait-Idle 5 "after s6 pause"
}
if ($hasNext) {
    Step "S6: SIMULATOR NEXT: click it ON." "s6 sim next on"
    Wait-Idle 5 "s6 next on"
    Ask "S6: NEXT on: which key lit on the console and what does the LCD say? (describe)" "s6 next on console" | Out-Null
    Step "S6: with NEXT on, SIMULATOR channel fader 4: up to about HALF, hold 2 s, back to ZERO (levels go blind into the Next fader)." "s6 sim fader 4 with next on"
    Wait-Idle 5 "after s6 fader 4"
    Ask "S6: did any light change while fader 4 was up with NEXT on? (y / n / describe)" "s6 light with next on" | Out-Null
    Step "S6: SIMULATOR NEXT: click it OFF." "s6 sim next off"
    Wait-Idle 5 "after s6 next"
}
Step "S6: SIMULATOR CLEAR: one press; wait 5 s; press twice quickly; wait 5 s; press three times quickly (levels only, nothing is deleted, F109)." "s6 sim clear 1, 2, 3"
Confirm-Done "When the three CLEAR series are done." "s6 clear done"
Wait-Idle 10 "after S6"

# --- S7: a channel fader with the MASTER at half (F96: post-MASTER levels in the objects) ---
Step "S7: SIMULATOR MASTER: drag it slowly to about HALF." "s7 sim master to half"
Wait-Idle 5 "s7 master at half"
Step "S7: SIMULATOR channel fader 1: to FULL, hold 3 s, back to ZERO." "s7 sim fader 1 full and zero with master half"
Wait-Idle 5 "after s7 fader 1"
Step "S7: SIMULATOR MASTER: back to FULL." "s7 sim master to full"
Wait-Idle 10 "after S7"

# --- S8: a SmartSoft restart (the System pair and the download once more; the counter restarts) ---
Close-SmartSoft 1
Wait-Idle 10 "between sessions 1 and 2"
Start-SmartSoft 2
Wait-Idle 30 "session 2 download"
Step "S8: open the CONSOLE SIMULATOR again, then SIMULATOR channel fader 1: up to about HALF, hold 2 s, back to ZERO." "s8 simulator again, sim fader 1 half and back"
Wait-Idle 10 "after S8"

# --- End ---
Wait-Idle 30 "end of actions"
Close-SmartSoft 2
Confirm-Done "Stop USBPcap (Ctrl+C in its window)." "usbpcap stopped (owner)"
Ask "Physical fader positions at the end (channel faders, MASTER, BUMPS) and the LEDs of SOLO, BLACKOUT, IND 1 / 2, STACK, NEXT? (describe)" "end positions" | Out-Null
Write-Host ""
Write-Host "Save the show to the SD card on the console NOW, to another FREE number (the after-session file; it must equal the backup byte for byte: no record function was used)." -ForegroundColor Yellow
$after = ""
while ([string]::IsNullOrWhiteSpace($after)) {
    $after = Read-Host "   Type the file name the console used for this save (e.g. SHOW25.ASC); it is required"
}
Log ("after-session save: " + $after.Trim() + " (copy it into smartfade-sd-card-backup/ with the backup)")
Log "END guided E9c session"
Write-Host ""
Write-Host "Files to copy into docs/protocol/captures/ (with the date prefix ...-e9c-...):" -ForegroundColor Green
Write-Host ("   action log:   " + $LogFile) -ForegroundColor White
Write-Host "   USBPcap file: the .pcap you named when starting USBPcapCMD (split to the console before it is committed)" -ForegroundColor White
Write-Host ("   SD files:     " + $backup.Trim() + " (backup) and " + $after.Trim() + " (after) into smartfade-sd-card-backup/; they must be byte-identical") -ForegroundColor White
Write-Host "Afterwards put WinUSB back on interface 1 (Zadig) only when our software's next live session is due." -ForegroundColor Green
