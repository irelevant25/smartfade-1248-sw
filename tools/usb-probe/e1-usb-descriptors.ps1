# E1 (read-only) USB descriptor dump for the ETC SmartFade 1248 (VID 0x14D5).
# Mechanism: standard GET_DESCRIPTOR requests routed through the parent hub driver with
# IOCTL_USB_GET_DESCRIPTOR_FROM_NODE_CONNECTION, the same way USBView / USBTreeView work.
# Nothing is written to the device; only descriptor reads are issued.
param([int]$WantVid = 0x14D5)

$ErrorActionPreference = 'Continue'

$src = @"
using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
public static class UsbHubIo {
    [DllImport("kernel32.dll", SetLastError=true, CharSet=CharSet.Unicode)]
    static extern SafeFileHandle CreateFile(string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);
    [DllImport("kernel32.dll", SetLastError=true)]
    static extern bool DeviceIoControl(SafeFileHandle hDevice, uint dwIoControlCode, byte[] lpInBuffer, int nInBufferSize, byte[] lpOutBuffer, int nOutBufferSize, out int lpBytesReturned, IntPtr lpOverlapped);
    public static SafeFileHandle Open(string path) {
        // Access 0: the three IOCTLs used here are FILE_ANY_ACCESS, so no read/write right is needed.
        // (The E1 run of 2026-10-28 used GENERIC_WRITE like USBView; changed after the safety review.)
        SafeFileHandle h = CreateFile(path, 0, 2, IntPtr.Zero, 3, 0, IntPtr.Zero);
        if (h.IsInvalid) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        return h;
    }
    public static int Ioctl(SafeFileHandle h, uint code, byte[] buf) {
        int ret;
        if (!DeviceIoControl(h, code, buf, buf.Length, buf, buf.Length, out ret, IntPtr.Zero))
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        return ret;
    }
}
"@
Add-Type -TypeDefinition $src

$IOCTL_NODE_INFO    = 0x220408   # IOCTL_USB_GET_NODE_INFORMATION
$IOCTL_CONN_INFO_EX = 0x220448   # IOCTL_USB_GET_NODE_CONNECTION_INFORMATION_EX
$IOCTL_GET_DESC     = 0x220410   # IOCTL_USB_GET_DESCRIPTOR_FROM_NODE_CONNECTION
$HUB_GUID = '{f18a0e88-c30c-11d0-8815-00a0c906bed8}'  # GUID_DEVINTERFACE_USB_HUB

function Hex([byte[]]$b) {
    if ($null -eq $b -or $b.Length -eq 0) { return '' }
    return (($b | ForEach-Object { $_.ToString('X2') }) -join ' ')
}

function Get-Desc($h, [int]$port, [int]$type, [int]$index, [int]$wIndex, [int]$len, [byte]$bmRequest) {
    $buf = New-Object byte[] (12 + $len)
    [BitConverter]::GetBytes([uint32]$port).CopyTo($buf, 0)
    $buf[4] = $bmRequest
    $buf[5] = 6   # GET_DESCRIPTOR
    [BitConverter]::GetBytes([uint16](($type -shl 8) -bor $index)).CopyTo($buf, 6)
    [BitConverter]::GetBytes([uint16]$wIndex).CopyTo($buf, 8)
    [BitConverter]::GetBytes([uint16]$len).CopyTo($buf, 10)
    $n = [UsbHubIo]::Ioctl($h, $IOCTL_GET_DESC, $buf)
    if ($n -le 12) { return [byte[]]@() }
    return [byte[]]($buf[12..($n - 1)])
}

function Get-StringDesc($h, [int]$port, [int]$index, [int]$langid) {
    # Two-step like the hub driver itself: 2-byte header first, then the exact length.
    $hdr = Get-Desc $h $port 3 $index $langid 2 0x80
    $need = 2; if ($hdr.Length -ge 1 -and [int]$hdr[0] -gt 2) { $need = [int]$hdr[0] }
    $s = Get-Desc $h $port 3 $index $langid $need 0x80
    $txt = ''
    if ($s.Length -gt 2) { $txt = [System.Text.Encoding]::Unicode.GetString($s, 2, ([Math]::Min([int]$s[0], $s.Length) - 2)) }
    return @($txt, $s)
}

$hubs = Get-PnpDevice -PresentOnly | Where-Object { $_.Class -eq 'USB' -and ($_.Service -match 'hub' -or $_.InstanceId -match 'ROOT_HUB') }
$found = $false
foreach ($hub in $hubs) {
    $path = '\\?\' + ($hub.InstanceId -replace '\\', '#') + '#' + $HUB_GUID
    try { $h = [UsbHubIo]::Open($path) } catch { Write-Output ("hub open failed: {0} : {1}" -f $hub.InstanceId, $_.Exception.Message); continue }
    try {
        $ni = New-Object byte[] 128
        $ports = 16
        try { [void][UsbHubIo]::Ioctl($h, $IOCTL_NODE_INFO, $ni); $ports = [int]$ni[6] } catch { }
        for ($p = 1; $p -le $ports; $p++) {
            $cb = New-Object byte[] (35 + 11 * 32)
            [BitConverter]::GetBytes([uint32]$p).CopyTo($cb, 0)
            try { [void][UsbHubIo]::Ioctl($h, $IOCTL_CONN_INFO_EX, $cb) } catch { continue }
            $status = [BitConverter]::ToUInt32($cb, 31)
            if ($status -ne 1) { continue }
            $devVid = [BitConverter]::ToUInt16($cb, 12); $devPid = [BitConverter]::ToUInt16($cb, 14)
            Write-Output ("hub {0} port {1}: VID {2:X4} PID {3:X4}" -f $hub.InstanceId, $p, $devVid, $devPid)
            if ($devVid -ne $WantVid) { continue }
            $found = $true
            Write-Output ''
            Write-Output ('=== DEVICE on hub {0}, port {1}' -f $hub.InstanceId, $p)
            $npipes = [BitConverter]::ToUInt32($cb, 27)
            Write-Output ('CurrentConfigurationValue={0} Speed={1} (0 low, 1 full, 2 high, 3 super) DeviceIsHub={2} DeviceAddress={3} NumberOfOpenPipes={4} ConnectionStatus={5}' -f $cb[22], $cb[23], $cb[24], [BitConverter]::ToUInt16($cb, 25), $npipes, $status)
            for ($i = 0; $i -lt $npipes -and $i -lt 32; $i++) {
                $o = 35 + 11 * $i
                Write-Output ('  open pipe {0}: endpoint descriptor {1}  ScheduleOffset={2}' -f $i, (Hex ([byte[]]$cb[$o..($o + 6)])), [BitConverter]::ToUInt32($cb, $o + 7))
            }
            $dd = [byte[]]$cb[4..21]
            Write-Output ('Device descriptor (hub cache): ' + (Hex $dd))
            Write-Output ('  bLength={0} bDescriptorType={1} bcdUSB={2:X4} bDeviceClass={3:X2} bDeviceSubClass={4:X2} bDeviceProtocol={5:X2} bMaxPacketSize0={6} idVendor={7:X4} idProduct={8:X4} bcdDevice={9:X4} iManufacturer={10} iProduct={11} iSerialNumber={12} bNumConfigurations={13}' -f $dd[0], $dd[1], [BitConverter]::ToUInt16($dd, 2), $dd[4], $dd[5], $dd[6], $dd[7], [BitConverter]::ToUInt16($dd, 8), [BitConverter]::ToUInt16($dd, 10), [BitConverter]::ToUInt16($dd, 12), $dd[14], $dd[15], $dd[16], $dd[17])
            try { $live = Get-Desc $h $p 1 0 0 18 0x80; Write-Output ('Device descriptor (GET_DESCRIPTOR): ' + (Hex $live)) } catch { Write-Output ('GET_DESCRIPTOR device failed: ' + $_.Exception.Message) }

            $langid = 0
            try { $s0 = Get-Desc $h $p 3 0 0 4 0x80; Write-Output ('String 0 (LANGIDs, first 4 bytes): ' + (Hex $s0)); if ($s0.Length -ge 4) { $langid = [BitConverter]::ToUInt16($s0, 2) } } catch { Write-Output ('String 0 failed: ' + $_.Exception.Message) }
            foreach ($si in @(@('iManufacturer', [int]$dd[14]), @('iProduct', [int]$dd[15]), @('iSerialNumber', [int]$dd[16]))) {
                if ($si[1] -eq 0) { Write-Output ('{0}: index 0 (no string)' -f $si[0]); continue }
                try { $r = Get-StringDesc $h $p $si[1] $langid; Write-Output ('{0} (index {1}): "{2}"  raw: {3}' -f $si[0], $si[1], $r[0], (Hex $r[1])) } catch { Write-Output ('{0} (index {1}) failed: {2}' -f $si[0], $si[1], $_.Exception.Message) }
            }

            for ($c = 0; $c -lt [int]$dd[17]; $c++) {
                try {
                    $c9 = Get-Desc $h $p 2 $c 0 9 0x80
                    $total = [BitConverter]::ToUInt16($c9, 2)
                    $cfg = Get-Desc $h $p 2 $c 0 $total 0x80
                    Write-Output ''
                    Write-Output ('Configuration {0} descriptor ({1} bytes): {2}' -f $c, $cfg.Length, (Hex $cfg))
                    $o = 0; $iface = -1; $hidLens = @{}; $ifaceStrings = @{}
                    while ($o + 1 -lt $cfg.Length) {
                        $l = [int]$cfg[$o]; $t = [int]$cfg[$o + 1]
                        if ($l -lt 2) { break }
                        $d = [byte[]]$cfg[$o..($o + $l - 1)]
                        switch ($t) {
                            2 { Write-Output ('  CONFIG: wTotalLength={0} bNumInterfaces={1} bConfigurationValue={2} iConfiguration={3} bmAttributes=0x{4:X2} bMaxPower={5} ({6} mA)' -f [BitConverter]::ToUInt16($d, 2), $d[4], $d[5], $d[6], $d[7], $d[8], ([int]$d[8] * 2)) }
                            4 { $iface = [int]$d[2]; if ($d[8] -ne 0) { $ifaceStrings[$iface] = [int]$d[8] }; Write-Output ('  INTERFACE: bInterfaceNumber={0} bAlternateSetting={1} bNumEndpoints={2} bInterfaceClass=0x{3:X2} bInterfaceSubClass=0x{4:X2} bInterfaceProtocol=0x{5:X2} iInterface={6}' -f $d[2], $d[3], $d[4], $d[5], $d[6], $d[7], $d[8]) }
                            0x21 { $hl = [BitConverter]::ToUInt16($d, 7); $hidLens[$iface] = [int]$hl; Write-Output ('  HID: bcdHID={0:X4} bCountryCode={1} bNumDescriptors={2} bDescriptorType=0x{3:X2} wDescriptorLength={4}' -f [BitConverter]::ToUInt16($d, 2), $d[4], $d[5], $d[6], $hl) }
                            5 {
                                $ep = [int]$d[2]
                                $dir = 'OUT'; if ($ep -band 0x80) { $dir = 'IN' }
                                $types = @('control', 'isochronous', 'bulk', 'interrupt')
                                Write-Output ('  ENDPOINT: bEndpointAddress=0x{0:X2} ({1} {2}) bmAttributes=0x{3:X2} ({4}) wMaxPacketSize={5} bInterval={6}' -f $ep, $dir, ($ep -band 0x0F), $d[3], $types[[int]$d[3] -band 3], [BitConverter]::ToUInt16($d, 4), $d[6])
                            }
                            default { Write-Output ('  descriptor type 0x{0:X2} len {1}: {2}' -f $t, $l, (Hex $d)) }
                        }
                        $o += $l
                    }
                    foreach ($k in $ifaceStrings.Keys) {
                        try { $r = Get-StringDesc $h $p $ifaceStrings[$k] $langid; Write-Output ('  iInterface for interface {0} (index {1}): "{2}"  raw: {3}' -f $k, $ifaceStrings[$k], $r[0], (Hex $r[1])) } catch { Write-Output ('  iInterface for interface {0} failed: {1}' -f $k, $_.Exception.Message) }
                    }
                    foreach ($k in $hidLens.Keys) {
                        try { $rd = Get-Desc $h $p 0x22 0 $k $hidLens[$k] 0x81; Write-Output ('HID report descriptor, interface {0} ({1} bytes): {2}' -f $k, $rd.Length, (Hex $rd)) }
                        catch { Write-Output ('HID report descriptor, interface {0}: hub request failed: {1}' -f $k, $_.Exception.Message) }
                    }
                } catch { Write-Output ('Configuration {0} failed: {1}' -f $c, $_.Exception.Message) }
            }
        }
    } finally { $h.Close() }
}
if (-not $found) { Write-Output ('No connected device with VID {0:X4} found on any hub port.' -f $WantVid) }
