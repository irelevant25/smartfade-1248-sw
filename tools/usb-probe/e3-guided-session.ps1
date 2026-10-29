# Guided operator session for experiment E3 (console actions while a USBPcap capture runs).
# Shows one instruction at a time; the operator presses Enter at the moment an action starts,
# and every step is stamped with the PC clock (the clock USBPcap uses). Nothing here talks to
# the console or to SmartSoft; it only writes the action log.
#
# Safety rule 3 gate: the first prompt asks for the name of the SD-card show backup made just
# now; the script logs "backup taken: <name>" and continues only then. Whoever runs the session
# starts SmartSoft only after that line exists in the log.
#
# -LogFile is mandatory and must be a NEW file: action logs are hashed evidence, so the script
# refuses to append to an existing file.
#
# Revisions: 2026-10-29 E3 run (SHA-256 fa5d5ff1…2e12, see the E3 record); 2026-10-29 after the
# protocol-safety review: backup gate, mandatory new log file.
param([Parameter(Mandatory = $true)][string]$LogFile)

if (Test-Path -LiteralPath $LogFile) {
    Write-Host ("Refusing to append to the existing file {0}: action logs are hashed evidence. Choose a new file name." -f $LogFile) -ForegroundColor Red
    exit 1
}

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
function Wait-Idle([int]$seconds, [string]$why) {
    Write-Host ("   Now do not touch anything for {0} seconds ({1}) ..." -f $seconds, $why) -ForegroundColor Cyan
    Start-Sleep -Seconds $seconds
    Log ("wait {0} s done ({1})" -f $seconds, $why)
    Write-Host "   OK." -ForegroundColor Cyan
}

$host.UI.RawUI.WindowTitle = "E3 guided session"
Write-Host "E3 guided session. A USB capture should already be running in another window; leave it alone." -ForegroundColor Green
Write-Host "Follow the yellow instructions one by one. Press Enter exactly when you START each action." -ForegroundColor Green
Log "START guided E3 session (PC clock, local time)"

Write-Host ""
Write-Host "Safety rule 3: save the show to the SD card on the console NOW (Menu > Save), before SmartSoft starts." -ForegroundColor Yellow
$backup = ""
while ([string]::IsNullOrWhiteSpace($backup)) {
    $backup = Read-Host "   Type the file name the console used for the backup (e.g. SHOW05.ASC); it is required"
}
Log ("backup taken: " + $backup.Trim())

$ans = Read-Host "Question: was the console switched OFF at any time since the previous capture? (y / n / unsure)"
Log ("owner answer: console powered off since the previous capture = " + $ans)

Write-Host ""
Write-Host "SmartSoft is started for you now that the backup is logged." -ForegroundColor Yellow
Confirm-Done "Wait until SmartSoft shows ONLINE." "smartsoft online (owner)"
Wait-Idle 20 "idle after Online"

Step "CHANNEL FADER 1 (the leftmost channel fader): move it slowly ALL THE WAY UP, taking about 5 seconds. Do not touch it again until told." "fader1 up start"
Confirm-Done "When it is at the TOP (then hands off)." "fader1 at full"
Wait-Idle 5 "fader 1 at full"
Step "Channel fader 1: move it slowly ALL THE WAY DOWN, about 5 seconds." "fader1 down start"
Confirm-Done "When it is at ZERO." "fader1 at zero"
Wait-Idle 5 "fader 1 at zero"

Step "CHANNEL FADER 5: move it slowly ALL THE WAY UP." "fader5 up start"
Confirm-Done "When it is at the TOP." "fader5 at full"
Wait-Idle 5 "fader 5 at full"
Step "Channel fader 5: move it slowly ALL THE WAY DOWN." "fader5 down start"
Confirm-Done "When it is at ZERO." "fader5 at zero"
Wait-Idle 5 "fader 5 at zero"

Step "Switch the faders to MEMORY mode (the bump LEDs turn red)." "memory mode start"
Confirm-Done "When the bump LEDs are red." "memory mode on"
Wait-Idle 3 "memory mode"
Step "Press and HOLD the BUMP button under MEMORY fader 1 (page 1). Keep holding it." "bump1 press"
Confirm-Done "Now RELEASE the bump button." "bump1 release"
Wait-Idle 5 "after bump"

Step "MASTER fader (rightmost): move it down to about HALF." "master to half start"
Wait-Idle 3 "MASTER at half"
Step "MASTER fader: move it back to FULL." "master to full start"
Wait-Idle 5 "MASTER at full"

Step "Close SmartSoft now (the close button of its window). Do not save anything if it asks." "smartsoft close start"
Confirm-Done "When SmartSoft is closed." "smartsoft closed"
Wait-Idle 5 "after close"

Log "END guided E3 session"
Write-Host ""
Write-Host "All done. Tell Claude in the chat that you finished; Claude stops the capture." -ForegroundColor Green
Read-Host "   Press Enter to close this window" | Out-Null
