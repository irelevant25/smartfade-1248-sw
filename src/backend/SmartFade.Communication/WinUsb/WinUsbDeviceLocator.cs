using System.Runtime.Versioning;
using Microsoft.Win32;

namespace SmartFade.Communication.WinUsb;

/// <summary>
/// Finds the device path of the console's interface 1 once the owner has bound WinUSB to it
/// (Zadig on the entry with USB ID 14D5 0200 01, the composite child USB\VID_14D5&amp;PID_0200&amp;MI_01;
/// the name Zadig shows for it is not evidence, E1 lists "ETC SmartFade" and "USB High-Speed-Pipe";
/// E1, F42, H7). Zadig stores the interface GUID it registered under the device instance's
/// "Device Parameters\DeviceInterfaceGUIDs"; the path is then looked up through the Configuration
/// Manager for present devices only. Interface 0 (HID) and every other device are never matched.
/// </summary>
[SupportedOSPlatform("windows")]
public static class WinUsbDeviceLocator
{
    public const string VendorId = "14D5";
    public const string ProductId = "0200";
    public const int InterfaceNumber = 1;

    private const string EnumKey = @"SYSTEM\CurrentControlSet\Enum\USB\VID_" + VendorId + "&PID_" + ProductId + "&MI_01";

    /// <summary>The device paths of present consoles' interface 1 bound to WinUSB; empty when there is none.</summary>
    public static IReadOnlyList<string> FindDevicePaths()
    {
        var paths = new List<string>();
        foreach (var guid in FindRegisteredInterfaceGuids())
        {
            foreach (var path in ListPresentInterfaces(guid))
            {
                if (path.Contains("vid_" + VendorId + "&pid_" + ProductId + "&mi_01", StringComparison.OrdinalIgnoreCase) && !paths.Contains(path))
                {
                    paths.Add(path);
                }
            }
        }

        return paths;
    }

    /// <summary>The interface GUIDs WinUSB registered for the console's interface 1 (one per Zadig installation).</summary>
    public static IReadOnlyList<Guid> FindRegisteredInterfaceGuids()
    {
        var guids = new List<Guid>();
        using var key = Registry.LocalMachine.OpenSubKey(EnumKey);
        if (key is null)
        {
            return guids;
        }

        foreach (var instance in key.GetSubKeyNames())
        {
            using var parameters = key.OpenSubKey(instance + @"\Device Parameters");
            if (parameters?.GetValue("DeviceInterfaceGUIDs") is not string[] values)
            {
                continue;
            }

            foreach (var value in values)
            {
                if (Guid.TryParse(value, out var guid) && !guids.Contains(guid))
                {
                    guids.Add(guid);
                }
            }
        }

        return guids;
    }

    private static IEnumerable<string> ListPresentInterfaces(Guid interfaceClass)
    {
        if (WinUsbNative.CM_Get_Device_Interface_List_Size(out var length, interfaceClass, null, WinUsbNative.CmGetDeviceInterfaceListPresent) != 0 || length < 2)
        {
            yield break;
        }

        var buffer = new char[length];
        if (WinUsbNative.CM_Get_Device_Interface_List(interfaceClass, null, buffer, length, WinUsbNative.CmGetDeviceInterfaceListPresent) != 0)
        {
            yield break;
        }

        foreach (var path in new string(buffer).Split('\0', StringSplitOptions.RemoveEmptyEntries))
        {
            yield return path;
        }
    }
}
