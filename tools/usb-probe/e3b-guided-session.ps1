# Guided operator session "E3b" (2026-10-30): the passive captures proposed by the E3 analysis
# (reverse-engineering.md, "Next capture (proposed by the E3 analysis)", items B1-B4 and the
# optional GO). A USBPcap capture must already be running. The script
#   - asks for the SD-card show backup made just now (safety rule 3) and logs "backup taken",
#   - starts SmartSoft itself (Start-Process) only after that line, three times (H31 restart test),
#   - shows one instruction at a time; the operator presses Enter at the moment an action starts,
#   - asks the owner a few zero-risk questions and logs the answers,
# and stamps every line with the PC clock (the clock USBPcap uses). Nothing here talks to the
# console: only SmartSoft does, exactly as when the owner starts it from the Start menu.
# -LogFile is mandatory and must be a NEW file (action logs are hashed evidence).
param([Parameter(Mandatory = $true)][string]$LogFile)

if (Test-Path -LiteralPath $LogFile) {
    Write-Host ("Refusing to append to the existing file {0}: action logs are hashed evidence. Choose a new file name." -f $LogFile) -ForegroundColor Red
    exit 1
}
$SmartSoft = 'C:\Program Files (x86)\ETC\SmartSoft\Binaries\SmartSoft.exe'
$enc = New-Object System.Text.UTF8Encoding $false
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
function Start-SmartSoft([int]$n) {
    Write-Host ""
    Write-Host ("Starting SmartSoft (session #{0}) ..." -f $n) -ForegroundColor Yellow
    Start-Process -FilePath $SmartSoft -WorkingDirectory (Split-Path $SmartSoft) | Out-Null
    Log ("smartsoft start #" + $n + " (Start-Process by the script)")
    Confirm-Done "Wait until SmartSoft shows ONLINE." ("smartsoft online #" + $n + " (owner)")
}
function Close-SmartSoft([int]$n) {
    Step "Close SmartSoft now (the close button of its window). Do not save anything if it asks." ("smartsoft close #" + $n + " start")
    Confirm-Done "When SmartSoft is closed." ("smartsoft closed #" + $n)
}

$host.UI.RawUI.WindowTitle = "E3b guided session"
Write-Host "E3b guided session. A USB capture should already be running in another window; leave it alone." -ForegroundColor Green
Write-Host "Follow the yellow instructions one by one. Press Enter exactly when you START each action." -ForegroundColor Green
Log "START guided E3b session (PC clock, local time)"

Write-Host ""
Write-Host "Safety rule 3: save the show to the SD card on the console NOW (Menu > Save), before SmartSoft starts." -ForegroundColor Yellow
$backup = ""
while ([string]::IsNullOrWhiteSpace($backup)) {
    $backup = Read-Host "   Type the file name the console used for the backup (e.g. SHOW05.ASC); it is required"
}
Log ("backup taken: " + $backup.Trim())

Ask "Was the console switched OFF at any time since yesterday's capture? (y / n / unsure)" "console powered off since E3"
Ask "Is anything connected to the console's DMX IN socket? (y / n / unsure)" "DMX IN connected"
Ask "Is the SOLO button lit right now? (y / n)" "SOLO lit at start"
Ask "Which fader is directly LEFT of MASTER: BUMPS, or something else? (type the label)" "fader left of MASTER"
Write-Host ""
Write-Host "Starting position: all channel faders at ZERO, MASTER at FULL, BUMPS at ZERO, faders in CHANNEL mode (1-24), BLACKOUT off." -ForegroundColor Yellow
Confirm-Done "Set that up now." "starting position set (owner)"

# --- B1: H31 restart test: three SmartSoft sessions without a console power cycle ---
Start-SmartSoft 1
Wait-Idle 15 "session 1 idle"
Close-SmartSoft 1
Wait-Idle 10 "between sessions 1 and 2"
Start-SmartSoft 2
Wait-Idle 15 "session 2 idle"
Close-SmartSoft 2
Wait-Idle 10 "between sessions 2 and 3"
Start-SmartSoft 3
Wait-Idle 15 "session 3 idle"

# --- B2: a channel at full while MASTER moves (H47, H57) ---
Step "CHANNEL FADER 5: move it slowly ALL THE WAY UP (fixture 1 should light)." "fader5 up start"
Confirm-Done "When it is at the TOP (hands off)." "fader5 at full"
Wait-Idle 5 "fader 5 at full, MASTER full"
Step "MASTER fader: move it slowly down to about HALF." "master to half start"
Confirm-Done "When it is at about half (hands off)." "master at half"
Wait-Idle 5 "MASTER at half, fader 5 full"
Step "MASTER fader: move it slowly back to FULL." "master to full start"
Confirm-Done "When it is at FULL." "master at full"
Wait-Idle 5 "MASTER full again"
Ask "What did fixture 1 do while MASTER went down and up? (e.g. dimmed and came back)" "fixture 1 during MASTER move"
Step "CHANNEL FADER 5: move it slowly back to ZERO." "fader5 down start"
Confirm-Done "When it is at ZERO." "fader5 at zero"
Wait-Idle 5 "fader 5 at zero"

# --- B3: the BUMPS fader and bumps in both modes, SOLO off and on (H46, H54) ---
Step "BUMPS fader: move it slowly from ZERO to FULL." "bumps up start"
Confirm-Done "When it is at FULL." "bumps at full"
Wait-Idle 5 "BUMPS at full"
Step "BUMPS fader: move it slowly back to ZERO." "bumps down start"
Confirm-Done "When it is at ZERO." "bumps at zero"
Wait-Idle 5 "BUMPS at zero"
Step "BUMPS fader: move it to FULL again and leave it there." "bumps up again start"
Confirm-Done "When it is at FULL." "bumps at full again"
Wait-Idle 5 "BUMPS at full"
Step "CHANNEL mode: press and HOLD the bump button under CHANNEL fader 7. Keep holding it." "channel bump 7 press"
Confirm-Done "Now RELEASE it." "channel bump 7 release"
Ask "What did the lights do during that bump? (e.g. fixture 1 blue came on)" "lights during channel bump 7"
Wait-Idle 5 "after channel bump"
Step "Switch the faders to MEMORY mode (the bump LEDs turn red)." "memory mode start"
Confirm-Done "When the bump LEDs are red." "memory mode on"
Ask "What colour is the LED under fader 21 now, and under fader 1?" "LED colours fader 21 and 1 in memory mode"
Wait-Idle 3 "memory mode"
Step "Press and HOLD the bump button under MEMORY fader 1 (SOLO off). Keep holding it." "memory bump 1 press, SOLO off"
Confirm-Done "Now RELEASE it." "memory bump 1 release"
Ask "What did the lights do during that bump?" "lights during memory bump 1, SOLO off"
Wait-Idle 5 "after memory bump, SOLO off"
Step "Press the SOLO button once (it should light)." "SOLO press"
Confirm-Done "Is SOLO lit now?" "SOLO on (owner)"
Wait-Idle 3 "SOLO on"
Step "Press and HOLD the bump button under MEMORY fader 1 again (SOLO on). Keep holding it." "memory bump 1 press, SOLO on"
Confirm-Done "Now RELEASE it." "memory bump 1 release, SOLO on"
Ask "What did the lights do this time?" "lights during memory bump 1, SOLO on"
Wait-Idle 5 "after memory bump, SOLO on"
Step "Press SOLO again to switch it off." "SOLO release"
Confirm-Done "When SOLO is off (not lit)." "SOLO off (owner)"
Wait-Idle 3 "SOLO off"
Step "BUMPS fader: move it back to ZERO." "bumps down final start"
Confirm-Done "When it is at ZERO." "bumps at zero final"
Wait-Idle 3 "BUMPS at zero"

# --- B4: the 25-48 fader bank (H59, H53, H56) ---
Step "Switch the faders back to CHANNEL mode (press the 1-24 FADERS button)." "channel mode 1-24 start"
Confirm-Done "When the console is in channel mode 1-24." "channel mode 1-24 on"
Wait-Idle 3 "channel mode 1-24"
Step "Press the 25-48 FADERS button." "bank 25-48 press"
Confirm-Done "When the console shows the 25-48 bank." "bank 25-48 on"
Wait-Idle 3 "bank 25-48"
Step "Physical fader 1 (leftmost): move it slowly ALL THE WAY UP, then after a moment ALL THE WAY DOWN." "fader1 in bank 25-48 move start"
Confirm-Done "When it is back at ZERO." "fader1 in bank 25-48 at zero"
Wait-Idle 5 "after bank 25-48 move"
Step "Press the 1-24 FADERS button again." "bank 1-24 press"
Confirm-Done "When the console shows the 1-24 bank." "bank 1-24 on"
Wait-Idle 3 "bank 1-24"

# --- optional: GO on the stack (H50) ---
$go = Read-Host "Optional: press GO on the crossfader section once (the stack runs step 1 with 5 s fades; lights may change)? (y / n)"
Log ("owner decision: GO step = " + $go)
if ($go -match '^[yY]') {
    Step "Press GO once." "GO press"
    Wait-Idle 12 "after GO"
    Ask "What did the lights and the console display do after GO?" "after GO"
}

Close-SmartSoft 3
Wait-Idle 5 "after close"
Log "END guided E3b session"
Write-Host ""
Write-Host "All done. Tell Claude in the chat that you finished; Claude stops the capture." -ForegroundColor Green
Read-Host "   Press Enter to close this window" | Out-Null
