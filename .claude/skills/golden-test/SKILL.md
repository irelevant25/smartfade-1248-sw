---
name: golden-test
description: >
  Write a golden protocol test (xUnit) for the SmartFade 1248 encoder/decoder using only bytes from a
  committed capture in docs/protocol/captures/. Use when the user says "add a golden test",
  "test the decoder against the capture", "protocol test for message X", "replay test".
---

# Golden protocol test

Golden tests pin the protocol layer to evidence. Expected bytes come from a committed capture,
never from a keyboard.

## Preconditions

- A normalized capture `docs/protocol/captures/YYYY-MM-DD-<slug>.jsonl` exists with the frame(s).
- The message is at least **OBSERVED** (for decode tests) or **VERIFIED** (for encode tests whose
  output could be sent to hardware) in `docs/protocol/known-facts.md`.
- The experiment record `.md` explains what the operator did when the frame appeared.

## Procedure

1. Identify the frames: capture file, `seq` numbers, direction, transfer type.
2. Load bytes **from the capture file** in the test (a small test helper that reads the `.jsonl`
   and returns `data` for a given `seq`). Do not paste hex into the test source. If a literal is
   unavoidable for readability, it must be accompanied by a comment
   `// from docs/protocol/captures/<file>.jsonl seq <n>` and a second assertion comparing it to
   the capture-loaded bytes.
3. Test naming: `Decode_<Message>_FromCapture_<slug>_Seq<n>` / `Encode_<Command>_MatchesCapture_<slug>_Seq<n>`.
4. Assert on decoded domain values that the experiment record documents (e.g. the channel the
   operator moved and the level reached), not on internal structure.
5. Add the test to the protocol test project; reference the capture in the test's XML doc comment.
6. In `docs/protocol/known-facts.md` note the test name next to the fact it pins (Source column).

## Template (shape only; no bytes)

```csharp
[Fact]
public void Decode_<Message>_FromCapture_<slug>_Seq<n>()
{
    // Evidence: docs/protocol/captures/<file>.jsonl seq <n>, experiment E?, status OBSERVED/VERIFIED
    var frame = CaptureFixture.Load("<file>.jsonl").Frame(<n>);

    var message = protocol.Decode(frame.Data);

    // Expected values come from the experiment record's action log
    message.Should().BeOfType<...>();
}
```

## Never

- Use bytes from documentation alone as golden data (documentation is DOCUMENTED, not VERIFIED).
- Modify a capture file to make a test pass. If the decoder disagrees with the capture, the
  decoder or the hypothesis is wrong; record it via `/protocol-status`.
