# Guided operator session "E12" (planned 2026-10-31): SmartSoft Online edits the PATCH and the STACK /
# SEQUENCES of the console while USBPcap records (passive; our software is not involved). It is the one
# capture that must show every message of those edits at least once (docs/protocol/reverse-engineering.md,
# "E12 runbook"): the Patch, Independents, Page, Sequence, SequenceMain and StepContent writes and the step
# commands InsertStep / DeleteStep / MoveStep / id 42, all INFERRED from SmartSoft's binaries so far and never
# seen on the bus. Every test change is restored before the end, and the show is saved to the SD card at the
# end for a byte-for-byte comparison with the backup. Test objects: channel 47, DMX 100-102 (nothing may be
# connected there), IND 1, memory 9 of page 1 (empty before and after), the last cue of the stack, the KOTLIK
# sequence (page 1, fader 21), page 12's name.
# Never in E12: the Console Simulator's wheel, Open showfile, New show, Save As, any Erase, Default times,
# REC SEQ, Tap mode, PAGE clicks in SmartSoft. Never drag a CHANNEL onto the un-patch bin (that unpatches every
# address of the channel): only the DMX cells named below.
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
    return $a
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
function Optional-Step([string]$instruction, [string]$tag, [bool]$enabled) {
    if ($enabled) { Step $instruction $tag } else { Log ("skipped (optional): " + $tag) }
}

$host.UI.RawUI.WindowTitle = "E12 guided session"
Write-Host "E12 guided session: SmartSoft edits the patch and the stack / sequences while USBPcap records; our software is not involved." -ForegroundColor Green
Write-Host "Follow the yellow instructions one by one. Press Enter exactly when you START each action. Leave the waits untouched." -ForegroundColor Green
Write-Host "Never: the Simulator's wheel, Open showfile, New show, Save As, Erase, Default times, REC SEQ, Tap mode, PAGE clicks in SmartSoft." -ForegroundColor Green
Write-Host "Never drag a CHANNEL onto the un-patch bin; only the DMX cells this guide names." -ForegroundColor Green
Write-Host "If SmartSoft shows any message box (e.g. 'No space to insert a cue here'), type its text at the next answer prompt." -ForegroundColor Green
Log "START guided E12 session (PC clock, local time)"

# --- Before anything: the backup, the owner's decisions, the test objects ---
Write-Host ""
Write-Host "Power-cycle the console first if you have not today, then: CHANNEL mode (FADERS 1-24 lit), all channel faders at ZERO," -ForegroundColor Yellow
Write-Host "MASTER and BUMPS at FULL, STACK unlit, fader 21 (KOTLIK) at ZERO, page 1, BLACKOUT / SOLO / IND off." -ForegroundColor Yellow
Confirm-Done "When the console is in that state." "starting position set (owner)"
Ask "Was the console power-cycled today before this session? (y / n)" "power cycled"
Write-Host ""
Write-Host "Safety rule 3: save the show to the SD card on the console NOW (Menu > Save/Load Show > Save), to a FREE number." -ForegroundColor Yellow
$backup = ""
while ([string]::IsNullOrWhiteSpace($backup)) {
    $backup = Read-Host "   Type the file name the console used for the backup (e.g. SHOW12.ASC); it is required"
}
Log ("backup taken: " + $backup.Trim() + " (copy it into smartfade-sd-card-backup/ after the session)")
Ask "Is NOTHING connected to DMX addresses 100, 101 and 102 (the rig is DMX 5-16)? (y / n)" "dmx 100-102 free"
Ask "Is memory 9 of page 1 EMPTY (its key unlit in MEMS mode)? (y / n)" "memory 9 empty"
Ask "Did you do the offline dry run of SmartSoft (USB unplugged) to find the Patch, Stack/Seq Edit, Page and Mem Edit controls? (y / n)" "dry run done"
Ask "You accept that E12 changes show data on purpose (test cue, test sequence step, test labels, test patch addresses, memory 9) and restores them, with the SD backup as the fallback? (y / n)" "acceptance of test changes"
$optC3 = (Ask "Optional C3: IND 1 button mode and GM changed and changed back in the Patch tab? (y / n)" "opt c3") -eq 'y'
$optD2 = (Ask "Optional D2: a point cue inserted between the last two original cues, later moved (tests MoveStep) and deleted? (y / n)" "opt d2") -eq 'y'
$optD5 = (Ask "Optional D5: a 'Direct cue' step inserted at the end of the stack and deleted again? (y / n)" "opt d5") -eq 'y'
$optF3 = (Ask "Optional F3: page 12 named T and cleared again? (y / n)" "opt f3") -eq 'y'
$optH = (Ask "Optional H: the console's OWN menus (Patch by dimmer in Blind, Unpatch dimmer, Step Timing), which use the physical wheel? (y / n)" "opt h") -eq 'y'

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
Write-Host '   & "C:\Program Files\USBPcap\USBPcapCMD.exe" -d \\.\USBPcap1 -A --inject-descriptors -o e12.pcap' -ForegroundColor White
Confirm-Done "When USBPcap is running." "usbpcap running (owner)"

# --- A: SmartSoft Online, baseline, the tabs ---
Start-SmartSoft 1
Wait-Idle 60 "A1 baseline: SmartSoft online, nothing touched (download 1)"
Step "A2: open the PATCH tab. Touch nothing in it." "a2 patch tab opened"
Wait-Idle 30 "a2 patch tab open, untouched"
Step "A2: open the STACK/SEQ EDIT tab. Touch nothing." "a2 stack seq tab opened"
Wait-Idle 30 "a2 stack seq tab open, untouched"
Step "A2: open the PAGE tab. Touch nothing." "a2 page tab opened"
Wait-Idle 30 "a2 page tab open, untouched"
Step "A2: open the MEM EDIT tab. Touch nothing." "a2 mem edit tab opened"
Wait-Idle 30 "a2 mem edit tab open, untouched"
Ask "A3, looking only: is there any control for a patch LEVEL (a percentage per DMX address) anywhere in the Patch tab? (y / n / describe)" "a3 patch level control"
Ask "A3, looking only: in Stack/Seq Edit with the STACK selected, is there a run-mode popup (Loop / One Shot / Manual) for the stack? (y / n / describe)" "a3 stack run mode popup"
Ask "A3, looking only: does the Insert step dialog offer 'Direct cue' besides Channel and Memory? (y / n / describe)" "a3 direct cue option"

# --- B: the test memory, with the console's own keys ---
Step "B1: on the CONSOLE physical channel fader 5 to about HALF." "b1 fader 5 to half"
Confirm-Done "When it is at about half." "b1 fader 5 at half"
Wait-Idle 10 "b1 fader 5 at half"
Step "B1: on the CONSOLE press REC MEM, then the bump under memory 9 (records the look into memory 9 of page 1)." "b1 console rec mem bump 9"
Wait-Idle 15 "after b1 record"
Step "B1: on the CONSOLE physical fader 5 back to ZERO." "b1 fader 5 to zero"
Confirm-Done "When it is at ZERO." "b1 fader 5 at zero"
Wait-Idle 15 "after b1"
Ask "Is memory 9's key lit dimmed red in MEMS mode now? (y / n)" "memory 9 recorded"

# --- C: the patch ---
Step "C1: PATCH tab: drag CHANNEL 47 onto the DMX 100 cell (channel 47 gets the address 100)." "c1 patch channel 47 to dmx 100"
Confirm-Done "When the drop is done and the cell shows 47." "c1 patch done"
Wait-Idle 15 "after c1"
Ask "What does the DMX 100 cell show now, and did the console's LCD or any LED react? (describe)" "c1 result"
Step "C2: PATCH tab: drag IND 1 onto the DMX 101 cell." "c2 patch ind 1 to dmx 101"
Confirm-Done "When the drop is done." "c2 patch done"
Wait-Idle 15 "after c2"
Ask "What does the DMX 101 cell show now (i1, 49, something else)? (describe)" "c2 result"
Optional-Step "C3 (optional): PATCH tab, IND 1 panel: change the BUTTON MODE to the other value." "c3 ind 1 button mode changed" $optC3
if ($optC3) { Wait-Idle 15 "after c3 button mode" }
Optional-Step "C3 (optional): change the BUTTON MODE back." "c3 ind 1 button mode restored" $optC3
if ($optC3) { Wait-Idle 15 "after c3 button mode restored" }
Optional-Step "C3 (optional): change the GM (affected by grand master) setting of IND 1." "c3 ind 1 gm changed" $optC3
if ($optC3) { Wait-Idle 15 "after c3 gm" }
Optional-Step "C3 (optional): change the GM setting back." "c3 ind 1 gm restored" $optC3
if ($optC3) { Wait-Idle 15 "after c3 gm restored" }

# --- D: the stack ---
Step "D1: STACK/SEQ EDIT tab, the STACK: right-click the LAST cue > Insert step > Memory 01/09 (page 1, memory 9), confirm." "d1 insert step after last cue"
Confirm-Done "When the new cue is in the list." "d1 inserted"
Wait-Idle 15 "after d1"
$d1 = Ask "D1: which cue number did SmartSoft give the new step (type it exactly, e.g. 15.0)?" "d1 cue number"
Optional-Step "D2 (optional): right-click the cue BEFORE the last original cue > Insert step > Memory 01/09 > a POINT cue between them (e.g. 13.5), confirm." "d2 insert point cue" $optD2
$d2 = ""
if ($optD2) {
    Confirm-Done "When the point cue is in the list." "d2 inserted"
    Wait-Idle 15 "after d2"
    $d2 = Ask "D2: which cue number does the point cue have (type it exactly)?" "d2 cue number"
}
Step ("D3: on cue " + $d1 + ": set its UP time to 3.0 and confirm.") "d3 up time"
Wait-Idle 15 "after d3 up"
Step ("D3: on cue " + $d1 + ": set its DOWN time to 4.0 and confirm.") "d3 down time"
Wait-Idle 15 "after d3 down"
Step ("D3: on cue " + $d1 + ": set its WAIT (follow-on, the third time) to 2.0 and confirm.") "d3 wait time"
Wait-Idle 15 "after d3 wait"
Ask "D3: what does SmartSoft call the third time column (Wait / Follow / Delay / other)?" "d3 third time name"
Step ("D4: on cue " + $d1 + ": set its label (name) to  T  and confirm.") "d4 step label T"
Wait-Idle 15 "after d4"
Optional-Step "D5 (optional): right-click the LAST cue > Insert step > DIRECT CUE (the third option), confirm, then set its label to  T ." "d5 direct cue inserted and labelled" $optD5
if ($optD5) { Wait-Idle 15 "after d5" }
$d5 = ""
if ($optD5) { $d5 = Ask "D5: which cue number does the Direct cue have?" "d5 cue number" }

# --- E: the KOTLIK sequence (page 1, fader 21) ---
Step "E1: STACK/SEQ EDIT tab: choose the sequence KOTLIK in the title-bar drop-down; right-click its step 7 > Insert step > Memory 01/09, confirm." "e1 insert sequence step"
Confirm-Done "When the new step is in the list." "e1 inserted"
Wait-Idle 15 "after e1"
Ask "E1: which step number does the new sequence step show in SmartSoft? (type it exactly)" "e1 step number"
Step "E2: on that new sequence step: set its UP time to 2.0 and confirm." "e2 sequence step up time"
Wait-Idle 15 "after e2"
Step "E3: KOTLIK's run mode: change it from Loop to ONE SHOT (the popup)." "e3 run mode one shot"
Wait-Idle 15 "after e3"
Step "E4: rename the sequence KOTLIK to  KOTLIX  and confirm." "e4 sequence label kotlix"
Wait-Idle 15 "after e4"
Ask "E: looking at the CONSOLE's LCD now (hold nothing): what does it show? (describe, or '-')" "e console lcd"

# --- F: memory content and names ---
Step "F1: MEM EDIT tab: open memory 01/09 and set channel 5 to 100 % in the table cell, confirm." "f1 mem edit channel 5 to 100"
Wait-Idle 15 "after f1"
Step "F2: PAGE tab: set the NAME of memory 01/09 to  T  and confirm." "f2 memory 9 name T"
Wait-Idle 15 "after f2"
Optional-Step "F3 (optional): PAGE tab: set the NAME of page 12 to  T  and confirm." "f3 page 12 name T" $optF3
if ($optF3) { Wait-Idle 15 "after f3" }

# --- G: the console's stored bodies ---
Close-SmartSoft 1
Wait-Idle 10 "between sessions 1 and 2"
Start-SmartSoft 2
Wait-Idle 30 "G1 download 2: the console's own bodies of every test object"
Ask "G1: in SmartSoft now, does the stack show your test cues with the times, the label T, and does KOTLIX show One Shot and the new step? (describe)" "g1 after restart"

# --- H: optional, the console's own menus (physical wheel; Blind only) ---
Optional-Step "H (optional): on the CONSOLE: Menu > Patch > Patch by dimmer, BLIND: dimmer 102 to channel 47, confirm with MENU, leave the menu." "h console patch dmx 102 blind" $optH
if ($optH) { Wait-Idle 15 "after h patch" }
Optional-Step "H (optional): on the CONSOLE: Menu > Patch > Patch - special > Unpatch dimmer 102, confirm with MENU, leave the menu." "h console unpatch dmx 102" $optH
if ($optH) { Wait-Idle 15 "after h unpatch" }
Optional-Step ("H (optional): on the CONSOLE: Menu > Sequences > Stack (XF) > Step Timing > one step: cue " + $d1 + ", set its Up time to 5.0, confirm, leave the menu.") "h console step timing" $optH
if ($optH) { Wait-Idle 15 "after h step timing" }
if ($optH) { Ask "H: what did SmartSoft show after each console edit (did the values follow)? (describe)" "h smartsoft followed" }

# --- I: restores ---
Optional-Step "I1 (only if F3 was done): PAGE tab: clear page 12's name (empty) and confirm." "i1 page 12 name cleared" $optF3
if ($optF3) { Wait-Idle 15 "after i1 page name" }
Step "I1: rename the sequence KOTLIX back to  KOTLIK  and confirm." "i1 sequence label restored"
Wait-Idle 15 "after i1 label"
Step "I1: KOTLIK's run mode back to LOOP." "i1 run mode loop"
Wait-Idle 15 "after i1 run mode"
Step "I1: KOTLIK: right-click the step you inserted in E1 > Delete step > confirm 'Are you sure?'." "i1 delete sequence step"
Wait-Idle 15 "after i1 delete"
Optional-Step ("I2 (only if D2 was done): the STACK: drag the point cue " + $d2 + " to between the last original cue and cue " + $d1 + " (the 'Move sequence step' dialog); accept the cue number offered.") "i2 move point cue" $optD2
if ($optD2) {
    Wait-Idle 15 "after i2 move"
    Ask "I2: which cue number did the moved step get?" "i2 moved cue number" | Out-Null
    Step "I2: right-click the moved point cue > Delete step > confirm." "i2 delete point cue"
    Wait-Idle 15 "after i2 delete point cue"
}
Step ("I2: right-click cue " + $d1 + " > Delete step > confirm.") "i2 delete d1 cue"
Wait-Idle 15 "after i2 delete d1"
Optional-Step ("I2 (only if D5 was done): right-click the Direct cue " + $d5 + " > Delete step > confirm.") "i2 delete direct cue" $optD5
if ($optD5) { Wait-Idle 15 "after i2 delete direct cue" }
Step "I3: PAGE tab: clear the name of memory 01/09 (empty) and confirm." "i3 memory 9 name cleared"
Wait-Idle 15 "after i3 name"
Step "I3: on the CONSOLE, with every fader at ZERO: press REC MEM, then the bump under memory 9 (an empty look is recorded: memory 9 emptied)." "i3 console rec mem empty bump 9"
Wait-Idle 15 "after i3 empty"
Ask "Is memory 9 empty again (key unlit in MEMS mode)? (y / n)" "memory 9 empty again"
Step "I4: PATCH tab: drag the DMX 101 cell (NOT the IND) onto the un-patch bin." "i4 unpatch dmx 101"
Wait-Idle 15 "after i4 dmx 101"
Step "I4: PATCH tab: drag the DMX 100 cell (NOT channel 47) onto the un-patch bin." "i4 unpatch dmx 100"
Wait-Idle 15 "after i4 dmx 100"
Ask "I4: are DMX 100 and 101 empty again, and does channel 47 still have the addresses it had before? (describe)" "i4 patch restored"

# --- J: the check ---
Close-SmartSoft 2
Wait-Idle 10 "between sessions 2 and 3"
Start-SmartSoft 3
Wait-Idle 30 "J1 download 3: everything should equal download 1"
Ask "J1: does SmartSoft show the stack, KOTLIK, memory 9 (empty), the patch and the names as before the session? (y / n / what differs)" "j1 restored view"
Close-SmartSoft 3
Confirm-Done "Stop USBPcap (Ctrl+C in its window)." "usbpcap stopped (owner)"
Write-Host ""
Write-Host "J2: save the show to the SD card on the console NOW, to another FREE number (the after-session file)." -ForegroundColor Yellow
$after = ""
while ([string]::IsNullOrWhiteSpace($after)) {
    $after = Read-Host "   Type the file name the console used for this save (e.g. SHOW13.ASC); it is required"
}
Log ("after-session save: " + $after.Trim() + " (copy it into smartfade-sd-card-backup/ with the backup)")
Log "END guided E12 session"
Write-Host ""
Write-Host "Files to copy into docs/protocol/captures/ (with the date prefix ...-e12-...):" -ForegroundColor Green
Write-Host ("   action log:   " + $LogFile) -ForegroundColor White
Write-Host "   USBPcap file: the .pcap you named when starting USBPcapCMD (split to the console before it is committed)" -ForegroundColor White
Write-Host ("   SD files:     " + $backup.Trim() + " (backup) and " + $after.Trim() + " (after) into smartfade-sd-card-backup/; they must be byte-identical") -ForegroundColor White
Write-Host "If they differ: on the console Menu > Save/Load Show > Load the backup (or Load patch from card for the patch alone)." -ForegroundColor Yellow
