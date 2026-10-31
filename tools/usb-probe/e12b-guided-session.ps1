# Guided operator session "E12b" (planned 2026-10-31, evening): the REPEAT of E12 after a console power cycle. SmartSoft
# Online edits the PATCH and the STACK / SEQUENCES while USBPcap records (passive; our software is not involved). A second,
# independent session is what turns E12's OBSERVED findings (H91-H99, H101, F151-F159; docs/protocol/captures/
# 2026-10-31-e12-smartsoft-patch-stack-edits.md, "Next capture to take") into VERIFIED behaviour, and it adds what E12 did not
# show: a CHANNEL step, the MANUAL run mode, inserts at other places of KOTLIK, an SD save of the EDITED show (the file forms),
# a "c-with-caron" and a full third line in a Direct cue's label, the IND values named by the owner, and (optional) the
# console's own Insert / Delete / Step Timing menus beside its patch menus. Every test change is restored before the end, and
# the final SD save must equal the backup byte for byte. Test objects as in E12: channel 47, DMX 100-102 (nothing may be
# connected there), IND 1, memory 9 of page 1 (empty before and after), the end of the stack, the KOTLIK sequence (page 1,
# fader 21), page 12's name.
# Lessons of E12 built in: a "look" in SmartSoft's editors is a write (opening a panel, leaving a cell), so nothing is opened
# or clicked beyond the step; the IND panel, PLAY and PAUSE are never touched outside their steps; a memory step has no label
# in SmartSoft (E12's D4 is gone); a Direct cue's label is set after the insert; the row that is right-clicked is logged, since
# "Insert step" put E12's new steps before the clicked row in a sequence and after it at the end of the stack.
# Never in E12b: the Console Simulator, Open showfile, New show, Save As, any Erase, Default times, REC SEQ, Tap mode, PAGE
# clicks in SmartSoft, PLAY / PAUSE in the stack view. Never drag a CHANNEL onto the un-patch bin: only the DMX cells named.
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
function Section-End([string]$name) {
    Ask ("End of " + $name + ": did you click, open or change ANYTHING in SmartSoft or on the console beyond the steps above? (n / describe exactly)") ($name + " extra actions")
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
Write-Host "E12b guided session: the repeat of E12. SmartSoft edits the patch and the stack / sequences while USBPcap records; our software is not involved." -ForegroundColor Green
Write-Host "Follow the yellow instructions one by one. Press Enter exactly when you START each action. Leave the waits untouched." -ForegroundColor Green
Write-Host "A look in SmartSoft's editors is a write: open, click and change NOTHING beyond the step named. Never the IND panel, PLAY or PAUSE outside their steps." -ForegroundColor Green
Write-Host "Never: the Simulator, Open showfile, New show, Save As, Erase, Default times, REC SEQ, Tap mode, PAGE clicks in SmartSoft." -ForegroundColor Green
Write-Host "Never drag a CHANNEL onto the un-patch bin; only the DMX cells this guide names." -ForegroundColor Green
Write-Host "If SmartSoft shows any message box, type its text at the next answer prompt." -ForegroundColor Green
Log "START guided E12b session (PC clock, local time)"

# --- Before anything: the power cycle, the backup, the owner's decisions ---
Write-Host ""
Write-Host "Power-cycle the console first (E12b must be a session of its own), then: CHANNEL mode (FADERS 1-24 lit), all channel faders at ZERO," -ForegroundColor Yellow
Write-Host "MASTER and BUMPS at FULL, STACK unlit, fader 21 (KOTLIK) at ZERO, page 1, BLACKOUT / SOLO / IND off." -ForegroundColor Yellow
Confirm-Done "When the console is in that state." "starting position set (owner)"
Ask "Was the console power-cycled just now, after E12? (y / n)" "power cycled"
$backup = Save-Show "the BACKUP before the session (safety rule 3)" "backup taken"
Ask "Is NOTHING connected to DMX addresses 100, 101 and 102 (the rig is DMX 5-16)? (y / n)" "dmx 100-102 free"
Ask "Is memory 9 of page 1 EMPTY (its key unlit in MEMS mode)? (y / n)" "memory 9 empty"
Ask "You accept that E12b changes show data on purpose (test cues incl. a Direct cue and a channel step, KOTLIK steps, run modes, labels, test patch addresses, memory 9, page 12's name), saves the EDITED show to the SD card under its own number, and restores everything, with the SD backup as the fallback? (y / n)" "acceptance of test changes"
$optH = (Ask "Optional H: the console's OWN menus (Patch by dimmer in Blind, Unpatch dimmer, Step Timing, Insert step, Delete step), which use the physical wheel? Recommended: they show what a read-only session of ours would see. (y / n)" "opt h") -eq 'y'

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

# --- A: SmartSoft Online, baseline, the tabs, one deliberate look ---
Start-SmartSoft 1
Wait-Idle 60 "A1 baseline: SmartSoft online, nothing touched (download 1)"
Step "A2: open the PATCH tab. Touch nothing in it (do not open the IND panel)." "a2 patch tab opened"
Wait-Idle 30 "a2 patch tab open, untouched"
Step "A2: open the STACK/SEQ EDIT tab. Touch nothing (no PLAY, no PAUSE, no right-click)." "a2 stack seq tab opened"
Wait-Idle 30 "a2 stack seq tab open, untouched"
Step "A2: open the PAGE tab. Touch nothing." "a2 page tab opened"
Wait-Idle 30 "a2 page tab open, untouched"
Step "A2: open the MEM EDIT tab. Touch nothing." "a2 mem edit tab opened"
Wait-Idle 30 "a2 mem edit tab open, untouched"
Step "A3: PATCH tab: open the IND 1 panel (click it), change NOTHING, and close it again. (E12 showed that this alone sends an Independents write.)" "a3 ind 1 panel opened and closed"
Wait-Idle 15 "after a3"
Section-End "A"

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
Section-End "B"

# --- C: the patch ---
Step "C1: PATCH tab: drag CHANNEL 47 onto the DMX 100 cell (channel 47 gets the address 100)." "c1 patch channel 47 to dmx 100"
Confirm-Done "When the drop is done and the cell shows 47." "c1 patch done"
Wait-Idle 15 "after c1"
Step "C2: PATCH tab: drag IND 1 onto the DMX 101 cell." "c2 patch ind 1 to dmx 101"
Confirm-Done "When the drop is done." "c2 patch done"
Wait-Idle 15 "after c2"
Ask "C2: what does the DMX 101 cell show (i1, 49, something else)?" "c2 result"
Step "C3: PATCH tab: open the IND 1 panel. Change nothing yet." "c3 ind 1 panel opened"
Wait-Idle 15 "after c3 panel opened"
Ask "C3: type EXACTLY what the panel shows for IND 1's BUTTON MODE and for its GM (grand master) setting now, e.g. 'button: Toggle, GM: Yes'." "c3 ind 1 values before"
Step "C3: change the BUTTON MODE of IND 1 to the other value, with ONE click or choice." "c3 ind 1 button mode changed"
Wait-Idle 15 "after c3 button mode"
Ask "C3: what does the panel show for the button mode now, and how many clicks did it take?" "c3 button mode after"
Step "C3: change the BUTTON MODE back, with one click or choice." "c3 ind 1 button mode restored"
Wait-Idle 15 "after c3 button mode restored"
Step "C3: change the GM setting of IND 1 to the other value." "c3 ind 1 gm changed"
Wait-Idle 15 "after c3 gm"
Ask "C3: what does the panel show for GM now?" "c3 gm after"
Step "C3: change the GM setting back, then close the IND 1 panel." "c3 ind 1 gm restored and panel closed"
Wait-Idle 15 "after c3 gm restored"
Section-End "C"

# --- D: the stack ---
Step "D1: STACK/SEQ EDIT tab, the STACK: right-click the LAST cue (the row of the highest cue number) > Insert step > Memory 01/09 (page 1, memory 9), confirm." "d1 insert step after last cue"
Confirm-Done "When the new cue is in the list." "d1 inserted"
Wait-Idle 15 "after d1"
$d1 = Ask "D1: which cue number did SmartSoft give the new step (type it exactly, e.g. 15.0), and which row had you right-clicked (its cue number)?" "d1 cue number and row"
$d1 = ($d1 -split '[ ,;]')[0]
Step "D2: the STACK: right-click the cue BEFORE the last ORIGINAL cue (in E12 that was 13.0) > Insert step > Memory 01/09; if the dialog offers a cue number, take a POINT cue (e.g. 12.1 or 13.1), confirm." "d2 insert point cue"
Confirm-Done "When the point cue is in the list." "d2 inserted"
Wait-Idle 15 "after d2"
$d2 = Ask "D2: which cue number does the point cue have (type it exactly), which row had you right-clicked, and was the dialog's cue-number field enabled?" "d2 cue number and row"
$d2 = ($d2 -split '[ ,;]')[0]
Step ("D3: on cue " + $d1 + ": set its UP time to 3.0 and confirm.") "d3 up time"
Wait-Idle 15 "after d3 up"
Step ("D3: on cue " + $d1 + ": set its DOWN time to 4.0 and confirm.") "d3 down time"
Wait-Idle 15 "after d3 down"
Step ("D3: on cue " + $d1 + ": set its WAIT (the third time) to 2.0 and confirm.") "d3 wait time"
Wait-Idle 15 "after d3 wait"
Step "D5: on the CONSOLE physical channel fader 5 to about HALF (so that the Direct cue has a level in it)." "d5 fader 5 to half"
Confirm-Done "When it is at about half." "d5 fader 5 at half"
Wait-Idle 10 "d5 fader 5 at half"
Step "D5: the STACK: right-click the LAST cue > Insert step > DIRECT CUE, confirm." "d5 direct cue inserted"
Confirm-Done "When the Direct cue is in the list." "d5 inserted"
Wait-Idle 15 "after d5 insert"
$d5 = Ask "D5: which cue number does the Direct cue have (type it exactly), and does its row show any level or content?" "d5 cue number"
$d5 = ($d5 -split '[ ,;]')[0]
Step "D5: on the CONSOLE physical fader 5 back to ZERO." "d5 fader 5 to zero"
Confirm-Done "When it is at ZERO." "d5 fader 5 at zero"
Wait-Idle 15 "after d5 fader zero"
Step ("D5: set the LABEL of the Direct cue " + $d5 + " to exactly  čAJ123456789012345  (18 characters, the first a c with a caron) and confirm.") "d5 direct cue label"
Wait-Idle 15 "after d5 label"
Ask "D5: what does SmartSoft show as the label now (type it exactly; did it cut it, refuse the č, or split it into lines)?" "d5 label shown"
Step "D6: the STACK: right-click the LAST cue > Insert step > CHANNEL > channel 47 (if the dialog asks for a level, type 50), confirm." "d6 channel step inserted"
Confirm-Done "When the channel step is in the list." "d6 inserted"
Wait-Idle 15 "after d6"
$d6 = Ask "D6: which cue number does the channel step have (type it exactly), and what did the dialog offer for a channel step (a level field? a dropdown?)?" "d6 cue number"
$d6 = ($d6 -split '[ ,;]')[0]
Section-End "D"

# --- E: the KOTLIK sequence (page 1, fader 21) ---
Step "E1: STACK/SEQ EDIT tab: choose the sequence KOTLIK in the title-bar drop-down; right-click its step 3 > Insert step > Memory 01/09, confirm." "e1 insert sequence step at 3"
Confirm-Done "When the new step is in the list." "e1 inserted"
Wait-Idle 15 "after e1"
$e1 = Ask "E1: which step number does the new step show, and where did it land (before or after the old step 3)?" "e1 step number"
$e1 = ($e1 -split '[ ,;]')[0]
Step "E1b: KOTLIK: right-click its LAST step > Insert step > Memory 01/09, confirm." "e1b insert sequence step at end"
Confirm-Done "When the second new step is in the list." "e1b inserted"
Wait-Idle 15 "after e1b"
$e1b = Ask "E1b: which step number does this new step show, and where did it land (before or after the old last step)?" "e1b step number"
$e1b = ($e1b -split '[ ,;]')[0]
Step ("E2: on the new step " + $e1 + ": set its UP time to 2.0 and confirm.") "e2 sequence step up time"
Wait-Idle 15 "after e2"
Step "E3: KOTLIK's run mode: the bare word 'Loop' in the header of KOTLIK's step list (no 'Run mode' label); click it and choose ONE SHOT." "e3 run mode one shot"
Wait-Idle 15 "after e3"
Step "E3b: the same word again (now 'One Shot'): click it and choose MANUAL." "e3b run mode manual"
Wait-Idle 15 "after e3b"
Step "E4: rename the sequence KOTLIK to  KOTLIX  and confirm." "e4 sequence label kotlix"
Wait-Idle 15 "after e4"
Ask "E: looking at the CONSOLE's LCD now (hold nothing): what does it show? (describe, or '-')" "e console lcd"
Section-End "E"

# --- F: memory content and names ---
Step "F1: MEM EDIT tab: open memory 01/09 and set channel 5 to 100 % in the table cell, confirm." "f1 mem edit channel 5 to 100"
Wait-Idle 15 "after f1"
Step "F2: PAGE tab: set the NAME of memory 01/09 to  T  and confirm." "f2 memory 9 name T"
Wait-Idle 15 "after f2"
Step "F3: PAGE tab: set the NAME of PAGE 12 (the LAST page, not page 1) to  T  and confirm." "f3 page 12 name T"
Wait-Idle 15 "after f3"
Ask "F3: which page did you rename (its number as SmartSoft shows it)?" "f3 page renamed"
Section-End "F"

# --- S: the edited show on the SD card (the file forms of the edited objects) ---
$edited = Save-Show "the EDITED show (One Shot / Manual, the new steps, the point cue, the Direct cue with its label and level, the channel step, the names)" "edited-state save"
Wait-Idle 15 "after the edited-state save"

# --- G: the console's stored bodies ---
Close-SmartSoft 1
Wait-Idle 10 "between sessions 1 and 2"
Start-SmartSoft 2
Wait-Idle 30 "G1 download 2: the console's own bodies of every test object"
Ask "G1: in SmartSoft now: do the stack's test cues show their times, the Direct cue its label, the channel step its channel, and does KOTLIX show Manual and its two new steps? (describe)" "g1 after restart"
Section-End "G"

# --- H: optional, the console's own menus (physical wheel; patch edits in Blind) ---
Optional-Step "H1 (optional): on the CONSOLE: Menu > Patch > Patch by dimmer, BLIND: dimmer 102 to channel 47, confirm with MENU, leave the menu." "h1 console patch dmx 102 blind" $optH
if ($optH) { Wait-Idle 15 "after h1 patch" }
Optional-Step "H2 (optional): on the CONSOLE: Menu > Patch > Patch - special > Unpatch dimmer 102, confirm with MENU, leave the menu." "h2 console unpatch dmx 102" $optH
if ($optH) { Wait-Idle 15 "after h2 unpatch" }
Optional-Step ("H3 (optional): on the CONSOLE: Menu > Sequences > Stack (XF) > Step Timing > one step: the DIRECT CUE " + $d5 + ", set its Up time to 5.0, confirm, leave the menu.") "h3 console step timing on the direct cue" $optH
if ($optH) { Wait-Idle 15 "after h3 step timing" }
Optional-Step "H4 (optional): on the CONSOLE: Menu > Sequences > Stack (XF) > Modify Steps > the LAST step > Insert (a memory step: memory 9 of page 1 if it asks), confirm, leave the menu." "h4 console insert stack step" $optH
if ($optH) { Wait-Idle 15 "after h4 insert" }
if ($optH) { Ask "H4: what did the console ask for and which cue number did the new step get? (describe)" "h4 console insert result" }
Optional-Step "H5 (optional): on the CONSOLE: Menu > Sequences > Stack (XF) > Modify Steps > that new step > Delete > confirm, leave the menu." "h5 console delete stack step" $optH
if ($optH) { Wait-Idle 15 "after h5 delete" }
Optional-Step "H6 (optional): on the CONSOLE: Menu > Sequences > Sequence (1-24) > sequence 21 (KOTLIX) > Modify Steps > the LAST step > Insert (memory 9 of page 1), confirm, leave the menu." "h6 console insert sequence step" $optH
if ($optH) { Wait-Idle 15 "after h6 insert" }
Optional-Step "H7 (optional): on the CONSOLE: the same menu > that new step > Delete > confirm, leave the menu." "h7 console delete sequence step" $optH
if ($optH) { Wait-Idle 15 "after h7 delete" }
if ($optH) { Ask "H: what did SmartSoft show after each console edit (did the values and the steps follow)? (describe)" "h smartsoft followed" }
if ($optH) { Section-End "H" }

# --- I: restores (by cue and step numbers; delete the highest first) ---
Step "I1: PAGE tab: clear page 12's name (empty) and confirm." "i1 page 12 name cleared"
Wait-Idle 15 "after i1 page name"
Step "I1: rename the sequence KOTLIX back to  KOTLIK  and confirm." "i1 sequence label restored"
Wait-Idle 15 "after i1 label"
Step "I1: KOTLIK's run mode back to LOOP (the word in the header, now 'Manual'; click it, choose Loop)." "i1 run mode loop"
Wait-Idle 15 "after i1 run mode"
Step ("I1: KOTLIK: right-click the step " + $e1b + " (inserted in E1b) > Delete step > confirm 'Are you sure?'.") "i1 delete sequence step e1b"
Wait-Idle 15 "after i1 delete e1b"
Step ("I1: KOTLIK: right-click the step " + $e1 + " (inserted in E1) > Delete step > confirm.") "i1 delete sequence step e1"
Wait-Idle 15 "after i1 delete e1"
Step ("I2: the STACK: drag the point cue " + $d2 + " to between the last ORIGINAL cue and cue " + $d1 + " (the 'Move sequence step' dialog); accept the cue number offered.") "i2 move point cue"
Wait-Idle 15 "after i2 move"
Ask "I2: which cue number did the moved step get, and what did the dialog offer?" "i2 moved cue number" | Out-Null
Step ("I2: right-click the channel step " + $d6 + " > Delete step > confirm.") "i2 delete channel step"
Wait-Idle 15 "after i2 delete channel step"
Step ("I2: right-click the Direct cue " + $d5 + " > Delete step > confirm.") "i2 delete direct cue"
Wait-Idle 15 "after i2 delete direct cue"
Step ("I2: right-click cue " + $d1 + " > Delete step > confirm.") "i2 delete d1 cue"
Wait-Idle 15 "after i2 delete d1"
Step "I2: right-click the moved point cue > Delete step > confirm." "i2 delete point cue"
Wait-Idle 15 "after i2 delete point cue"
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
Section-End "I"

# --- J: the check ---
Close-SmartSoft 2
Wait-Idle 10 "between sessions 2 and 3"
Start-SmartSoft 3
Wait-Idle 30 "J1 download 3: everything should equal download 1"
Ask "J1: does SmartSoft show the stack, KOTLIK (Loop, its old steps), memory 9 (empty), the patch and the names as before the session? (y / n / what differs)" "j1 restored view"
Close-SmartSoft 3
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
