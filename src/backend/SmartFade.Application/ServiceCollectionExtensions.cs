using Microsoft.Extensions.DependencyInjection;
using SmartFade.Application.Ports;
using SmartFade.Domain;

namespace SmartFade.Application;

public static class ServiceCollectionExtensions
{
    /// <summary>Registers the application layer. A device (<see cref="ISmartFadeDevice"/>) must be registered separately.</summary>
    public static IServiceCollection AddSmartFadeApplication(this IServiceCollection services)
    {
        services.AddSingleton(provider =>
        {
            var device = provider.GetRequiredService<ISmartFadeDevice>();
            return new ConsoleStateStore(
                device.Capabilities,
                new ConnectionState(ConnectionStatus.Disconnected, device.Mode, device.Description),
                device.SupportedCommands,
                device.WritableControls);
        });
        services.AddSingleton<ConsoleService>();
        return services;
    }
}
