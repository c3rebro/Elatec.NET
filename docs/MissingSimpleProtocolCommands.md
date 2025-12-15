# Simple Protocol commands not wrapped in `src/`

The current `TWN4ReaderDevice` wrappers cover the SYS (reset/version), RF tag search, ISO14443 transparent access, and MIFARE Classic/Ultralight helpers. The following Simple Protocol commands from DocRev25 are not surfaced in `src/` and require host-side framing/handling.

## API IO (0x01xx)
- **WriteByte** `[0100][Channel][Byte]` → `[00]`. Writes a single byte to a host/reader channel.【11669d†L1184-L1202】
- **ReadByte** `[0101][Channel]` → `[00][Byte]`. Reads a byte from the channel.【11669d†L1184-L1202】
- **TestEmpty/TestFull** `[0102|0103][Channel][Dir]` → `[00][Bool]`. Poll channel buffer state for read/write directions.【11669d†L1208-L1246】
- **GetBufferSize/GetByteCount** `[0104|0105][Channel][Dir]` → `[00][UInt16]` reporting configured buffer length or current fill level.【11669d†L1248-L1260】
- **SetCOMParameters** `[0106][Byte: Channel][Byte: Dir][UInt32: Baud][Byte: Parity][Byte: StopBits][Byte: DataBits]` → `[00][Bool]` to configure serial-style host links (see DocRev25 for field ordering). Subsequent commands assume the negotiated framing persists.【11669d†L1248-L1260】
- **GetUSBDeviceState/GetHostChannel** `[0107|0108]` → `[00][Byte]` returning USB state or host channel mapping.【11669d†L1248-L1260】
- **USBRemoteWakeup** `[0109]` → `[00]` to trigger remote wake.【11669d†L1248-L1260】
- **WriteBytes/ReadBytes** `[010A|010B][Channel][Len][Data]/[Channel][Len]` → `[00][Ack?][Payload]` for buffered multi-byte transfers; caller must respect buffer sizes/timing noted above.【11669d†L1248-L1260】

## API GPIO/Diagnostics (0x02xx)
DocRev25 exposes GPIO drive/LED/beeper frames beyond the basic `SetGpio*` helpers in `src/`:
- **GPIOConfigureOutputs/Inputs** `[0200|0201][Byte: Mask]` → `[00]` to set output vs. input direction.【18d50a†L47-L53】
- **GPIOSetBits/GPIOClearBits/GPIOToggleBits** `[0202|0203|0204][Byte: Mask]` → `[00]` for bitwise manipulation; **GPIOBlinkBits** `[0205][Mask][OnTime][OffTime][Repeat]` → `[00]` handles timed blinking.【18d50a†L47-L53】
- **GPIOTestBit** `[0206][Bit]` → `[00][Bool]` reads a single pin.【18d50a†L47-L53】
- **Beep/BeepOn/BeepOff** `[0207][Duration]` or `[0213|0214]` → `[00]` for one-shot or continuous tones; timing parameters are milliseconds.【18d50a†L53-L58】
- **DiagLEDOn/Off/Toggle/IsOn** `[0208|0209|020A|020B]` manage the diagnostic LED; `IsOn` returns `[00][Bool]`.【18d50a†L105-L109】
- **SendWiegand/SendOmron** `[020C|020D][Byte: BitLen][VarData]` → `[00][Bool]` emit interface frames after optional busy-time delays noted in DocRev25.【18d50a†L109-L114】
- **LEDInit/On/Off/Toggle/Blink** `[020E-0212]` configure and drive RGB panel LEDs; blink uses on/off times and repeat counters.【18d50a†L111-L115】

## API TILF LF/HF (0x03xx)
All TILF low-/high-frequency read/program/lock commands (selective page reads, special lock/write variants) lack wrappers. Each frame starts with `0x03` plus the operation code and carries page numbers, address ranges, optional passwords, and payload bytes; responses are `[00][Bool/Data...]` depending on the variant.【765278†L1-L25】

## API HITAG1S / HITAG2 (0x04xx)
The HITAG commands for block/page access, password set, and halt are not surfaced. They use `[04xx][Page/Block][Len][Data]` request frames with `[00][Bool/Data]` responses and rely on prior tag selection via RF.【765278†L25-L41】

## API SM4X00 (0x050x)
`SM4X00_GenericRaw` and `SM4X00_Generic` support transparent SM4X00 exchanges (`[0500|0501][Byte: Flags][Cmd][Data…][Buf]` → `[00][Bool][Data]`) but are absent from `src/`.【765278†L41-L48】

## API I2C host bridge (0x060x)
Reader-side I2C master controls are missing: `I2CInit`, `I2CDeInit`, `I2CMasterStart/Stop`, `Transmit/ReceiveByte`, `BeginWrite/BeginRead`, and `SetAck` with the byte-level request/response frames documented in DocRev25 (`[06xx][Addr/Data...]` → `[00][Bool/Data]`).【765278†L48-L63】

## API ISO15693 (0x0Dxx)
No ISO15693 transparent or helper wrappers exist:
- **ISO15693_GenericCommand** `[0D00][Flags][Command][Data…][BufferSize]` → `[00][Bool][Data]` for raw frame exchange.【d0c2f0†L3327-L3345】
- **GetSystemInformation / GetSystemInformationExt** `[0D01|0D02]` → `[00][Bool][SystemInfo(15 bytes)]`.【d0c2f0†L3347-L3355】【7f9f40†L3355-L3383】
- **GetTagTypeFromUID/SystemInfo** `[0D03|0D04][UID/SystemInfo]` → `[00][TagType]`.【7f9f40†L3385-L3427】
- **ReadSingleBlock / ReadSingleBlockExt / WriteSingleBlock / WriteSingleBlockExt** `[0D05-0D08][Block][BufferSize/Data]` → `[00][Bool][Data?]` (extended variants use 16‑bit blocks and optional flags; DocRev25 notes standard ISO15693 timing/CRC expectations).【7f9f40†L3429-L3434】

## Additional unwrapped areas
Beyond the categories above, DocRev25 lists many card- or interface-specific APIs not represented in `src/`, including crypto primitives (1.5.13), LEGIC/Desfire derivatives, FeliCa, SLE44xx memory ops, NTAG/Topaz helpers, SPI/BLE stacks, file-system (FS*) calls, and multiple proprietary card families (AT55, EM4150/4305, CTS, SRX, etc.). These follow the same framing pattern—`[API ID][FuncNo][Params…]` with `[00]` status+payload replies—and often note tag-presence timing constraints or buffer-size caps in the command descriptions’ remarks.【18d50a†L213-L757】

## Status/ACK behavior
DocRev25 uses `[00]` as the success byte in every response frame, followed by function-specific payloads (booleans, lengths, data arrays). Buffer-size parameters (`BufferSize`, `MaxLen`, `MaxRXByteCnt`) gate how many bytes the reader returns; callers must poll or throttle for channel/transport buffer fullness where documented (notably API IO read/write/test commands).

