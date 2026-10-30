using SmartFade.Communication.Capture;

namespace SmartFade.Communication.Tests;

public class ReplayHspPipeTests
{
    private static ReplayHspPipe Open()
    {
        var pipe = new ReplayHspPipe(CaptureFile.Load(TestPaths.Capture("2026-10-28-e2-smartsoft-connect.jsonl")));
        pipe.Open();
        return pipe;
    }

    [Fact]
    public void Accepts_the_bytes_SmartSoft_sent_and_returns_the_console_replies_in_order()
    {
        using var pipe = Open();

        pipe.Write(new byte[12]);   // E2 f11: the all-zero first poll
        var header = new byte[12];
        pipe.ReadExactly(header);    // f14: the type-4 reply announcing the 9-record list
        Assert.Equal(4, header[0]);
        var length = header[2] | (header[3] << 8);
        Assert.True(length > 0);
        var payload = new byte[length];
        pipe.ReadExactly(payload);
        Assert.Equal(0x28, payload[4]);   // DirtyObjects
        Assert.Equal(2, pipe.Reads);
        Assert.Equal(1, pipe.MatchedWrites);
    }

    [Fact]
    public void Refuses_a_write_that_differs_from_the_capture_and_names_the_frame()
    {
        using var pipe = Open();
        var wrong = new byte[12];
        wrong[0] = 0x02;

        var ex = Assert.Throws<HspPipeException>(() => pipe.Write(wrong));

        Assert.Contains("seq 6", ex.Message, StringComparison.Ordinal);
        Assert.Contains("020000000000000000000000", ex.Message, StringComparison.Ordinal);
        Assert.Equal(0, pipe.MatchedWrites);
    }

    [Fact]
    public void Refuses_a_read_of_the_wrong_length_or_at_the_wrong_moment()
    {
        using var pipe = Open();

        Assert.Throws<HspPipeException>(() => pipe.ReadExactly(new byte[12]));   // a write is due first
        pipe.Write(new byte[12]);
        Assert.Throws<HspPipeException>(() => pipe.ReadExactly(new byte[5]));
    }

    [Fact]
    public void Skips_SmartSoft_data_writes_and_counts_them()
    {
        var capture = CaptureFile.Load(TestPaths.Capture("2026-10-28-e2-smartsoft-connect.jsonl"));
        using var pipe = new ReplayHspPipe(capture);
        pipe.Open();
        var frames = capture.BulkFrames.Where(f => f.Data.Length > 0).ToArray();
        var buffer = new byte[4096];
        var index = 0;
        // walk the capture as the pipe sees it: write back SmartSoft's own polls and fetches, read the replies
        while (index < frames.Length)
        {
            var frame = frames[index];
            if (frame.Direction == CaptureDirection.Out)
            {
                if (frame.Data[0] == 1)
                {
                    index += 2;   // SmartSoft's data header and payload: the pipe skips them on the next write
                    continue;
                }

                pipe.Write(frame.Data);
            }
            else
            {
                pipe.ReadExactly(buffer.AsSpan(0, frame.Data.Length));
            }

            index++;
        }

        Assert.True(pipe.AtEnd);
        Assert.Equal(2, pipe.SkippedSmartSoftWrites);
        Assert.Equal(3001 + 228, pipe.MatchedWrites);
    }

    [Fact]
    public void Is_closed_until_opened()
    {
        var pipe = new ReplayHspPipe(CaptureFile.Load(TestPaths.Capture("2026-10-28-e2-smartsoft-connect.jsonl")));

        Assert.False(pipe.IsOpen);
        Assert.Throws<HspPipeException>(() => pipe.Write(new byte[12]));
        Assert.Contains("e2-smartsoft-connect", pipe.Description, StringComparison.Ordinal);
    }
}
