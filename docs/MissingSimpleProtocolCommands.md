# Simple Protocol coverage audit

> **Status: stale / needs re-audit**
>
> This file was created against an earlier source layout and must not be used as an authoritative command map. Several entries became outdated as additional Simple Protocol wrappers were implemented, and some API/function identifiers in the previous inventory were incorrect.
>
> Use [`DocRev25.txt`](DocRev25.txt) as the protocol reference until this coverage audit has been regenerated from the current `src/` tree.

## Verified status for API PERIPH

Issue #24 triggered a full check of the PERIPH group against DocRev25.

- Simple Protocol API PERIPH is `0x04xx`.
- The documented PERIPH commands `0x0400` through `0x0417` (excluding undocumented/reserved function numbers) are wrapped by `TWN4ReaderDevice`.
- `Beep` is `0x0407`.
- `LEDInit` is `0x0410`; LED on/off/toggle/blink are `0x0411` through `0x0414`.
- `BeepOn` and `BeepOff` are `0x0416` and `0x0417`.
- GPIO outputs must be configured as outputs before set/clear/toggle/blink operations. This GPIO initialization requirement is separate from the dedicated beeper commands.

Some functions present in the TWN4 App/API `SYSFUNC` interface are not commands of the stock Simple Protocol. They must not be assigned Simple Protocol command numbers merely because their App/API function numbers appear similar.

## Follow-up

A complete command-by-command coverage audit should compare the current `src/Api/Readers/TWN4ReaderDevice/Protocols/` implementation directly with DocRev25 before publishing a new "missing commands" inventory.
