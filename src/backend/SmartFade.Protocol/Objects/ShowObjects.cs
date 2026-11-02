using System.Buffers.Binary;

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
/// (INFERRED, F85), block 52 the SOLO LED (INFERRED from one press / release pair, F100) and blocks
/// 55 / 56 the 1-24 / 25-48 bank buttons (OBSERVED in E3b, F98); the other blocks are open (H56).
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

/// <summary>
/// The 12-byte step record of the stack and of a sequence: `[u16 BE cue × 10][type][u16 BE memory index][x][u16 BE up]
/// [u16 BE down][u16 BE wait]`, the times in tenths of a second. OBSERVED against the console's own files: SHOW04 19 / 19 stack
/// and 7 / 7 sequence steps (E2, F74), SHOW05 57 / 57 again (E3b), SHOW15 22 / 22 (E12b, F162); the layout is H41, the third
/// time the console's "W" (H14 CONFIRMED, F154). The type is `$$CUEPARAMS`: 1 a memory step whose index is 24 × page + fader
/// (0-based, the file's `$$MEMORY p f`, H13), 2 a "State" step with its own levels and label (F154, F161), 0 a channel step
/// whose index bytes hold the channel − 1 (F160, OBSERVED once). Byte 5 ("x") was 0 in every record seen; its meaning is UNKNOWN.
/// </summary>
[Evidence(EvidenceStatus.Observed, "F74, F154, F160, F162", "layout H41 against three show files; the type readings H12 (CONFIRMED), the channel step F160 once; x UNKNOWN")]
public sealed record StepRecord(int CueTenths, byte Type, int MemoryIndex, byte Byte5, int UpTenths, int DownTenths, int WaitTenths)
{
    public const int Size = 12;

    public static StepRecord Decode(ReadOnlySpan<byte> record)
    {
        if (record.Length < Size)
        {
            throw new InvalidDataException($"Step record: {record.Length} bytes, {Size} expected.");
        }

        return new StepRecord(
            BinaryPrimitives.ReadUInt16BigEndian(record),
            record[2],
            BinaryPrimitives.ReadUInt16BigEndian(record[3..]),
            record[5],
            BinaryPrimitives.ReadUInt16BigEndian(record[6..]),
            BinaryPrimitives.ReadUInt16BigEndian(record[8..]),
            BinaryPrimitives.ReadUInt16BigEndian(record[10..]));
    }
}

/// <summary>
/// Object 4 (StepContent): `[a = step index][step record]`, 13 bytes (F74; the index is the fetch qualifier a), or for a type-2
/// "State" step `[a][step record][level count 0x60][96 levels][text block]`, 148 bytes (E12 seq 139240, E12b seq 87517; F153,
/// F154, F161: the console stored SmartSoft's label block verbatim). Lengths as the downloads showed them (13, 148); another
/// layout is refused.
/// </summary>
[Evidence(EvidenceStatus.Observed, "F74, F153, F154, F161", "13-byte records VERIFIED as bodies (F74, E2 + E3); the 148-byte State step OBSERVED in E12 and E12b")]
public sealed record StepContentObject(int Index, StepRecord Step, IReadOnlyList<byte>? Levels, TextBlock? Label)
{
    public static StepContentObject Decode(ReadOnlySpan<byte> body)
    {
        if (body.Length == 1 + StepRecord.Size)
        {
            return new StepContentObject(body[0], StepRecord.Decode(body[1..]), null, null);
        }

        if (body.Length < 2 + StepRecord.Size)
        {
            throw new InvalidDataException($"StepContent: {body.Length} bytes.");
        }

        var levelCount = body[1 + StepRecord.Size];
        if (body.Length != 2 + StepRecord.Size + levelCount + TextBlock.Size)
        {
            throw new InvalidDataException($"StepContent: level count {levelCount} does not match the body length {body.Length}.");
        }

        var levels = body.Slice(2 + StepRecord.Size, levelCount).ToArray();
        return new StepContentObject(body[0], StepRecord.Decode(body[1..]), levels, TextBlock.Decode(body[(2 + StepRecord.Size + levelCount)..]));
    }
}

/// <summary>
/// Object 3 (SequenceMain), 46 bytes: `[steps][b][u16 BE c][u16 BE d][u16 BE e][text block]`, the stack's header: SHOW04's
/// `$SEQUENCE 48 2 0 256 256` with 19 `CUE` steps is `13 02 00 00 01 00 01 00` (E2 seq 332, F74; the step count H35 CONFIRMED,
/// it followed SmartSoft's step commands in E12 and E12b, F151). The meaning of b to e is UNKNOWN (H42); they are kept raw.
/// </summary>
[Evidence(EvidenceStatus.Observed, "F74, F151", "body VERIFIED (F74); b-e UNKNOWN (H42)")]
public sealed record SequenceMainObject(int StepCount, int B, int C, int D, int E, TextBlock Label)
{
    public const int HeaderSize = 8;

    public static SequenceMainObject Decode(ReadOnlySpan<byte> body)
    {
        if (body.Length != HeaderSize + TextBlock.Size)
        {
            throw new InvalidDataException($"SequenceMain: {body.Length} bytes, {HeaderSize + TextBlock.Size} expected.");
        }

        return new SequenceMainObject(
            body[0],
            body[1],
            BinaryPrimitives.ReadUInt16BigEndian(body[2..]),
            BinaryPrimitives.ReadUInt16BigEndian(body[4..]),
            BinaryPrimitives.ReadUInt16BigEndian(body[6..]),
            TextBlock.Decode(body[HeaderSize..]));
    }
}

/// <summary>
/// Object 2 (Sequence): `[page][number][steps][b][u16 BE c][u16 BE d][u16 BE e][text block][steps × step record]`, 10 + 38 + 12n
/// bytes (132 with 7 steps, E2 seq 328; 144 with 8, E12; 156 with 9, E12b seq 87433; F74, F156). SHOW04's KOTLIK
/// (`$SEQUENCE 0 0 0 256 256`, 7 `$CUE` steps) is `00 00 07 00 00 00 01 00 01 00`; b is the run mode as SmartSoft writes it
/// (0 Loop, 1 One Shot, 2 Manual; F154, F162). Page and number are the fetch qualifiers (a, b), (0, 0) for the one sequence
/// ever seen; their reading as the sequence's page and slot is INFERRED (S11.16 row 3). c, d, e are UNKNOWN (H42).
/// </summary>
[Evidence(EvidenceStatus.Observed, "F74, F154, F156, F162", "body VERIFIED (F74); b = run mode as SmartSoft writes it (F154, F162); page / number INFERRED; c-e UNKNOWN")]
public sealed record SequenceObject(int Page, int Number, int B, int C, int D, int E, TextBlock Label, IReadOnlyList<StepRecord> Steps)
{
    public const int HeaderSize = 10;

    public static SequenceObject Decode(ReadOnlySpan<byte> body)
    {
        if (body.Length < HeaderSize + TextBlock.Size)
        {
            throw new InvalidDataException($"Sequence: {body.Length} bytes.");
        }

        var count = body[2];
        if (body.Length != HeaderSize + TextBlock.Size + count * StepRecord.Size)
        {
            throw new InvalidDataException($"Sequence: step count {count} does not match the body length {body.Length}.");
        }

        var steps = new StepRecord[count];
        for (var i = 0; i < count; i++)
        {
            steps[i] = StepRecord.Decode(body.Slice(HeaderSize + TextBlock.Size + i * StepRecord.Size, StepRecord.Size));
        }

        return new SequenceObject(
            body[0],
            body[1],
            body[3],
            BinaryPrimitives.ReadUInt16BigEndian(body[4..]),
            BinaryPrimitives.ReadUInt16BigEndian(body[6..]),
            BinaryPrimitives.ReadUInt16BigEndian(body[8..]),
            TextBlock.Decode(body.Slice(HeaderSize, TextBlock.Size)),
            steps);
    }
}

/// <summary>
/// Objects 8 (Page) and 9 (ChannelText), 39 bytes: `[a][text block]`, the label of page a + 1 (12) or of channel a + 1 (96); the
/// labels matched SHOW04's `$PAGE` and `$CHANNEL` blocks 12 / 12 and 96 / 96 (F73, E2; again in E3b and E12b).
/// </summary>
[Evidence(EvidenceStatus.Verified, "F73", "bodies VERIFIED as the console's (E2 + E3); the text matched the file, byte order H40 reading A (CONFIRMED for the console's side, F161)")]
public sealed record LabelObject(int Index, TextBlock Label)
{
    public static LabelObject Decode(ReadOnlySpan<byte> body)
    {
        if (body.Length != 1 + TextBlock.Size)
        {
            throw new InvalidDataException($"Label object: {body.Length} bytes, {1 + TextBlock.Size} expected.");
        }

        return new LabelObject(body[0], TextBlock.Decode(body[1..]));
    }
}
