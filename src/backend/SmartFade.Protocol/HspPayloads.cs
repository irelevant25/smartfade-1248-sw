using System.Buffers.Binary;

namespace SmartFade.Protocol;

/// <summary>
/// Console → PC payload: [seq][0x00][u16 LE body length][object id][body]. Byte 1 was 0x00 in
/// every payload of E2 and E3, also across the sequence wrap, so it is not the high byte of a
/// 16-bit counter; its meaning is UNKNOWN (VERIFIED layout, F54 / F55).
/// </summary>
[Evidence(EvidenceStatus.Verified, "F54, F55")]
public sealed record HspInboundPayload(byte Sequence, byte Byte1, ushort BodyLength, int ObjectId, ReadOnlyMemory<byte> Body)
{
    public const int HeaderSize = 5;

    public static HspInboundPayload Parse(ReadOnlyMemory<byte> payload)
    {
        var span = payload.Span;
        if (span.Length < HeaderSize)
        {
            throw new ArgumentException($"An inbound payload has at least {HeaderSize} bytes, got {span.Length}.", nameof(payload));
        }

        var bodyLength = BinaryPrimitives.ReadUInt16LittleEndian(span[2..]);
        if (bodyLength != span.Length - HeaderSize)
        {
            throw new InvalidDataException($"Inbound payload length word {bodyLength} does not match the {span.Length - HeaderSize} body bytes present.");
        }

        return new HspInboundPayload(span[0], span[1], bodyLength, span[4], payload[HeaderSize..]);
    }
}

/// <summary>
/// PC → console payload as SmartSoft sends it after a Data header: [seq][u16 LE body length]
/// [object id][body] (VERIFIED for SmartSoft's two System writes, F54 / F58, and its ConsoleEvents, F131).
/// A decoder: the only outbound payloads our code builds are <see cref="HspOutbound"/>'s ConsoleEvents.
/// </summary>
[Evidence(EvidenceStatus.Verified, "F54, F58, F131", "layout as sent by SmartSoft (the System writes and the ConsoleEvents); not clearance to send")]
public sealed record HspOutboundPayload(byte Sequence, ushort BodyLength, int ObjectId, ReadOnlyMemory<byte> Body)
{
    public const int HeaderSize = 4;

    public static HspOutboundPayload Parse(ReadOnlyMemory<byte> payload)
    {
        var span = payload.Span;
        if (span.Length < HeaderSize)
        {
            throw new ArgumentException($"An outbound payload has at least {HeaderSize} bytes, got {span.Length}.", nameof(payload));
        }

        var bodyLength = BinaryPrimitives.ReadUInt16LittleEndian(span[1..]);
        if (bodyLength != span.Length - HeaderSize)
        {
            throw new InvalidDataException($"Outbound payload length word {bodyLength} does not match the {span.Length - HeaderSize} body bytes present.");
        }

        return new HspOutboundPayload(span[0], bodyLength, span[3], payload[HeaderSize..]);
    }
}
