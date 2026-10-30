namespace SmartFade.Domain;

/// <summary>
/// A console text label: three lines of up to six characters, as the console stores it for
/// memories, sequences, pages, channels and independents (OBSERVED, F73). The wire byte order
/// of the characters is still a HYPOTHESIS (H40); the text itself matched the show file.
/// </summary>
public sealed record Label(string Line1, string Line2, string Line3)
{
    public const int Lines = 3;
    public const int CharactersPerLine = 6;

    public static Label Empty { get; } = new(string.Empty, string.Empty, string.Empty);

    public bool IsEmpty => Line1.Length == 0 && Line2.Length == 0 && Line3.Length == 0;

    public override string ToString() =>
        string.Join(" ", new[] { Line1, Line2, Line3 }.Where(line => line.Length > 0));
}
