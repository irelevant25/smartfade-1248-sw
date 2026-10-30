namespace SmartFade.Protocol.Objects;

/// <summary>
/// Object 0 (Memory), 138 bytes: [a][b][01][0x60][96 levels][text block]. <see cref="Page"/> and
/// <see cref="Slot"/> are the fetch qualifiers (a, b), which equal the show file's `$$MEMORY p f`;
/// whether they are the console's physical page and fader is still OPEN (H25, E6 probe). Levels
/// are raw 0..255; the bodies are VERIFIED as "the console returns these bodies" (F71), the
/// reading 0xFF = 100 % against the show file is OBSERVED once (F72); body[2] = 0x01 meaning
/// UNKNOWN (H39).
/// </summary>
[Evidence(EvidenceStatus.Verified, "F71, F72", "reading against the show file OBSERVED once; H39")]
public sealed record MemoryObject(int Page, int Slot, byte Byte2, int LevelCount, IReadOnlyList<byte> Levels, TextBlock Label)
{
    public static MemoryObject Decode(ReadOnlySpan<byte> body)
    {
        if (body.Length < 4)
        {
            throw new InvalidDataException("Memory: body shorter than 4 bytes.");
        }

        var levelCount = body[3];
        if (body.Length != 4 + levelCount + TextBlock.Size)
        {
            throw new InvalidDataException($"Memory: level count {levelCount} does not match the body length {body.Length}.");
        }

        var label = TextBlock.Decode(body[(4 + levelCount)..]);
        return new MemoryObject(body[0], body[1], body[2], levelCount, body.Slice(4, levelCount).ToArray(), label);
    }
}

/// <summary>One of the two independents in object 12: four raw bytes (meaning UNKNOWN, H44) and a label.</summary>
public sealed record IndependentRecord(byte A, byte B, byte C, byte D, TextBlock Label)
{
    public const int Size = 4 + TextBlock.Size;
}

/// <summary>Object 12 (Independents), 85 bytes: [count = 2] + count × [a][b][c][d][text block] (VERIFIED bytes, F76; fields UNKNOWN, H44).</summary>
[Evidence(EvidenceStatus.Verified, "F76", "field meaning: H44")]
public sealed record IndependentsObject(IReadOnlyList<IndependentRecord> Independents)
{
    public static IndependentsObject Decode(ReadOnlySpan<byte> body)
    {
        if (body.Length < 1)
        {
            throw new InvalidDataException("Independents: empty body.");
        }

        var count = body[0];
        if (body.Length != 1 + count * IndependentRecord.Size)
        {
            throw new InvalidDataException($"Independents: count {count} does not match the body length {body.Length}.");
        }

        var records = new IndependentRecord[count];
        for (var i = 0; i < count; i++)
        {
            var record = body.Slice(1 + i * IndependentRecord.Size, IndependentRecord.Size);
            records[i] = new IndependentRecord(record[0], record[1], record[2], record[3], TextBlock.Decode(record[4..]));
        }

        return new IndependentsObject(records);
    }
}

/// <summary>
/// Object 21 (DisplayContent): [character count][characters], 40 characters = 2 lines × 20 on the
/// 1248's LCD (OBSERVED, F86). A fader's cell is 0x20 at level 0 or one of 0x80–0x86 rising with
/// the level, 0x86 being the top state at full; 0x87 has never been seen (F86; bar-graph reading
/// INFERRED from the manual, H49). The seven seen glyphs are rendered as block characters of rising
/// height with 0x86 as the full bar; any other byte renders as U+FFFD.
/// </summary>
[Evidence(EvidenceStatus.Observed, "F86", "glyph reading: H49; 0x87 never seen")]
public sealed record DisplayContentObject(IReadOnlyList<byte> Raw, IReadOnlyList<string> Lines)
{
    public const int LineLength = 20;
    private const string BarGlyphs = "▂▃▄▅▆▇█";   // 0x80 .. 0x86

    public static DisplayContentObject Decode(ReadOnlySpan<byte> body)
    {
        var n = ConsoleVersionObject.RequireLengthPrefix(body, 1, "DisplayContent");
        var raw = body.Slice(1, n).ToArray();
        var lines = new List<string>();
        for (var offset = 0; offset < raw.Length; offset += LineLength)
        {
            var count = Math.Min(LineLength, raw.Length - offset);
            var chars = new char[count];
            for (var i = 0; i < count; i++)
            {
                chars[i] = ToChar(raw[offset + i]);
            }

            lines.Add(new string(chars));
        }

        return new DisplayContentObject(raw, lines);
    }

    private static char ToChar(byte b)
    {
        if (b is >= 0x20 and < 0x7F)
        {
            return (char)b;
        }

        if (b >= 0x80 && b - 0x80 < BarGlyphs.Length)
        {
            return BarGlyphs[b - 0x80];
        }

        return '�';
    }
}

/// <summary>One LED of object 22: two RGB triples, read as the LED's two blink phases (INFERRED, H48).</summary>
public readonly record struct LedBlock(byte R1, byte G1, byte B1, byte R2, byte G2, byte B2)
{
    public const int Size = 6;

    public bool PhasesDiffer => R1 != R2 || G1 != G2 || B1 != B2;
}

/// <summary>
/// Object 22 (LedStatus): [count = 75][count × 6 bytes]. Blocks 1–24 follow the physical faders'
/// bump LEDs (OBSERVED for blocks 1, 5, 7 and the memory-mode colours of blocks 1–24, F84 and the
/// E3b record; the mapping of the remaining fader blocks is INFERRED); block 53 is the Blackout LED
/// (INFERRED, F85), block 52 the SOLO LED and blocks 55 / 56 the 1-24 / 25-48 bank buttons (OBSERVED
/// in E3b); the other blocks are open (H56).
/// </summary>
[Evidence(EvidenceStatus.Observed, "F84, F85, F98, F100", "block map: H48, H55, H56; layout VERIFIED in part (F84)")]
public sealed record LedStatusObject(IReadOnlyList<LedBlock> Blocks)
{
    public static LedStatusObject Decode(ReadOnlySpan<byte> body)
    {
        if (body.Length < 1)
        {
            throw new InvalidDataException("LedStatus: empty body.");
        }

        var count = body[0];
        if (body.Length != 1 + count * LedBlock.Size)
        {
            throw new InvalidDataException($"LedStatus: count {count} does not match the body length {body.Length}.");
        }

        var blocks = new LedBlock[count];
        for (var i = 0; i < count; i++)
        {
            var b = body.Slice(1 + i * LedBlock.Size, LedBlock.Size);
            blocks[i] = new LedBlock(b[0], b[1], b[2], b[3], b[4], b[5]);
        }

        return new LedStatusObject(blocks);
    }

    /// <summary>Block <paramref name="number"/> (1-based, as in the knowledge base).</summary>
    public LedBlock Block(int number) => Blocks[number - 1];
}
