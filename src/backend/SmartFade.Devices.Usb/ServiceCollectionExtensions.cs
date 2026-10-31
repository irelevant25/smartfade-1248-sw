using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SmartFade.Application.Ports;
using SmartFade.Communication;
using SmartFade.Communication.WinUsb;

namespace SmartFade.Devices.Usb;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the USB device as the <see cref="ISmartFadeDevice"/> (read-only, or with live control when
    /// `Device:Usb:LiveControlDate` names today, SC-2). Only reachable through the explicit configuration
    /// `Device:Mode=Usb` confirmed for the day (`Device:Usb:ConfirmDate`); the pipe is opened on Connect, never at start-up, and only on
    /// the console's interface 1 bound to WinUSB (Zadig, owner action). A hosted service stops a
    /// running session when the host shuts down.
    /// </summary>
    public static IServiceCollection AddUsbSmartFadeDevice(this IServiceCollection services, IConfiguration configuration, string backendVersion)
    {
        services.Configure<UsbDeviceOptions>(configuration.GetSection(UsbDeviceOptions.SectionName));
        services.AddSingleton<Func<IHspPipe>>(provider => () => OpenConsolePipe(provider.GetRequiredService<IOptions<UsbDeviceOptions>>().Value));
        services.AddSingleton<Func<string, ITransferSink?>>(provider => pipeDescription =>
        {
            var options = provider.GetRequiredService<IOptions<UsbDeviceOptions>>().Value;
            var file = Path.Combine(options.CaptureLogDirectory, $"{DateTime.UtcNow:yyyy-MM-dd-HHmmss}-usb-session.jsonl");
            return new TransferLog(file, pipeDescription, backendVersion);
        });
        services.AddSingleton<UsbSmartFadeDevice>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<UsbDeviceOptions>>();
            return new UsbSmartFadeDevice(
                options,
                provider.GetRequiredService<Func<IHspPipe>>(),
                provider.GetRequiredService<Func<string, ITransferSink?>>(),
                provider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<UsbSmartFadeDevice>>(),
                incompleteWriteMarker: new IncompleteWriteMarker(options.Value.CaptureLogDirectory));   // beside the transfer logs; outlives a restart
        });
        services.AddSingleton<ISmartFadeDevice>(provider => provider.GetRequiredService<UsbSmartFadeDevice>());
        services.AddHostedService<UsbSessionLifetime>();
        return services;
    }

    /// <summary>
    /// True when `Device:Usb:ConfirmDate` names today's local date: hardware mode is confirmed
    /// for this day only, so a value left behind in a shell or a settings file expires by itself.
    /// </summary>
    public static bool IsConfirmedForToday(string? confirmDate, DateOnly today) =>
        DateOnly.TryParseExact(confirmDate, "yyyy-MM-dd", out var date) && date == today;

    /// <summary>
    /// The start-up check of live control's write switch (SC-2 item 6), next to <see cref="IsConfirmedForToday"/>:
    /// absent, the device is read-only; set, it must name today and come with `Device:Mode=Usb` (whose own
    /// `Device:Usb:ConfirmDate` check stays as it is), and the timing settings in <paramref name="options"/> must not be
    /// slower than SmartSoft's cadence (<see cref="UsbDeviceOptions.LiveControlTimingProblem"/>), or the backend refuses
    /// to start. The device checks the date and the timings again at Connect and the date at every command.
    /// </summary>
    public static void ValidateLiveControlDate(string? liveControlDate, bool usbMode, DateOnly today, UsbDeviceOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(liveControlDate))
        {
            return;
        }

        if (!usbMode)
        {
            throw new InvalidOperationException(
                "Device:Usb:LiveControlDate is set but Device:Mode is not Usb: live control exists only on top of the read-only USB mode (SC-2 item 6).");
        }

        if (!IsConfirmedForToday(liveControlDate, today))
        {
            throw new InvalidOperationException(
                $"Device:Usb:LiveControlDate={liveControlDate} is not today ({today:yyyy-MM-dd}): live control is confirmed per day, never left switched on (SC-2 item 6). Leave it out for a read-only session.");
        }

        if (options?.LiveControlTimingProblem() is { } timing)
        {
            throw new InvalidOperationException($"Device:Usb:LiveControlDate is set, but {timing}. Leave those settings at their defaults for live control.");
        }
    }

    private static IHspPipe OpenConsolePipe(UsbDeviceOptions options)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("The USB device uses WinUSB and runs on Windows only.");
        }

        var path = options.DevicePath;
        if (string.IsNullOrWhiteSpace(path))
        {
            var paths = WinUsbDeviceLocator.FindDevicePaths();
            path = paths.Count switch
            {
                0 => throw new HspPipeException("No SmartFade 1248 with WinUSB on interface 1 was found. Bind WinUSB to the console's interface with USB ID 14D5 0200 01 with Zadig first (docs/protocol/reverse-engineering.md, E8 runbook)."),
                1 => paths[0],
                _ => throw new HspPipeException($"{paths.Count} consoles found; set Device:Usb:DevicePath to choose one."),
            };
        }

        return new WinUsbHspPipe(path, options.ReplyTimeout);
    }

    /// <summary>Stops a running read-only session when the host shuts down, so that the console is never left mid-exchange.</summary>
    private sealed class UsbSessionLifetime : IHostedService
    {
        private readonly UsbSmartFadeDevice _device;

        public UsbSessionLifetime(UsbSmartFadeDevice device)
        {
            _device = device;
        }

        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken) => _device.DisconnectAsync(cancellationToken);
    }
}
