namespace SmartFade.Protocol;

/// <summary>Status vocabulary of docs/protocol/README.md.</summary>
public enum EvidenceStatus
{
    /// <summary>Reproduced on our console in two independent sessions with recorded evidence.</summary>
    Verified,

    /// <summary>Seen in a capture or on hardware at least once.</summary>
    Observed,

    /// <summary>Stated in official ETC documentation, not checked on our unit.</summary>
    Documented,

    /// <summary>Derived from static analysis or consistent patterns in data.</summary>
    Inferred,

    /// <summary>A theory that still needs a specific test.</summary>
    Hypothesis,

    Unknown,
}

/// <summary>
/// Links a protocol element to the knowledge base (fact / hypothesis IDs in
/// docs/protocol/known-facts.md and hypotheses.md). VERIFIED is necessary, not sufficient:
/// nothing in this assembly is cleared for sending to a console (CLAUDE.md), and this
/// assembly contains decoders only.
/// </summary>
[AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = false)]
public sealed class EvidenceAttribute : Attribute
{
    public EvidenceAttribute(EvidenceStatus status, string facts, string? note = null)
    {
        Status = status;
        Facts = facts;
        Note = note;
    }

    public EvidenceStatus Status { get; }

    public string Facts { get; }

    public string? Note { get; }
}
