# Guided operator session "E15" (written 2026-11-02, late evening, by an Opus 5.5 agent at the owner's request; parse-checked,
# not run): the passive capture of the four record flows from SmartSoft's Console Simulator (docs/protocol/reverse-engineering.md,
# "Owner feedback after playing", the E15 line; SC-3 needs it first): REC MEM, EDIT MEM and COPY, which E9 and E9b drove from the
# Simulator (known-facts F136, F145, F146), and REC SEQ, which no PC was ever seen sending. SNAPSHOT stays out. SmartSoft's
# Simulator sends, USBPcap records; this script only logs, starts SmartSoft and compares files on the PC; our own software is not
# involved and sends nothing.
# Show data changes ON PURPOSE: two EMPTY memories of page 1 named by the owner (N recorded with channel 1 at about half, then
# edited (channel 2 added at full), then copied to M) and an UNUSED sequence fader of page 1 (S, two channel steps: channel 1,
# then channel 2). The console lists what it stores (E9, E9b); none of it is reversible by the protocol. Restoration: after the
# session, SmartSoft closed and nothing connected, the console's own Load show of the SD backup (console manual v3.0 p. 6,
# DOCUMENTED), then a new SD save compared with the backup byte for byte (SHA-256, here or later).
# The flows as the console manual v3.0 describes them (DOCUMENTED; known-facts F107, F149): REC MEM + a bump stores the look at
# the press and returns to the previous fader mode; EDIT MEM + a bump selects a memory for editing, EDIT MEM again stores, CLEAR
# aborts; COPY + a source bump + a target bump; REC SEQ + a location bump (21-24), then one bump per step (a channel in channel
# mode), REC SEQ again ends (printed p. 37). The step times stay as the console offers them: the wheel is never touched (owner
# decision). The LCD texts quoted as "seen" are E8 / E8b / E9 / E9b observations (F116, the E9 record), never a promise.
# Lessons built in: E9 edited memory 7 through a wrong bump (F139): every selection is checked on the LCD ("01/NN") before
# anything is changed, an empty target is checked by its LED before it is pressed, and if anything is not as expected ONE press
# of CLEAR aborts the record function without storing (F107, F109). A look into SmartSoft's editor tabs can send a write (E12,
# F158): only the Simulator window is used.
# Never in E15: SNAPSHOT, MAGIC, UNDO, the wheel, RATE, GO, STACK, NEXT, PAUSE, BLACKOUT, SOLO, IND 1 / 2, page changes, CLEAR
# except as the abort, any SmartSoft edit or tab (Patch, Independents, Settings, labels, Mem Edit, steps), Open showfile, New show,
# Save As, any Erase, and the console's own keys unless a step asks.
# -LogFile is mandatory and must be a NEW file (action logs are hashed evidence).
param(
    [Parameter(Mandatory = $true)][string]$LogFile,
    [string]$Repo = "",
    [string]$SmartSoft = 'C:\Program Files (x86)\ETC\SmartSoft\Binaries\SmartSoft.exe'
)

# $PSScriptRoot is not usable in a parameter default under -File on Windows PowerShell 5.1. The repository is only used to
# suggest the folder for the SD comparison (smartfade-sd-card-backup/).
if ([string]::IsNullOrWhiteSpace($Repo)) {
    $scriptPath = $MyInvocation.MyCommand.Path
    $Repo = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $scriptPath))
}
$defaultFolder = ""
if (Test-Path -LiteralPath (Join-Path $Repo "SmartFade.sln")) { $defaultFolder = Join-Path $Repo "smartfade-sd-card-backup" }
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
# A step inside a record function: typing abort instead of pressing Enter means the console asks for something the step does not
# name; the caller then aborts with one CLEAR.
function Step-Or-Abort([string]$instruction, [string]$tag) {
    Write-Host ""
    Write-Host $instruction -ForegroundColor Yellow
    $r = Read-Host "   Press Enter at the moment you START doing this (or type abort if the console asks for something this step does not name)"
    if ($r.Trim().ToLowerInvariant() -eq 'abort') {
        Log ($tag + ": NOT done, the owner typed abort")
        return $false
    }
    Log $tag
    return $true
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
        Log ("STOP: '" + $tag + "' was not answered y; E15 does not start")
        Write-Host "E15 does not start without this answer. Nothing was sent; SmartSoft was not started. Tell the assistant what is missing." -ForegroundColor Red
        exit 2
    }
}
# A number from the owner, one of $allowed and none of $taken.
function Ask-Number([string]$question, [string]$tag, [int[]]$allowed, [int[]]$taken) {
    while ($true) {
        $a = Ask $question $tag
        $n = 0
        if ([int]::TryParse($a, [ref]$n) -and ($allowed -contains $n) -and -not ($taken -contains $n)) { return $n }
        Write-Host ("   '{0}' is not one of {1}, or it is already chosen in this session. Again, please." -f $a, ($allowed -join ', ')) -ForegroundColor Red
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
# SmartSoft's start: any dialog it shows is typed before it is answered, and answered NO / Cancel (E15 never sends a show).
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
    $b = Read-Host "   Answer it with NO / Cancel / Don't send (E15 never sends a show): type the button you will press, press Enter, then press it"
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
# The two SD files copied to a folder on the PC and compared by SHA-256: 'equal', 'different' or 'skipped'.
function Compare-Sd([string]$before, [string]$after, [string]$tag) {
    Write-Host ""
    Write-Host ("Copy " + $before + " and " + $after + " from the SD card into a folder on the PC (as after every session), then put the card back into the console.") -ForegroundColor Yellow
    $folder = ""
    while ([string]::IsNullOrWhiteSpace($folder)) {
        $folder = (Read-Host ("   The folder's full path (press Enter for '" + $defaultFolder + "')")).Trim().Trim('"')
        if ([string]::IsNullOrWhiteSpace($folder)) { $folder = $defaultFolder }
    }
    $a = Join-Path $folder $before
    $b = Join-Path $folder $after
    while (-not ((Test-Path -LiteralPath $a) -and (Test-Path -LiteralPath $b))) {
        $r = Read-Host ("   Press Enter when " + $a + " and " + $b + " exist (or type skip to compare later)")
        if ($r.Trim().ToLowerInvariant() -eq 'skip') {
            Log ($tag + ": comparison skipped by the owner (to be done later)")
            return 'skipped'
        }
    }
    $ha = Sha256 $a
    $hb = Sha256 $b
    Log ($tag + ": " + $a + " sha256 " + $ha + ", " + $b + " sha256 " + $hb)
    if ($ha -eq $hb) {
        Log ($tag + ": EQUAL byte for byte")
        Write-Host "   Equal byte for byte." -ForegroundColor Green
        return 'equal'
    }
    Log ($tag + ": DIFFERENT")
    Write-Host "   Different." -ForegroundColor Red
    return 'different'
}
# The console's answer at a point of a flow: the LCD verbatim and the LEDs (F116 showed every record screen by its LEDs too).
function Ask-Console([string]$p, [string]$when, [string]$seen) {
    $hint = ""
    if (-not [string]::IsNullOrWhiteSpace($seen)) { $hint = " (" + $seen + ")" }
    Ask ("LCD " + $when + $hint + ": type both lines exactly as shown.") ($p + "lcd " + $when) | Out-Null
    Ask ("LEDs " + $when + ": which keys and bumps are lit or blinking, and in which colour? (describe)") ($p + "leds " + $when) | Out-Null
}
function Flow-End([string]$p, [string]$name) {
    Ask ("End of " + $name + ": did you click anything else in the Simulator or SmartSoft, or touch the console? (n / what)") ($p + "extra actions") | Out-Null
}
# Not as expected: one CLEAR aborts the record function without storing (F107, F109; E9: CLEAR inside EDIT MEM listed no Memory),
# the faders this pass moved go back to zero, and the pass ends.
function Abort-Flow([string]$p, [string]$why) {
    Log ($p + "ABORT: " + $why)
    Write-Host ""
    Write-Host "Not as expected: click SIMULATOR CLEAR ONCE (one press aborts a record function without storing), then nothing else. The rest of this pass is skipped." -ForegroundColor Red
    Step "SIMULATOR CLEAR: click it ONCE." ($p + "abort: sim clear once")
    Wait-Idle 3 ($p + "after the abort")
    Ask-Console $p "after CLEAR" ""
    Step "SIMULATOR: drag channel faders 1 and 2 back to ZERO if either is up (else just press Enter)." ($p + "abort: sim faders 1 and 2 to zero")
    return $false
}
# Still not as expected after one CLEAR: no second CLEAR (two presses in a series clear intensities, F109); the pass ends.
function Stop-Pass([string]$p, [string]$why) {
    Log ($p + "STOP: " + $why)
    Write-Host "Still not as expected: press nothing more in the Simulator; the rest of this pass is skipped." -ForegroundColor Red
    Ask-Console $p "at the stop" ""
    Step "SIMULATOR: drag channel faders 1 and 2 back to ZERO if either is up (else just press Enter)." ($p + "stop: sim faders 1 and 2 to zero")
    return $false
}

function Flow-RecMem([int]$s, [int]$n) {
    $p = "s" + $s + " rec mem "
    Write-Host ""
    Write-Host ("Pass {0}, flow 1 of 4, REC MEM: channel 1 at about half recorded into memory {1} of page 1." -f $s, $n) -ForegroundColor Green
    Step "SIMULATOR channel fader 1: drag it slowly up to about HALF and leave it there (channel 1 lights at about half on the rig)." ($p + "sim fader 1 to half")
    Confirm-Done "When it is at about half." ($p + "sim fader 1 at half")
    Wait-Idle 5 ($p + "fader 1 at half")
    Step "SIMULATOR REC MEM: click it once." ($p + "sim rec mem")
    Wait-Idle 3 ($p + "rec mem screen")
    Ask-Console $p "after REC MEM" "seen in E9 / E9b: 'Record Memory Pg:1' / 'Select Bump or Stack'"
    if (-not (Is-Yes (Ask ("Is the bump under fader {0} blinking BRIGHT red (an empty memory; recorded ones blink dimmed)? (y / n)" -f $n) ($p + "target shown empty")))) {
        return (Abort-Flow $p ("memory " + $n + " is not shown as empty"))
    }
    if (-not (Step-Or-Abort ("SIMULATOR: click the bump under fader {0} (memory {0} of page 1). The console stores the look at this press and returns to channel mode (manual, F107; E9, E9b)." -f $n) ($p + "sim bump " + $n))) {
        return (Abort-Flow $p "stopped before the bump")
    }
    Wait-Idle 5 ($p + "after the store")
    Ask-Console $p "after the bump" "seen in E9 / E9b: back to the channel view"
    if (-not (Is-Yes (Ask "Is the console back in channel mode (FADERS 1-24 lit, REC MEM dark, the LCD's normal screen)? (y / n)" ($p + "back in channel mode")))) {
        return (Abort-Flow $p "the console did not return to channel mode after the store")
    }
    Step "SIMULATOR channel fader 1: drag it slowly back to ZERO." ($p + "sim fader 1 to zero")
    Confirm-Done "When it is at ZERO." ($p + "sim fader 1 at zero")
    Wait-Idle 5 ($p + "after fader 1")
    Flow-End $p "REC MEM"
    return $true
}

function Flow-EditMem([int]$s, [int]$n) {
    $p = "s" + $s + " edit mem "
    $sel = "01/{0:D2}" -f $n
    Write-Host ""
    Write-Host ("Pass {0}, flow 2 of 4, EDIT MEM: channel 2 at full added to memory {1} (a blind edit), then stored." -f $s, $n) -ForegroundColor Green
    Step "SIMULATOR EDIT MEM: click it once." ($p + "sim edit mem")
    Wait-Idle 3 ($p + "edit mem screen")
    Ask-Console $p "after EDIT MEM" "seen in E9: 'Edit Memory' / 'Select Bump or Stack'"
    if (-not (Is-Yes (Ask ("Is the bump under fader {0} blinking (recorded memories blink in EDIT MEM, empty ones stay steady)? (y / n)" -f $n) ($p + "memory offered")))) {
        return (Abort-Flow $p ("memory " + $n + " is not offered for editing"))
    }
    if (-not (Step-Or-Abort ("SIMULATOR: click the bump under fader {0} (selects memory {0} for editing)." -f $n) ($p + "sim bump " + $n))) {
        return (Abort-Flow $p "stopped before the selection")
    }
    Wait-Idle 3 ($p + "memory selected")
    $lcd = Ask ("LCD now: type both lines exactly (seen in E9: 'Edit:' / '01/09' for memory 9; it must name " + $sel + ").") ($p + "lcd after the selection")
    if ($lcd -notmatch [regex]::Escape($sel)) {
        if (-not (Is-Yes (Ask ("You typed '" + $lcd + "'. Does the LCD name " + $sel + " (page 01, memory " + $n + ")? (y / n)") ($p + "selection confirmed")))) {
            return (Abort-Flow $p ("the LCD did not name " + $sel + ": a wrong memory may be selected; aborted without storing (the E9 lesson)"))
        }
    }
    if (-not (Step-Or-Abort "SIMULATOR channel fader 2: drag it slowly up to FULL (the edit: channel 2 into the memory; blind, the stage should not change; the manual p. 44 says intensities may have to be matched by the faders, and channel 2 is at zero in the memory where the fader starts, so nothing should need matching: INFERRED)." ($p + "sim fader 2 to full"))) {
        return (Abort-Flow $p "stopped before the edit")
    }
    Confirm-Done "When it is at FULL." ($p + "sim fader 2 at full")
    Wait-Idle 3 ($p + "edited")
    Ask "During the edit: did anything change on stage, and what do channel 2's bump LED and the LCD show? (describe)" ($p + "during the edit") | Out-Null
    if (-not (Step-Or-Abort "SIMULATOR EDIT MEM: click it once more (stores the edit; the console returns to the fader mode it had before: manual, F107)." ($p + "sim edit mem store"))) {
        return (Abort-Flow $p "stopped before the store")
    }
    Wait-Idle 5 ($p + "after the store")
    Ask-Console $p "after the store" "seen in E9: line 2 'SmartFade Menu' for about 1.4 s, then the fader view"
    Ask "Is anything lit on stage now (channel 2, whose Simulator fader is still at full)? (n / describe)" ($p + "stage after the store") | Out-Null
    Step "SIMULATOR channel fader 2: drag it slowly back to ZERO." ($p + "sim fader 2 to zero")
    Confirm-Done "When it is at ZERO." ($p + "sim fader 2 at zero")
    Wait-Idle 5 ($p + "after fader 2")
    Flow-End $p "EDIT MEM"
    return $true
}

function Flow-Copy([int]$s, [int]$n, [int]$m) {
    $p = "s" + $s + " copy "
    $src = "01/{0:D2}" -f $n
    Write-Host ""
    Write-Host ("Pass {0}, flow 3 of 4, COPY: memory {1} copied to the empty memory {2} of page 1." -f $s, $n, $m) -ForegroundColor Green
    Step "SIMULATOR COPY: click it once." ($p + "sim copy")
    Wait-Idle 3 ($p + "copy screen")
    Ask-Console $p "after COPY" "seen in E9: 'Copy Memory/Seq' / 'Select source'"
    if (-not (Step-Or-Abort ("SIMULATOR: click the bump under fader {0} (the source, memory {0})." -f $n) ($p + "sim source bump " + $n))) {
        return (Abort-Flow $p "stopped before the source")
    }
    Wait-Idle 3 ($p + "source chosen")
    $lcd = Ask ("LCD now: type both lines exactly (seen in E9: 'Copy 01/09' / 'Select target' for memory 9; it must name " + $src + ").") ($p + "lcd after the source")
    if ($lcd -notmatch [regex]::Escape($src)) {
        if (-not (Is-Yes (Ask ("You typed '" + $lcd + "'. Does the LCD name " + $src + " (page 01, memory " + $n + ")? (y / n)") ($p + "source confirmed")))) {
            return (Abort-Flow $p ("the LCD did not name " + $src + ": a wrong source may be selected; aborted before the target"))
        }
    }
    if (-not (Is-Yes (Ask ("Is the bump under fader {0} blinking BRIGHT (an empty target; recorded memories blink dimmed now)? (y / n)" -f $m) ($p + "target shown empty")))) {
        return (Abort-Flow $p ("memory " + $m + " is not shown as an empty target"))
    }
    if (-not (Step-Or-Abort ("SIMULATOR: click the bump under fader {0} (the target, memory {0})." -f $m) ($p + "sim target bump " + $m))) {
        return (Abort-Flow $p "stopped before the target")
    }
    Wait-Idle 5 ($p + "after the copy")
    Ask-Console $p "after the target" "E9: the console wrote the target and listed it"
    # The manual (printed p. 44) says the source is recorded at the target press, not whether COPY ends there: COPY still shown is
    # no fault, one CLEAR leaves it ("press CLEAR to exit").
    if (-not (Is-Yes (Ask "Is the console back in channel mode (FADERS 1-24 lit, COPY dark, the LCD's normal screen)? (y / n = COPY or another screen is still shown)" ($p + "back in channel mode")))) {
        Step "SIMULATOR CLEAR: click it ONCE (it leaves COPY; the copy was stored at the target press, manual v3.0 p. 44: 'As soon as the button is pressed, the source memory is recorded')." ($p + "sim clear leaves copy")
        Wait-Idle 3 ($p + "after clear")
        Ask-Console $p "after CLEAR" ""
        if (-not (Is-Yes (Ask "Is the console in channel mode now (FADERS 1-24 lit, COPY dark)? (y / n)" ($p + "channel mode after clear")))) {
            return (Stop-Pass $p "the console did not leave COPY after one CLEAR")
        }
    }
    Flow-End $p "COPY"
    return $true
}

function Flow-RecSeq([int]$s, [int]$q) {
    $p = "s" + $s + " rec seq "
    Write-Host ""
    Write-Host ("Pass {0}, flow 4 of 4, REC SEQ (never seen from a PC): a sequence of two channel steps, channel 1 then channel 2, on fader {1} of page 1." -f $s, $q) -ForegroundColor Green
    Write-Host "   The console manual v3.0 (printed p. 37): REC SEQ; the location bump (21-24); one bump per step; REC SEQ again ends. No wheel: the steps keep the times the console offers." -ForegroundColor Green
    Write-Host "   Type EVERY LCD prompt exactly: this flow has never been captured. If the console asks for anything a step does not name, type abort at that step." -ForegroundColor Green
    Step "SIMULATOR REC SEQ: click it once." ($p + "sim rec seq")
    Wait-Idle 3 ($p + "rec seq screen")
    Ask-Console $p "after REC SEQ" "seen in E8 / E8b from the console's own key: 'Record Sequence' / 'Select Sequence'"
    if (-not (Is-Yes (Ask ("Is the bump under fader {0} blinking BRIGHT yellow (an empty sequence slot; KOTLIK's fader 21 blinks dimmed)? (y / n)" -f $q) ($p + "location shown empty")))) {
        return (Abort-Flow $p ("fader " + $q + " is not shown as an empty sequence slot"))
    }
    if (-not (Step-Or-Abort ("SIMULATOR: click the bump under fader {0} (the location; the manual: the console then returns to the previous fader mode and every usable bump blinks at full)." -f $q) ($p + "sim location bump " + $q))) {
        return (Abort-Flow $p "stopped before the location")
    }
    Wait-Idle 3 ($p + "location chosen")
    Ask-Console $p "after the location" ""
    if (-not (Step-Or-Abort "Step 1: SIMULATOR: click the bump under fader 1 (channel 1 as the first step)." ($p + "sim step 1 bump 1"))) {
        return (Abort-Flow $p "stopped before step 1")
    }
    Wait-Idle 3 ($p + "after step 1")
    Ask-Console $p "after step 1" ""
    Ask "Did anything light on stage at that press? (n / describe)" ($p + "stage at step 1") | Out-Null
    if (-not (Step-Or-Abort "Step 2: SIMULATOR: click the bump under fader 2 (channel 2 as the second step)." ($p + "sim step 2 bump 2"))) {
        return (Abort-Flow $p "stopped before step 2")
    }
    Wait-Idle 3 ($p + "after step 2")
    Ask-Console $p "after step 2" ""
    Ask "Did anything light on stage at that press? (n / describe)" ($p + "stage at step 2") | Out-Null
    if (-not (Step-Or-Abort "SIMULATOR REC SEQ: click it once more (ends the recording; the manual: the console returns to the fader mode it had before)." ($p + "sim rec seq end"))) {
        return (Abort-Flow $p "stopped before the end")
    }
    Wait-Idle 5 ($p + "after the end")
    Ask-Console $p "after the end" ""
    if (-not (Is-Yes (Ask "Is the console back in channel mode (FADERS 1-24 lit, REC SEQ dark, the LCD's normal screen)? (y / n)" ($p + "back in channel mode")))) {
        return (Abort-Flow $p "the console did not leave REC SEQ")
    }
    Flow-End $p "REC SEQ"
    return $true
}

function Run-Pass([int]$s, [int]$n, [int]$m, [int]$q, [bool]$recSeq) {
    $ok = Flow-RecMem $s $n
    if ($ok) { $ok = Flow-EditMem $s $n }
    if ($ok) { $ok = Flow-Copy $s $n $m }
    if ($ok -and $recSeq) { $ok = Flow-RecSeq $s $q }
    if (-not $recSeq) { Log ("s" + $s + " rec seq skipped: the Simulator has no REC SEQ key") }
    if ($ok) { Log ("s" + $s + " pass complete") } else { Log ("s" + $s + " pass ended early (see the ABORT line above)") }
    return $ok
}

$host.UI.RawUI.WindowTitle = "E15 guided session"
Write-Host "E15 guided session: SmartSoft's Console Simulator drives REC MEM, EDIT MEM, COPY and REC SEQ while USBPcap records; our software is not involved." -ForegroundColor Green
Write-Host "Follow the yellow instructions one by one. Press Enter exactly when you START each action. Leave the waits untouched." -ForegroundColor Green
Write-Host "Only the Simulator window: no SmartSoft tab or cell (a look there can send a write, F158). Touch the console only when a step asks." -ForegroundColor Green
Write-Host "Never: SNAPSHOT, MAGIC, UNDO, the wheel, RATE, GO, STACK, NEXT, PAUSE, BLACKOUT, SOLO, IND, page changes, CLEAR except as the abort, Open showfile, New show, Save As, Erase." -ForegroundColor Green
Log "START guided E15 session (PC clock, local time)"

# --- Gate 1: show data changes on purpose, and the way back ---
Write-Host ""
Write-Host "E15 changes show data ON PURPOSE: two empty memories of page 1 are recorded (one also edited) and a sequence is recorded on an unused" -ForegroundColor Red
Write-Host "sequence fader of page 1, from SmartSoft's Simulator. The console stored such recordings from the Simulator in E9 / E9b (REC SEQ from a PC was never" -ForegroundColor Red
Write-Host "seen); nothing of it can be undone over USB. After the session the SD backup you make next is loaded back on the console; that is the way back." -ForegroundColor Red
Require-Yes "Do you accept that E15 records two memories and a sequence into your console's show on purpose, from SmartSoft's Simulator? (y / n)" "accept show data changes"
Require-Yes "Restoration: after the session, SmartSoft closed and nothing connected, you load the backup on the console from the SD card (Menu > Save/Load Show > Load show, the backup's number chosen with the wheel, confirmed with MENU; console manual v3.0 p. 6), then save the show to a NEW number, and the two files are compared byte for byte. Is that your restoration method? (y / n)" "restoration method"

# --- Gate 2: safety rule 3, the backup; the usual questions; the rig ---
$backup = Save-Show "the BACKUP before the session (safety rule 3; the way back)." "backup taken"
Ask "Was the console switched OFF since the last session? (y / n / unsure)" "console powered off since the last session" | Out-Null
Ask "What is on your rig today (fixtures and their DMX addresses)? (describe)" "rig today" | Out-Null
Require-Yes "The rig: channel 1 lights at about half (REC MEM), channel 2 may light at full after the edit's store until its Simulator fader is back at zero, and the REC SEQ step presses may flash channels 1 and 2; is that safe? (y / n)" "rig decision"

# --- Gate 3: the targets, empty in your show (page 1 only: no page change in E15) ---
Write-Host ""
Write-Host "Targets. In your show (SHOW04.ASC = ... = SHOW31.ASC) page 1 holds memories 1-8 and 13, and its sequence faders are 21-24 with KOTLIK on 21" -ForegroundColor Yellow
Write-Host "(F149, H55). Choose EMPTY ones of page 1: memories among 9-12 and 14-20, a sequence fader among 22-24. The console shows them too:" -ForegroundColor Yellow
Write-Host "an empty slot blinks BRIGHT in the record screens, and the guide asks you to check that before every press." -ForegroundColor Yellow
$emptyMemories = @(9, 10, 11, 12, 14, 15, 16, 17, 18, 19, 20)
$n1 = Ask-Number "Memory N of page 1 for REC MEM and EDIT MEM (empty; one of 9-12, 14-20)?" "target memory n" $emptyMemories @()
$m1 = Ask-Number "Memory M of page 1 for COPY's target (empty; one of 9-12, 14-20; not N)?" "target memory m" $emptyMemories @($n1)
$s1 = Ask-Number "Sequence fader S of page 1 for REC SEQ (unused; 22, 23 or 24)?" "target sequence fader s" @(22, 23, 24) @()
Log ("targets of pass 1: memory " + $n1 + " (rec mem, edit mem), memory " + $m1 + " (copy target), sequence fader " + $s1 + " (rec seq), all page 1")

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
$pcapName = 'e15-' + (Get-Date -Format 'yyyyMMdd-HHmm') + '.pcap'
Write-Host ""
Write-Host "In an ADMIN PowerShell window start USBPcap on the console's root hub, into a NEW file (an absolute path; an admin window starts in C:\Windows\System32):" -ForegroundColor Yellow
Write-Host ('   & "C:\Program Files\USBPcap\USBPcapCMD.exe" -d \\.\USBPcap1 -A --inject-descriptors -o "' + (Join-Path (Get-Location) $pcapName) + '"') -ForegroundColor White
Log ("usbpcap file name suggested to the owner: " + $pcapName)
Confirm-Done "When USBPcap is running." "usbpcap running (owner)"
Write-Host ""
Write-Host "Starting position on the console: CHANNEL mode (FADERS 1-24 lit, bank 1-24), page 1, all channel faders at ZERO, MASTER at FULL, BUMPS at FULL," -ForegroundColor Yellow
Write-Host "BLACKOUT off, SOLO off, IND 1 and IND 2 off, STACK off, NEXT off (unlit), no GO fade and no sequence running, nothing armed, no menu open." -ForegroundColor Yellow
Confirm-Done "Set that up now. From then on touch the console only when a step asks." "starting position set (owner)"

# --- Session 1: SmartSoft Online, the baseline, the Simulator, pass 1 ---
Start-SmartSoft 1
Wait-Idle 60 "baseline: SmartSoft online, nothing touched (its System pair and the download)"
Step "Open SmartSoft's CONSOLE SIMULATOR window. Touch nothing in it afterwards." "s1 simulator opened"
Wait-Idle 15 "s1 simulator open, untouched"
Ask "Which record keys does the SIMULATOR show (for example REC MEM, EDIT MEM, COPY, REC SEQ, MAGIC, SNAPSHOT, UNDO)? Type their labels as shown; press none." "simulator record keys" | Out-Null
$hasRecSeq = Is-Yes (Ask "Does the SIMULATOR have a REC SEQ key? (y / n)" "simulator has rec seq")
$pass1 = Run-Pass 1 $n1 $m1 $s1 $hasRecSeq
Wait-Idle 10 "end of pass 1"

# --- A SmartSoft restart: its download shows the console's show after pass 1, object by object ---
Close-SmartSoft 1
Wait-Idle 10 "between sessions 1 and 2"
Start-SmartSoft 2
Wait-Idle 60 "session 2: SmartSoft's pair and download, the console's show after pass 1"
$last = 2
$targets = @($n1, $m1)
$sequences = @($s1)

# --- Optional, never required: a second pass into three other empty slots, then one more restart ---
$second = $false
if ($pass1) {
    $second = Is-Yes (Ask "Optional, never required: a SECOND pass of the four flows now, in this SmartSoft session, into three OTHER empty slots of page 1 (about 10 minutes), so that the console's answers are seen twice in one capture (as E9d's two passes)? (y / n)" "opt second pass")
}
if ($second) {
    $n2 = Ask-Number "Second pass, memory N2 (empty; one of 9-12, 14-20; not used above)?" "target memory n2" $emptyMemories @($n1, $m1)
    $m2 = Ask-Number "Second pass, memory M2 for COPY's target (empty; one of 9-12, 14-20; not used above)?" "target memory m2" $emptyMemories @($n1, $m1, $n2)
    $s2 = Ask-Number "Second pass, sequence fader S2 (unused; 22, 23 or 24; not S)?" "target sequence fader s2" @(22, 23, 24) @($s1)
    Log ("targets of pass 2: memory " + $n2 + ", memory " + $m2 + ", sequence fader " + $s2 + ", all page 1")
    Step "Open SmartSoft's CONSOLE SIMULATOR window again. Touch nothing in it afterwards." "s2 simulator opened"
    Wait-Idle 15 "s2 simulator open, untouched"
    Run-Pass 2 $n2 $m2 $s2 $hasRecSeq | Out-Null
    Wait-Idle 10 "end of pass 2"
    Close-SmartSoft 2
    Wait-Idle 10 "between sessions 2 and 3"
    Start-SmartSoft 3
    Wait-Idle 60 "session 3: SmartSoft's pair and download after pass 2"
    $last = 3
    $targets = @($n1, $m1, $n2, $m2)
    $sequences = @($s1, $s2)
} else {
    Log "skipped (optional): the second pass"
}

# --- End: SmartSoft closed, USBPcap stopped, the restoration and the SD comparison ---
Close-SmartSoft $last
Confirm-Done "Stop USBPcap (Ctrl+C in its window)." "usbpcap stopped (owner)"
Ask "Physical fader positions at the end (channel faders, MASTER, BUMPS) and the LEDs of SOLO, BLACKOUT, IND 1 / 2, STACK, NEXT? (describe)" "end positions" | Out-Null
$edited = ""
if (Is-Yes (Ask "Optional, recommended: save the EDITED show to the SD card first, to another FREE number (how the console writes the new memories and the sequence into its file, for asc-format.md; the save changes nothing)? (y / n)" "opt edited-show save")) {
    $edited = Save-Show "the EDITED show (evidence, before the restoration)." "edited-show save"
}
Write-Host ""
Write-Host ("Restoration, SmartSoft closed: Menu > Save/Load Show > Load show; choose " + $backup + " with the wheel (the default offered may be another show: check the number on the LCD); confirm with MENU (console manual v3.0 p. 6).") -ForegroundColor Yellow
Step "Load the backup on the console now." "restore: load show from the card start"
Ask "What did the LCD show while loading, and is the console back on its normal screen? (describe)" "restore: load show lcd" | Out-Null
Step ("Check on the CONSOLE: press FADERS MEMS; the bumps of memories " + ($targets -join ', ') + " must be dark (empty) and the bumps of fader(s) " + ($sequences -join ', ') + " not yellow (no sequence); then press FADERS 1-24.") "restore: memory-mode check"
Ask "Were those memories dark and those faders' bumps not yellow, and is the console back in channel mode? (y / describe)" "restore: memory-mode check result" | Out-Null
$after = Save-Show "after the restoration, to compare with the backup (it should equal it byte for byte)." "after-restore save"
$cmp = Compare-Sd $backup $after "after-restore comparison"
if ($cmp -eq 'different') {
    Log "after the restoration the save differs from the backup"
    Write-Host "Different: keep every file, do nothing else to the console's show, and tell the assistant before you use the console for a show (the console's own load-and-save round trip has never been compared: E6)." -ForegroundColor Red
} elseif ($cmp -eq 'skipped') {
    Write-Host "Not compared now: compare the two files soon, before the console is used for a show." -ForegroundColor Yellow
}
Ask "Did you touch the console, click anything in SmartSoft, or run anything during the session that this guide did not ask for? (n / what and when)" "unasked actions" | Out-Null
Log "END guided E15 session"
Write-Host ""
Write-Host "Files to copy into docs/protocol/captures/ (with the date prefix ...-e15-...):" -ForegroundColor Green
Write-Host ("   action log:   " + $LogFile) -ForegroundColor White
Write-Host "   USBPcap file: the .pcap you named when starting USBPcapCMD (split to the console before it is committed)" -ForegroundColor White
if ([string]::IsNullOrWhiteSpace($edited)) {
    Write-Host ("   SD files:     " + $backup + " (backup) and " + $after + " (after the restoration) into smartfade-sd-card-backup/; they must be byte-identical") -ForegroundColor White
} else {
    Write-Host ("   SD files:     " + $backup + " (backup), " + $edited + " (the edited show) and " + $after + " (after the restoration) into smartfade-sd-card-backup/; backup and after must be byte-identical") -ForegroundColor White
}
