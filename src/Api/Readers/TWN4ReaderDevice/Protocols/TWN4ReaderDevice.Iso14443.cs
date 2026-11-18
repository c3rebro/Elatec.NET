using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Elatec.NET.Helpers.ByteArrayHelper.Extensions;

namespace Elatec.NET
{
    public partial class TWN4ReaderDevice
    {
        #region API_ISO14443 / ISO14443 Transparent Transponder Access Functions

        public static readonly byte API_ISO14443 = 18;

        /// <summary>
        /// This function delivers the ATS (Answer To Select) of a ISO14443A layer 4 transponder.
        /// </summary>
        /// <returns>The ATS if one is found, otherwise null.</returns>
        /// <remarks>
        ///     Legic-capable TWN4 readers internally trigger the RATS/ATS handshake as part of <see cref="SearchTagAsync"/>,
        ///     so attempting to perform a manual RATS over <see cref="ISO14443_3_TdxAsync(byte[], ushort)"/> may fail.
        ///     This method surfaces the cached ATS value without requiring that low-level exchange.
        /// </remarks>
        public async Task<byte[]> ISO14443A_GetAtsAsync()
        {
            var parser = await CallFunctionAsync(new byte[] { API_ISO14443, 0, /* maxBytes */ byte.MaxValue });
            var success = parser.ParseBool();
            if (success)
            {
                var result = parser.ParseVarByteArray();
                return result;
            }
            return null;
        }

        /// <summary>
        ///     This function delivers the ATQB (Answer To Request TypeB) of the last detected ISO14443B compliant transponder.<br/>
        ///     Note: This function cannot be called on TWN4 MultiTech Legic.
        /// </summary>
        /// <returns>The ATQB if one is found, otherwise null.</returns>
        public async Task<byte[]> ISO14443B_GetAtqbAsync()
        {
            var parser = await CallFunctionAsync(new byte[] { API_ISO14443, 1, /* maxBytes */ byte.MaxValue });
            var success = parser.ParseBool();
            if (success)
            {
                var result = parser.ParseVarByteArray();
                return result;
            }
            return null;
        }

        /// <summary>
        ///     This function can be used to probe if a ISO14443-4 transponder is still in reading range. The internal state
        ///     of the transponder remains unchanged. <br/>
        ///     Note: This function cannot be called on TWN4 MultiTech Legic.
        /// </summary>
        /// <returns></returns>
        public async Task<bool> ISO14443_4_CheckPresenceAsync()
        {
            var parser = await CallFunctionAsync(new byte[] { API_ISO14443, 2 });
            var result = parser.ParseBool();
            return result;
        }

        /// <summary>
        ///     This function can be used for transparent exchange of data between reader and ISO14443-4 transponders.
        ///     All framing of layer 4 subset is already done by the reader, so only the payload needs to be passed
        ///     to the function.
        /// </summary>
        /// <param name="TX">Data that shall be transmitted to the transponder.</param>
        /// <returns>The response of the transponder.</returns>
        public async Task<byte[]> ISO14443_4_TdxAsync(byte[] TX)
        {
            List<byte> bytes = new List<byte> { API_ISO14443, 3 };
            bytes.Add((byte)TX.Length);
            bytes.AddRange(TX);
            bytes.Add(byte.MaxValue); // MaxRXByteCnt
            
            var parser = await CallFunctionAsync(bytes.ToArray());
            var success = parser.ParseBool();
            if (success)
            {
                var result = parser.ParseVarByteArray();
                return result;
            }
            return null;
        }

        /// <summary>
        ///     This function delivers the ATQA (Answer To Request TypeA) of the last detected ISO14443A compliant transponder.
        ///     The ATQA consists of two bytes, parsed in LSB-first order.
        /// </summary>
        /// <returns>The ATQA if one is found, otherwise null.</returns>
        public async Task<ushort?> ISO14443A_GetAtqaAsync()
        {
            var parser = await CallFunctionAsync(new byte[] { API_ISO14443, 4 });
            var success = parser.ParseBool();
            if (success)
            {
                var result = parser.ParseUInt16();
                return result;
            }
            return null;
        }

        /// <summary>
        /// This function delivers the SAK (Select Acknowledge) of the last detected ISO14443A compliant transponder.
        /// </summary>
        /// <returns>The SAK if one is found, otherwise null.</returns>
        public async Task<byte?> ISO14443A_GetSakAsync()
        {
            var parser = await CallFunctionAsync(new byte[] { API_ISO14443, 5 });
            var success = parser.ParseBool();
            if (success)
            {
                var result = parser.ParseByte();
                return result;
            }
            return null;
        }

        /// <summary>
        ///     This function delivers the transponder’s answer to the ATTRIB command, which is sent automatically
        ///     during selection process by the reader. <br/>
        ///     Note: This function cannot be called on TWN4 MultiTech Legic.
        /// </summary>
        /// <returns>The response of the transponder.</returns>
        public async Task<byte[]> ISO14443B_GetAnswerToAttribAsync()
        {
            var parser = await CallFunctionAsync(new byte[] { API_ISO14443, 6, /* maxBytes */ byte.MaxValue });
            var success = parser.ParseBool();
            if (success)
            {
                var result = parser.ParseVarByteArray();
                return result;
            }
            return null;
        }

        /// <summary>
        ///     This function can be used for transparent exchange of data between reader and ISO14443-3 transponders.
        ///     The function does not calculate any CRC or other overhead by itself, so if necessary this has to be
        ///     conducted on host side.
        /// </summary>
        /// <param name="TX">Data that shall be transmitted to the transponder.</param>
        /// <param name="timeout">Response timeout in milliseconds.</param>
        /// <returns>The response of the transponder.</returns>
        public async Task<byte[]> ISO14443_3_TdxAsync(byte[] TX, ushort timeout)
        {
            List<byte> bytes = new List<byte> { API_ISO14443, 7 };
            bytes.Add((byte)TX.Length);
            bytes.AddRange(TX);
            bytes.Add(byte.MaxValue); // MaxRXByteCnt
            bytes.AddUInt16(timeout);

            var parser = await CallFunctionAsync(bytes.ToArray());
            var success = parser.ParseBool();
            if (success)
            {
                var result = parser.ParseVarByteArray();
                return result;
            }
            return null;
        }

        /// <summary>
        /// Use this function to search the RF field for ISO14443A transponders. The result is a list of the UID of the respective transponders.
        /// </summary>
        /// <returns>A list containing the UIDs of all transponders.</returns>
        public async Task<List<byte[]>> ISO14443A_SearchMultiTagAsync()
        {
            var parser = await CallFunctionAsync(new byte[] { API_ISO14443, 8, /* maxIDBytes */ byte.MaxValue });
            var tagList = new List<byte[]>();

            var found = parser.ParseBool();
            if (found)
            {
                var count = parser.ParseByte();
                parser.ParseByte(); // Total number of bytes. We don't need this.
                for (int i = 0; i < count; i++)
                {
                    var tag = parser.ParseVarByteArray();
                    tagList.Add(tag);
                }
            }

            return tagList;
        }


        /// <summary>
        /// Use this function to select one of the discovered transponders for further operations.
        /// IMPORTANT: This does not work on Legic capable TWN4 Mutitec Readers. Use SearchTag instead.
        /// </summary>
        /// <param name="uid">Specify the UID of the transponder to be selected.</param>
        /// <returns>If the operation was successful, the return value is true, otherwise it is false.</returns>
        /// <remarks>
        ///     Legic variants already perform card selection within the Legic co-processor; issuing this call after
        ///     <see cref="SearchTagAsync"/> will therefore return false even though a tag is present. Downstream
        ///     operations (e.g., <see cref="ISO14443A_GetAtsAsync"/>) should rely on the discovery results instead of
        ///     explicit selection on those devices.
        /// </remarks>
        public async Task<bool> ISO14443A_SelectTagAsync(byte[] uid)
        {
            List<byte> bytes = new List<byte> { API_ISO14443, 9 };
            bytes.Add((byte)uid.Length);
            bytes.AddRange(uid);

            var parser = await CallFunctionAsync(bytes.ToArray());
            var success = parser.ParseBool();
            return success;
        }

        // TODO: SYSFUNC(API_ISO14443, 10, bool preISO14443B_GetATR(byte* ATR, int* ATRByteCnt, int MaxATRByteCnt))

        /// <summary>
        /// Reselect a transponder.
        /// </summary>
        /// <returns></returns>
        /// <remarks>SYSFUNC(API_ISO14443, 11, bool ISO14443A_Reselect(void))<br/>
        ///     This internal method is not documented in TWN4 API reference.</remarks>
        public async Task<bool> ISO14443A_Reselect()
        {
            var parser = await CallFunctionAsync(new byte[] { API_ISO14443, 11 });
            var result = parser.ParseBool();
            return result;
        }

        #endregion

        #region RATS by hand

        // RATS: E0, then FSDI (high nibble, 8 = 256 bytes) and CID (low nibble, 0). A transponder answers within about 5 ms.
        private static readonly byte[] RatsCommand = { 0xE0, 0x80 };

        private const ushort RatsTimeoutMilliseconds = 20;

        /// <summary>
        /// Calculates the CRC_A of ISO/IEC 14443-3 (initial value 0x6363, polynomial 0x8408), which <see cref="ISO14443_3_TdxAsync"/> neither
        /// adds nor checks.
        /// </summary>
        /// <param name="data">The bytes that the CRC is calculated over.</param>
        /// <returns>The two CRC bytes in the order they are sent, the lower byte first.</returns>
        internal static byte[] CrcA(byte[] data)
        {
            ushort crc = 0x6363;
            foreach (var value in data)
            {
                var b = (byte)(value ^ (byte)(crc & 0xFF));
                b = (byte)(b ^ (byte)(b << 4));
                crc = (ushort)((crc >> 8) ^ (b << 8) ^ (b << 3) ^ (b >> 4));
            }

            return new[] { (byte)(crc & 0xFF), (byte)(crc >> 8) };
        }

        /// <summary>
        /// Takes the ATS out of the answer to a RATS: the length byte TL, the rest of the ATS (TL bytes in all) and the CRC_A of it.
        /// </summary>
        /// <param name="answer">What <see cref="ISO14443_3_TdxAsync"/> returned.</param>
        /// <returns>
        /// The ATS as <see cref="ISO14443A_GetAtsAsync"/> gives it (TL first, no CRC), or <see langword="null"/> when there is no answer
        /// or the answer is no ATS (wrong length, wrong CRC).
        /// </returns>
        internal static byte[] ExtractAts(byte[] answer)
        {
            if (answer == null || answer.Length < 3)
            {
                return null;
            }

            int length = answer[0];
            if (length < 1 || answer.Length != length + 2)
            {
                return null;
            }

            var ats = new byte[length];
            Array.Copy(answer, ats, length);
            var crc = CrcA(ats);

            return answer[length] == crc[0] && answer[length + 1] == crc[1] ? ats : null;
        }

        /// <summary>
        /// Tells whether a SAK is that of a chip that answers like a MIFARE Classic chip: bit 4 set (Classic 1K, 4K, MIFARE Plus in
        /// security level 1), no ISO/IEC 14443-4 bit (that chip gets its RATS from the reader), no Mini or 2K bit and no RFU bit.
        /// </summary>
        /// <param name="sak">The SAK of the chip.</param>
        /// <returns><see langword="true"/> for SAK 08 and 18.</returns>
        internal static bool AnswersLikeClassic(byte sak)
        {
            return (sak & 0x23) == 0 && (sak & 0x08) != 0;
        }

        /// <summary>
        /// Asks the chip for its ATS with a RATS sent by hand, for a chip that the reader has not sent one to.
        /// </summary>
        /// <remarks>
        /// A TWN4 sends the RATS during SearchTag only to a chip that says it talks ISO/IEC 14443-4 (SAK bit 6). A MIFARE Plus in
        /// security level 1 does not say so (SAK 08 or 18), it answers like a Classic chip, but it answers a RATS and a Classic chip
        /// does not (NXP AN10833). The chip is searched again afterwards: one that answered is in layer 4 and no longer takes Classic
        /// commands, one that did not answer is idle. A reader that does not offer the function is treated like a chip that does not answer.
        /// </remarks>
        /// <param name="uid">The ID of the chip.</param>
        /// <returns>The ATS as <see cref="ISO14443A_GetAtsAsync"/> gives it, or <see langword="null"/> when the chip does not answer.</returns>
        private async Task<byte[]> ReadAtsWithRatsAsync(byte[] uid)
        {
            byte[] ats = null;
            try
            {
                var frame = RatsCommand.Concat(CrcA(RatsCommand)).ToArray();
                ats = ExtractAts(await ISO14443_3_TdxAsync(frame, RatsTimeoutMilliseconds));
            }
            catch (TwnException)
            {
                // The reader does not offer the function. Treated like a chip that does not answer.
            }

            await SearchTagAgainAsync(uid);

            return ats;
        }

        #endregion
    }
}
