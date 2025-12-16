using System.Collections.Generic;
using System.Threading.Tasks;
using Elatec.NET.Helpers.ByteArrayHelper;
using Elatec.NET.Helpers.ByteArrayHelper.Extensions;

namespace Elatec.NET
{
    public partial class TWN4ReaderDevice
    {
        #region API_RF

        public static readonly byte API_RF = 5;

        // API_RF function numbers (Simple Protocol: 0x0500..0x0504)
        public static readonly byte RF_SEARCHTAG = 0;
        public static readonly byte RF_SETRFOFF = 1;
        public static readonly byte RF_SETTAGTYPES = 2;
        public static readonly byte RF_GETTAGTYPES = 3;
        public static readonly byte RF_GETSUPPORTEDTAGTYPES = 4;

        /// <summary>
        /// Calculates the TagTypes bitmask value for a given TagType code (see Appendix A in the Simple Protocol spec).
        /// </summary>
        /// <param name="tagType">The TagType code (e.g. 0x80 for MIFARE).</param>
        /// <returns>The corresponding UInt32 mask bit (TAGMASK = 1 &lt;&lt; (TagType &amp; 0x1F)).</returns>
        public static uint TagMaskFromTagType(byte tagType) => 1u << (tagType & 0x1F);


        /// <summary>
        ///     Use this function to search a transponder in the reading range of TWN4. TWN4 is searching for all types
        ///     of transponders, which have been specified via function SetTagTypes. If a transponder has been found,
        ///     tag type, length of ID and ID data itself are returned.
        /// </summary>
        /// <remarks>
        ///     Simple Protocol command: 0x0500 (API_RF / SearchTag).
        /// </remarks>
        /// <returns>
        ///     A <see cref="SearchTagResult" /> or <see langword="null" /> if no tag was detected.
        ///     This overload requests the maximum ID length (MaxIDBytes = 0xFF).
        /// </returns>
        public async Task<SearchTagResult> SearchTagAsync()
        {
            return await SearchTagAsync(byte.MaxValue);
        }

        /// <summary>
        ///     Use this function to search a transponder in the reading range of TWN4. TWN4 is searching for all types
        ///     of transponders, which have been specified via function SetTagTypes. If a transponder has been found,
        ///     tag type, length of ID and ID data itself are returned.
        /// </summary>
        /// <remarks>
        ///     Simple Protocol command: 0x0500 (API_RF / SearchTag).
        /// </remarks>
        /// <param name="maxIdBytes">Maximum number of ID bytes to return (MaxIDBytes).</param>
        /// <returns>A <see cref="SearchTagResult" /> or <see langword="null" /> if no tag was detected.</returns>
        public async Task<SearchTagResult> SearchTagAsync(byte maxIdBytes)
        {
            var parser = await CallFunctionAsync(new byte[] { API_RF, RF_SEARCHTAG, maxIdBytes });
            var found = parser.ParseBool();
            if (!found)
                return null;

            return new SearchTagResult
            {
                ChipType = (ChipType)parser.ParseByte(),
                IDBitCount = parser.ParseByte(),
                IDBytes = parser.ParseVarByteArray()
            };
        }

        public class SearchTagResult
        {
            /// <summary>
            /// Property is called TagType in the API.
            /// </summary>
            public ChipType ChipType { get; set; }
            public byte IDBitCount { get; set; }
            public byte[] IDBytes { get; set; }

            public string IDHexString
            {
                get
                {
                    return ByteArrayConverter.GetStringFrom(IDBytes);
                }
            }
        }

        /// <summary>
        ///     Turn off RF field. If no further operations are required on a transponder found via function SearchTag you
        ///     may use this command to minimize power consumption of TWN4.
        /// </summary>
        /// <returns></returns>
        public async Task SetRFOffAsync()
        {
            await CallFunctionAsync(new byte[] { API_RF, RF_SETRFOFF });
        }

        /// <summary>
        /// Use this function to configure the transponders, which are searched by function SearchTag.
        /// </summary>
        /// <remarks>
        /// The parameters are bitmasks (UInt32) of enabled tag technologies, split into LF and HF.
        /// If you have "TagType codes" (e.g. 0x80 for MIFARE), convert them using <see cref="TagMaskFromTagType(byte)"/>.
        /// </remarks>
        /// <param name="lfTagTypes">LF tag type mask.</param>
        /// <param name="hfTagTypes">HF tag type mask.</param>
        public async Task SetTagTypesAsync(LFTagTypes lfTagTypes, HFTagTypes hfTagTypes)
        {
            await SetTagTypesAsync((uint)lfTagTypes, (uint)hfTagTypes);
        }

        /// <summary>
        /// Use this function to configure the transponders, which are searched by function SearchTag.
        /// </summary>
        /// <remarks>
        /// This overload takes the raw UInt32 masks exactly as defined by the Simple Protocol command 0x0502.
        /// </remarks>
        /// <param name="tagTypesLF">LF tag types bitmask.</param>
        /// <param name="tagTypesHF">HF tag types bitmask.</param>
        public async Task SetTagTypesAsync(uint tagTypesLF, uint tagTypesHF)
        {
            List<byte> bytes = new List<byte> { API_RF, RF_SETTAGTYPES };
            bytes.AddUInt32(tagTypesLF);
            bytes.AddUInt32(tagTypesHF);
            await CallFunctionAsync(bytes.ToArray());
        }

        /// <summary>
        ///     This function returns the transponder types currently being searched for by function SearchTag separated
        ///     by frequency (LF and HF).
        /// </summary>
        /// <returns>Tag types.</returns>
        public async Task<GetTagTypesResult> GetTagTypesAsync()
        {
            var parser = await CallFunctionAsync(new byte[] { API_RF, RF_GETTAGTYPES });
            var lf = parser.ParseUInt32();
            var hf = parser.ParseUInt32();

            return new GetTagTypesResult() { LFTagTypes = (LFTagTypes)lf, HFTagTypes = (HFTagTypes)hf };
        }

        /// <summary>
        /// Tag type masks currently configured for <see cref="SearchTagAsync()"/> (Simple Protocol: 0x0503).
        /// </summary>
        public class GetTagTypesResult
        {
            /// <summary>LF tag types bitmask.</summary>
            public LFTagTypes LFTagTypes { get; internal set; }
            /// <summary>HF tag types bitmask.</summary>
            public HFTagTypes HFTagTypes { get; internal set; }
        }


        /// <summary>
        ///     This function returns the transponder types, which are actually supported by the individual TWN4 separated
        ///     by frequency (LF and HF). Also the P-option is taken into account. This means, if the specific TWN4
        ///     has no option P, the appropriate transponders are not returned as supported type of transponder.
        /// </summary>
        /// <returns>Tag types.</returns>
        public async Task<GetSupportedTagTypesResult> GetSupportedTagTypesAsync()
        {
            var parser = await CallFunctionAsync(new byte[] { API_RF, RF_GETSUPPORTEDTAGTYPES });
            var lf = parser.ParseUInt32();
            var hf = parser.ParseUInt32();

            return new GetSupportedTagTypesResult() { LFTagTypes = (LFTagTypes)lf, HFTagTypes = (HFTagTypes)hf };
        }

        /// <summary>
        /// Tag type masks supported by this device (Simple Protocol: 0x0504).
        /// </summary>
        public class GetSupportedTagTypesResult
        {
            /// <summary>LF tag types bitmask.</summary>
            public LFTagTypes LFTagTypes { get; internal set; }
            /// <summary>HF tag types bitmask.</summary>
            public HFTagTypes HFTagTypes { get; internal set; }
        }

        #endregion
    }
}
