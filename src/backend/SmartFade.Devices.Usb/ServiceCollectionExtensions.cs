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
    /// Registers the read-only USB device as the <see cref="ISmartFadeDevice"/>. Only reachable
    /// through the explicit configuration `Device:Mode=Usb` confirmed for the day
    /// (`Device:Usb:ConfirmDate`); the pipe is opened on Connect, never at start-up, and only on
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
        services.AddSingleton<UsbSmartFadeDevice>(provider => new UsbSmartFadeDevice(
            provider.GetRequiredService<IOptions<UsbDeviceOptions>>(),
            provider.GetRequiredService<Func<IHspPipe>>(),
            provider.GetRequiredService<Func<string, ITransferSink?>>(),
            provider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<UsbSmartFadeDevice>>()));
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
