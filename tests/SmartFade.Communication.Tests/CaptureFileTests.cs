using SmartFade.Communication.Capture;

namespace SmartFade.Communication.Tests;

public class CaptureFileTests
{
    [Fact]
    public void Loads_the_E2_capture_header_and_transfers()
    {
        var capture = CaptureFile.Load(TestPaths.Capture("2026-10-28-e2-smartsoft-connect.jsonl"));

        Assert.Equal("smartfade-capture", capture.Header.Format);
        Assert.Equal("8F350496438DE66E4AEA70B6BE5206ADBBFEC4D8A61F8C3782FA6A122580CB82", capture.Header.SourceSha256);
        Assert.Equal("3.0.2.9.0.43", capture.Header.Firmware);
        Assert.Equal("14D5", capture.Header.DeviceVid);
        Assert.Equal("0200", capture.Header.DevicePid);
        Assert.Equal(6483, capture.Frames.Count);
        Assert.Equal(6483, capture.Header.Transfers);

        // the first bulk transfer is SmartSoft's first poll: an all-zero 12-byte header (E2 f11)
        var firstBulk = capture.BulkFrames.First();
        Assert.Equal(CaptureDirection.Out, firstBulk.Direction);
        Assert.Equal("0x04", firstBulk.Endpoint);
        Assert.Equal(new byte[12], firstBulk.Data);
    }

    [Fact]
    public void Synthetic_descriptor_frames_are_excluded_from_bulk_frames()
    {
        var capture = CaptureFile.Load(TestPaths.Capture("2026-10-28-e2-smartsoft-connect.jsonl"));
        Assert.Contains(capture.Frames, f => f.Synthetic);
        Assert.DoesNotContain(capture.BulkFrames, f => f.Synthetic);
    }

    [Fact]
    public void Loads_the_E3_capture_with_its_action_log_notes()
    {
        var capture = CaptureFile.Load(TestPaths.Capture("2026-10-29-e3-console-actions.jsonl"));
        Assert.Equal(20326, capture.Frames.Count);
        Assert.Contains(capture.Notes, n => n.Text.Contains("fader1 up start", StringComparison.Ordinal));
    }
}
