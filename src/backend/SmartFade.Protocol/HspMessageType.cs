namespace SmartFade.Protocol;

/// <summary>
/// Word 0 of the 12-byte HSP header. 0, 1 and 2 go PC → console, 3 and 4 console → PC
/// (VERIFIED, F52 / F53).
/// </summary>
[Evidence(EvidenceStatus.Verified, "F52, F53")]
public enum HspMessageType : ushort
{
    /// <summary>PC → console: idle poll, no payload. The console answers with a reply header.</summary>
    Poll = 0,

    /// <summary>PC → console: a data write follows (payload of header word 1 bytes).</summary>
    Data = 1,

    /// <summary>PC → console: fetch the object named by words 2..5 (id, a, b, c).</summary>
    Fetch = 2,

    /// <summary>Console → PC: the fetched object follows; words 2..5 echo the fetch.</summary>
    ObjectReply = 3,

    /// <summary>Console → PC: poll reply; word 1 = 0 when nothing is pending, else a DirtyObjects list follows.</summary>
    PollReply = 4,
}
