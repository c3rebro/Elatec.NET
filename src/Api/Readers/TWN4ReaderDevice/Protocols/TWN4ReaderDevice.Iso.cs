using System.Collections.Generic;
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

    }
}
