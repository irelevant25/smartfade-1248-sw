namespace SmartFade.Protocol.Tests;

/// <summary>
/// Golden framing tests. Every byte string below is copied from the committed E2 capture
/// (docs/protocol/captures/2026-10-28-e2-smartsoft-connect.jsonl: seq 6 = f11, seq 7 = f14,
/// seq 8 = f16, seq 9 = f17, seq 105 = f209, seq 106 = f211, all on the E2 record's golden-test
/// list); the one deliberately malformed input is a truncated copy of seq 8 and is labelled so.
/// </summary>
public class HspFramingTests
{
    [Fact]
    public void Parses_the_first_poll_header_of_E2()
    {
        // E2 f11: SmartSoft's first poll, all words zero
        var header = HspHeader.Parse(Convert.FromHexString("000000000000000000000000"));
        Assert.Equal(HspMessageType.Poll, header.MessageType);
        Assert.Equal(0, header.Length);
        Assert.True(header.IsFromPc);
    }

    [Fact]
    public void Parses_a_fetch_header_of_E2()
    {
        // E2 f17: fetch of object 17 (PlaybackInfo), the first record of the first dirty list
        var header = HspHeader.Parse(Convert.FromHexString("020000001100000000000000"));
        Assert.Equal(HspMessageType.Fetch, header.MessageType);
        Assert.Equal(17, header.ObjectId);
        Assert.Equal((int)HspObjectId.PlaybackInfo, header.ObjectId);
        Assert.Equal(0, header.QualifierA);
    }

    [Fact]
    public void Parses_the_first_poll_reply_and_its_dirty_list_of_E2()
    {
        // E2 f14: type-4 reply header announcing 0x34 = 52 payload bytes
        var header = HspHeader.Parse(Convert.FromHexString("040034000000000000000000"));
        Assert.Equal(HspMessageType.PollReply, header.MessageType);
        Assert.Equal(52, header.Length);

        // E2 f16: the 52-byte payload: seq 0xF6, byte1 0, len 0x2F = 47, id 0x28 = 40 (DirtyObjects)
        var bytes = Convert.FromHexString("F6002F0028000911000000000E0000000016000000000D000000000F000000001700000000170000000015000000002400000000");
        var payload = HspInboundPayload.Parse(bytes);
        Assert.Equal(0xF6, payload.Sequence);
        Assert.Equal(0, payload.Byte1);
        Assert.Equal(47, payload.BodyLength);
        Assert.Equal((int)HspObjectId.DirtyObjects, payload.ObjectId);

        var list = DirtyObjectsList.Decode(payload.Body.Span);
        Assert.Equal(9, list.Records.Count);
        Assert.Equal(new[] { 17, 14, 22, 13, 15, 23, 23, 21, 36 }, list.Records.Select(r => r.ObjectId).ToArray());
        Assert.All(list.Records, r => Assert.Equal(0, r.C));
        Assert.Equal("FileName", list.Records[8].Name);
    }

    [Fact]
    public void Parses_SmartSofts_first_System_write_of_E2()
    {
        // E2 f209 + f211: data header (len 7) and payload: seq 0, len 3, id 0x27 = 39 (System), body 09 00 00
        var header = HspHeader.Parse(Convert.FromHexString("010007000000000000000000"));
        Assert.Equal(HspMessageType.Data, header.MessageType);
        Assert.Equal(7, header.Length);

        var payload = HspOutboundPayload.Parse(Convert.FromHexString("00030027090000"));
        Assert.Equal(0, payload.Sequence);
        Assert.Equal(3, payload.BodyLength);
        Assert.Equal((int)HspObjectId.System, payload.ObjectId);
        Assert.Equal(new byte[] { 0x09, 0x00, 0x00 }, payload.Body.ToArray());
    }

    [Fact]
    public void Rejects_a_header_of_the_wrong_size() =>
        Assert.Throws<ArgumentException>(() => HspHeader.Parse(new byte[11]));

    [Fact]
    public void Rejects_an_inbound_payload_whose_length_word_disagrees() =>
        // the first 6 bytes of E2 seq 8 (f16): a deliberately truncated input, not a capture record
        Assert.Throws<InvalidDataException>(() => HspInboundPayload.Parse(Convert.FromHexString("F6002F002800")));
}
