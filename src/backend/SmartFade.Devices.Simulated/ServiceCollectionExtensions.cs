using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartFade.Application.Ports;

namespace SmartFade.Devices.Simulated;

public static class ServiceCollectionExtensions
{
    /// <summary>Registers the simulator as the <see cref="ISmartFadeDevice"/>; options come from the "Device:Simulation" section.</summary>
    public static IServiceCollection AddSimulatedSmartFadeDevice(this IServiceCollection services, IConfiguration? configuration = null)
    {
        if (configuration is not null)
        {
            services.Configure<SimulationOptions>(configuration.GetSection(SimulationOptions.SectionName));
        }
        else
        {
            services.AddOptions<SimulationOptions>();
        }

        services.AddSingleton<ISmartFadeDevice, SimulatedSmartFadeDevice>();
        return services;
    }
}
