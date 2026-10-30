using SmartFade.Domain;

namespace SmartFade.Devices.Simulated;

public sealed class SimulationOptions
{
    public const string SectionName = "Device:Simulation";

    /// <summary>Delay between a command and the simulated console's report of its new state.</summary>
    public int CommandLatencyMilliseconds { get; set; } = 15;

    /// <summary>Delay between Connect and the simulated "Online".</summary>
    public int ConnectDelayMilliseconds { get; set; } = 200;

    /// <summary>Fault injection: every command throws after its latency (the desired/actual gap stays open).</summary>
    public bool FailCommands { get; set; }

    public ConsoleCapabilities Capabilities { get; set; } = ConsoleCapabilities.SmartFade1248;
}
