# Operator action log for capture experiments (E3 and later).
# Run it in a second terminal while a USBPcap capture is running. Type a short note and press
# Enter at the moment you do something on the console (or in SmartSoft); each line is stamped
# with the PC clock (same clock USBPcap uses), so the capture-analyst can correlate frames.
# Nothing here talks to the console. Stop with an empty line or Ctrl+C.
#
# The log file must be NEW: action logs are hashed evidence, so an existing file is never
# appended to. Default: action-log-<timestamp>.txt in the current directory (move it to
# docs/protocol/captures/ with a date prefix afterwards).
# Revisions: 2026-10-29 (SHA-256 561e525d…0cc5); 2026-10-29 after the protocol-safety review:
# no appending to existing files, fixed banner.
param([string]$LogFile = ("action-log-{0:yyyyMMdd-HHmmss}.txt" -f (Get-Date)))

if (Test-Path -LiteralPath $LogFile) {
    Write-Output ("Refusing to append to the existing file {0}: action logs are hashed evidence. Choose a new file name." -f $LogFile)
    exit 1
}
$enc = New-Object System.Text.UTF8Encoding $false
$full = [IO.Path]::GetFullPath((Join-Path (Get-Location) $LogFile))
Write-Output ("Logging to {0}. Type a note + Enter at the moment of each action; an empty line ends." -f $full)
[System.IO.File]::AppendAllText($full, ("{0:yyyy-MM-dd HH:mm:ss.fff} START (PC clock, local time)`r`n" -f (Get-Date)), $enc)
while ($true) {
    $note = Read-Host "action"
    if ([string]::IsNullOrWhiteSpace($note)) { break }
    $line = "{0:yyyy-MM-dd HH:mm:ss.fff} {1}" -f (Get-Date), $note
    [System.IO.File]::AppendAllText($full, $line + "`r`n", $enc)
    Write-Output ("  logged: " + $line)
}
[System.IO.File]::AppendAllText($full, ("{0:yyyy-MM-dd HH:mm:ss.fff} END`r`n" -f (Get-Date)), $enc)
