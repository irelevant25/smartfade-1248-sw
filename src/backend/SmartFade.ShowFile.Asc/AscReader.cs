using System.Globalization;
using System.Text;

namespace SmartFade.ShowFile.Asc;

/// <summary>
/// Reads a SmartFade / SmartSoft <c>.asc</c> show file into a <see cref="ShowFile"/> without losing
/// anything: unknown lines are kept verbatim as <see cref="UnknownRecord"/>s with a warning, numbers
/// and times stay as the file wrote them, and the verbatim lines travel with the result so that the
/// file can be written back byte for byte. The structure and the keywords are the OBSERVED ones of
/// docs/formats/asc-format.md; the meaning of most fields is INFERRED at most and is not interpreted
/// here beyond splitting the tokens.
/// </summary>
public static class AscReader
{
    private static readonly Encoding Latin1 = Encoding.Latin1;

    public static AscReadResult Read(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        var text = Latin1.GetString(bytes);
        var lineEnding = text.Contains("\r\n", StringComparison.Ordinal) ? AscLineEnding.CrLf : AscLineEnding.Lf;
        var lines = SplitLines(text, lineEnding);
        var parser = new Parser(lines, lineEnding);
        return parser.Parse();
    }

    public static AscReadResult Read(string path) => Read(File.ReadAllBytes(path));

    private static List<string> SplitLines(string text, AscLineEnding lineEnding)
    {
        var separator = lineEnding == AscLineEnding.CrLf ? "\r\n" : "\n";
        var parts = text.Split(separator, StringSplitOptions.None);
        var lines = new List<string>(parts);
        // a file that ends with a line break yields one empty final part; it is the terminator, not a line
        if (lines.Count > 0 && lines[^1].Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        return lines;
    }

    private sealed class Parser
    {
        private readonly IReadOnlyList<string> _lines;
        private readonly AscLineEnding _lineEnding;
        private readonly List<AscWarning> _warnings = new();
        private readonly List<UnknownRecord> _unknown = new();
        private readonly List<Sequence> _sequences = new();
        private readonly List<Memory> _memories = new();
        private readonly List<PatchEntry> _patch = new();
        private readonly List<Independent> _independents = new();
        private readonly List<LabelledItem> _channels = new();
        private readonly List<LabelledItem> _pages = new();
        private readonly List<Setting> _settings = new();
        private string _manufacturer = string.Empty;
        private string _console = string.Empty;
        private string _softwareVersion = string.Empty;
        private string _ident = string.Empty;
        private bool _clearAll;
        private Sequence? _mainStack;
        private string _section = "(header)";
        private int _index;

        public Parser(IReadOnlyList<string> lines, AscLineEnding lineEnding)
        {
            _lines = lines;
            _lineEnding = lineEnding;
        }

        private int LineNumber => _index + 1;

        public AscReadResult Parse()
        {
            var ended = false;
            while (_index < _lines.Count)
            {
                var line = _lines[_index];
                if (line.Length == 0)
                {
                    _index++;
                    continue;
                }

                var keyword = Keyword(line);
                switch (keyword)
                {
                    case "MANUFACTURER":
                        _manufacturer = Rest(line, keyword);
                        _index++;
                        break;
                    case "CONSOLE":
                        _console = Rest(line, keyword);
                        _index++;
                        break;
                    case "$SOFTWAREVERSION":
                        _softwareVersion = Rest(line, keyword);
                        _index++;
                        break;
                    case "IDENT":
                        _ident = Rest(line, keyword);
                        _index++;
                        break;
                    case "CLEAR":
                        _clearAll = Rest(line, keyword) == "ALL";
                        _index++;
                        break;
                    case "!":
                        _section = Rest(line, keyword);
                        _index++;
                        break;
                    case "$SEQUENCE":
                        ParseSequence(line);
                        break;
                    case "GROUP":
                        ParseMemory(line);
                        break;
                    case "PATCH":
                        ParsePatch(line);
                        _index++;
                        break;
                    case "$INDEPENDENT":
                        ParseIndependent(line);
                        break;
                    case "$CHANNEL":
                        _channels.Add(ParseLabelled(line, keyword));
                        break;
                    case "$PAGE":
                        _pages.Add(ParseLabelled(line, keyword));
                        break;
                    case "SET":
                        ParseSetting(line);
                        _index++;
                        break;
                    case "ENDDATA":
                        ended = true;
                        _index++;
                        break;
                    default:
                        Unknown(line, keyword);
                        _index++;
                        break;
                }

                if (ended)
                {
                    break;
                }
            }

            if (!ended)
            {
                _warnings.Add(new AscWarning(_lines.Count, "ENDDATA is missing: the file may be truncated."));
            }

            while (_index < _lines.Count)
            {
                if (_lines[_index].Length > 0)
                {
                    Unknown(_lines[_index], Keyword(_lines[_index]));
                }

                _index++;
            }

            var show = new ShowFile(
                _manufacturer,
                _console,
                _softwareVersion,
                _ident,
                _clearAll,
                _mainStack,
                _sequences,
                _memories,
                _patch,
                _independents,
                _channels,
                _pages,
                _settings,
                _unknown,
                _lineEnding);
            return new AscReadResult(show, _lines, _warnings);
        }

        private void ParseSequence(string line)
        {
            var fields = Fields(line);
            if (fields.Length != 6 || !TryInts(fields, 1, out var values))
            {
                Unknown(line, "$SEQUENCE");
                _index++;
                return;
            }

            _index++;
            SkipBlank();
            TextBlock? label = null;
            if (_index < _lines.Count && Keyword(_lines[_index]) == "Text")
            {
                label = ParseTextBlock();
                SkipBlank();
            }

            var steps = new List<Step>();
            while (_index < _lines.Count)
            {
                var keyword = Keyword(_lines[_index]);
                if (keyword is "CUE" or "$CUE")
                {
                    steps.Add(ParseStep());
                    SkipBlank();
                }
                else
                {
                    break;
                }
            }

            var sequence = new Sequence(values[0], values[1], values[2], values[3], values[4], label, steps);
            if (sequence.IsMainStack && _mainStack is null)
            {
                _mainStack = sequence;
            }
            else
            {
                _sequences.Add(sequence);
            }
        }

        private Step ParseStep()
        {
            var header = _lines[_index];
            var number = Rest(header, Keyword(header)).Trim();   // the file right-aligns it in four characters; the writer pads it again
            _index++;
            (int Page, int Fader)? memory = null;
            int? cueParams = null;
            string upTime = string.Empty, upDelay = string.Empty, downTime = string.Empty, downDelay = string.Empty, followOn = string.Empty;
            var channels = new List<ChannelLevelPercent>();
            TextBlock? label = null;
            while (_index < _lines.Count && _lines[_index].Length > 0)
            {
                var line = _lines[_index];
                var keyword = Keyword(line);
                var fields = Fields(line);
                switch (keyword)
                {
                    case "$$MEMORY" when fields.Length == 3 && TryInts(fields, 1, out var pf):
                        memory = (pf[0], pf[1]);
                        _index++;
                        break;
                    case "$$CUEPARAMS" when fields.Length == 2 && int.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var cp):
                        cueParams = cp;
                        _index++;
                        break;
                    case "UP" when fields.Length == 3:
                        (upTime, upDelay) = (fields[1], fields[2]);
                        _index++;
                        break;
                    case "DOWN" when fields.Length == 3:
                        (downTime, downDelay) = (fields[1], fields[2]);
                        _index++;
                        break;
                    case "FOLLOWON" when fields.Length == 2:
                        followOn = fields[1];
                        _index++;
                        break;
                    case "CHAN":
                        channels.AddRange(ParseChannels(line));
                        _index++;
                        break;
                    case "Text":
                        label = ParseTextBlock();
                        break;
                    case "CUE" or "$CUE" or "$SEQUENCE" or "GROUP" or "!" or "ENDDATA":
                        return new Step(number, memory, cueParams, upTime, upDelay, downTime, downDelay, followOn, channels, label);
                    default:
                        Unknown(line, keyword);
                        _index++;
                        break;
                }
            }

            return new Step(number, memory, cueParams, upTime, upDelay, downTime, downDelay, followOn, channels, label);
        }

        private void ParseMemory(string line)
        {
            var fields = Fields(line);
            if (fields.Length != 2 || !int.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
            {
                Unknown(line, "GROUP");
                _index++;
                return;
            }

            _index++;
            var channels = new List<ChannelLevelPercent>();
            while (_index < _lines.Count && Keyword(_lines[_index]) == "CHAN")
            {
                channels.AddRange(ParseChannels(_lines[_index]));
                _index++;
            }

            var label = Keyword(_lines.ElementAtOrDefault(_index) ?? string.Empty) == "Text" ? ParseTextBlock() : TextBlock.Empty;
            _memories.Add(new Memory(number, channels, label));
        }

        private void ParsePatch(string line)
        {
            var fields = Fields(line);
            // PATCH 1 d/c/l d/c/l ... (five triples per line, two on the last)
            for (var i = 2; i < fields.Length; i++)
            {
                var parts = fields[i].Split('/');
                if (parts.Length == 3 && TryInts(parts, 0, out var v))
                {
                    _patch.Add(new PatchEntry(v[0], v[1], v[2]));
                }
                else
                {
                    _warnings.Add(new AscWarning(LineNumber, $"PATCH entry '{fields[i]}' is not d/c/l; kept as unknown."));
                    Unknown(line, "PATCH");
                    return;
                }
            }
        }

        private void ParseIndependent(string line)
        {
            var fields = Fields(line);
            if (fields.Length != 6 || !TryInts(fields, 1, out var v))
            {
                Unknown(line, "$INDEPENDENT");
                _index++;
                return;
            }

            _index++;
            var label = Keyword(_lines.ElementAtOrDefault(_index) ?? string.Empty) == "Text" ? ParseTextBlock() : TextBlock.Empty;
            _independents.Add(new Independent(v[0], v[1], v[2], v[3], v[4], label));
        }

        private LabelledItem ParseLabelled(string line, string keyword)
        {
            var fields = Fields(line);
            var number = fields.Length == 2 && int.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : -1;
            if (number < 0)
            {
                _warnings.Add(new AscWarning(LineNumber, $"{keyword} without a number: '{line}'."));
            }

            _index++;
            var label = Keyword(_lines.ElementAtOrDefault(_index) ?? string.Empty) == "Text" ? ParseTextBlock() : TextBlock.Empty;
            return new LabelledItem(number, label);
        }

        private void ParseSetting(string line)
        {
            var fields = Fields(line);
            if (fields.Length < 3)
            {
                Unknown(line, "SET");
                return;
            }

            _settings.Add(new Setting(fields[1], string.Join(' ', fields.Skip(2))));
        }

        /// <summary>Text, then $$Text 1..3, each optional; the texts are the rest of each line, verbatim.</summary>
        private TextBlock ParseTextBlock()
        {
            var text = string.Empty;
            var numbered = new[] { string.Empty, string.Empty, string.Empty };
            if (_index < _lines.Count && Keyword(_lines[_index]) == "Text")
            {
                text = Rest(_lines[_index], "Text");
                _index++;
            }

            while (_index < _lines.Count && Keyword(_lines[_index]) == "$$Text")
            {
                var line = _lines[_index];
                var rest = Rest(line, "$$Text");
                var space = rest.IndexOf(' ', StringComparison.Ordinal);
                var numberText = space < 0 ? rest : rest[..space];
                var value = space < 0 ? string.Empty : rest[(space + 1)..];
                if (int.TryParse(numberText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n is >= 1 and <= 3)
                {
                    numbered[n - 1] = value;
                }
                else
                {
                    _warnings.Add(new AscWarning(LineNumber, $"$$Text with an unexpected line number: '{line}'."));
                    Unknown(line, "$$Text");
                }

                _index++;
            }

            return new TextBlock(text, numbered[0], numbered[1], numbered[2]);
        }

        private IEnumerable<ChannelLevelPercent> ParseChannels(string line)
        {
            var fields = Fields(line);
            for (var i = 1; i < fields.Length; i++)
            {
                var parts = fields[i].Split('/');
                if (parts.Length == 2 && TryInts(parts, 0, out var v))
                {
                    yield return new ChannelLevelPercent(v[0], v[1]);
                }
                else
                {
                    _warnings.Add(new AscWarning(LineNumber, $"CHAN entry '{fields[i]}' is not channel/percent; skipped."));
                }
            }
        }

        private void SkipBlank()
        {
            while (_index < _lines.Count && _lines[_index].Length == 0)
            {
                _index++;
            }
        }

        private void Unknown(string line, string keyword)
        {
            _unknown.Add(new UnknownRecord(keyword, line, LineNumber, _section));
            _warnings.Add(new AscWarning(LineNumber, $"Unknown or malformed line kept verbatim: '{line}'."));
        }

        private static string Keyword(string line)
        {
            var space = line.IndexOf(' ', StringComparison.Ordinal);
            return space < 0 ? line : line[..space];
        }

        /// <summary>Everything after the keyword and one space, verbatim (trailing spaces included).</summary>
        private static string Rest(string line, string keyword) =>
            line.Length > keyword.Length + 1 ? line[(keyword.Length + 1)..] : string.Empty;

        private static string[] Fields(string line) => line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        private static bool TryInts(string[] fields, int from, out int[] values)
        {
            values = new int[fields.Length - from];
            for (var i = from; i < fields.Length; i++)
            {
                if (!int.TryParse(fields[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out values[i - from]))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
