using System;

namespace Elatec.NET.Sam
{
    /// <summary>
    /// A short command APDU for a SAM: header, optional data field (Lc is derived from its length) and optional Le.
    /// </summary>
    public sealed class SamApdu
    {
        public SamApdu(byte cla, byte ins, byte p1, byte p2, byte[] data = null, byte? le = null)
        {
            if (data != null && data.Length > byte.MaxValue)
                throw new ArgumentException("The data field of a short APDU must not exceed 255 bytes.", nameof(data));

            Cla = cla;
            Ins = ins;
            P1 = p1;
            P2 = p2;
            Data = data ?? new byte[0];
            Le = le;
        }

        public byte Cla { get; }

        public byte Ins { get; }

        public byte P1 { get; }

        public byte P2 { get; }

        /// <summary>The data field, empty if the command has none.</summary>
        public byte[] Data { get; }

        /// <summary>The expected response length (0x00 means up to 256 bytes), <see langword="null"/> if Le is not transmitted.</summary>
        public byte? Le { get; }
    }

    /// <summary>
    /// The response APDU of a SAM: the response data field and the status word.
    /// </summary>
    public sealed class SamApduResponse
    {
        public SamApduResponse(byte[] data, ushort statusWord)
        {
            Data = data ?? new byte[0];
            StatusWord = statusWord;
        }

        public byte[] Data { get; }

        /// <summary>The status word SW1SW2, e.g. 0x9000.</summary>
        public ushort StatusWord { get; }

        public byte Sw1
        {
            get { return (byte)(StatusWord >> 8); }
        }

        public byte Sw2
        {
            get { return (byte)StatusWord; }
        }
    }
}
