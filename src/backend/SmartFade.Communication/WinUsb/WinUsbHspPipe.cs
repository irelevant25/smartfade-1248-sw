using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace SmartFade.Communication.WinUsb;

/// <summary>
/// <see cref="IHspPipe"/> over WinUSB on the console's interface 1: bulk OUT 0x04 and bulk IN
/// 0x83, 64-byte packets (E1, F42, VERIFIED F51). Each write is one transfer and each read asks
/// for exactly the bytes the session expects, the way the ETC driver's WriteFile / ReadFile
/// produced one URB per transfer (F51), so the console sees the same transfer sizes. The device
/// path must name VID 14D5 / PID 0200 interface 1, whatever its source; before the first transfer
/// the interface descriptor and both pipes are checked against E1, and anything else is refused.
/// No control transfer is issued by this class, no pipe is reset, no stall is cleared and nothing
/// is retried: a failed or timed-out transfer throws and the session stops. ASSUMPTION (not
/// observed yet): WinUSB itself may issue standard control requests when the handle is opened
/// (descriptor reads); the E8 USBPcap capture records EP0 to settle this.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WinUsbHspPipe : IHspPipe
{
    public const byte BulkOut = 0x04;
    public const byte BulkIn = 0x83;
    /// <summary>The console's interface 1 as Windows names it in a device path (E1: VID_14D5, PID_0200, MI_01).</summary>
    public const string RequiredPathFragment = "vid_14d5&pid_0200&mi_01";
    private const byte VendorSpecificClass = 0xFF;
    private const int UsbdPipeTypeBulk = 2;

    private readonly string _devicePath;
    private readonly uint _timeoutMilliseconds;
    private WinUsbNative.SafeFileHandleOwner? _file;
    private nint _interface;

    public WinUsbHspPipe(string devicePath, TimeSpan transferTimeout)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(devicePath);
        if (!devicePath.Contains(RequiredPathFragment, StringComparison.OrdinalIgnoreCase))
        {
            throw new HspPipeException($"refusing a device path that is not the console's interface 1 ({RequiredPathFragment})");
        }

        _devicePath = devicePath;
        _timeoutMilliseconds = (uint)Math.Clamp(transferTimeout.TotalMilliseconds, 50, 10_000);
    }

    public string Description => $"WinUSB {_devicePath}";

    public bool IsOpen => _interface != 0;

    public void Open()
    {
        if (IsOpen)
        {
            return;
        }

        // Exclusive, overlapped-capable handle: WinUsb_Initialize requires FILE_FLAG_OVERLAPPED
        // (Microsoft's WinUsb_Initialize reference); the synchronous WinUsb_ReadPipe / WritePipe
        // calls below pass no OVERLAPPED and block until the transfer or its timeout.
        var file = WinUsbNative.CreateFile(
            _devicePath,
            WinUsbNative.GenericRead | WinUsbNative.GenericWrite,
            0,
            0,
            WinUsbNative.OpenExisting,
            WinUsbNative.FileAttributeNormal | WinUsbNative.FileFlagOverlapped,
            0);
        if (file.IsInvalid)
        {
            var error = Marshal.GetLastWin32Error();
            file.Dispose();
            throw new HspPipeException($"cannot open the console's interface: {new Win32Exception(error).Message}");
        }

        if (!WinUsbNative.WinUsb_Initialize(file, out var handle))
        {
            var error = Marshal.GetLastWin32Error();
            file.Dispose();
            throw new HspPipeException($"WinUsb_Initialize failed on the console's interface: {new Win32Exception(error).Message}");
        }

        _file = file;
        _interface = handle;
        try
        {
            VerifyInterface();
            ConfigurePipe(BulkOut);
            ConfigurePipe(BulkIn);
        }
        catch
        {
            Close();
            throw;
        }
    }

    public void Close()
    {
        if (_interface != 0)
        {
            WinUsbNative.WinUsb_Free(_interface);
            _interface = 0;
        }

        _file?.Dispose();
        _file = null;
    }

    public void Dispose() => Close();

    public void Write(ReadOnlySpan<byte> data)
    {
        EnsureOpen();
        if (data.IsEmpty)
        {
            throw new HspPipeException("refusing an empty transfer");
        }

        var buffer = data.ToArray();
        if (!WinUsbNative.WinUsb_WritePipe(_interface, BulkOut, ref buffer[0], (uint)buffer.Length, out var transferred, 0))
        {
            throw new HspPipeException($"bulk OUT of {buffer.Length} bytes failed: {new Win32Exception(Marshal.GetLastWin32Error()).Message}");
        }

        if (transferred != buffer.Length)
        {
            throw new HspPipeException($"bulk OUT sent {transferred} of {buffer.Length} bytes");
        }
    }

    public void ReadExactly(Span<byte> buffer)
    {
        EnsureOpen();
        if (buffer.IsEmpty)
        {
            throw new HspPipeException("refusing an empty read");
        }

        var scratch = new byte[buffer.Length];
        if (!WinUsbNative.WinUsb_ReadPipe(_interface, BulkIn, ref scratch[0], (uint)scratch.Length, out var transferred, 0))
        {
            throw new HspPipeException($"bulk IN of {buffer.Length} bytes failed: {new Win32Exception(Marshal.GetLastWin32Error()).Message}");
        }

        if (transferred != buffer.Length)
        {
            throw new HspPipeException($"bulk IN returned {transferred} of the {buffer.Length} bytes expected");
        }

        scratch.CopyTo(buffer);
    }

    private void VerifyInterface()
    {
        if (!WinUsbNative.WinUsb_QueryInterfaceSettings(_interface, 0, out var descriptor))
        {
            throw new HspPipeException($"cannot read the interface descriptor: {new Win32Exception(Marshal.GetLastWin32Error()).Message}");
        }

        if (descriptor.InterfaceNumber != WinUsbDeviceLocator.InterfaceNumber || descriptor.InterfaceClass != VendorSpecificClass || descriptor.NumEndpoints != 2)
        {
            throw new HspPipeException(
                $"not the console's HSP interface: interface {descriptor.InterfaceNumber}, class 0x{descriptor.InterfaceClass:X2}, {descriptor.NumEndpoints} endpoints (expected interface 1, class 0xFF, 2 endpoints; E1)");
        }

        var seen = new HashSet<byte>();
        for (byte index = 0; index < descriptor.NumEndpoints; index++)
        {
            if (!WinUsbNative.WinUsb_QueryPipe(_interface, 0, index, out var pipe))
            {
                throw new HspPipeException($"cannot read pipe {index}: {new Win32Exception(Marshal.GetLastWin32Error()).Message}");
            }

            if (pipe.PipeType != UsbdPipeTypeBulk || pipe.MaximumPacketSize != 64)
            {
                throw new HspPipeException($"pipe 0x{pipe.PipeId:X2} is not a 64-byte bulk pipe (type {pipe.PipeType}, packet {pipe.MaximumPacketSize})");
            }

            seen.Add(pipe.PipeId);
        }

        if (!seen.Contains(BulkOut) || !seen.Contains(BulkIn))
        {
            throw new HspPipeException($"expected bulk pipes 0x04 and 0x83, found {string.Join(", ", seen.Select(p => $"0x{p:X2}"))}");
        }
    }

    private void ConfigurePipe(byte pipeId)
    {
        var timeout = _timeoutMilliseconds;
        if (!WinUsbNative.WinUsb_SetPipePolicy(_interface, pipeId, WinUsbNative.PipeTransferTimeout, sizeof(uint), ref timeout))
        {
            throw new HspPipeException($"cannot set the transfer timeout on pipe 0x{pipeId:X2}: {new Win32Exception(Marshal.GetLastWin32Error()).Message}");
        }

        // Never clear a stall on our own (that is a control transfer to the device); a stalled pipe ends the session.
        var off = false;
        if (!WinUsbNative.WinUsb_SetPipePolicy(_interface, pipeId, WinUsbNative.AutoClearStall, sizeof(bool), ref off))
        {
            throw new HspPipeException($"cannot disable auto clear stall on pipe 0x{pipeId:X2}: {new Win32Exception(Marshal.GetLastWin32Error()).Message}");
        }

        if (pipeId == BulkIn)
        {
            // A transfer longer than the read asked for fails instead of being split over two reads
            // (host-side policy, no bus traffic): every read is exactly one transfer of the announced size.
            var noPartial = false;
            if (!WinUsbNative.WinUsb_SetPipePolicy(_interface, pipeId, WinUsbNative.AllowPartialReads, sizeof(bool), ref noPartial))
            {
                throw new HspPipeException($"cannot disable partial reads on pipe 0x{pipeId:X2}: {new Win32Exception(Marshal.GetLastWin32Error()).Message}");
            }
        }
    }

    private void EnsureOpen()
    {
        if (!IsOpen)
        {
            throw new HspPipeException("the WinUSB pipe is not open");
        }
    }
}
