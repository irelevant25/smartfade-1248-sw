# Guided operator session "E12b" (planned 2026-10-31, evening; trimmed the same evening at the owner's request to what is still
# needed, nothing of E12 is repeated for its own sake): a second, independent SmartSoft Online session after a console power
# cycle, recorded passively by USBPcap (our software is not involved), that (1) shows a second time the writes E12 saw once
# (a Patch drag, the Independents panel, InsertStep / StepContent / Sequence / Page, MoveStep, DeleteStep: H91-H99, H101,
# F151-F159 become VERIFIED as behaviour only with this), and (2) adds what E12 did not show: a CHANNEL step, the MANUAL run mode,
# inserts at two places of KOTLIK (H102, H103), a Direct cue WITH a level and a label holding a c-with-caron and a full third line
# (H40), the IND values named by the owner (H44, H93), an SD save of the EDITED show (the file forms), and, optional, the console's
# own Insert / Delete / Step Timing menus (H106). Memory 9 is not used (its writes are VERIFIED): the test steps reference memory
# 01/01, which exists and is not changed by a step that points at it. Every test change is restored before the end and the final
# SD save must equal the backup byte for byte. Test objects: channel 47 and DMX 100 (nothing may be connected there), IND 1, the
# end of the stack, the KOTLIK sequence (page 1, fader 21), page 12's name.
# Lessons of E12 built in: a "look" in SmartSoft's editors is a write, so nothing is opened or clicked beyond the step (never the IND
# panel, PLAY or PAUSE outside their steps); a memory step has no label in SmartSoft; a Direct cue's label is set after the insert;
# the row that is right-clicked and the landing place are logged ("Insert step" put E12's new steps before the clicked row in a
# sequence and after it at the end of the stack).
# Never in E12b: the Console Simulator, Open showfile, New show, Save As, any Erase, Default times, REC SEQ, Tap mode, PAGE clicks
# in SmartSoft, PLAY / PAUSE in the stack view. Never drag a CHANNEL onto the un-patch bin: only the DMX 100 cell.
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
function First-Token([string]$answer) {
    $t = ($answer.Trim() -split '[ ,;]+')
    if ($t.Count -gt 0) { return $t[0] }
    return $answer.Trim()
}
function Wait-Idle([int]$seconds, [string]$why) {
    Write-Host ("   Now do not touch anything for {0} seconds ({1}) ..." -f $seconds, $why) -ForegroundColor Cyan
    Start-Sleep -Seconds $seconds
    Log ("wait {0} s done ({1})" -f $seconds, $why)
    Write-Host "   OK." -ForegroundColor Cyan
}
function Section-End([string]$name) {
    Ask ("End of " + $name + ": did you click, open or change ANYTHING in SmartSoft or on the console beyond the steps above? (n / describe exactly)") ($name + " extra actions") | Out-Null
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
function Save-Show([string]$what, [string]$tag) {
    Write-Host ""
    Write-Host ("Save the show to the SD card on the console NOW (Menu > Save/Load Show > Save), to a FREE number: " + $what) -ForegroundColor Yellow
    $name = ""
    while ([string]::IsNullOrWhiteSpace($name)) {
        $name = Read-Host "   Type the file name the console used (e.g. SHOW14.ASC); it is required"
    }
    Log ($tag + ": " + $name.Trim() + " (copy it into smartfade-sd-card-backup/ after the session)")
    return $name.Trim()
}

$host.UI.RawUI.WindowTitle = "E12b guided session"
Write-Host "E12b guided session (trimmed): SmartSoft edits the patch and the stack / sequences while USBPcap records; our software is not involved. About 30 minutes." -ForegroundColor Green
Write-Host "Follow the yellow instructions one by one. Press Enter exactly when you START each action. Leave the waits untouched." -ForegroundColor Green
Write-Host "A look in SmartSoft's editors is a write: open, click and change NOTHING beyond the step named. Never the IND panel, PLAY or PAUSE outside their steps." -ForegroundColor Green
Write-Host "Never: the Simulator, Open showfile, New show, Save As, Erase, Default times, REC SEQ, Tap mode, PAGE clicks in SmartSoft." -ForegroundColor Green
Write-Host "Never drag a CHANNEL onto the un-patch bin; only the DMX 100 cell at the end." -ForegroundColor Green
Write-Host "If SmartSoft shows any message box, type its text at the next answer prompt." -ForegroundColor Green
Log "START guided E12b session (PC clock, local time)"

# --- Before anything: the power cycle, the backup, the owner's decisions ---
Write-Host ""
Write-Host "Power-cycle the console first (E12b must be a session of its own), then: CHANNEL mode (FADERS 1-24 lit), all channel faders at ZERO," -ForegroundColor Yellow
Write-Host "MASTER and BUMPS at FULL, STACK unlit, fader 21 (KOTLIK) at ZERO, page 1, BLACKOUT / SOLO / IND off." -ForegroundColor Yellow
Confirm-Done "When the console is in that state." "starting position set (owner)"
Ask "Was the console power-cycled just now, after E12? (y / n)" "power cycled" | Out-Null
$backup = Save-Show "the BACKUP before the session (safety rule 3)" "backup taken"
Ask "Is NOTHING connected to DMX address 100 (the rig is DMX 5-16)? (y / n)" "dmx 100 free" | Out-Null
Ask "You accept that E12b changes show data on purpose (test cues incl. a Direct cue and a channel step, KOTLIK steps and run mode, IND 1's settings, channel 47's patch, page 12's name), saves the EDITED show to the SD card under its own number, and restores everything, with the SD backup as the fallback? (y / n)" "acceptance of test changes" | Out-Null
$optH = (Ask "Optional H: the console's OWN menus (Step Timing on the Direct cue, Insert step and Delete step in the stack and in KOTLIK), which use the physical wheel; they show what a read-only session of ours would see (H106)? (y / n)" "opt h") -eq 'y'

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
Write-Host "In an ADMIN PowerShell window start USBPcap on the console's root hub, into a NEW file (an absolute path; an admin window starts in C:\Windows\System32):" -ForegroundColor Yellow
Write-Host ('   & "C:\Program Files\USBPcap\USBPcapCMD.exe" -d \\.\USBPcap1 -A --inject-descriptors -o "' + (Join-Path (Get-Location) 'e12b.pcap') + '"') -ForegroundColor White
Confirm-Done "When USBPcap is running." "usbpcap running (owner)"

# --- A: SmartSoft Online, the baseline download ---
Start-SmartSoft 1
Wait-Idle 60 "A1 baseline: SmartSoft online, nothing touched (download 1)"

# --- C: the patch and the IND values ---
Step "C1: PATCH tab: drag CHANNEL 47 onto the DMX 100 cell (channel 47 gets the address 100)." "c1 patch channel 47 to dmx 100"
Confirm-Done "When the drop is done and the cell shows 47." "c1 patch done"
Wait-Idle 15 "after c1"
Step "C3: PATCH tab: open the IND 1 panel. Change nothing yet." "c3 ind 1 panel opened"
Wait-Idle 15 "after c3 panel opened"
Ask "C3: type EXACTLY what the panel shows for IND 1's BUTTON MODE and for its GM (grand master) setting now, e.g. 'button: Toggle, GM: Yes'." "c3 ind 1 values before" | Out-Null
Step "C3: change the BUTTON MODE of IND 1 to the other value, with ONE click or choice." "c3 ind 1 button mode changed"
Wait-Idle 15 "after c3 button mode"
Ask "C3: what does the panel show for the button mode now, and how many clicks did it take?" "c3 button mode after" | Out-Null
Step "C3: change the BUTTON MODE back, with one click or choice." "c3 ind 1 button mode restored"
Wait-Idle 15 "after c3 button mode restored"
Step "C3: change the GM setting of IND 1 to the other value." "c3 ind 1 gm changed"
Wait-Idle 15 "after c3 gm"
Ask "C3: what does the panel show for GM now?" "c3 gm after" | Out-Null
Step "C3: change the GM setting back, then close the IND 1 panel." "c3 ind 1 gm restored and panel closed"
Wait-Idle 15 "after c3 gm restored"
Section-End "C"

# --- D: the stack ---
Step "D5: on the CONSOLE physical channel fader 5 to about HALF (so that the Direct cue has a level in it)." "d5 fader 5 to half"
Confirm-Done "When it is at about half." "d5 fader 5 at half"
Wait-Idle 10 "d5 fader 5 at half"
Step "D5: STACK/SEQ EDIT tab, the STACK: right-click the LAST cue (the row of the highest cue number) > Insert step > DIRECT CUE, confirm." "d5 direct cue inserted"
Confirm-Done "When the Direct cue is in the list." "d5 inserted"
Wait-Idle 15 "after d5 insert"
$d5 = First-Token (Ask "D5: which cue number does the Direct cue have (type it first, e.g. 15.0), which row had you right-clicked, and does its row show a level or content?" "d5 cue number")
Step "D5: on the CONSOLE physical fader 5 back to ZERO." "d5 fader 5 to zero"
Confirm-Done "When it is at ZERO." "d5 fader 5 at zero"
Wait-Idle 15 "after d5 fader zero"
Step ("D5: set the LABEL of the Direct cue " + $d5 + " to exactly  čAJ123456789012345  (18 characters, the first a c with a caron) and confirm.") "d5 direct cue label"
Wait-Idle 15 "after d5 label"
Ask "D5: what does SmartSoft show as the label now (type it exactly; did it cut it, refuse the č, or split it into lines)?" "d5 label shown" | Out-Null
Step "D6: the STACK: right-click the LAST cue > Insert step > CHANNEL > channel 47 (if the dialog asks for a level, type 50), confirm." "d6 channel step inserted"
Confirm-Done "When the channel step is in the list." "d6 inserted"
Wait-Idle 15 "after d6"
$d6 = First-Token (Ask "D6: which cue number does the channel step have (type it first), and what did the dialog offer for a channel step (a level field? a dropdown?)?" "d6 cue number")
Step ("D6: on the channel step " + $d6 + ": set its UP time to 3.0 and confirm.") "d6 channel step up time"
Wait-Idle 15 "after d6 up time"
Step "D2: the STACK: right-click the cue BEFORE the last ORIGINAL cue (in E12 that was 13.0) > Insert step > Memory 01/01 (page 1, memory 1); if the dialog offers a cue number, take a POINT cue (e.g. 12.1 or 13.1), confirm." "d2 insert point cue"
Confirm-Done "When the point cue is in the list." "d2 inserted"
Wait-Idle 15 "after d2"
$d2 = First-Token (Ask "D2: which cue number does the point cue have (type it first), which row had you right-clicked, and was the dialog's cue-number field enabled?" "d2 cue number")
Section-End "D"

# --- E: the KOTLIK sequence (page 1, fader 21) ---
Step "E1: STACK/SEQ EDIT tab: choose the sequence KOTLIK in the title-bar drop-down; right-click its step 3 > Insert step > Memory 01/01, confirm." "e1 insert sequence step at 3"
Confirm-Done "When the new step is in the list." "e1 inserted"
Wait-Idle 15 "after e1"
$e1 = First-Token (Ask "E1: which step number does the new step show (type it first), and where did it land (before or after the old step 3)?" "e1 step number")
Step "E1b: KOTLIK: right-click its LAST step > Insert step > Memory 01/01, confirm." "e1b insert sequence step at end"
Confirm-Done "When the second new step is in the list." "e1b inserted"
Wait-Idle 15 "after e1b"
$e1b = First-Token (Ask "E1b: which step number does this new step show (type it first), and where did it land (before or after the old last step)?" "e1b step number")
Step "E3: KOTLIK's run mode: the bare word 'Loop' in the header of KOTLIK's step list (no 'Run mode' label); click it and choose ONE SHOT." "e3 run mode one shot"
Wait-Idle 15 "after e3"
Step "E3b: the same word again (now 'One Shot'): click it and choose MANUAL." "e3b run mode manual"
Wait-Idle 15 "after e3b"
Section-End "E"

# --- F: a page name ---
Step "F3: PAGE tab: set the NAME of PAGE 12 (the LAST page, not page 1) to  T  and confirm." "f3 page 12 name T"
Wait-Idle 15 "after f3"
Ask "F3: which page did you rename (its number as SmartSoft shows it)?" "f3 page renamed" | Out-Null

# --- S: the edited show on the SD card (the file forms of the edited objects) ---
$edited = Save-Show "the EDITED show (Manual, the new KOTLIK steps, the point cue, the Direct cue with its label and level, the channel step, page 12's name)" "edited-state save"
Wait-Idle 15 "after the edited-state save"

# --- G: the console's stored bodies ---
Close-SmartSoft 1
Wait-Idle 10 "between sessions 1 and 2"
Start-SmartSoft 2
Wait-Idle 30 "G1 download 2: the console's own bodies of every test object"
Ask "G1: in SmartSoft now: do the stack's test cues show their times, the Direct cue its label, the channel step its channel, and does KOTLIK show Manual and its two new steps? (describe)" "g1 after restart" | Out-Null

# --- H: optional, the console's own menus (physical wheel) ---
Optional-Step ("H3 (optional): on the CONSOLE: Menu > Sequences > Stack (XF) > Step Timing > one step: the DIRECT CUE " + $d5 + ", set its Up time to 5.0, confirm, leave the menu.") "h3 console step timing on the direct cue" $optH
if ($optH) { Wait-Idle 15 "after h3 step timing" }
Optional-Step "H4 (optional): on the CONSOLE: Menu > Sequences > Stack (XF) > Modify Steps > the LAST step > Insert (a memory step: memory 1 of page 1 if it asks), confirm, leave the menu." "h4 console insert stack step" $optH
if ($optH) { Wait-Idle 15 "after h4 insert" }
if ($optH) { Ask "H4: what did the console ask for and which cue number did the new step get? (describe)" "h4 console insert result" | Out-Null }
Optional-Step "H5 (optional): on the CONSOLE: Menu > Sequences > Stack (XF) > Modify Steps > that new step > Delete > confirm, leave the menu." "h5 console delete stack step" $optH
if ($optH) { Wait-Idle 15 "after h5 delete" }
Optional-Step "H6 (optional): on the CONSOLE: Menu > Sequences > Sequence (1-24) > sequence 21 (KOTLIK) > Modify Steps > the LAST step > Insert (memory 1 of page 1), confirm, leave the menu." "h6 console insert sequence step" $optH
if ($optH) { Wait-Idle 15 "after h6 insert" }
Optional-Step "H7 (optional): on the CONSOLE: the same menu > that new step > Delete > confirm, leave the menu." "h7 console delete sequence step" $optH
if ($optH) { Wait-Idle 15 "after h7 delete" }
if ($optH) { Ask "H: what did SmartSoft show after each console edit (did the values and the steps follow)? (describe)" "h smartsoft followed" | Out-Null }

# --- I: restores (by cue and step numbers; delete the highest first) ---
Step "I1: PAGE tab: clear page 12's name (empty) and confirm." "i1 page 12 name cleared"
Wait-Idle 15 "after i1 page name"
Step "I1: KOTLIK's run mode back to LOOP (the word in the header, now 'Manual'; click it, choose Loop)." "i1 run mode loop"
Wait-Idle 15 "after i1 run mode"
Step ("I1: KOTLIK: right-click the step " + $e1b + " (inserted in E1b) > Delete step > confirm 'Are you sure?'.") "i1 delete sequence step e1b"
Wait-Idle 15 "after i1 delete e1b"
Step ("I1: KOTLIK: right-click the step " + $e1 + " (inserted in E1) > Delete step > confirm.") "i1 delete sequence step e1"
Wait-Idle 15 "after i1 delete e1"
Step ("I2: the STACK: drag the point cue " + $d2 + " to between the last ORIGINAL cue and the Direct cue " + $d5 + " (the 'Move sequence step' dialog); accept the cue number offered.") "i2 move point cue"
Wait-Idle 15 "after i2 move"
Ask "I2: which cue number did the moved step get, and what did the dialog offer?" "i2 moved cue number" | Out-Null
Step ("I2: right-click the channel step " + $d6 + " > Delete step > confirm.") "i2 delete channel step"
Wait-Idle 15 "after i2 delete channel step"
Step ("I2: right-click the Direct cue " + $d5 + " > Delete step > confirm.") "i2 delete direct cue"
Wait-Idle 15 "after i2 delete direct cue"
Step "I2: right-click the moved point cue > Delete step > confirm." "i2 delete point cue"
Wait-Idle 15 "after i2 delete point cue"
Step "I4: PATCH tab: drag the DMX 100 cell (NOT channel 47) onto the un-patch bin." "i4 unpatch dmx 100"
Wait-Idle 15 "after i4 dmx 100"
Ask "I4: is DMX 100 empty again, and does channel 47 still have the addresses it had before? (describe)" "i4 patch restored" | Out-Null
Section-End "I"

# --- J: the end ---
Close-SmartSoft 2
Confirm-Done "Stop USBPcap (Ctrl+C in its window)." "usbpcap stopped (owner)"
$after = Save-Show "the AFTER-session save (must equal the backup byte for byte)" "after-session save"
Step "Finally, on the CONSOLE: press FADERS 1-24 if MEMS is lit, so that the console is back in channel mode." "console back in channel mode"
Log "END guided E12b session"
Write-Host ""
Write-Host "Files to copy into docs/protocol/captures/ (with the date prefix ...-e12b-...):" -ForegroundColor Green
Write-Host ("   action log:   " + $LogFile) -ForegroundColor White
Write-Host "   USBPcap file: the .pcap you named when starting USBPcapCMD (split to the console before it is committed)" -ForegroundColor White
Write-Host ("   SD files:     " + $backup + " (backup), " + $edited + " (edited state) and " + $after + " (after) into smartfade-sd-card-backup/; backup and after must be byte-identical") -ForegroundColor White
Write-Host "If backup and after differ: on the console Menu > Save/Load Show > Load the backup (or Load patch from card for the patch alone)." -ForegroundColor Yellow
