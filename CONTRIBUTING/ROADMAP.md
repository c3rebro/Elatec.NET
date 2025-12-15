# TWN4ReaderDevice Public API Inventory and Roadmap

This note inventories the public surface of `TWN4ReaderDevice` grouped by protocol family and captures follow-up checks against the TWN4 Simple Protocol, DESFire, and API PDF command sets.

## Public API inventory (by protocol section)

### System functions (API_SYS)
- `ResetAsync`, `StartBootloaderAsync`, `GetSysTicksAsync`, `GetVersionStringAsync`, `GetUsbTypeAsync`, `GetDeviceTypeAsync`.
- Power/identity/configuration helpers: `SleepAsync`, `GetDeviceUidAsync`, `SetParametersAsync`, `GetLastErrorAsync`, `GetProdSerNoAsync`, `GetVersionInfoAsync`.

### Peripheral / GPIO (API_PERIPH)
- GPIO configuration and IO: `GpioConfigureOutputsAsync`, `GpioConfigureInputsAsync`, `GpioSetBitsAsync`, `GpioClearBitsAsync`, `GpioToggleBitsAsync`, `GpioBlinkBitsAsync`, `GpioTestBitAsync`.
- Beeper and diagnostic LED: `BeepAsync`, `BeepOnAsync`, `BeepOffAsync`, `DiagLedOnAsync`, `DiagLedOffAsync`, `DiagLedToggleAsync`, `DiagLedIsOnAsync`.
- LED helpers: `LedInitAsync` (overloads), `LedOnAsync`, `LedOffAsync`, `LedToggleAsync`, `LedBlinkAsync`.

### RF / tag discovery (API_RF)
- Constants expose RF command numbers plus `TagMaskFromTagType` helper.
- Discovery and RF settings: `SearchTagAsync` (with overload), `SetRFOffAsync`, `SetTagTypesAsync` (two overloads), `GetTagTypesAsync`, `GetSupportedTagTypesAsync`.
- Returned models: `SearchTagResult`, `GetTagTypesResult`, `GetSupportedTagTypesResult`.

### MIFARE Classic (API_MIFARECLASSIC)
- Session/authentication: `MifareClassic_LoginAsync`.
- Block IO/value operations: `MifareClassic_ReadBlockAsync`, `MifareClassic_WriteBlockAsync`, `MifareClassic_ReadValueBlockAsync`, `MifareClassic_WriteValueBlockAsync`, `MifareClassic_IncrementValueBlockAsync`, `MifareClassic_DecrementValueBlockAsync`, `MifareClassic_CopyValueBlockAsync`.

### MIFARE Ultralight & NTAG (API_MIFAREULTRALIGHT)
- Ultralight page IO: `MifareUltralight_ReadPageAsync`, `MifareUltralight_WritePageAsync`.
- Ultralight C auth/key provisioning: `MifareUltralightC_AuthenticateAsync` (hex and byte overloads), `MifareUltralightC_SAMAuthenticateAsync`, `MifareUltralightC_WriteKeyFromSAMAsync`.
- Ultralight EV1 features: `MifareUltralightEV1_FastReadAsync`, `MifareUltralightEV1_IncCounterAsync`, `MifareUltralightEV1_ReadCounterAsync`, `MifareUltralightEV1_ReadSigAsync`, `MifareUltralightEV1_GetVersionAsync`, `MifareUltralightEV1_PwdAuthAsync`, `MifareUltralightEV1_CheckTearingEventAsync`.

### MIFARE DESFire (API_MIFAREDESFIRE)
- Application management: `MifareDesfire_GetAppIDsAsync` (two overloads), `MifareDesfire_CreateApplicationAsync`, `MifareDesfire_DeleteApplicationAsync`, `MifareDesfire_SelectApplicationAsync`.
- Authentication/keys: `MifareDesfire_AuthenticateAsync`, `MifareDesfire_GetKeySettingsAsync`, `MifareDesfire_GetKeyVersionAsync`, `MifareDesfire_ChangeKeySettingsAsync`, `MifareDesfire_ChangeKeyAsync`, `MifareDesfire_SetDefaultKeyAsync`, `MifareDesfire_SetAtsAsync`, `MifareDesfire_DisableFormatCardAsync`, `MifareDesfire_EnableRandomIdAsync`.
- File enumeration and configuration: `MifareDesfire_GetFileIDsAsync`, `MifareDesfire_GetFileSettingsAsync`, `MifareDesfire_ChangeFileSettingsAsync`, `MifareDesfire_CreateStdDataFileAsync`, `MifareDesfire_CreateValueFileAsync`, `MifareDesfire_CreateRecordFileAsync`, `MifareDesfire_DeleteFileAsync`.
- Data/value operations: `MifareDesfire_ReadDataAsync`, `MifareDesfire_WriteDataAsync`, `MifareDesfire_ReadRecordsAsync`, `MifareDesfire_WriteRecordAsync`, `MifareDesfire_ClearRecordFileAsync`, `MifareDesfire_GetValueAsync`, `MifareDesfire_CreditAsync`, `MifareDesfire_DebitAsync`, `MifareDesfire_LimitedCreditAsync`, `MifareDesfire_GetFreeMemoryAsync`, `MifareDesfire_FormatTagAsync`, `MifareDesfire_CommitTransactionAsync`, `MifareDesfire_AbortTransactionAsync`, `MifareDesfire_GetUidAsync` (with optional buffer size), `MifareDesfire_GetVersionAsync`.

### ISO14443 (API_ISO14443)
- Activation data: `ISO14443A_GetAtsAsync`, `ISO14443B_GetAtqbAsync`, `ISO14443A_GetAtqaAsync`, `ISO14443A_GetSakAsync`, `ISO14443B_GetAnswerToAttribAsync`.
- Exchange/presence: `ISO14443_4_CheckPresenceAsync`, `ISO14443_4_TdxAsync`, `ISO14443_3_TdxAsync`.
- Multi-tag flow: `ISO14443A_SearchMultiTagAsync`, `ISO14443A_SelectTagAsync`, `ISO14443A_Reselect`.

### High-level helpers
- Feedback: `PlayMelody` plus nested `Tone` model.
- Tag identification: `GetSingleChipAsync`.
- Connection lifecycle and low-level calls: `ConnectAsync`, `DisconnectAsync`, `CallFunctionRawAsync`, `CallFunctionParserAsync`, `CallFunctionAsync`.
- Properties: `AvailableReadersCount`, `IsConnected`, `PortName`, `IsTWN4LegicReader`.

## Checklist against vendor command sets

- **Simple Protocol coverage (System/Peripheral/RF):** Implemented calls map to documented API numbers (e.g., SYS 1–10, PERIPH 0–23, RF 0–4). Missing/not implemented calls flagged in code include system diagnostic/parameter helpers (`SYSFUNC` 0, 11, 14, 16–19), Wiegand/Omron output and GPIO sequences (`PERIPH` 12–15, 21, 24), and ISO14443 ATR helper (`ISO14443` 10). These should be evaluated for necessity and mapped to parser types.
- **DESFire commands:** Core EV1 operations (app management, data/value files, transactions) are present. Remaining PDF-listed features to consider: EV2/EV3 secure messaging options, ISO7816 wrapping, and advanced key diversification are not exposed; no unit tests assert command sequencing or error handling.
- **Simple Protocol data models:** Helper types (`SearchTagResult`, `GetTagTypesResult`, `GetSupportedTagTypesResult`, `Tone`) are defined but lack XML remarks linking to specific protocol frames. Adding references to command numbers and expected status codes would improve traceability.
- **Testing/documentation gaps:** No unit tests cover command routing or parser expectations. Consider adding protocol-specific test matrices (e.g., success/failure parsing for each command above) and inline remarks for operations derived from internal/undocumented API PDF entries (e.g., `GetVersionInfoAsync`, `ISO14443A_Reselect`).

Use this list to validate future contributions: new features should map to the appropriate protocol region, fill the noted gaps, and add tests/docs that mirror the Simple Protocol and DESFire reference behavior.
