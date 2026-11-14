#pragma warning disable S2360 // Optional parameters are intended: callers skip some of them by name (e.g. le: 0, maxRxByteCount: 128), which overloads cannot express.
#pragma warning disable S2333 // TWN4ReaderDevice is split over many files, this analysis only sees one of them.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Elatec.NET.Helpers.ByteArrayHelper.Extensions;

namespace Elatec.NET
{
    public partial class TWN4ReaderDevice
    {
        #region API_ISO7816 / ISO7816 Contact Card Functions

        /// <summary>
        /// TWN4 Simple Protocol API identifier for the ISO7816 contact card functions (0x10xx).
        /// </summary>
        /// <remarks>
        /// Use these functions to talk to ISO7816 compliant contact cards in the reader's slots, e.g. a SAM
        /// (Secure Access Module such as the NXP SAM AV3) in <see cref="Iso7816Channel.Sam1"/>.
        /// A typical communication flow looks like this:
        /// <list type="number">
        ///     <item><see cref="ISO7816_GetSlotStatusAsync"/> to see if a card is inserted.</item>
        ///     <item><see cref="ISO7816_IccPowerOnAsync"/> to activate the card and receive the ATR.</item>
        ///     <item>Optionally a PPS exchange via <see cref="ISO7816_TransceiveAsync"/> followed by <see cref="ISO7816_SetCommSettingsAsync"/>.</item>
        ///     <item><see cref="ISO7816_ExchangeAPDUAsync"/> to exchange APDUs.</item>
        ///     <item><see cref="ISO7816_IccPowerOffAsync"/> to deactivate the card.</item>
        /// </list>
        /// </remarks>
        public static readonly byte API_ISO7816 = 0x10;

        private static readonly byte ISO7816_GETSLOTSTATUS = 0x00;
        private static readonly byte ISO7816_ICCPOWERON = 0x01;
        private static readonly byte ISO7816_ICCPOWEROFF = 0x02;
        private static readonly byte ISO7816_SETCOMMSETTINGS = 0x03;
        private static readonly byte ISO7816_TRANSCEIVE = 0x04;
        private static readonly byte ISO7816_EXCHANGEAPDU = 0x05;
        private static readonly byte ISO7816_T0_TPDU = 0x06;
        private static readonly byte ISO7816_CHECKWELLKNOWNCARDS = 0x07;

        private static readonly byte ISO7816_FLAG_LEPRESENT = 0x01;
        private static readonly byte ISO7816_FLAG_EXTENDEDAPDU = 0x02;

        /// <summary>
        /// Queries the physical status of a card slot, e.g. to find out if a card is inserted.
        /// The internal state of the card is not changed.
        /// Simple Protocol command: 0x1000.
        /// </summary>
        /// <param name="channel">The card slot to query.</param>
        /// <returns>The slot status, or <see langword="null"/> if the reader reported the operation as failed.</returns>
        public async Task<Iso7816SlotStatus> ISO7816_GetSlotStatusAsync(Iso7816Channel channel)
        {
            var parser = await CallFunctionAsync(new[] { API_ISO7816, ISO7816_GETSLOTSTATUS, (byte)channel });
            var success = parser.ParseBool();
            if (!success)
            {
                return null;
            }

            return ParseIso7816SlotStatus(parser);
        }

        /// <summary>
        /// Activates the card in the given slot and returns its ATR (Answer To Reset). All communication parameters
        /// are reset to their defaults. On TWN4 Desktop this always performs a warm reset, on TWN4 SmartCard the first
        /// call performs a cold reset and subsequent calls a warm reset until the card is deactivated.
        /// Simple Protocol command: 0x1001.
        /// </summary>
        /// <param name="channel">The card slot to activate.</param>
        /// <param name="powerSelect">Operating voltage, only effective on TWN4 SmartCard readers.</param>
        /// <param name="maxAtrByteCount">Maximum number of ATR bytes the reader may return.</param>
        /// <returns>The ATR and slot state, or <see langword="null"/> if the reader reported the operation as failed (e.g. no card inserted).</returns>
        public async Task<Iso7816PowerOnResult> ISO7816_IccPowerOnAsync(Iso7816Channel channel, Iso7816PowerSelect powerSelect = Iso7816PowerSelect.Auto, byte maxAtrByteCount = byte.MaxValue)
        {
            var parser = await CallFunctionAsync(new[] { API_ISO7816, ISO7816_ICCPOWERON, (byte)channel, maxAtrByteCount, (byte)powerSelect });
            var success = parser.ParseBool();
            if (!success)
            {
                return null;
            }

            var atr = parser.ParseVarByteArray();
            var statusRegister = parser.ParseByte();

            return new Iso7816PowerOnResult
            {
                Atr = atr,
                IccStatus = DecodeIso7816IccStatus(statusRegister),
                CommandStatus = DecodeIso7816CommandStatus(statusRegister),
                Error = parser.ParseByte()
            };
        }

        /// <summary>
        /// Deactivates and powers off the card in the given slot. On TWN4 SmartCard a subsequent
        /// <see cref="ISO7816_IccPowerOnAsync"/> leads to a cold reset.
        /// Simple Protocol command: 0x1002.
        /// </summary>
        /// <param name="channel">The card slot to deactivate.</param>
        /// <returns>The slot status, or <see langword="null"/> if the reader reported the operation as failed.</returns>
        public async Task<Iso7816SlotStatus> ISO7816_IccPowerOffAsync(Iso7816Channel channel)
        {
            var parser = await CallFunctionAsync(new[] { API_ISO7816, ISO7816_ICCPOWEROFF, (byte)channel });
            var success = parser.ParseBool();
            if (!success)
            {
                return null;
            }

            return ParseIso7816SlotStatus(parser);
        }

        /// <summary>
        /// Assigns new communication settings to the card slot. Call this after a PPS exchange
        /// (see <see cref="ISO7816_TransceiveAsync"/>) so the negotiated parameters become valid.
        /// Specific parameters must be obtained from the ATR, see ISO/IEC 7816-3.
        /// Simple Protocol command: 0x1003.
        /// </summary>
        /// <param name="channel">The card slot to configure.</param>
        /// <param name="settings">The new communication settings.</param>
        /// <exception cref="ArgumentNullException"><paramref name="settings"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException"><paramref name="settings"/> lacks the protocol data for its protocol.</exception>
        /// <exception cref="ReaderException">The reader reported that the operation failed.</exception>
        public async Task ISO7816_SetCommSettingsAsync(Iso7816Channel channel, Iso7816CommSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            var bytes = new List<byte> { API_ISO7816, ISO7816_SETCOMMSETTINGS, (byte)channel };
            bytes.AddRange(settings.ToBytes());

            var parser = await CallFunctionAsync(bytes.ToArray());
            var success = parser.ParseBool();

            if (!success)
            {
                throw new ReaderException("Setting the ISO7816 communication settings failed.", ReaderError.General);
            }
        }

        /// <summary>
        /// Transparent, byte-wise communication with the card, e.g. for a PPS (Protocol and Parameter Selection) request.
        /// Use <see cref="ISO7816_ExchangeAPDUAsync"/> for regular APDU exchange.
        /// Simple Protocol command: 0x1004.
        /// </summary>
        /// <param name="channel">The card slot to communicate with.</param>
        /// <param name="tx">The bytes to transmit to the card.</param>
        /// <param name="maxRxByteCount">Maximum number of bytes the reader may return.</param>
        /// <returns>The bytes received from the card, or <see langword="null"/> if the reader reported the operation as failed.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="tx"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException"><paramref name="tx"/> is longer than 65535 bytes.</exception>
        public async Task<byte[]> ISO7816_TransceiveAsync(Iso7816Channel channel, byte[] tx, byte maxRxByteCount = byte.MaxValue)
        {
            if (tx == null)
            {
                throw new ArgumentNullException(nameof(tx));
            }

            if (tx.Length > ushort.MaxValue)
            {
                throw new ArgumentException("Data to transmit must not exceed 65535 bytes.", nameof(tx));
            }

            var bytes = new List<byte> { API_ISO7816, ISO7816_TRANSCEIVE, (byte)channel };
            bytes.AddUInt16((ushort)tx.Length);
            bytes.AddRange(tx);
            bytes.Add(maxRxByteCount);

            var parser = await CallFunctionAsync(bytes.ToArray());
            var success = parser.ParseBool();
            if (!success)
            {
                return null;
            }

            return parser.ParseUInt16LengthPrefixedByteArray();
        }

        /// <summary>
        /// Exchanges an APDU with the card using the T=0 or T=1 protocol according to ISO/IEC 7816-3.
        /// Simple Protocol command: 0x1005.
        /// </summary>
        /// <param name="channel">The card slot to communicate with.</param>
        /// <param name="cla">Class byte.</param>
        /// <param name="ins">Instruction byte.</param>
        /// <param name="p1">Parameter 1.</param>
        /// <param name="p2">Parameter 2.</param>
        /// <param name="data">The command data field, <see langword="null"/> or empty for none. Lc is derived from its length.</param>
        /// <param name="le">The expected length of the response data field (Le), <see langword="null"/> if Le shall not be transmitted.</param>
        /// <param name="extendedApdu">Send the APDU as extended APDU. Without it, <paramref name="data"/> is limited to 255 bytes.</param>
        /// <param name="maxRxByteCount">Size of the reader's receive buffer for the response data field.</param>
        /// <returns>The response data and status word, or <see langword="null"/> if the reader reported the operation as failed.</returns>
        /// <exception cref="ArgumentException"><paramref name="data"/> is too long for the chosen APDU format.</exception>
        /// <remarks>
        /// The status word is the card's SW1SW2, e.g. 0x9000 for success. A card error such as 0x6A82 is not a failure
        /// of this call: it is returned in <see cref="Iso7816ApduResponse.StatusWord"/>.
        /// </remarks>
        public async Task<Iso7816ApduResponse> ISO7816_ExchangeAPDUAsync(Iso7816Channel channel, byte cla, byte ins, byte p1, byte p2,
            byte[] data = null, ushort? le = null, bool extendedApdu = false, ushort maxRxByteCount = 256)
        {
            var txData = data ?? Array.Empty<byte>();
            if (txData.Length > (extendedApdu ? ushort.MaxValue : byte.MaxValue))
            {
                throw new ArgumentException(extendedApdu
                    ? "The data field of an extended APDU must not exceed 65535 bytes."
                    : "The data field of a short APDU must not exceed 255 bytes, use an extended APDU for more.", nameof(data));
            }

            // Simple Protocol: ISO7816_ExchangeAPDU [1005][Byte: Channel][Byte Array(9): Header][Byte Array(Var), 2 LB: TXData][UInt16: MaxRXByteCnt]
            // Header: [CLA][INS][P1][P2][UInt16: Lc][UInt16: Le][Byte: Flags (bit 0 = LePresent, bit 1 = ExtendedAPDU)]
            var bytes = new List<byte> { API_ISO7816, ISO7816_EXCHANGEAPDU, (byte)channel, cla, ins, p1, p2 };
            bytes.AddUInt16((ushort)txData.Length);
            bytes.AddUInt16(le.GetValueOrDefault());
            bytes.Add((byte)((le.HasValue ? ISO7816_FLAG_LEPRESENT : 0) | (extendedApdu ? ISO7816_FLAG_EXTENDEDAPDU : 0)));
            bytes.AddUInt16((ushort)txData.Length);
            bytes.AddRange(txData);
            bytes.AddUInt16(maxRxByteCount);

            var parser = await CallFunctionAsync(bytes.ToArray());
            var success = parser.ParseBool();
            if (!success)
            {
                return null;
            }

            return ParseIso7816ApduResponse(parser);
        }

        /// <summary>
        /// Exchanges a T=0 TPDU with the card. In contrast to <see cref="ISO7816_ExchangeAPDUAsync"/> the header
        /// is passed on as is, P3 is the raw T=0 length byte (Lc or Le, depending on the direction of the command).
        /// Simple Protocol command: 0x1006.
        /// </summary>
        /// <param name="channel">The card slot to communicate with.</param>
        /// <param name="cla">Class byte.</param>
        /// <param name="ins">Instruction byte.</param>
        /// <param name="p1">Parameter 1.</param>
        /// <param name="p2">Parameter 2.</param>
        /// <param name="p3">Parameter 3, the T=0 length byte.</param>
        /// <param name="data">The command data field, <see langword="null"/> or empty for none.</param>
        /// <param name="maxRxByteCount">Size of the reader's receive buffer for the response data field.</param>
        /// <returns>The response data and status word, or <see langword="null"/> if the reader reported the operation as failed.</returns>
        /// <exception cref="ArgumentException"><paramref name="data"/> is longer than 65535 bytes.</exception>
        public async Task<Iso7816ApduResponse> ISO7816_T0_TPDUAsync(Iso7816Channel channel, byte cla, byte ins, byte p1, byte p2, byte p3,
            byte[] data = null, ushort maxRxByteCount = 256)
        {
            var txData = data ?? Array.Empty<byte>();
            if (txData.Length > ushort.MaxValue)
            {
                throw new ArgumentException("Data field must not exceed 65535 bytes.", nameof(data));
            }

            // Simple Protocol: ISO7816_T0_TPDU [1006][Byte: Channel][Byte Array(5): Header][Byte Array(Var), 2 LB: TXData][UInt16: MaxRXByteCnt]
            var bytes = new List<byte> { API_ISO7816, ISO7816_T0_TPDU, (byte)channel, cla, ins, p1, p2, p3 };
            bytes.AddUInt16((ushort)txData.Length);
            bytes.AddRange(txData);
            bytes.AddUInt16(maxRxByteCount);

            var parser = await CallFunctionAsync(bytes.ToArray());
            var success = parser.ParseBool();
            if (!success)
            {
                return null;
            }

            return ParseIso7816ApduResponse(parser);
        }

        /// <summary>
        /// Checks whether the card in the given slot is one of the cards the firmware knows.
        /// Simple Protocol command: 0x1007.
        /// </summary>
        /// <param name="channel">The card slot to check.</param>
        /// <returns>
        /// The 4 byte card type, or <see langword="null"/> if the reader reported the operation as failed.
        /// The meaning of the value is not described in the vendor documentation, it is returned unchanged.
        /// </returns>
        public async Task<byte[]> ISO7816_CheckWellKnownCardsAsync(Iso7816Channel channel)
        {
            var parser = await CallFunctionAsync(new[] { API_ISO7816, ISO7816_CHECKWELLKNOWNCARDS, (byte)channel });
            var success = parser.ParseBool();
            if (!success)
            {
                return null;
            }

            return parser.ParseFixByteArray(4);
        }

        // The CCID slot status register: bits 0-1 = bmICCStatus, bits 2-5 = RFU, bits 6-7 = bmCommandStatus.
        private static Iso7816IccStatus DecodeIso7816IccStatus(byte statusRegister)
        {
            return (Iso7816IccStatus)(statusRegister & 0x03);
        }

        private static byte DecodeIso7816CommandStatus(byte statusRegister)
        {
            return (byte)(statusRegister >> 6);
        }

        // TISO7816SlotStatus: [Byte: bStatus][Byte: bError][Byte: bClockStatus]
        private static Iso7816SlotStatus ParseIso7816SlotStatus(ResponseParser parser)
        {
            var statusRegister = parser.ParseByte();

            return new Iso7816SlotStatus
            {
                IccStatus = DecodeIso7816IccStatus(statusRegister),
                CommandStatus = DecodeIso7816CommandStatus(statusRegister),
                Error = parser.ParseByte(),
                ClockStatus = (Iso7816ClockStatus)parser.ParseByte()
            };
        }

        // [Byte Array(Var), 2 LB: RXData][UInt16: StatusWord]
        private static Iso7816ApduResponse ParseIso7816ApduResponse(ResponseParser parser)
        {
            return new Iso7816ApduResponse
            {
                Data = parser.ParseUInt16LengthPrefixedByteArray(),
                StatusWord = parser.ParseUInt16()
            };
        }

        /// <summary>
        /// The status of a contact card slot, as reported by the CCID compliant slot status.
        /// </summary>
        public class Iso7816SlotStatus
        {
            /// <summary>Physical state of the slot (bmICCStatus).</summary>
            public Iso7816IccStatus IccStatus { get; set; }

            /// <summary>Command status according to CCID (bmCommandStatus): 0 = processed without error, 1 = failed, 2 = time extension requested.</summary>
            public byte CommandStatus { get; set; }

            /// <summary>Error code according to CCID (bError).</summary>
            public byte Error { get; set; }

            /// <summary>Clock status of the slot (bClockStatus).</summary>
            public Iso7816ClockStatus ClockStatus { get; set; }
        }

        /// <summary>
        /// The result of activating a contact card.
        /// </summary>
        public class Iso7816PowerOnResult
        {
            /// <summary>The ATR (Answer To Reset) of the card.</summary>
            public byte[] Atr { get; set; }

            /// <summary>Physical state of the slot (bmICCStatus), <see cref="Iso7816IccStatus.PresentAndActive"/> after a successful activation.</summary>
            public Iso7816IccStatus IccStatus { get; set; }

            /// <summary>Command status according to CCID (bmCommandStatus).</summary>
            public byte CommandStatus { get; set; }

            /// <summary>Error code according to CCID (bError).</summary>
            public byte Error { get; set; }

            /// <summary>The ATR as hex string.</summary>
            public string AtrHexString
            {
                get
                {
                    return ByteArrayConverter.GetStringFrom(Atr);
                }
            }
        }

        /// <summary>
        /// The response of the card to an APDU: the response data field and the status word.
        /// </summary>
        public class Iso7816ApduResponse
        {
            /// <summary>The data field of the response, empty if the card returned none.</summary>
            public byte[] Data { get; set; }

            /// <summary>The status word SW1SW2 of the card, e.g. 0x9000.</summary>
            public ushort StatusWord { get; set; }

            /// <summary>First status byte.</summary>
            public byte Sw1
            {
                get { return (byte)(StatusWord >> 8); }
            }

            /// <summary>Second status byte.</summary>
            public byte Sw2
            {
                get { return (byte)StatusWord; }
            }

            /// <summary><see langword="true"/> if the status word is 0x9000 (normal processing).</summary>
            public bool IsSuccess
            {
                get { return StatusWord == 0x9000; }
            }
        }

        /// <summary>
        /// Communication settings of a contact card slot (TISO7816CommSettings). Set <see cref="T0"/> or
        /// <see cref="T1"/> according to <see cref="Protocol"/>, the other one is ignored.
        /// </summary>
        public class Iso7816CommSettings
        {
            /// <summary>The protocol to use.</summary>
            public Iso7816Protocol Protocol { get; set; }

            /// <summary>The clock frequency applied to the card.</summary>
            public Iso7816Frequency Frequency { get; set; }

            /// <summary>A non-ISO value for F.</summary>
            public ushort F { get; set; }

            /// <summary>A non-ISO value for D.</summary>
            public ushort D { get; set; }

            /// <summary>Protocol data for <see cref="Iso7816Protocol.T0"/>.</summary>
            public Iso7816ProtocolDataT0 T0 { get; set; }

            /// <summary>Protocol data for <see cref="Iso7816Protocol.T1"/>.</summary>
            public Iso7816ProtocolDataT1 T1 { get; set; }

            // 14 byte structure: [Protocol][Freq][UInt16: F][UInt16: D][8 byte ProtocolData union]
            internal byte[] ToBytes()
            {
                var bytes = new List<byte> { (byte)Protocol, (byte)Frequency };
                bytes.AddUInt16(F);
                bytes.AddUInt16(D);

                switch (Protocol)
                {
                    case Iso7816Protocol.T0:
                        if (T0 == null)
                        {
                            throw new ArgumentException("Protocol data for T=0 is missing in the communication settings.");
                        }

                        bytes.AddRange(T0.ToBytes());
                        break;
                    case Iso7816Protocol.T1:
                        if (T1 == null)
                        {
                            throw new ArgumentException("Protocol data for T=1 is missing in the communication settings.");
                        }

                        bytes.AddRange(T1.ToBytes());
                        break;
                    default:
                        throw new ArgumentException("The protocol of the communication settings must be T=0 or T=1.");
                }

                return bytes.ToArray();
            }
        }

        /// <summary>
        /// Protocol data for T=0 (TProtocolDataT0).
        /// </summary>
        public class Iso7816ProtocolDataT0
        {
            /// <summary>Bit 7-4: FI, bit 3-0: DI, as obtained from TA1 of the ATR.</summary>
            public byte FindexDindex { get; set; }

            /// <summary>Shall be 0x00.</summary>
            public byte Tccks { get; set; }

            /// <summary>Extra guard time between two characters (0 to 254 etu, 0xFF is the same as 0x00), as obtained from TC1 of the ATR.</summary>
            public byte GuardTime { get; set; }

            /// <summary>Waiting time between command and response, as specified in TC2 of the ATR (default 10).</summary>
            public byte WaitingInteger { get; set; }

            /// <summary>Shall be 0x00.</summary>
            public byte ClockStop { get; set; }

            // The union is 8 bytes long, the T=0 data fills the first 5.
            internal byte[] ToBytes()
            {
                return new byte[] { FindexDindex, Tccks, GuardTime, WaitingInteger, ClockStop, 0, 0, 0 };
            }
        }

        /// <summary>
        /// Protocol data for T=1 (TProtocolDataT1).
        /// </summary>
        public class Iso7816ProtocolDataT1
        {
            /// <summary>Bit 7-4: FI, bit 3-0: DI, as obtained from TA1 of the ATR.</summary>
            public byte FindexDindex { get; set; }

            /// <summary>Shall be 0x00.</summary>
            public byte Tccks { get; set; }

            /// <summary>Extra guard time (0 to 254 etu, 0xFF reduces the guard time by 1 etu), as obtained from TC1 of the ATR.</summary>
            public byte GuardTime { get; set; }

            /// <summary>Bit 7-4: BWI (0-9), bit 3-0: CWI (0-15), as specified in the first TB for T=1 in the ATR.</summary>
            public byte WaitingIntegers { get; set; }

            /// <summary>Shall be 0x00.</summary>
            public byte ClockStop { get; set; }

            /// <summary>Negotiated IFSC in bytes, as specified in the first TA for T=1 in the ATR.</summary>
            public byte Ifsc { get; set; }

            /// <summary>Shall be 0x00.</summary>
            public byte NadValue { get; set; }

            /// <summary>
            /// Not described in the API document, but part of the 8 byte structure of the firmware (bWTX).
            /// The vendor's example sets it to 0x00.
            /// </summary>
            public byte Wtx { get; set; }

            internal byte[] ToBytes()
            {
                return new[] { FindexDindex, Tccks, GuardTime, WaitingIntegers, ClockStop, Ifsc, NadValue, Wtx };
            }
        }

        #endregion
    }
}
