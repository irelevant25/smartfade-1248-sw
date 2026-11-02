# Guided operator session "E13" (written 2026-11-02, late evening, by an Opus 5.5 agent at the owner's request; parse-checked,
# not run): the FIRST session of our own software that sends SmartSoft's start-up pair at Connect (docs/protocol/safety-checks.md
# "SC-4"): the two System messages 09 00 00 and 07 00 00 (id 39), once per Connect, where SmartSoft sends them (after reply
# headers #40 and #79; the second only after the console's version was listed and read as 3.0.2.9.0.43), so that the console
# lists its version and then its whole state and show (the 157-161-record download, F181 / F182) WITHOUT anyone touching the
# panel; then the MASTER probe of SC-4 clause 4 as amended on 2026-11-02, late evening: one tap of an unpatched channel's bump,
# switching to the other channel bank and back by SC-5's automatic switch when that channel is there, every control refused
# while it runs (in this show channel 47, bump 23 of bank 25-48: FADERS 25-48, the tap, FADERS 1-24); then a read-only minute
# and SC-2's sanity control (channel fader 1).
# SC-4 is a conditional GO for building, reviewing and this ONE first session on the owner's go: the pair goes in this session
# only because the SC-4 review passed and the owner gives the go on the day. E13 clears nothing by itself (a second session of
# ours makes the pair's behaviour towards our host VERIFIED; SC-4's decision stands or is annotated).
# Nothing in this script talks to the console. The script
#   - refuses to go on unless the SC-4 review section of safety-checks.md shows a PASS and the owner confirms it, and the owner
#     gives the written acceptances SC-4's "Conditions for E13" list (clause 3's residual, the blind window, the recovery through
#     SmartSoft, the MASTER probe as amended) and the rig answer,
#   - takes the safety-rule-3 gate (the SD backup's name) and records the versions (backend commit, Windows build, firmware from
#     Menu > Diagnostics, cable),
#   - has the console set to the starting position and power-cycled, then left untouched until a step asks (setting the position
#     moves faders, and a console keeps every change listed for the next host, F177; after the power cycles of E3b, E8b and E9d
#     only LedStatus and DisplayContent were pending, after E11 attempt 1's nothing, F166, H65 (a hypothesis): so the state
#     reaches the UI by the download, and the probe, which runs only when FaderInfo was not reported since Connect, has its reason
#     to run),
#   - wants USBPcap running from BEFORE the WinUSB bind and interface 1 bound to WinUSB before the backend starts,
#   - starts the backend with hardware mode, live control and the state at Connect confirmed for today, and the UI,
#   - asks after Connect what the console did, what the UI shows and what became of the probe, then the read-only minute and W1,
#   - ends with Disconnect at an idle point, ETC's driver back on interface 1, the SD save for the byte comparison and the list of
#     files to copy; if a fault cuts the download it first leads the recovery, one SmartSoft Online start (SC-4 clause 2).
# Abort rules (SC-1 / SC-2 / SC-4 / SC-5): any Error in the UI ends the session (Disconnect; read the backend log before anything
# else; never reconnect before); anything the console does that you did not cause ends it (note the time); never power-cycle the
# console to recover from a fault; if INCOMPLETE-WRITE.txt appears in the capture folder, nothing connects again until you have
# read the log and removed the file yourself.
# -LogFile is mandatory and must be a NEW file (action logs are hashed evidence).
param(
    [Parameter(Mandatory = $true)][string]$LogFile,
    [string]$Repo = "",
    [string]$SmartSoft = 'C:\Program Files (x86)\ETC\SmartSoft\Binaries\SmartSoft.exe'
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
if (-not (Test-Path -LiteralPath $SmartSoft)) {
    Write-Host ("SmartSoft was not found at {0}. It is the way out after a cut download (SC-4 clause 2, acceptance 3); pass -SmartSoft <path to SmartSoft.exe>." -f $SmartSoft) -ForegroundColor Red
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
        Log ("STOP: '" + $tag + "' was not answered y; E13 does not start (SC-4)")
        Write-Host "E13 does not start without this answer (SC-4). Nothing was sent. Tell the assistant what is missing." -ForegroundColor Red
        exit 2
    }
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
function Shorten([string]$s, [int]$n) {
    if ($s.Length -gt $n) { return $s.Substring(0, $n) + " ..." }
    return $s
}
# The "### Review" part of the SC-4 section of safety-checks.md (from the line "## SC-4" to the next "## " line), or an empty list.
function Sc4-ReviewSection {
    $out = New-Object System.Collections.Generic.List[string]
    $path = Join-Path $Repo "docs\protocol\safety-checks.md"
    if (-not (Test-Path -LiteralPath $path)) { return ,$out }
    $inSc4 = $false
    $inReview = $false
    foreach ($l in [IO.File]::ReadAllLines($path)) {
        if ($l.StartsWith('## ')) {
            $inSc4 = $l.StartsWith('## SC-4')
            $inReview = $false
            continue
        }
        if (-not $inSc4) { continue }
        if ($l.StartsWith('### ')) { $inReview = $l.StartsWith('### Review') }
        if ($inReview) { $out.Add($l) }
    }
    return ,$out
}
# The Connect outcome as one of three cases: 'downloaded' (n: no refusal line, no Error), 'not sent' (the bar's "The console's state
# was not requested: ..."), 'error' (an Error or Disconnected). Anything else is confirmed once, because 'error' ends the session.
function Connect-Outcome([string]$raw) {
    $a = $raw.Trim().ToLowerInvariant()
    if ($a -eq 'n' -or $a -eq 'no' -or $a -eq '-') { return 'downloaded' }
    if ($a -match 'not requested') { return 'not sent' }
    $c = Ask ("You typed '" + $raw.Trim() + "'. Which is it? (e = an Error or Disconnected: the session ends / r = the line 'The console's state was not requested: ...' / d = neither: the download completed)") "connect outcome confirmation"
    $c = $c.ToLowerInvariant()
    if ($c -eq 'r') { return 'not sent' }
    if ($c -eq 'd') { return 'downloaded' }
    return 'error'
}
# W1's answer as one of three cases: 'refused' (a, or any text with 'refus'), 'followed' (b, y, yes, followed) or 'not followed'
# (c, n, no, or anything else). A stop case is confirmed once, because it ends the session (E11 attempt 2 ended on a mistyped letter).
function W1-Case([string]$raw) {
    $a = $raw.Trim().ToLowerInvariant()
    if ($a -eq 'a' -or $a -match 'refus') { return 'refused' }
    if ($a -eq 'b' -or $a -eq 'y' -or $a -eq 'yes' -or $a -eq 'followed') { return 'followed' }
    $sure = Ask ("You typed '" + $raw.Trim() + "': this means the console did NOT follow a sent event, and the session ENDS (H66 stop). Is that what you saw? (y = yes, end the session / n = the level followed, go on)") "w1 stop confirmation"
    if ($sure.ToLowerInvariant() -eq 'n' -or $sure.ToLowerInvariant() -eq 'no') { return 'followed' }
    return 'not followed'
}
# After a control: the console's answer as the UI shows it, and the abort rule for an Error.
function After-Control([string]$what, [string]$tag) {
    $a = Ask ("After " + $what + ": what did the UI show, in this order: the console's reported change (level or LED), any REFUSAL text, any ERROR text? (describe, or 'followed')") $tag
    if ($a -match '(?i)error') {
        Log ("STOP: an Error was reported after " + $what + "; the session ends (abort rule)")
        Write-Host "An Error ends the session: press DISCONNECT in the UI now, read the backend log before anything else, never reconnect before. Then finish this script's end steps." -ForegroundColor Red
        return $false
    }
    return $true
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

$host.UI.RawUI.WindowTitle = "E13 guided session"
Write-Host "E13 guided session: the first session of our own software with SmartSoft's start-up pair at Connect (SC-4): the console's state and show arrive without a panel action, then the MASTER probe, a read-only minute and channel fader 1." -ForegroundColor Green
Write-Host "Follow the yellow instructions one by one. Press Enter exactly when you START each action. Leave the waits untouched." -ForegroundColor Green
Write-Host "Touch the console ONLY when a yellow step tells you to. The UI's notes describe the state; none of them is an instruction for you (E11b: a hint read as 'not usable' led to two panel presses)." -ForegroundColor Green
Write-Host "Abort rules: an Error in the UI ends the session (Disconnect, read the backend log, never reconnect before); anything the console does that you did" -ForegroundColor Green
Write-Host "not cause ends it (note the time); never power-cycle the console to recover from a fault; INCOMPLETE-WRITE.txt in the capture folder is removed only after reading the log." -ForegroundColor Green
Write-Host "Never save to or load from the SD card while our software is connected: during E12b's save the console stopped answering USB for about 0.85 s (F163); our session would end with a fault." -ForegroundColor Green
Log "START guided E13 session (PC clock, local time)"
Log ("repository: " + $Repo)
$captureDir = Join-Path $env:LOCALAPPDATA "SmartFade\captures"
if (Test-Path -LiteralPath (Join-Path $captureDir "INCOMPLETE-WRITE.txt")) {
    Log "STOP: INCOMPLETE-WRITE.txt exists in the capture folder; E13 does not start"
    Write-Host ("INCOMPLETE-WRITE.txt exists in " + $captureDir + ": a Data write of an earlier session did not complete. Read that session's transfer log first; the backend refuses to connect while the file exists. E13 does not start. Nothing was sent.") -ForegroundColor Red
    exit 3
}

# --- Gate 1: the SC-4 review, the written acceptances (SC-4 "Conditions for E13"), the rig ---
Write-Host ""
Write-Host "SC-4 gate. Read docs/protocol/safety-checks.md, section 'SC-4' (the Decision with clauses 1-6, 'Conditions for E13', 'Implementation' and its review), before answering." -ForegroundColor Yellow
$reviewLines = Sc4-ReviewSection
if ($reviewLines.Count -eq 0) {
    Log "STOP: the SC-4 section of safety-checks.md has no Review part; E13 does not start (SC-4 item 10)"
    Write-Host "The SC-4 section of docs/protocol/safety-checks.md has no 'Review' part yet: the protocol-safety-reviewer pass (SC-4 item 10) is not recorded. E13 does not start. Nothing was sent." -ForegroundColor Red
    exit 2
}
$passLines = @($reviewLines | Where-Object { $_ -cmatch '\bPASS\b' })
Write-Host ("   " + (Shorten $reviewLines[0] 220)) -ForegroundColor White
foreach ($l in ($passLines | Select-Object -First 6)) { Write-Host ("   " + (Shorten $l 220)) -ForegroundColor White }
Log ("sc4 review section: '" + (Shorten $reviewLines[0] 120) + "', lines with PASS: " + $passLines.Count)
if ($passLines.Count -eq 0) {
    Log "STOP: no line of the SC-4 review says PASS; E13 does not start (SC-4 item 10)"
    Write-Host "No line of the SC-4 review says PASS: the review has not passed (or is not recorded as passed). E13 does not start. Nothing was sent." -ForegroundColor Red
    exit 2
}
Require-Yes "Review: does the SC-4 review above record a PASS of the protocol-safety-reviewer, every blocker and major fixed and the minors fixed or accepted in its table, over the code you will run today? (y / n)" "sc4 review passed"
Require-Yes "Acceptance 1 (SC-4 clause 3, the residual): at Connect the device sends SmartSoft's start-up pair BLIND, as SmartSoft does (about 1.3 s and 2.5 s after Connect), and refuses it only if the console already reported NEXT, BLACKOUT, a record function armed or bank 25-48 in this session. SmartSoft was seen sending it in channel and memory mode, with SOLO lit and the MASTER below full, with STACK lit and with a sequence running (F185); never with a menu open, a bump held, a GO fade running or an SD operation under way. In such a state the console's answer is UNKNOWN (the only effect ever observed is a listing; the erase and upload values cannot be represented). You accept that residual and set the starting position below so that it does not arise. (y / n)" "accept clause 3 residual"
Require-Yes "Acceptance 2 (SC-4 clause 2, the blind window): from Connect until the download's last record every control in the UI is refused ('the console's state is being requested / downloaded'); the download took 7.2-7.6 s in 20 SmartSoft sessions (F182), about 10 s after Connect in all; while the MASTER probe runs right after it, every control is refused too ('the MASTER probe is under way'). Nothing done in the UI in those windows reaches the console. (y / n)" "accept blind window"
Require-Yes "Acceptance 3 (SC-4 clause 2, the recovery): if a fault cuts the download, the session ends, our software never repeats the pair, and what the console still holds for a host is UNKNOWN (H116). The way out is ONE SmartSoft Online start (ETC's driver on interface 1, Memory Integrity off), whose own pair and download take whatever is pending; this guide leads that step if it is needed, and SmartSoft stays installed for it. (y / n)" "accept recovery through smartsoft"
Require-Yes "Acceptance 4 (SC-4 clause 4 as amended on 2026-11-02, late evening, the MASTER probe): the download never carries the MASTER (FaderInfo), so once after it the device taps the bump of an UNPATCHED channel (a press and its release, 46.6-76.7 ms apart for every tap of ours in E11 attempt 3, F168) to make the console list FaderInfo. In your show channels 1-24 are all patched and channel 47 is not (no DMX in E9d), so the probe is bump 23 of bank 25-48: the device presses FADERS 25-48 and waits for the console's report (2 s, else the session ends), taps bump 23, waits until the tap's reports are in, presses FADERS 1-24 and waits for the report again (SC-5's automatic switch, page 1 only); no DMX output changes (OBSERVED for channel 47 in E9d), and the console is left in bank 1-24 as it was found. If the probe stops on the way (a write dropped, another bank reported, a record function armed), nothing more of it is sent and it does not switch back: the console may then stay in bank 25-48, which the log names. Our software has never sent this probe nor a bump of bank 25-48: E13 observes both first. If it cannot run or stops, the UI shows 'Move the MASTER once on the console' and this guide asks you for one MASTER movement. (y / n)" "accept master probe"
Require-Yes "Acceptance 5: the acceptances you gave at E11b's gate (SC-2 and SC-5: the automatic switch, the matching rule, the bounded hold with its lost-release risk, the last mover, the panel-bump residual, the hold overrun with a slow console) hold for this session: the probe uses SC-5's switch and bump path, and the sanity control is SC-2's channel fader 1. (y / n)" "accept sc2 and sc5 as at e11b"
Ask "What is on your rig today (fixtures and their DMX addresses)? (describe)" "rig today" | Out-Null
Require-Yes "The rig: channel 1 lights at about 20 % on whatever is patched to it (W1, the last step), the probe's channel 47 is unpatched in this show (no DMX), and nothing else is sent; is that safe? (y / n)" "rig decision"
Write-Host ""
Write-Host "The start-up pair goes in this session ONLY because the SC-4 review passed and you give the go today. E13 clears nothing by itself: a second session" -ForegroundColor Yellow
Write-Host "of ours makes the pair's behaviour towards our host VERIFIED, and SC-4's decision stands or is annotated afterwards." -ForegroundColor Yellow
Require-Yes "Your go for E13 today, after reading SC-4 and answering the acceptances above? (y / n)" "go for e13 today"

# --- Gate 2: safety rule 3, the backup; the versions (panel actions BEFORE the starting position) ---
$backup = Save-Show "the BACKUP before the session (safety rule 3; SC-4 item 3)." "backup taken"
$commit = ""
try { $commit = (& git -C $Repo rev-parse --short HEAD 2>$null) } catch { $commit = "" }
if ([string]::IsNullOrWhiteSpace($commit)) { $commit = "(git not available)" }
$dirty = ""
try { $dirty = (& git -C $Repo status --porcelain 2>$null | Out-String) } catch { $dirty = "" }
if (-not [string]::IsNullOrWhiteSpace($dirty)) { $commit = $commit + " (the working tree has uncommitted changes)" }
Log ("backend commit: " + $commit)
Log ("windows build: " + [System.Environment]::OSVersion.Version.ToString())
$fw = Ask "Firmware: open Menu > Diagnostics on the console, read the software version, then LEAVE the menu (back to the normal screen). What does it say (e.g. 3.0.2.9.0.43)?" "firmware"
if ($fw -notmatch '3\.0\.2\.9\.0\.43') {
    Log "STOP: the firmware is not 3.0.2.9.0.43; E13 does not start (SC-4 item 9: the version gate sends the second message only to 3.0.2.9.0.43)"
    Write-Host "SC-4's version gate (item 9, F59, S11.17) sends the second message only to a console reading 3.0.2.9.0.43: with another firmware E13 cannot do what it is for. E13 does not start; tell the assistant. Nothing was sent." -ForegroundColor Red
    exit 2
}
Ask "Cable: which USB cable and port (e.g. the A-B cable straight into the PC's rear port)?" "cable" | Out-Null

# --- The starting position, a power cycle, then hands off the console ---
Write-Host ""
Write-Host "Starting position on the console: CHANNEL mode (FADERS 1-24 lit, bank 1-24), page 1, every channel fader at ZERO, MASTER at FULL, BUMPS at FULL," -ForegroundColor Yellow
Write-Host "SOLO off, BLACKOUT off (its LED dark, not blinking), NEXT off, STACK unlit, no GO fade and no sequence running, IND 1 / IND 2 off," -ForegroundColor Yellow
Write-Host "no record function armed (REC MEM, EDIT MEM, COPY, REC SEQ dark), no bump held, no menu open (the LCD on its normal screen)." -ForegroundColor Yellow
Confirm-Done "Set that up now." "starting position set (owner)"
Write-Host ""
Write-Host "Now power-cycle the console (off, about 10 s, on). Why: setting the position moved faders, and the console keeps every change listed for the next host" -ForegroundColor Yellow
Write-Host "(F177); after the power cycles of E3b, E8b and E9d only LedStatus and DisplayContent were pending, after E11 attempt 1's nothing (F166; H65, a hypothesis), so the state reaches" -ForegroundColor Yellow
Write-Host "the UI by the download, and the MASTER probe, which runs only when the console has not reported FaderInfo since Connect, has its reason to run." -ForegroundColor Yellow
Step "Power-cycle the console now." "console power cycle start"
Confirm-Done "When the console shows its normal screen again." "console ready after the power cycle (owner)"
$look = Ask "LOOK only, do not touch: is FADERS 1-24 lit, are SOLO, BLACKOUT, NEXT, STACK, IND 1 / IND 2 and the record keys dark, and is the LCD on its normal screen? (y / what differs)" "starting position after the power cycle (looked)"
if (-not (Is-Yes $look)) {
    Log "deviation: the console was not in the starting position after the power cycle (see the answer above)"
    Step "Set ONLY what differs, with keys where you can (a fader moved now makes the console list the MASTER at Connect, and the probe will then not be needed)." "starting position corrected after the power cycle"
    Ask "What did you press or move? (describe)" "starting position correction" | Out-Null
}
Write-Host ""
Write-Host "From now on do NOT touch the console until a yellow step asks you to." -ForegroundColor Yellow
Log "hands off the console from here (owner told)"

# --- Gate 3: USBPcap before the WinUSB bind (SC-2 item 8; H68), the Zadig bind, the INCOMPLETE-WRITE check ---
Write-Host ""
Write-Host "USBPcap must record from BEFORE the WinUSB bind. In an ADMIN PowerShell window start it on the console's root hub, into a NEW file (an absolute path; an admin window starts in C:\Windows\System32):" -ForegroundColor Yellow
# A new file per attempt (a raw capture must never be overwritten).
$pcapName = 'e13-' + (Get-Date -Format 'yyyyMMdd-HHmm') + '.pcap'
Write-Host ('   & "C:\Program Files\USBPcap\USBPcapCMD.exe" -d \\.\USBPcap1 -A --inject-descriptors -o "' + (Join-Path (Get-Location) $pcapName) + '"') -ForegroundColor White
Log ("usbpcap file name suggested to the owner: " + $pcapName)
Confirm-Done "When USBPcap is running." "usbpcap running (owner)"
Log ("memory integrity: " + (MemoryIntegrityState))
Log ("interface 1 driver service before the bind: " + (Interface1Service))
Write-Host ""
Write-Host "Now bind WinUSB to the console's interface 1 with Zadig: Options > List All Devices, pick the entry with" -ForegroundColor Yellow
Write-Host "USB ID 14D5 0200 01 (never the one ending in 00), driver WinUSB, Replace Driver." -ForegroundColor Yellow
Step "Start Zadig's Replace Driver now (skip with Enter if interface 1 is already on WinUSB)." "zadig bind start"
$svc = Interface1Service
while ($svc -ne 'WinUSB') {
    Write-Host ("   Interface 1 is bound to '{0}', not to WinUSB yet." -f $svc) -ForegroundColor Red
    Read-Host "   Finish the Zadig bind, then press Enter to check again" | Out-Null
    $svc = Interface1Service
}
Log "interface 1 driver service after the bind: WinUSB"
if (Test-Path -LiteralPath (Join-Path $captureDir "INCOMPLETE-WRITE.txt")) {
    Log "STOP: INCOMPLETE-WRITE.txt exists in the capture folder; E13 does not start"
    Write-Host ("INCOMPLETE-WRITE.txt exists in " + $captureDir + ": a Data write of an earlier session did not complete. Read that session's transfer log first; the backend refuses to connect while the file exists. E13 does not start. Stop USBPcap.") -ForegroundColor Red
    exit 3
}

# --- The backend with live control and the state at Connect for today, and the UI ---
$today = "{0:yyyy-MM-dd}" -f (Get-Date)
Write-Host ""
Write-Host ("Starting the backend in hardware mode WITH LIVE CONTROL and the STATE AT CONNECT (all three dates must be today, {0}) and the UI in two new windows:" -f $today) -ForegroundColor Yellow
Write-Host ("   dotnet run --project src/backend/SmartFade.Api -- --Device:Mode=Usb --Device:Usb:ConfirmDate={0} --Device:Usb:LiveControlDate={0} --Device:Usb:StartupPairDate={0}" -f $today) -ForegroundColor White
Start-Process -FilePath "dotnet" -ArgumentList @("run", "--project", "src/backend/SmartFade.Api", "--", "--Device:Mode=Usb", ("--Device:Usb:ConfirmDate=" + $today), ("--Device:Usb:LiveControlDate=" + $today), ("--Device:Usb:StartupPairDate=" + $today)) -WorkingDirectory $Repo | Out-Null
Log ("backend started by the script: dotnet run --project src/backend/SmartFade.Api -- --Device:Mode=Usb --Device:Usb:ConfirmDate=" + $today + " --Device:Usb:LiveControlDate=" + $today + " --Device:Usb:StartupPairDate=" + $today)
Start-Process -FilePath "cmd.exe" -ArgumentList @("/k", "npm start") -WorkingDirectory (Join-Path $Repo "src/frontend") | Out-Null
Log "ui dev server started by the script: npm start in src/frontend"
Write-Host "   Open http://localhost:4200 when the UI window says it is ready (the API alone is at http://localhost:5160). BEFORE you press Connect the bar must show" -ForegroundColor Yellow
Write-Host "   HARDWARE, LIVE CONTROL and STATE AT CONNECT, and the line 'Before Connect: no menu open on the console, no SD save or load, no record function armed," -ForegroundColor Yellow
Write-Host "   no bump held': a reminder of the starting position you have set, it asks nothing of you. The panels say 'Not reported by the console yet'." -ForegroundColor Yellow
Confirm-Done "When the UI shows HARDWARE, LIVE CONTROL and STATE AT CONNECT." "ui ready, three badges (owner)"
Ask "Is anything other than the bar's CONNECT button, its view buttons (Live, Patch, Stack / Seq) and its Show menu (files on this PC only) enabled now? (n / what)" "ui enabled controls before connect" | Out-Null

# --- Connect: the pair, the download and the probe, the console untouched ---
Write-Host ""
Write-Host "   (From here until Disconnect: no SD save or load on the console, no menu.)" -ForegroundColor Yellow
Write-Host "After you press Connect keep your hands OFF the console and WATCH it for about 15 seconds: the FADERS LEDs (1-24 / 25-48), the bump LEDs, the LCD and the stage." -ForegroundColor Yellow
Step "Press CONNECT in the UI (one click), then only watch the console and the bar." "connect pressed"
Confirm-Done "When the bar's line 'Requesting the console's state...' or 'Downloading the console's state... N / M' has gone (about 10 s after Connect)." "bar: the download line has gone (owner)"
Ask "Console, from Connect until now: did the FADERS 25-48 LED light briefly and FADERS 1-24 come back? (y / n / describe, in the order you saw it)" "console faders leds during connect" | Out-Null
Ask "Console: did one bump LED light or blink once? Which one (count from the left, 1-24)? (n / which)" "console bump led during connect" | Out-Null
Ask "Stage: did any light come up, go down or flicker? (n / describe)" "stage during connect" | Out-Null
Ask "Console LCD: what did it show during those seconds, and what does it show now (both lines, as exactly as you can)? (describe)" "lcd during and after connect" | Out-Null
Ask "Bar: if you saw the download's count ('Downloading the console's state... N / M'), type M; else '-'" "download record count seen" | Out-Null
$st = Ask "Bar: does it show the line 'The console's state was not requested: ...', an Error, or Disconnected? Type n if none of them, else the text exactly." "state at connect refusal or error"
$outcome = Connect-Outcome $st
Log ("connect outcome: " + $outcome)
$ok = ($outcome -eq 'downloaded')
if ($outcome -eq 'not sent') {
    Log "STOP: the start-up pair was not sent (the reason above); no control is tried; E13 is not done (the reason goes to the assistant)"
    Write-Host "The device did not send the pair (its reason is in the bar and the backend log). Nothing is tried in this session: go to the end steps; never press Connect again to retry." -ForegroundColor Red
}
if ($outcome -eq 'error') {
    Log "STOP: an Error at Connect, during the download or the probe; the session ends (abort rule)"
    Write-Host "An Error ends the session. Do not press Connect again. The end steps below ask whether the download was cut and lead the recovery if it was." -ForegroundColor Red
}

# --- What the UI shows after the download, and the probe's outcome ---
if ($ok) {
    Ask "LIVE view: which bank does the FADERS column light, which mode and page does the UI show, and do the Record and Crossfader panels show the key LEDs? (describe)" "ui mode bank page keys after download" | Out-Null
    Ask "LIVE view: the channel levels and outputs (all at zero?), IND 1 / IND 2 (off?), the bar's 'Show' and 'Firmware' entries; is any 'Not reported by the console yet' left, and where? (describe)" "ui levels independents file name firmware after download" | Out-Null
    Ask "Memories of page 1: which ones show as recorded (your show: 1-8 and 13, as SHOW31.ASC holds), and has the line 'Dimmed memories were not delivered by the console in this session' gone? (describe)" "ui memories after download" | Out-Null
    Step "Click the bar's PATCH view (a view of the UI: nothing is sent; look only, click no cell)." "ui patch view opened"
    Ask "PATCH: does it show the console's patch (in your show DMX 47 drives channel 50 = IND 2, DMX 97 drives channel 24, channels 47 and 49 are on no address, as the SHOW files say)? (y / what it shows)" "ui patch after download" | Out-Null
    Step "Click the bar's STACK / SEQ view (look only, click no step)." "ui stack view opened"
    Ask "STACK / SEQ: does it show the stack's steps and the sequence KOTLIK with its steps? (y / what it shows)" "ui stack after download" | Out-Null
    Step "Click the bar's LIVE view again." "ui live view again"
    $probe = Ask "Masters panel: does it show the MASTER and BUMPS levels (type levels), or the line 'Move the MASTER once on the console' (type hint, then the reason shown when you hover over it), or still 'Not reported by the console yet' (type not reported)?" "ui master after download"
    if ($probe -match '(?i)hint|move the master|not reported') {
        Step "MASTER (the one panel action SC-4 keeps for this case): on the CONSOLE move the MASTER down a little and back to FULL." "master moved on the panel (probe not effective)"
        Wait-Idle 5 "master back at full"
        Ask "Has the hint gone, and does the Masters panel show the MASTER at full now? (y / what it shows)" "ui master after the panel movement" | Out-Null
    }
    $bank = Ask "Console: is it in bank 1-24 now (FADERS 1-24 lit), as before Connect? (y / n: which FADERS key is lit)" "console bank after the probe"
    $w1Note = ""
    if (-not (Is-Yes $bank)) {
        Log "the console is not in bank 1-24 after the probe (the probe may have stopped on the way: the backend log says where)"
        $w1Note = " The console is not in bank 1-24: the device presses FADERS 1-24 first (SC-5's automatic switch), then the fader."
    }

    # --- The read-only minute ---
    Wait-Idle 60 "read-only minute after the download: nothing is sent unless the UI asks"
    Ask "During the minute: did anything on the console or in the UI change by itself? (n / describe)" "read-only minute observation" | Out-Null

    # --- W1: SC-2's sanity control, channel fader 1 ---
    Step ("W1 (SC-2's sanity control): in the UI, drag CHANNEL FADER 1 slowly to about 20 % and release it (its first event carries the level the console reported, 0, then your drag; SC-5)." + $w1Note) "w1 channel fader 1 to 20"
    Wait-Idle 10 "after w1 up"
    $h66 = Ask "W1: three cases. (a) The UI shows 'Refused: ...': type a, or the refusal text (nothing was sent). (b) The fader went out and the UI's channel 1 level followed within a second: type b or y. (c) The fader went out (no refusal) and the level did NOT follow: type c or no." "h66 channel 1 followed"
    $answer = W1-Case $h66
    if ($answer -eq 'refused') {
        Log "W1 refused by the device, nothing sent; the cause is in the refusal text"
        Step "W1: a refusal means the console's state is not reported as SC-2 needs it (most likely the MASTER): on the CONSOLE move the MASTER down a little and back to FULL, wait 10 s, then drag CHANNEL FADER 1 in the UI to about 20 % once more." "w1 retry after the master on the panel"
        Wait-Idle 10 "after w1 retry"
        $h66 = Ask "W1 retry: (a) refused again: type a or the text; (b) sent and followed: b or y; (c) sent and not followed: c or no." "h66 channel 1 followed retry"
        $answer = W1-Case $h66
    }
    if ($answer -eq 'refused') {
        Log "STOP: W1 refused twice; no event was sent; the session ends without a control (the cause goes to the assistant)"
        Write-Host "Refused twice: nothing was sent. Go to the end steps; the refusal texts go to the assistant." -ForegroundColor Red
        $ok = $false
    } elseif ($answer -ne 'followed') {
        Log "STOP: the console did not follow channel fader 1 (H66 for writes); the session ends, nothing is retried"
        Write-Host "The console did not follow a sent event: go to the end steps (the session ends, nothing is retried)." -ForegroundColor Red
        $ok = $false
    }
}
if ($ok) {
    Step "W1: in the UI, drag CHANNEL FADER 1 back to ZERO." "w1 channel fader 1 to zero"
    Wait-Idle 10 "after w1 down"
    $ok = After-Control "channel fader 1" "w1 result"
}
if ($ok) { Log "E13 steps covered: Connect with the pair and the download, the UI's state, the probe (or the MASTER from the panel), the read-only minute, W1" } else { Log "E13 stopped before its steps were covered (see the STOP line above)" }
if ($outcome -ne 'error') { Wait-Idle 10 "end of actions, an idle point" }

# --- End: Disconnect at an idle point, the recovery if the download was cut, the driver, the save ---
if ($outcome -ne 'error') {
    Step "Press DISCONNECT in the UI (at an idle point: nothing pending)." "disconnect pressed"
    Confirm-Done "When the UI shows Disconnected." "disconnected (owner)"
} else {
    Step "Press DISCONNECT in the UI if it still shows Connected (else just press Enter here). Do NOT press Connect again." "disconnect after the error"
}
Ask "Does the UI or the backend window show a message about a key still held, a release that could not be sent, or a write that did not complete? Type n if there is no such message, else its text." "end-of-session report" | Out-Null
if (Test-Path -LiteralPath (Join-Path $captureDir "INCOMPLETE-WRITE.txt")) {
    Log "INCOMPLETE-WRITE.txt exists after the session"
    Write-Host ("INCOMPLETE-WRITE.txt exists in " + $captureDir + ": read the transfer log of this session before anything else; remove the file yourself only after that.") -ForegroundColor Red
}
Confirm-Done "Close the backend window (Ctrl+C in it). Keep USBPcap RUNNING for now." "backend stopped (owner)"
$recover = $false
if ($outcome -eq 'error') {
    $cut = Ask "Before the Error, did the bar's download count reach its end (N = M), or was the download cut? (complete / cut / unsure)" "download cut or complete"
    $recover = ($cut.ToLowerInvariant() -ne 'complete')
}
if ($recover) {
    Log "RECOVERY (SC-4 clause 2): one SmartSoft Online start takes whatever the cut download left pending; USBPcap keeps recording"
    Write-Host ""
    Write-Host "Recovery (acceptance 3): put ETC's driver back on interface 1 and start SmartSoft ONCE; its own pair and download take whatever is pending. USBPcap keeps recording." -ForegroundColor Yellow
    $mi = MemoryIntegrityState
    Log ("memory integrity: " + $mi)
    Write-Host "Device Manager > the console's interface-1 device > Update driver > Browse my computer > Let me pick > 'USB High-Speed-Pipe' (Memory Integrity must be off)." -ForegroundColor Yellow
    $svc = Interface1Service
    while ($svc -ne 'ETCHSP') {
        $r = Read-Host ("   Interface 1 is bound to '" + $svc + "'. Switch the driver, then press Enter to check again (or type later if it cannot be done today)")
        if ($r.Trim().ToLowerInvariant() -eq 'later') { break }
        $svc = Interface1Service
    }
    if ($svc -eq 'ETCHSP') {
        Log "interface 1 driver service: ETCHSP (ETC's driver) for the recovery"
        Start-SmartSoft 1
        Wait-Idle 60 "SmartSoft's own pair and download after the cut download"
        Close-SmartSoft 1
    } else {
        Log "RECOVERY POSTPONED: ETC's driver could not be bound today; the next SmartSoft Online start is the recovery; our backend is not started before it"
        Write-Host "The recovery waits: start SmartSoft Online once (ETC's driver, Memory Integrity off) before our backend connects to the console again, and tell the assistant." -ForegroundColor Red
    }
}
Confirm-Done "Stop USBPcap now (Ctrl+C in its window)." "usbpcap stopped (owner)"
$svc = Interface1Service
if ($svc -ne 'ETCHSP') {
    Write-Host ""
    Write-Host "Put ETC's driver back on interface 1 (SmartSoft and E14 / E15 need it): Device Manager > the console's interface-1 device > Update driver > Browse my computer > Let me pick > 'USB High-Speed-Pipe'." -ForegroundColor Yellow
    while ($svc -ne 'ETCHSP') {
        $r = Read-Host ("   Interface 1 is bound to '" + $svc + "'. Switch the driver, then press Enter to check again (or type skip to leave it for now)")
        if ($r.Trim().ToLowerInvariant() -eq 'skip') { break }
        $svc = Interface1Service
    }
}
Log ("interface 1 driver service at the end: " + (Interface1Service))
Ask "Physical fader positions at the end (channel 1, BUMPS, MASTER), the FADERS LED that is lit, and the LEDs of SOLO and BLACKOUT? (describe)" "end positions" | Out-Null
$after = Save-Show "the after-session file; it must equal the backup byte for byte (no show data may have changed: F184 for the pair; the probe and channel fader 1 are live controls)." "after-session save"
Ask "Did you touch the console, click anything in the UI, or run anything during the session that this guide did not ask for? (n / what and when)" "unasked actions" | Out-Null
Log "END guided E13 session"
Write-Host ""
Write-Host "SC-4's conditional GO covers this ONE first session with the pair, nothing more, and E13 clears nothing by itself: do not start the backend with" -ForegroundColor Yellow
Write-Host "--Device:Usb:StartupPairDate again before the assistant has recorded E13 and SC-4 is annotated. Any other later backend run is OUTSIDE this session's conditions: start USBPcap first" -ForegroundColor Yellow
Write-Host "(a new file name) and save the show to the SD card afterwards, or leave the console alone (E11 attempt 3 and E11b: later runs had no USBPcap and no SD pair)." -ForegroundColor Yellow
Write-Host ""
Write-Host "Files to copy into docs/protocol/captures/ (with the date prefix ...-e13-...):" -ForegroundColor Green
Write-Host ("   action log:   " + $LogFile) -ForegroundColor White
Write-Host ("   backend log:  " + $captureDir + " (the newest .jsonl: the evidence of every transfer our software made)") -ForegroundColor White
Write-Host "   USBPcap file: the .pcap you named when starting USBPcapCMD (split to the console before it is committed)" -ForegroundColor White
Write-Host ("   SD files:     " + $backup + " (backup) and " + $after + " (after) into smartfade-sd-card-backup/; they must be byte-identical") -ForegroundColor White
