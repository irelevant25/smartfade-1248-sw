using SmartFade.Communication.Capture;

namespace SmartFade.Communication.Tests;

public class TransportTests
{
    [Fact]
    public async Task Loopback_echoes_what_was_sent()
    {
        var transport = new LoopbackTransport();
        byte[]? received = null;
        transport.DataReceived += (_, e) => received = e.Data.ToArray();

        await transport.OpenAsync(CancellationToken.None);
        await transport.SendAsync(new byte[] { 1, 2, 3 }, CancellationToken.None);

        Assert.Equal(new byte[] { 1, 2, 3 }, received);
        Assert.Single(transport.Sent);
    }

    [Fact]
    public async Task Loopback_rejects_sending_while_closed()
    {
        var transport = new LoopbackTransport();
        await Assert.ThrowsAsync<InvalidOperationException>(() => transport.SendAsync(new byte[] { 1 }, CancellationToken.None));
    }

    [Fact]
    public async Task Replay_plays_the_inbound_side_of_a_capture_and_only_records_sends()
    {
        var capture = CaptureFile.Load(TestPaths.Capture("2026-10-28-e2-smartsoft-connect.jsonl"));
        var transport = new ReplayTransport(capture);
        var received = new List<byte[]>();
        transport.DataReceived += (_, e) => received.Add(e.Data.ToArray());

        await transport.OpenAsync(CancellationToken.None);
        Assert.Equal(3245, transport.RemainingInbound);   // E2: 3 245 bulk IN URBs (F51)

        Assert.True(transport.PlayNext());
        Assert.Single(received);
        Assert.Equal(12, received[0].Length);   // the first reply header

        await transport.SendAsync(new byte[12], CancellationToken.None);
        Assert.Single(transport.Sent);

        var played = transport.PlayAll();
        Assert.Equal(3244, played);
        Assert.False(transport.PlayNext());
    }
}
