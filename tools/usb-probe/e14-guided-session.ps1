# Guided operator session "E14" (written 2026-11-02, late evening, by an Opus 5.5 agent at the owner's request; parse-checked,
# not run): the passive capture of SmartSoft's "send show" (docs/protocol/reverse-engineering.md, "Owner feedback after playing",
# the E14 line): SmartSoft Online opens a show file THREE times while USBPcap records: (1) the same show as on the console (a PC
# copy of the SD backup made just before), (2) a copy with exactly one channel label changed (channel 1's label to E14, made by
# the owner in a text editor and checked here line by line), (3) the original again. SmartSoft sends; this script only logs,
# checks files on the PC and starts SmartSoft; our own software is not involved and sends nothing.
# This is the FIRST capture of a DESTRUCTIVE path (known-facts F121): SmartSoft's "Open showfile" ends, when Online, in the full
# upload of SmartSoft's show (smartsoft-analysis.md S11.15 row 7, INFERRED from the code, never captured), and the SmartSoft
# manual 3.0.1 p. 24 says "If a console is connected the same show will be loaded to the console" (DOCUMENTED). Each open
# replaces the console's show with what SmartSoft sends; the SD backup is the only way back (the console's own Load show from
# the card, console manual v3.0 p. 6, DOCUMENTED). How SmartSoft offers the open (which item, which dialogs, whether it asks
# before it sends) is NOT known: the owner names the item and types every dialog's text before answering it; the guide assumes
# no flow. A look into SmartSoft's editor tabs can itself send a write (E12, F158): no cell or field is clicked, and no tab but
# the one that holds the open item, if it is in a tab (S11.15 row 7 places "Open showfile" in the System tab: INFERRED).
# After open 3 SmartSoft is restarted once (its download shows the console's show object by object, which the SD file cannot show
# completely, F144), closed, and the after-session SD save is compared here with the backup (SHA-256); if they differ, the backup
# is loaded back on the console from the card and compared again.
# Never in E14: the faders, the keys, the Console Simulator, any record function, any SmartSoft edit (Patch, Independents,
# Settings, labels, memories, steps), cell or field clicks in SmartSoft, any tab but the one that holds the open item, page
# clicks, New show, Save, Save As, any Erase, the wheel.
# One session is enough; a repeat (E14b) is a separate decision.
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
function Is-Yes([string]$a) {
    $x = $a.Trim().ToLowerInvariant()
    return ($x -eq 'y' -or $x -eq 'yes')
}
function Require-Yes([string]$question, [string]$tag) {
    $a = Ask $question $tag
    if (-not (Is-Yes $a)) {
        Log ("STOP: '" + $tag + "' was not answered y; E14 does not start")
        Write-Host "E14 does not start without this answer. Nothing was sent; SmartSoft was not started. Tell the assistant what is missing." -ForegroundColor Red
        exit 2
    }
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
# SmartSoft's start: any dialog it shows is typed before it is answered, and answered NO / Cancel (a start must not send a show).
function Start-SmartSoft([int]$n) {
    Write-Host ""
    Write-Host ("Starting SmartSoft (session #{0}) ..." -f $n) -ForegroundColor Yellow
    Start-Process -FilePath $SmartSoft -WorkingDirectory (Split-Path $SmartSoft) | Out-Null
    Log ("smartsoft start #" + $n + " (Start-Process by the script)")
    $t = Read-Host "   Press Enter when SmartSoft shows ONLINE; if a dialog appears first, type its text here BEFORE you answer it"
    if ([string]::IsNullOrWhiteSpace($t)) {
        Log ("smartsoft online #" + $n + " (owner; no dialog while starting)")
        return
    }
    Log ("smartsoft start #" + $n + " dialog text = " + $t)
    $b = Read-Host "   Answer it with NO / Cancel / Don't send (a start must not send a show): type the button you will press, press Enter, then press it"
    Log ("smartsoft start #" + $n + " dialog button = " + $b)
    Confirm-Done "Wait until SmartSoft shows ONLINE (another dialog: note its text at the next answer)." ("smartsoft online #" + $n + " (owner)")
}
function Close-SmartSoft([int]$n) {
    Step "Close SmartSoft now (the close button of its window). Do NOT save anything if it asks." ("smartsoft close #" + $n + " start")
    Confirm-Done "When SmartSoft is closed." ("smartsoft closed #" + $n)
}
function Save-Show([string]$what, [string]$tag) {
    Write-Host ""
    Write-Host ("Save the show to the SD card on the console NOW (Menu > Save/Load Show, to a FREE number, as for every backup): " + $what) -ForegroundColor Yellow
    $name = ""
    while ([string]::IsNullOrWhiteSpace($name)) {
        $name = Read-Host "   Type the file name the console used (e.g. SHOW32.ASC); it is required"
    }
    Log ($tag + ": " + $name.Trim() + " (copy it into smartfade-sd-card-backup/ after the session)")
    return $name.Trim()
}
function Sha256([string]$path) {
    return (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
}
# A file's lines as the bytes are (Latin-1: one byte, one character), split at LF only, so that a CR, a byte-order mark or any
# other changed byte shows up as a difference.
function Read-RawLines([string]$path) {
    $bytes = [IO.File]::ReadAllBytes($path)
    $text = [Text.Encoding]::GetEncoding(28591).GetString($bytes)
    return ,($text.Split([char]10))
}
function Visible([string]$s) {
    $sb = New-Object System.Text.StringBuilder
    foreach ($ch in $s.ToCharArray()) {
        $c = [int]$ch
        if ($c -lt 32 -or $c -gt 126) { [void]$sb.Append(('<{0:X2}>' -f $c)) } else { [void]$sb.Append($ch) }
    }
    return $sb.ToString()
}
function Line-Diffs([string]$a, [string]$b) {
    $x = Read-RawLines $a
    $y = Read-RawLines $b
    $out = New-Object System.Collections.Generic.List[object]
    $n = [Math]::Max($x.Length, $y.Length)
    for ($i = 0; $i -lt $n; $i++) {
        $old = '(no line)'
        $new = '(no line)'
        if ($i -lt $x.Length) { $old = $x[$i] }
        if ($i -lt $y.Length) { $new = $y[$i] }
        if (-not [string]::Equals($old, $new, [StringComparison]::Ordinal)) {
            $out.Add([pscustomobject]@{ Line = $i + 1; Old = $old; New = $new })
        }
    }
    return ,$out
}
# Every dialog SmartSoft shows during an open: its text typed BEFORE it is answered, then the button chosen by the dialog rule.
# Returns $false when a Cancel was chosen (the guide then ends the opens).
function Log-Dialogs([string]$p) {
    $i = 1
    $go = $true
    while ($true) {
        $t = Read-Host ("   " + $p + "if SmartSoft shows a dialog now, type its title and text exactly BEFORE you answer it; press Enter alone when there is none (or no more)")
        if ([string]::IsNullOrWhiteSpace($t)) {
            Log ($p + "no (further) dialog")
            break
        }
        Log ($p + "dialog " + $i + " text = " + $t)
        $b = Read-Host "   Which button will you press by the dialog rule (yes = load / send to the console; no = do not save; cancel = erase / delete / new show or anything else)? Type it, press Enter, THEN press that button"
        Log ($p + "dialog " + $i + " button = " + $b)
        if ($b -match '(?i)cancel') { $go = $false }
        $i++
    }
    return $go
}
# One open: the item, the dialogs before the file dialog, the file dialog's types, the file, the dialogs after it, what SmartSoft,
# the console and the stage showed. Returns $true only when the open went through.
function Run-Open([int]$n, [string]$file, [string]$what) {
    $p = "open " + $n + " "
    Write-Host ""
    Write-Host ("Open {0} of 3: {1}" -f $n, $what) -ForegroundColor Green
    Write-Host ("   File: " + $file) -ForegroundColor White
    if (-not (Test-Path -LiteralPath $file)) {
        Log ($p + "STOP: the file is missing: " + $file)
        Write-Host "The file is missing: the opens end here; the guide goes to its end steps." -ForegroundColor Red
        return $false
    }
    Step "Click SmartSoft's item that opens a show (the one you named, in its menu or its one tab; click no cell, field or other button on the way). If a dialog comes before the file dialog, the guide asks for its text next." ($p + "open item clicked")
    $go = Log-Dialogs ($p + "before the file dialog: ")
    if (-not $go) {
        Log ($p + "STOP: cancelled before the file dialog")
        return $false
    }
    $fd = Ask "Is SmartSoft's FILE dialog open now? (y / what is shown instead)" ($p + "file dialog open")
    if (-not (Is-Yes $fd)) {
        Log ($p + "STOP: no file dialog; the opens end")
        Write-Host "Cancel whatever is open in SmartSoft (Cancel / Esc), click nothing else; the guide goes to its end steps." -ForegroundColor Red
        return $false
    }
    Ask "File dialog: which file types does its type list show (exactly)?" ($p + "file dialog types") | Out-Null
    Step ("In the file dialog choose " + $file + " and press its OPEN button.") ($p + "file chosen, open pressed")
    $go = Log-Dialogs ($p + "after the file dialog: ")
    Confirm-Done "When SmartSoft has finished: no dialog and no progress bar left, its window usable again." ($p + "smartsoft finished (owner)")
    Ask "What did SmartSoft show while it opened and sent (a progress bar, a message, nothing)? About how many seconds did it take? (describe)" ($p + "smartsoft during the open") | Out-Null
    Ask "Console LCD during and after it (both lines, as exactly as you can)? (describe)" ($p + "console lcd") | Out-Null
    Ask "Did anything change on stage or on the console's LEDs (bump LEDs, FADERS keys, STACK, BLACKOUT, IND)? (n / describe)" ($p + "stage and leds") | Out-Null
    Ask "Does the console show a show or file name now WITHOUT opening a menu (its LCD), and did it change? (describe / '-')" ($p + "console file name") | Out-Null
    Ask "What does SmartSoft's title bar show now? (type it)" ($p + "smartsoft title") | Out-Null
    if ($n -eq 2) {
        Ask "Is channel 1's new label E14 visible WITHOUT clicking (on the SmartSoft view already shown, on the console's LCD)? (describe / '-')" ($p + "label e14 visible") | Out-Null
    }
    if ($n -eq 3) {
        Ask "Is channel 1's label empty again where you saw E14 (without clicking)? (describe / '-')" ($p + "label empty again") | Out-Null
    }
    $through = Ask "Did the open go through (SmartSoft loaded the file, nothing was cancelled, no error)? (y / describe)" ($p + "went through")
    Section-End ("open " + $n)
    return ($go -and (Is-Yes $through))
}
# The after-session file next to the backup's copy in the folder, compared by SHA-256: 'equal', 'different' or 'skipped'.
function Compare-InFolder([string]$folder, [string]$before, [string]$after, [string]$tag) {
    $a = Join-Path $folder $before
    $b = Join-Path $folder $after
    Write-Host ""
    Write-Host ("Copy " + $after + " from the SD card into " + $folder + " (next to " + $before + "), then put the card back into the console.") -ForegroundColor Yellow
    while (-not ((Test-Path -LiteralPath $a) -and (Test-Path -LiteralPath $b))) {
        $r = Read-Host ("   Press Enter when " + $b + " exists (or type skip to compare later)")
        if ($r.Trim().ToLowerInvariant() -eq 'skip') {
            Log ($tag + ": comparison skipped by the owner (to be done later)")
            return 'skipped'
        }
    }
    $ha = Sha256 $a
    $hb = Sha256 $b
    Log ($tag + ": " + $before + " sha256 " + $ha + ", " + $after + " sha256 " + $hb)
    if ($ha -eq $hb) {
        Log ($tag + ": EQUAL byte for byte")
        Write-Host "   Equal byte for byte." -ForegroundColor Green
        return 'equal'
    }
    Log ($tag + ": DIFFERENT")
    Write-Host "   Different." -ForegroundColor Red
    return 'different'
}

$host.UI.RawUI.WindowTitle = "E14 guided session"
Write-Host "E14 guided session: SmartSoft opens a show file three times while Online ('send show') and USBPcap records; our software is not involved." -ForegroundColor Green
Write-Host "Follow the yellow instructions one by one. Press Enter exactly when you START each action. Leave the waits untouched." -ForegroundColor Green
Write-Host "If SmartSoft shows any dialog or message, type its text into this window BEFORE you answer it (the guide asks for it)." -ForegroundColor Green
Write-Host "Never: the faders, the keys, the Console Simulator, record functions, any SmartSoft edit, cell or field clicks in SmartSoft, any tab but the one that holds the open item, page clicks, New show, Save, Save As, Erase, the wheel." -ForegroundColor Green
Log "START guided E14 session (PC clock, local time)"

# --- Gate 1: the destructive path, in plain words ---
Write-Host ""
Write-Host "E14 is the FIRST capture of a DESTRUCTIVE path (F121). Each of the three opens replaces the console's WHOLE show with what SmartSoft sends:" -ForegroundColor Red
Write-Host "memories, sequences, the stack, the patch, the labels, and whatever else the upload carries (never captured: UNKNOWN). If an open goes wrong" -ForegroundColor Red
Write-Host "(a cut transfer, a file SmartSoft reads differently, a crash), the console may be left with an incomplete or different show. The SD backup" -ForegroundColor Red
Write-Host "you make in the next step is the ONLY way back: the console's own Load show from the card (Menu > Save/Load Show > Load show, the backup's" -ForegroundColor Red
Write-Host "number chosen with the wheel, confirmed with MENU: console manual v3.0 p. 6)." -ForegroundColor Red
Require-Yes "Do you accept that the console's show is replaced three times in this session, with the SD backup as the only way back? (y / n)" "accept show replaced three times"
Require-Yes "Is the console's own Load show from the SD card your way back if anything goes wrong, and do you know how to do it on this console? (y / n)" "load show from card is the way back"

# --- Gate 2: safety rule 3, the backup; the usual questions ---
$backup = Save-Show "the BACKUP before the session (safety rule 3; the only way back)." "backup taken"
Ask "Was the console switched OFF since the last session? (y / n / unsure)" "console powered off since the last session" | Out-Null
Ask "What is on your rig today (fixtures and their DMX addresses)? (describe)" "rig today" | Out-Null
Require-Yes "The rig: with every fader at zero nothing should light, but what an upload does to the console's output was never seen (UNKNOWN); the show sent is your own (same patch, IND 2 on DMX 47). Is the rig safe for that? (y / n)" "rig decision"

# --- Gate 3: the backup's copy on the PC ---
Write-Host ""
Write-Host ("Copy " + $backup + " from the SD card into a NEW folder on the PC, outside the repository (for example C:\E14), then put the card back into the console.") -ForegroundColor Yellow
Write-Host "Not into smartfade-sd-card-backup/: that folder holds the files the console wrote, and the edited copy below is not one." -ForegroundColor Yellow
$folder = ""
$original = ""
while ($true) {
    $folder = (Ask "Type the folder's full path (e.g. C:\E14)." "pc folder").Trim('"')
    if ([string]::IsNullOrWhiteSpace($folder)) { continue }
    $original = Join-Path $folder $backup
    if (Test-Path -LiteralPath $original) { break }
    Write-Host ("   " + $original + " does not exist. Copy the file there, then type the folder again.") -ForegroundColor Red
}
$originalHash = Sha256 $original
Log ("pc copy of the backup: " + $original + " sha256 " + $originalHash)

# --- Gate 4: the copy with exactly one label changed (channel 1's label to E14), made by the owner and checked here ---
# The value E14: three printable ASCII characters, at most 6 per label line and no '&' (docs/formats/asc-format.md, "Label and
# memory rules learned from the files", OBSERVED on 34 files); the Text line is derived from line 1 when line 1 has fewer than 6
# characters (same table), so exactly two lines of the $CHANNEL 1 block change. The file itself is the owner's, not the script's.
$lines = Read-RawLines $original
$idx = -1
for ($i = 0; $i -lt $lines.Length; $i++) {
    if ($lines[$i].TrimEnd() -eq '$CHANNEL 1') { $idx = $i; break }
}
if ($idx -lt 0 -or ($idx + 2) -ge $lines.Length -or -not $lines[$idx + 1].StartsWith('Text ') -or -not $lines[$idx + 2].StartsWith('$$Text 1 ')) {
    Log 'STOP: the backup''s copy has no "$CHANNEL 1" block followed by its Text and $$Text 1 lines; E14 does not start'
    Write-Host "The backup's copy does not hold the '`$CHANNEL 1' label block this guide expects (asc-format.md). E14 does not start; tell the assistant. Nothing was sent." -ForegroundColor Red
    exit 2
}
$cr = ""
if ($lines[$idx + 1].EndsWith("`r")) { $cr = "`r" }
$expectText = 'Text E14' + $cr
$expectLine1 = '$$Text 1 E14' + $cr
Write-Host ""
Write-Host "In the backup's copy, channel 1's label block reads (line numbers of the file; quotes added):" -ForegroundColor Yellow
for ($i = $idx; $i -le $idx + 4 -and $i -lt $lines.Length; $i++) { Write-Host ("   line {0}: '{1}'" -f ($i + 1), (Visible $lines[$i])) -ForegroundColor White }
Log ("channel 1 label block in the backup's copy: lines " + ($idx + 1) + "-" + ($idx + 5) + ": '" + (Visible $lines[$idx + 1]) + "' / '" + (Visible $lines[$idx + 2]) + "'")
Write-Host "Make the second copy yourself: in the same folder copy the file to E14LABEL.ASC (an 8.3 name like the console's files; console manual p. 6)," -ForegroundColor Yellow
Write-Host "open the COPY in Notepad and change exactly these two lines of the '`$CHANNEL 1' block (section '! Channel Info'), nothing else:" -ForegroundColor Yellow
Write-Host ("   line {0}: '{1}'  ->  'Text E14'" -f ($idx + 2), (Visible $lines[$idx + 1])) -ForegroundColor White
Write-Host ("   line {0}: '{1}'  ->  '`$`$Text 1 E14'" -f ($idx + 3), (Visible $lines[$idx + 2])) -ForegroundColor White
Write-Host "Keep every other line, space and line end as it is (Notepad keeps the file's line ends; do not use Word or WordPad), then save the copy." -ForegroundColor Yellow
$edited = ""
while ($true) {
    $name = (Ask "Type the copy's file name (press Enter for E14LABEL.ASC)." "edited copy name").Trim('"')
    if ([string]::IsNullOrWhiteSpace($name)) { $name = 'E14LABEL.ASC' }
    $edited = Join-Path $folder $name
    if (-not (Test-Path -LiteralPath $edited)) {
        Write-Host ("   " + $edited + " does not exist yet.") -ForegroundColor Red
        continue
    }
    $diffs = Line-Diffs $original $edited
    Write-Host ("   The copy differs from the backup's copy in {0} line(s):" -f $diffs.Count) -ForegroundColor White
    foreach ($d in ($diffs | Select-Object -First 12)) {
        Write-Host ("   line {0}: '{1}' -> '{2}'" -f $d.Line, (Visible $d.Old), (Visible $d.New)) -ForegroundColor White
        Log ("edited copy check: line " + $d.Line + ": '" + (Visible $d.Old) + "' -> '" + (Visible $d.New) + "'")
    }
    $good = ($diffs.Count -eq 2) -and ($diffs[0].Line -eq $idx + 2) -and ($diffs[0].New -ceq $expectText) -and ($diffs[1].Line -eq $idx + 3) -and ($diffs[1].New -ceq $expectLine1)
    if ($good) {
        Log "edited copy check: exactly the two expected lines differ"
        Write-Host "   Exactly the two expected lines differ." -ForegroundColor Green
        break
    }
    Log "edited copy check: NOT as expected"
    $r = Read-Host "   Not exactly the two expected lines. Make the copy again from the backup's copy and redo the edit, then press Enter to check again (or type stop to end E14 here)"
    if ($r.Trim().ToLowerInvariant() -eq 'stop') {
        Log "STOP: the edited copy was not made as expected; E14 does not start"
        Write-Host "E14 does not start. Nothing was sent; SmartSoft was not started." -ForegroundColor Red
        exit 2
    }
}
$editedHash = Sha256 $edited
Log ("pc edited copy: " + $edited + " sha256 " + $editedHash)
Ask "In your own words: what did you change in the copy, and with which editor? (describe)" "edited copy owner statement" | Out-Null

# --- Gate 5: the dialog rule (SmartSoft's flow is not known) ---
Write-Host ""
Write-Host "The dialog rule for the three opens. Type every dialog's text into this window BEFORE you answer it, then:" -ForegroundColor Yellow
Write-Host "  - a question whether to load / send / transfer the show to the console, or to replace the console's show: answer YES / OK (that transfer is what E14 captures);" -ForegroundColor Yellow
Write-Host "  - a question whether to SAVE the show SmartSoft has open now: answer NO / Don't save (no PC file is written; Save As would also send a file name to the console, F121);" -ForegroundColor Yellow
Write-Host "  - anything else (an error, a version message, a question you are not sure about): CANCEL (or OK if it is the only button); the guide then ends the opens;" -ForegroundColor Yellow
Write-Host "  - a dialog that mentions ERASE, DELETE or NEW SHOW: CANCEL, even if it also offers to send (this wins over the first line; F121: the erase commands are never" -ForegroundColor Yellow
Write-Host "    to be triggered); the guide then ends the opens and the text goes to the assistant." -ForegroundColor Yellow
Require-Yes "Do you accept this dialog rule? (y / n)" "accept dialog rule"

# --- The driver: SmartSoft needs ETC's driver on interface 1 ---
Log ("memory integrity: " + (MemoryIntegrityState))
$svc = Interface1Service
while ($svc -ne 'ETCHSP') {
    Write-Host ""
    Write-Host ("Interface 1 is bound to '{0}'. SmartSoft needs ETC's driver (service ETCHSP)." -f $svc) -ForegroundColor Red
    Write-Host "Device Manager > the console's interface-1 device > Update driver > Browse my computer > Let me pick > 'USB High-Speed-Pipe'. Memory Integrity must be off." -ForegroundColor Yellow
    Read-Host "   Switch the driver, then press Enter to check again" | Out-Null
    $svc = Interface1Service
}
Log "interface 1 driver service: ETCHSP (ETC's driver)"

# --- USBPcap before SmartSoft, into a new file per attempt ---
$pcapName = 'e14-' + (Get-Date -Format 'yyyyMMdd-HHmm') + '.pcap'
Write-Host ""
Write-Host "In an ADMIN PowerShell window start USBPcap on the console's root hub, into a NEW file (an absolute path; an admin window starts in C:\Windows\System32):" -ForegroundColor Yellow
Write-Host ('   & "C:\Program Files\USBPcap\USBPcapCMD.exe" -d \\.\USBPcap1 -A --inject-descriptors -o "' + (Join-Path (Get-Location) $pcapName) + '"') -ForegroundColor White
Log ("usbpcap file name suggested to the owner: " + $pcapName)
Confirm-Done "When USBPcap is running." "usbpcap running (owner)"
Write-Host ""
Write-Host "Starting position on the console: CHANNEL mode (FADERS 1-24 lit, bank 1-24), page 1, all channel faders at ZERO, MASTER at FULL, BUMPS at FULL," -ForegroundColor Yellow
Write-Host "BLACKOUT off, SOLO off, IND 1 and IND 2 off, STACK off, NEXT off (unlit), no GO fade and no sequence running, nothing armed, no menu open." -ForegroundColor Yellow
Confirm-Done "Set that up now. From then on touch the console only when a step asks." "starting position set (owner)"

# --- SmartSoft Online, the baseline, the open item ---
Start-SmartSoft 1
Wait-Idle 60 "baseline: SmartSoft online, nothing touched (its System pair and the download)"
Ask "Without choosing anything: which SmartSoft menu item or button opens a show file, and where is it (a menu, or a tab: then name the tab)? You may open a menu, or switch to that ONE tab, to read it; click no cell, field or other button. Type its path exactly, e.g. 'menu > item' or 'tab > button'." "smartsoft open item" | Out-Null

# --- The three opens, a minute apart ---
$ok = Run-Open 1 $original "the SAME show as on the console (the PC copy of the backup)"
if ($ok) {
    Wait-Idle 60 "after open 1"
    $ok = Run-Open 2 $edited "the copy with channel 1's label changed to E14"
}
if ($ok) {
    Wait-Idle 60 "after open 2"
    $ok = Run-Open 3 $original "the ORIGINAL again (channel 1's label back to empty)"
}
if ($ok) {
    Log "E14 opens covered: 1 (same show), 2 (channel 1 labelled E14), 3 (original again)"
} else {
    Log "E14 ended before the three opens were covered (see the STOP lines above)"
}
Wait-Idle 60 "after the last open"

# --- A SmartSoft restart: its download shows the console's show after the opens, object by object ---
Close-SmartSoft 1
Wait-Idle 10 "between sessions 1 and 2"
Start-SmartSoft 2
Wait-Idle 60 "session 2: SmartSoft's pair and download, the console's show after the opens"
Close-SmartSoft 2

# --- End: USBPcap, the PC files, the save, the comparison, the restore if needed ---
Confirm-Done "Stop USBPcap (Ctrl+C in its window)." "usbpcap stopped (owner)"
$o2 = Sha256 $original
$e2 = Sha256 $edited
Log ("pc files after the session: original sha256 " + $o2 + " (" + $(if ($o2 -eq $originalHash) { "unchanged" } else { "CHANGED" }) + "), edited sha256 " + $e2 + " (" + $(if ($e2 -eq $editedHash) { "unchanged" } else { "CHANGED" }) + ")")
Ask "Physical fader positions at the end (channel faders, MASTER, BUMPS) and the LEDs of SOLO, BLACKOUT, IND 1 / 2, STACK, NEXT? (describe)" "end positions" | Out-Null
$after = Save-Show "the after-session file; after open 3 it should equal the backup byte for byte." "after-session save"
$cmp = Compare-InFolder $folder $backup $after "after-session comparison"
$after2 = ""
if ($cmp -eq 'different') {
    Log "the after-session save differs from the backup: the show is restored from the SD backup by the console's own Load show"
    Write-Host ""
    Write-Host "The console's show differs from the backup after the opens (the difference itself is evidence: keep both files)." -ForegroundColor Red
    Write-Host ("Restore it now, SmartSoft closed: Menu > Save/Load Show > Load show; choose " + $backup + " with the wheel (the default offered may be another show: check the number on the LCD); confirm with MENU (console manual v3.0 p. 6).") -ForegroundColor Yellow
    Step "Load the backup on the console now." "restore: load show from the card start"
    Ask "What did the LCD show while loading, and is the console back on its normal screen? (describe)" "restore: load show lcd" | Out-Null
    $after2 = Save-Show "after the restore, to compare with the backup again." "after-restore save"
    $cmp2 = Compare-InFolder $folder $backup $after2 "after-restore comparison"
    if ($cmp2 -ne 'equal') {
        Log "after the restore the show is still not shown equal to the backup (different or not compared)"
        Write-Host "Not shown equal: keep every file, do nothing else to the console's show, and tell the assistant before you use the console for a show." -ForegroundColor Red
    }
} elseif ($cmp -eq 'skipped') {
    Write-Host "Not compared now: compare the two files soon; if they differ, the backup is loaded back on the console (Load show) before the console is used for a show." -ForegroundColor Yellow
}
Ask "Did you touch the console, click anything in SmartSoft, or run anything during the session that this guide did not ask for? (n / what and when)" "unasked actions" | Out-Null
Log "END guided E14 session"
Write-Host ""
Write-Host "Files to copy into docs/protocol/captures/ (with the date prefix ...-e14-...):" -ForegroundColor Green
Write-Host ("   action log:   " + $LogFile) -ForegroundColor White
Write-Host "   USBPcap file: the .pcap you named when starting USBPcapCMD (split to the console before it is committed)" -ForegroundColor White
Write-Host ("   PC files:     " + $original + " and " + $edited + " (what SmartSoft opened; keep them unchanged)") -ForegroundColor White
if ([string]::IsNullOrWhiteSpace($after2)) {
    Write-Host ("   SD files:     " + $backup + " (backup) and " + $after + " (after) into smartfade-sd-card-backup/") -ForegroundColor White
} else {
    Write-Host ("   SD files:     " + $backup + " (backup), " + $after + " (after the opens) and " + $after2 + " (after the restore) into smartfade-sd-card-backup/") -ForegroundColor White
}
