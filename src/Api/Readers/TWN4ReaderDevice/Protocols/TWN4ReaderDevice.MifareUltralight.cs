using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Elatec.NET.Cards.Mifare;
using Elatec.NET.Helpers.ByteArrayHelper;
using Elatec.NET.Helpers.ByteArrayHelper.Extensions;

namespace Elatec.NET
{
    public partial class TWN4ReaderDevice
    {
        #region API_MIFAREULTRALIGHT / Mifare Ultralight Functions

        public static readonly byte API_MIFAREULTRALIGHT = 12;

        /// <summary>
        ///     Though the page size of this transponder family is 4 bytes, the transponder always returns 16 bytes of data.
        ///     This is achieved by reading four consecutive data pages, e.g. if page 4 is to be read, the transponder also
        ///     returns the content of page 5, 6 and 7. The transponder incorporates an integrated roll-back mechanism
        ///     if reading is done beyond the last physical available page address.E.g., in case of reading page 14 of
        ///     MIFARE Ultralight this would result in reading page 14, 15, 0, 1.
        /// </summary>
        /// <param name="page">Specify the address of the page to be read. The valid range of this parameter
        ///     is between 0 and 15 (Ultralight) or 0 and 43 (Ultralight C).</param>
        /// <returns></returns>
        public async Task<byte[]> MifareUltralight_ReadPageAsync(byte page)
        {
            var parser = await CallFunctionAsync(new byte[] { API_MIFAREULTRALIGHT, 0, page });
            var success = parser.ParseBool();
            if (success)
            {
                var result = parser.ParseFixByteArray(16);
                return result;
            }
            return null;
        }

        /// <summary>
        /// Write 4 bytes to a MIFARE Ultralight (and Ultralight C) page.
        /// </summary>
        /// <param name="page">Page address to write.</param>
        /// <param name="data">Exactly 4 bytes of data.</param>
        public async Task MifareUltralight_WritePageAsync(byte page, byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (data.Length != 4) throw new ArgumentException("MIFARE Ultralight page size is 4 bytes.", nameof(data));

            List<byte> bytes = new List<byte> { API_MIFAREULTRALIGHT, 1, page };
            bytes.AddRange(data);

            var parser = await CallFunctionAsync(bytes.ToArray());
            var success = parser.ParseBool();

            if (!success)
            {
                throw ReaderException.For(ReaderError.AccessDenied);
            }
        }

        /// <summary>
        /// Authenticate on MIFARE Ultralight C using a 16 byte key.
        /// </summary>
        /// <param name="keyHex">16 byte key as hex string (32 hex chars).</param>
        public async Task MifareUltralightC_AuthenticateAsync(string keyHex)
        {
            if (keyHex == null) throw new ArgumentNullException(nameof(keyHex));

            var key = ByteArrayConverter.GetBytesFrom(keyHex);
            if (key == null || key.Length != 16)
                throw new ArgumentException("Ultralight C key must be 16 bytes (32 hex characters).", nameof(keyHex));

            await MifareUltralightC_AuthenticateAsync(key);
        }

        /// <summary>
        /// Authenticate on MIFARE Ultralight C using a 16 byte key.
        /// </summary>
        /// <param name="key">16 byte key.</param>
        public async Task MifareUltralightC_AuthenticateAsync(byte[] key)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (key.Length != 16) throw new ArgumentException("Ultralight C key must be 16 bytes.", nameof(key));

            List<byte> bytes = new List<byte> { API_MIFAREULTRALIGHT, 2 };
            bytes.AddRange(key);

            var parser = await CallFunctionAsync(bytes.ToArray());
            var success = parser.ParseBool();

            if (!success)
            {
                throw ReaderException.For(ReaderError.AccessDenied);
            }
        }

        /// <summary>
        /// Authenticate on MIFARE Ultralight C using a key stored in the SAM (optional diversification input).
        /// </summary>
        /// <param name="keyNo">SAM key number.</param>
        /// <param name="keyVersion">SAM key version.</param>
        /// <param name="divInput">Diversification input (may be null/empty).</param>
        public async Task MifareUltralightC_SAMAuthenticateAsync(byte keyNo, byte keyVersion, byte[] divInput = null)
        {
            var div = divInput ?? Array.Empty<byte>();
            if (div.Length > byte.MaxValue) throw new ArgumentException("DIVInput too large (max 255 bytes).", nameof(divInput));

            List<byte> bytes = new List<byte> { API_MIFAREULTRALIGHT, 3, keyNo, keyVersion, (byte)div.Length };
            bytes.AddRange(div);

            var parser = await CallFunctionAsync(bytes.ToArray());
            var success = parser.ParseBool();

            if (!success)
            {
                throw ReaderException.For(ReaderError.AccessDenied);
            }
        }

        /// <summary>
        /// Write Ultralight C key to tag from SAM (optional diversification input).
        /// </summary>
        /// <param name="keyNo">SAM key number.</param>
        /// <param name="keyVersion">SAM key version.</param>
        /// <param name="divInput">Diversification input (may be null/empty).</param>
        public async Task MifareUltralightC_WriteKeyFromSAMAsync(byte keyNo, byte keyVersion, byte[] divInput = null)
        {
            var div = divInput ?? Array.Empty<byte>();
            if (div.Length > byte.MaxValue) throw new ArgumentException("DIVInput too large (max 255 bytes).", nameof(divInput));

            List<byte> bytes = new List<byte> { API_MIFAREULTRALIGHT, 4, keyNo, keyVersion, (byte)div.Length };
            bytes.AddRange(div);

            var parser = await CallFunctionAsync(bytes.ToArray());
            var success = parser.ParseBool();

            if (!success)
            {
                throw ReaderException.For(ReaderError.AccessDenied);
            }
        }

        /// <summary>
        /// The Fast Read function reads a number of pages beginning at a starting page from the transponder.
        /// </summary>
        /// <param name="startPage">Specify the address of the starting page.</param>
        /// <param name="numberOfPages">Specify the number of pages to be read.</param>
        /// <returns></returns>
        public async Task<byte[]> MifareUltralightEV1_FastReadAsync(byte startPage, byte numberOfPages)
        {
            var parser = await CallFunctionAsync(new byte[] { API_MIFAREULTRALIGHT, 5, startPage, numberOfPages });
            var success = parser.ParseBool();
            if (success)
            {
                var result = parser.ParseVarByteArray();
                return result;
            }
            return null;
        }

        /// <summary>
        /// Increment one of the Ultralight EV1 counters.
        /// </summary>
        /// <param name="counterAddr">Counter address.</param>
        /// <param name="incrValue">Increment value (UInt32).</param>
        public async Task MifareUltralightEV1_IncCounterAsync(byte counterAddr, uint incrValue)
        {
            List<byte> bytes = new List<byte> { API_MIFAREULTRALIGHT, 6, counterAddr };
            bytes.AddUInt32(incrValue);

            var parser = await CallFunctionAsync(bytes.ToArray());
            var success = parser.ParseBool();

            if (!success)
            {
                throw ReaderException.For(ReaderError.AccessDenied);
            }
        }

        /// <summary>
        /// Read one of the Ultralight EV1 counters.
        /// </summary>
        /// <param name="counterAddr">Counter address.</param>
        /// <returns>Counter value or null on failure.</returns>
        public async Task<uint?> MifareUltralightEV1_ReadCounterAsync(byte counterAddr)
        {
            var parser = await CallFunctionAsync(new byte[] { API_MIFAREULTRALIGHT, 7, counterAddr });
            var success = parser.ParseBool();
            if (success)
            {
                return parser.ParseUInt32();
            }
            return null;
        }

        /// <summary>
        /// Read the ECC signature from an Ultralight EV1 tag.
        /// </summary>
        /// <returns>32 byte ECC signature or null on failure.</returns>
        public async Task<byte[]> MifareUltralightEV1_ReadSigAsync()
        {
            var parser = await CallFunctionAsync(new byte[] { API_MIFAREULTRALIGHT, 8 });
            var success = parser.ParseBool();
            if (success)
            {
                return parser.ParseFixByteArray(32);
            }
            return null;
        }

        /// <summary>
        /// Get version information from an Ultralight EV1 tag.
        /// </summary>
        /// <returns>8 byte version information or null on failure.</returns>
        public async Task<byte[]> MifareUltralightEV1_GetVersionAsync()
        {
            var parser = await CallFunctionAsync(new byte[] { API_MIFAREULTRALIGHT, 9 });
            var success = parser.ParseBool();
            if (success)
            {
                return parser.ParseFixByteArray(8);
            }
            return null;
        }

        /// <summary>
        /// Perform password authentication (PWD_AUTH) on Ultralight EV1 / NTAG compatible tags.
        /// </summary>
        /// <param name="password">4 byte password.</param>
        /// <param name="pwdAck">2 byte PACK / PwdAck.</param>
        public async Task MifareUltralightEV1_PwdAuthAsync(byte[] password, byte[] pwdAck)
        {
            if (password == null) throw new ArgumentNullException(nameof(password));
            if (pwdAck == null) throw new ArgumentNullException(nameof(pwdAck));
            if (password.Length != 4) throw new ArgumentException("Password must be 4 bytes.", nameof(password));
            if (pwdAck.Length != 2) throw new ArgumentException("PwdAck must be 2 bytes.", nameof(pwdAck));

            List<byte> bytes = new List<byte> { API_MIFAREULTRALIGHT, 10 };
            bytes.AddRange(password);
            bytes.AddRange(pwdAck);

            var parser = await CallFunctionAsync(bytes.ToArray());
            var success = parser.ParseBool();

            if (!success)
            {
                throw ReaderException.For(ReaderError.AccessDenied);
            }
        }

        /// <summary>
        /// Check tearing event flag for an Ultralight EV1 counter.
        /// </summary>
        /// <param name="counterAddr">Counter address.</param>
        /// <returns>ValidFlag byte or null on failure.</returns>
        public async Task<byte?> MifareUltralightEV1_CheckTearingEventAsync(byte counterAddr)
        {
            var parser = await CallFunctionAsync(new byte[] { API_MIFAREULTRALIGHT, 11, counterAddr });
            var success = parser.ParseBool();
            if (success)
            {
                return parser.ParseByte();
            }
            return null;
        }

        /// <summary>
        /// Tells the members of the MIFARE Ultralight and NTAG family apart by the answer to GET_VERSION.
        /// They share ATQA 0x0044 and SAK 0x00, NXP AN10833 cannot separate them without it.
        /// </summary>
        /// <param name="version">The 8 byte answer, see <see cref="MifareUltralightEV1_GetVersionAsync"/>.</param>
        /// <returns>
        /// The sub type, or <see cref="MifareChipSubType.MifareUltralight"/> if the answer is not one of a chip that is known here
        /// (other NTAG variants such as NTAG I2C or a different memory size are not guessed from the size alone).
        /// </returns>
        internal static MifareChipSubType ClassifyUltralightFamily(byte[] version)
        {
            // [0] header 0x00, [1] vendor 0x04 (NXP), [2] product type, [3] product subtype, [4] major, [5] minor, [6] storage size, [7] protocol 0x03
            if (version == null || version.Length != 8 || version[0] != 0x00 || version[1] != 0x04)
            {
                return MifareChipSubType.MifareUltralight;
            }

            var storageSize = version[6];

            switch (version[2])
            {
                case 0x03: // MIFARE Ultralight EV1, the 17 pF and 50 pF variants have the same sizes
                    switch (storageSize)
                    {
                        case 0x0B:
                            return MifareChipSubType.MifareUltralight_EV1_MF0UL11;
                        case 0x0E:
                            return MifareChipSubType.MifareUltralight_EV1_MF0UL21;
                        default:
                            return MifareChipSubType.MifareUltralight_EV1;
                    }

                case 0x04: // NTAG: subtype 0x01 = 17 pF (NTAG210, NTAG212), subtype 0x02 = 50 pF (NTAG213, NTAG215, NTAG216)
                    switch ((version[3] << 8) | storageSize)
                    {
                        case 0x010B:
                            return MifareChipSubType.NTAG_210;
                        case 0x010E:
                            return MifareChipSubType.NTAG_212;
                        case 0x020F:
                            return MifareChipSubType.NTAG_213;
                        case 0x0211:
                            return MifareChipSubType.NTAG_215;
                        case 0x0213:
                            return MifareChipSubType.NTAG_216;
                        default:
                            return MifareChipSubType.MifareUltralight;
                    }

                default:
                    return MifareChipSubType.MifareUltralight;
            }
        }

        /// <summary>
        /// Identifies a chip of the Ultralight and NTAG family with GET_VERSION, see <see cref="ClassifyUltralightFamily"/>.
        /// </summary>
        /// <remarks>
        /// A chip that does not know GET_VERSION (MIFARE Ultralight, Ultralight C) answers with a NAK and is idle afterwards, every
        /// following command would fail. It is therefore searched again before this method returns. SelectTag cannot be used for
        /// that, it does not work on Legic capable readers. Such a chip is reported as <see cref="MifareChipSubType.MifareUltralight"/>.
        /// </remarks>
        private async Task<MifareChipSubType> DetectUltralightFamilySubTypeAsync(MifareChip chip)
        {
            byte[] version = null;
            try
            {
                version = await MifareUltralightEV1_GetVersionAsync();
            }
            catch (TwnException)
            {
                // The reader does not offer the function. Treated like a chip that does not answer.
            }

            if (version != null)
            {
                return ClassifyUltralightFamily(version);
            }

            // Up to three searches, so that with a second tag in the field the identified one is the active one again.
            for (var attempt = 0; attempt < 3; attempt++)
            {
                var tag = await SearchTagAsync();
                if (tag == null || tag.IDBytes.SequenceEqual(chip.UID))
                {
                    break;
                }
            }

            return MifareChipSubType.MifareUltralight;
        }

        #endregion
    }
}
