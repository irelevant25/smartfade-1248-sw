using System.Globalization;
using SmartFade.ShowFile.Asc;

// A thin command line over SmartFade.ShowFile.Asc (docs/architecture.md: "ASC converter: library plus a thin CLI").
// It reads files and writes to the console only; it never writes a show file over another one.
//
//   asc-converter inspect <file.asc>     what the file holds, with the reader's warnings
//   asc-converter roundtrip <file.asc>   read, render again, compare byte for byte (exit code 0 = identical)
//   asc-converter render <file.asc> <out.asc>   read and write the rendered copy to a new file (refuses to overwrite)

return args switch
{
    ["inspect", var path] => Inspect(path),
    ["roundtrip", var path] => RoundTrip(path),
    ["render", var path, var output] => Render(path, output),
    _ => Usage(),
};

static int Usage()
{
    Console.Error.WriteLine("usage: asc-converter inspect <file.asc> | roundtrip <file.asc> | render <file.asc> <out.asc>");
    return 2;
}

static int Inspect(string path)
{
    var read = AscReader.Read(path);
    var show = read.Show;
    var ci = CultureInfo.InvariantCulture;
    Console.WriteLine(string.Create(ci, $"{Path.GetFileName(path)}: {read.Lines.Count} lines, {show.LineEnding} line endings"));
    Console.WriteLine(string.Create(ci, $"  {show.Manufacturer} {show.Console}, software {show.SoftwareVersion}, IDENT {show.Ident}{(show.ClearAll ? ", CLEAR ALL" : string.Empty)}"));
    if (show.MainStack is { } main)
    {
        Console.WriteLine(string.Create(ci, $"  main stack: $SEQUENCE {main.A} {main.B} {main.C} {main.D} {main.E}, {main.Steps.Count} steps"));
    }

    foreach (var sequence in show.Sequences)
    {
        Console.WriteLine(string.Create(ci, $"  sequence {sequence.A}: {sequence.Steps.Count} steps, label '{sequence.Label?.Line1}'"));
    }

    Console.WriteLine(string.Create(ci, $"  {show.Memories.Count} memories: {string.Join(", ", show.Memories.Select(m => m.Number + (m.Label.Line1.Length > 0 ? " " + m.Label.Line1 : string.Empty)))}"));
    Console.WriteLine(string.Create(ci, $"  patch: {show.Patch.Count} entries, {show.Patch.Count(p => p.Channel != 0)} patched, {show.Patch.Count(p => p.Level != 100)} with a level other than 100"));
    Console.WriteLine(string.Create(ci, $"  {show.Independents.Count} independents, {show.Channels.Count} channel labels ({show.Channels.Count(c => !c.Label.IsEmpty)} named), {show.Pages.Count} pages ({show.Pages.Count(p => !p.Label.IsEmpty)} named)"));
    Console.WriteLine(string.Create(ci, $"  settings: {string.Join(", ", show.Settings.Select(s => s.Key + "=" + s.Value))}"));
    if (show.Unknown.Count > 0)
    {
        Console.WriteLine(string.Create(ci, $"  {show.Unknown.Count} unknown lines kept verbatim"));
    }

    foreach (var warning in read.Warnings)
    {
        Console.WriteLine(string.Create(ci, $"  warning, line {warning.LineNumber}: {warning.Message}"));
    }

    return 0;
}

static int RoundTrip(string path)
{
    var bytes = File.ReadAllBytes(path);
    var read = AscReader.Read(bytes);
    var rendered = AscWriter.Render(read.Show);
    if (bytes.AsSpan().SequenceEqual(rendered))
    {
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{Path.GetFileName(path)}: identical after read and render ({bytes.Length} bytes)"));
        return 0;
    }

    var first = 0;
    while (first < bytes.Length && first < rendered.Length && bytes[first] == rendered[first])
    {
        first++;
    }

    var line = bytes.AsSpan(0, first).ToArray().Count(b => b == (byte)'\n') + 1;
    Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{Path.GetFileName(path)}: differs at byte {first} (line {line}); file {bytes.Length} bytes, rendered {rendered.Length}"));
    return 1;
}

static int Render(string path, string output)
{
    if (File.Exists(output))
    {
        Console.Error.WriteLine("refusing to overwrite " + output);
        return 3;
    }

    var read = AscReader.Read(path);
    File.WriteAllBytes(output, AscWriter.Render(read.Show));
    Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"wrote {output} ({read.Warnings.Count} reader warnings)"));
    return 0;
}
