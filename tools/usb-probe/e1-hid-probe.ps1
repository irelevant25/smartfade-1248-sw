# E1 (read-only) HID interface probe for the ETC SmartFade 1248 (VID 0x14D5).
# Reads HID attributes and strings through the Windows HID class driver, reconstructs the
# report structure from the preparsed data (Windows exposes no raw report descriptor to user
# mode), then listens passively to input reports for a few seconds.
# Nothing is sent to the console: no output reports, no feature reports, no WriteFile.
# (HidD_GetManufacturerString / HidD_GetProductString make the HID class driver issue standard
# GET_DESCRIPTOR(STRING) requests on endpoint 0; those are reads.)
# Rule for this project: never add HID output/feature reports to this tool. An enabling message
# may only be reproduced after it has been observed from SmartSoft (not SFDownloader) and understood.
# Revision history: 2026-10-28 E1 run; 2026-10-28 after safety review: report grouping fixed
# (field [2] instead of the report-ID byte) and all reports are printed.
param([int]$WantVid = 0x14D5, [int]$ListenSeconds = 10)

$ErrorActionPreference = 'Continue'

$src = @"
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;
public static class HidProbe {
    [DllImport("kernel32.dll", SetLastError=true, CharSet=CharSet.Unicode)]
    static extern SafeFileHandle CreateFile(string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);
    [DllImport("kernel32.dll", SetLastError=true)]
    static extern bool ReadFile(SafeFileHandle h, byte[] buf, int n, out int read, IntPtr overlapped);
    [DllImport("kernel32.dll", SetLastError=true)]
    static extern bool CancelIoEx(SafeFileHandle h, IntPtr overlapped);
    [DllImport("hid.dll", SetLastError=true)] static extern bool HidD_GetAttributes(SafeFileHandle h, byte[] attr);
    [DllImport("hid.dll", SetLastError=true)] static extern bool HidD_GetManufacturerString(SafeFileHandle h, byte[] buf, int len);
    [DllImport("hid.dll", SetLastError=true)] static extern bool HidD_GetProductString(SafeFileHandle h, byte[] buf, int len);
    [DllImport("hid.dll", SetLastError=true)] static extern bool HidD_GetSerialNumberString(SafeFileHandle h, byte[] buf, int len);
    [DllImport("hid.dll", SetLastError=true)] static extern bool HidD_GetPreparsedData(SafeFileHandle h, out IntPtr pp);
    [DllImport("hid.dll", SetLastError=true)] static extern bool HidD_FreePreparsedData(IntPtr pp);
    [DllImport("hid.dll")] static extern int HidP_GetCaps(IntPtr pp, byte[] caps);
    [DllImport("hid.dll")] static extern int HidP_GetValueCaps(int reportType, byte[] caps, ref ushort len, IntPtr pp);
    [DllImport("hid.dll")] static extern int HidP_GetButtonCaps(int reportType, byte[] caps, ref ushort len, IntPtr pp);
    [DllImport("hid.dll")] static extern int HidP_GetLinkCollectionNodes(byte[] nodes, ref uint len, IntPtr pp);
    const int HIDP_STATUS_SUCCESS = 0x00110000;
    public static SafeFileHandle Open(string path, bool read) {
        uint access = read ? 0x80000000u : 0u;
        SafeFileHandle h = CreateFile(path, access, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
        if (h.IsInvalid) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        return h;
    }
    public static byte[] Attributes(SafeFileHandle h) {
        byte[] a = new byte[12]; BitConverter.GetBytes(12).CopyTo(a, 0);
        if (!HidD_GetAttributes(h, a)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        return a;
    }
    public static string Str(SafeFileHandle h, int which) {
        byte[] b = new byte[512]; bool ok;
        if (which == 0) ok = HidD_GetManufacturerString(h, b, 512);
        else if (which == 1) ok = HidD_GetProductString(h, b, 512);
        else ok = HidD_GetSerialNumberString(h, b, 512);
        if (!ok) return "<failed, Win32 error " + Marshal.GetLastWin32Error() + ">";
        string s = System.Text.Encoding.Unicode.GetString(b); int z = s.IndexOf('\0');
        return z >= 0 ? s.Substring(0, z) : s;
    }
    public static IntPtr Preparsed(SafeFileHandle h) {
        IntPtr pp; if (!HidD_GetPreparsedData(h, out pp)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()); return pp;
    }
    public static void Free(IntPtr pp) { HidD_FreePreparsedData(pp); }
    public static byte[] Caps(IntPtr pp) {
        byte[] c = new byte[64]; int st = HidP_GetCaps(pp, c);
        if (st != HIDP_STATUS_SUCCESS) throw new Exception("HidP_GetCaps status 0x" + st.ToString("X8"));
        return c;
    }
    public static byte[] ValueCaps(IntPtr pp, int type, int count) {
        if (count == 0) return new byte[0];
        byte[] c = new byte[72 * count]; ushort n = (ushort)count; int st = HidP_GetValueCaps(type, c, ref n, pp);
        if (st != HIDP_STATUS_SUCCESS) throw new Exception("HidP_GetValueCaps status 0x" + st.ToString("X8"));
        return c;
    }
    public static byte[] ButtonCaps(IntPtr pp, int type, int count) {
        if (count == 0) return new byte[0];
        byte[] c = new byte[72 * count]; ushort n = (ushort)count; int st = HidP_GetButtonCaps(type, c, ref n, pp);
        if (st != HIDP_STATUS_SUCCESS) throw new Exception("HidP_GetButtonCaps status 0x" + st.ToString("X8"));
        return c;
    }
    public static byte[] LinkNodes(IntPtr pp, int count) {
        if (count == 0) return new byte[0];
        byte[] c = new byte[24 * count]; uint n = (uint)count; int st = HidP_GetLinkCollectionNodes(c, ref n, pp);
        if (st != HIDP_STATUS_SUCCESS) throw new Exception("HidP_GetLinkCollectionNodes status 0x" + st.ToString("X8"));
        return c;
    }
    public static List<string> Listen(SafeFileHandle h, int len, int ms, int max) {
        List<string> result = new List<string>();
        Stopwatch sw = Stopwatch.StartNew();
        Thread t = new Thread(delegate() {
            byte[] buf = new byte[len];
            while (sw.ElapsedMilliseconds < ms && result.Count < max) {
                int n;
                if (!ReadFile(h, buf, len, out n, IntPtr.Zero)) { result.Add("read ended, Win32 error " + Marshal.GetLastWin32Error()); break; }
                result.Add(sw.Elapsed.TotalSeconds.ToString("F3") + " " + BitConverter.ToString(buf, 0, n).Replace("-", " "));
            }
        });
        t.IsBackground = true; t.Start(); t.Join(ms + 100);
        CancelIoEx(h, IntPtr.Zero); t.Join(1000);
        return result;
    }
}
"@
Add-Type -TypeDefinition $src

function Hex([byte[]]$b) { if ($null -eq $b -or $b.Length -eq 0) { return '' }; return (($b | ForEach-Object { $_.ToString('X2') }) -join ' ') }
function U16($b, $o) { return [BitConverter]::ToUInt16($b, $o) }
function I32($b, $o) { return [BitConverter]::ToInt32($b, $o) }

function Show-Caps([string]$label, [byte[]]$caps, [bool]$isValue) {
    $n = [int]($caps.Length / 72)
    for ($i = 0; $i -lt $n; $i++) {
        $o = 72 * $i
        $line = '  {0} #{1}: UsagePage=0x{2:X4} ReportID={3} BitField=0x{4:X2} LinkCollection={5} LinkUsage=0x{6:X4} LinkUsagePage=0x{7:X4} IsRange={8} IsAbsolute={9}' -f $label, $i, (U16 $caps ($o + 0)), $caps[$o + 2], (U16 $caps ($o + 4)), (U16 $caps ($o + 6)), (U16 $caps ($o + 8)), (U16 $caps ($o + 10)), $caps[$o + 12], $caps[$o + 15]
        if ($isValue) {
            $line += ' HasNull={0} BitSize={1} ReportCount={2} LogicalMin={3} LogicalMax={4} PhysicalMin={5} PhysicalMax={6}' -f $caps[$o + 16], (U16 $caps ($o + 18)), (U16 $caps ($o + 20)), (I32 $caps ($o + 40)), (I32 $caps ($o + 44)), (I32 $caps ($o + 48)), (I32 $caps ($o + 52))
        } else {
            $line += ' ReportCount={0}' -f (U16 $caps ($o + 16))
        }
        if ($caps[$o + 12] -ne 0) {
            $line += ' UsageMin=0x{0:X4} UsageMax=0x{1:X4} DataIndexMin={2} DataIndexMax={3}' -f (U16 $caps ($o + 56)), (U16 $caps ($o + 58)), (U16 $caps ($o + 68)), (U16 $caps ($o + 70))
        } else {
            $line += ' Usage=0x{0:X4} DataIndex={1}' -f (U16 $caps ($o + 56)), (U16 $caps ($o + 68))
        }
        Write-Output $line
    }
}

$hidDevs = Get-PnpDevice -PresentOnly | Where-Object { $_.InstanceId -like ('HID\VID_{0:X4}*' -f $WantVid) }
if (-not $hidDevs) { Write-Output ('No present HID device with VID {0:X4}.' -f $WantVid) }
foreach ($d in $hidDevs) {
    $path = '\\?\' + ($d.InstanceId -replace '\\', '#') + '#{4d1e55b2-f16f-11cf-88cb-001111000030}'
    Write-Output ('=== {0}' -f $d.InstanceId)
    Write-Output ('path: {0}' -f $path)
    $canRead = $true
    try { $h = [HidProbe]::Open($path, $true) } catch {
        Write-Output ('open with GENERIC_READ failed: {0}; retrying with no access rights' -f $_.Exception.Message)
        $canRead = $false
        try { $h = [HidProbe]::Open($path, $false) } catch { Write-Output ('open failed: {0}' -f $_.Exception.Message); continue }
    }
    try {
        $a = [HidProbe]::Attributes($h)
        Write-Output ('HIDD_ATTRIBUTES: VendorID=0x{0:X4} ProductID=0x{1:X4} VersionNumber=0x{2:X4}' -f (U16 $a 4), (U16 $a 6), (U16 $a 8))
        Write-Output ('Manufacturer string: "{0}"' -f [HidProbe]::Str($h, 0))
        Write-Output ('Product string:      "{0}"' -f [HidProbe]::Str($h, 1))
        Write-Output ('Serial number:       "{0}"' -f [HidProbe]::Str($h, 2))
        $pp = [HidProbe]::Preparsed($h)
        try {
            $c = [HidProbe]::Caps($pp)
            $inLen = U16 $c 4
            Write-Output ('HIDP_CAPS: Usage=0x{0:X4} UsagePage=0x{1:X4} InputReportByteLength={2} OutputReportByteLength={3} FeatureReportByteLength={4} LinkCollectionNodes={5} InputButtonCaps={6} InputValueCaps={7} InputDataIndices={8} OutputButtonCaps={9} OutputValueCaps={10} OutputDataIndices={11} FeatureButtonCaps={12} FeatureValueCaps={13} FeatureDataIndices={14}' -f (U16 $c 0), (U16 $c 2), $inLen, (U16 $c 6), (U16 $c 8), (U16 $c 44), (U16 $c 46), (U16 $c 48), (U16 $c 50), (U16 $c 52), (U16 $c 54), (U16 $c 56), (U16 $c 58), (U16 $c 60), (U16 $c 62))
            $nodes = [HidProbe]::LinkNodes($pp, (U16 $c 44))
            for ($i = 0; $i -lt [int]($nodes.Length / 24); $i++) { $o = 24 * $i; Write-Output ('  LinkCollection #{0}: LinkUsage=0x{1:X4} LinkUsagePage=0x{2:X4} Parent={3} Children={4} NextSibling={5} FirstChild={6} CollectionType={7} IsAlias={8}' -f $i, (U16 $nodes $o), (U16 $nodes ($o + 2)), (U16 $nodes ($o + 4)), (U16 $nodes ($o + 6)), (U16 $nodes ($o + 8)), (U16 $nodes ($o + 10)), $nodes[$o + 12], (($nodes[$o + 13] -band 1))) }
            Show-Caps 'Input value'    ([HidProbe]::ValueCaps($pp, 0, (U16 $c 48)))  $true
            Show-Caps 'Input button'   ([HidProbe]::ButtonCaps($pp, 0, (U16 $c 46))) $false
            Show-Caps 'Output value'   ([HidProbe]::ValueCaps($pp, 1, (U16 $c 54)))  $true
            Show-Caps 'Output button'  ([HidProbe]::ButtonCaps($pp, 1, (U16 $c 52))) $false
            Show-Caps 'Feature value'  ([HidProbe]::ValueCaps($pp, 2, (U16 $c 60)))  $true
            Show-Caps 'Feature button' ([HidProbe]::ButtonCaps($pp, 2, (U16 $c 58))) $false
        } finally { [HidProbe]::Free($pp) }
        if ($canRead -and $ListenSeconds -gt 0 -and $inLen -gt 0) {
            Write-Output ('Listening passively for input reports for {0} s (InputReportByteLength={1}) ...' -f $ListenSeconds, $inLen)
            $t0 = Get-Date
            $reports = [HidProbe]::Listen($h, [int]$inLen, $ListenSeconds * 1000, 5000)
            $data = @($reports | Where-Object { $_ -notmatch '^read ended' })
            Write-Output ('Reports received: {0} (start {1:HH:mm:ss.fff})' -f $data.Count, $t0)
            # Buffer byte 0 is Windows' report-ID byte (0x00 here, the descriptor defines no IDs);
            # the first byte the console sent is buffer byte 1 = field [2] of the line.
            $byId = @{}
            foreach ($r in $data) { $id = ($r -split ' ')[2]; if ($byId.ContainsKey($id)) { $byId[$id]++ } else { $byId[$id] = 1 } }
            foreach ($k in ($byId.Keys | Sort-Object)) { Write-Output ('  payload byte0 (buffer byte 1)=0x{0}: {1} reports' -f $k, $byId[$k]) }
            $data | ForEach-Object { Write-Output ('  ' + $_) }
            $reports | Where-Object { $_ -match '^read ended' } | ForEach-Object { Write-Output ('  ' + $_) }
        }
    } finally { $h.Close() }
}
