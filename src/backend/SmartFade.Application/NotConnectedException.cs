namespace SmartFade.Application;

/// <summary>A command was issued while the console is not connected.</summary>
public sealed class NotConnectedException : InvalidOperationException
{
    public NotConnectedException()
        : base("The console is not connected.")
    {
    }
}
