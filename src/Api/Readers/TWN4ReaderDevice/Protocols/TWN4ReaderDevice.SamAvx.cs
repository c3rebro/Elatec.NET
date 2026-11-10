using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Elatec.NET.Helpers.ByteArrayHelper.Extensions;

namespace Elatec.NET
{
    public partial class TWN4ReaderDevice
    {
        #region API_SAMAVX / NXP SAM AV1/AV2 Functions

        /// <summary>
        /// TWN4 Simple Protocol API identifier for the NXP SAM AV1/AV2 functions (0x22xx).
        /// </summary>
        /// <remarks>
        /// A NXP SAM AV1/AV2 card must be inserted into one of the reader's SAM slots before using these functions.
        /// The reader scans the slots on power-up, so a correctly inserted SAM is detected automatically.
        /// These functions only cover SAM AV1/AV2. To talk to other SAMs, e.g. a SAM AV3, exchange APDUs with the
        /// card directly via the ISO7816 functions, see <see cref="ISO7816_ExchangeAPDUAsync"/>.
        /// </remarks>
        public static readonly byte API_SAMAVX = 0x22;

        private static readonly byte SAMAVX_AUTHENTICATEHOST = 0x00;
        private static readonly byte SAMAVX_GETKEYENTRY = 0x01;
        private static readonly byte SAMAVX_KEYLENGTH = 0x10;
        private static readonly byte SAMAVX_MAX_CRYPTOENV = 3;

        /// <summary>
        /// Performs a mutual three-pass authentication between the host (reader) and the inserted SAM AV1/AV2 card.
        /// Depending on the security settings of the SAM, this authentication may be required before other security
        /// related operations, e.g. <see cref="MifareUltralightC_SAMAuthenticateAsync(byte, byte, byte[])"/>.
        /// Simple Protocol command: 0x2200.
        /// </summary>
        /// <param name="keyNo">Number of the SAM key entry used for the host authentication.</param>
        /// <param name="key">The 16 byte host key.</param>
        /// <param name="keyType">Type of the key. The authentication is performed according to this type.</param>
        /// <param name="cryptoEnv">Cryptographic environment to use (0 to 3, i.e. CRYPTO_ENV0 to CRYPTO_ENV3).</param>
        /// <exception cref="ArgumentNullException"><paramref name="key"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException"><paramref name="key"/> is not 16 bytes long.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="cryptoEnv"/> is greater than 3 or <paramref name="keyType"/> is not defined.</exception>
        /// <exception cref="ReaderException">The reader reported that the authentication failed.</exception>
        public async Task SAMAVx_AuthenticateHostAsync(byte keyNo, byte[] key, SamAvxKeyType keyType, byte cryptoEnv = 0)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (key.Length != SAMAVX_KEYLENGTH) throw new ArgumentException("SAM AV1/AV2 host key must be 16 bytes.", nameof(key));
            if (cryptoEnv > SAMAVX_MAX_CRYPTOENV) throw new ArgumentOutOfRangeException(nameof(cryptoEnv), "Cryptographic environment must be between 0 and 3.");
            if (!Enum.IsDefined(typeof(SamAvxKeyType), keyType)) throw new ArgumentOutOfRangeException(nameof(keyType), "Key type must be 3DES or AES.");

            // Simple Protocol: SAMAVx_AuthenticateHost [2200][Byte: CryptoEnv][Byte: KeyNo][Byte Array(Var): Key][Byte: KeyType]
            List<byte> bytes = new List<byte> { API_SAMAVX, SAMAVX_AUTHENTICATEHOST, cryptoEnv, keyNo, (byte)key.Length };
            bytes.AddRange(key);
            bytes.Add((byte)keyType);

            var parser = await CallFunctionAsync(bytes.ToArray());
            var success = parser.ParseBool();

            if (!success)
            {
                throw new ReaderException("SAM AV1/AV2 host authentication failed.");
            }
        }

        /// <summary>
        /// Performs a mutual three-pass authentication between the host (reader) and the inserted SAM AV1/AV2 card.
        /// Simple Protocol command: 0x2200.
        /// </summary>
        /// <param name="keyNo">Number of the SAM key entry used for the host authentication.</param>
        /// <param name="keyHex">The 16 byte host key as hex string (32 hex chars).</param>
        /// <param name="keyType">Type of the key. The authentication is performed according to this type.</param>
        /// <param name="cryptoEnv">Cryptographic environment to use (0 to 3, i.e. CRYPTO_ENV0 to CRYPTO_ENV3).</param>
        /// <inheritdoc cref="SAMAVx_AuthenticateHostAsync(byte, byte[], SamAvxKeyType, byte)"/>
        public async Task SAMAVx_AuthenticateHostAsync(byte keyNo, string keyHex, SamAvxKeyType keyType, byte cryptoEnv = 0)
        {
            if (keyHex == null) throw new ArgumentNullException(nameof(keyHex));

            var key = ByteArrayConverter.GetBytesFrom(keyHex);
            if (key.Length != SAMAVX_KEYLENGTH)
                throw new ArgumentException("SAM AV1/AV2 host key must be 16 bytes (32 hex characters).", nameof(keyHex));

            await SAMAVx_AuthenticateHostAsync(keyNo, key, keyType, cryptoEnv);
        }

        /// <summary>
        /// Queries the key entry with the given number from the inserted SAM AV1/AV2 card.
        /// Depending on the security settings of the SAM, a prior <see cref="SAMAVx_AuthenticateHostAsync(byte, byte[], SamAvxKeyType, byte)"/> may be necessary.
        /// Simple Protocol command: 0x2201.
        /// </summary>
        /// <param name="keyNo">Number of the key entry to query.</param>
        /// <returns>The key entry, or <see langword="null"/> if the reader reported the operation as failed.</returns>
        public async Task<SamAvxKeyEntry> SAMAVx_GetKeyEntryAsync(byte keyNo)
        {
            // Simple Protocol: SAMAVx_GetKeyEntry [2201][Byte: KeyNo] -> [00][Bool: Result][Byte Array(13): TSAMAVxKeyEntryData]
            var parser = await CallFunctionAsync(new byte[] { API_SAMAVX, SAMAVX_GETKEYENTRY, keyNo });
            var success = parser.ParseBool();
            if (!success)
                return null;

            return new SamAvxKeyEntry
            {
                VersionKeyA = parser.ParseByte(),
                VersionKeyB = parser.ParseByte(),
                VersionKeyC = parser.ParseByte(),
                DfAid = parser.ParseUInt32(),
                DfKeyNo = parser.ParseByte(),
                KeyNoCek = parser.ParseByte(),
                KeyNoVcek = parser.ParseByte(),
                RefNoKuc = parser.ParseByte(),
                Settings = parser.ParseUInt16()
            };
        }

        /// <summary>
        /// A key entry of a SAM AV1/AV2 card. Maps the 13 byte TSAMAVxKeyEntryData structure of the firmware,
        /// multi-byte values are transferred least-significant byte first.
        /// </summary>
        public class SamAvxKeyEntry
        {
            /// <summary>Version of key A.</summary>
            public byte VersionKeyA { get; set; }

            /// <summary>Version of key B.</summary>
            public byte VersionKeyB { get; set; }

            /// <summary>Version of key C.</summary>
            public byte VersionKeyC { get; set; }

            /// <summary>Associated DESFire application ID (DF_AID).</summary>
            public uint DfAid { get; set; }

            /// <summary>Associated DESFire key number (DF_KeyNo).</summary>
            public byte DfKeyNo { get; set; }

            /// <summary>Key number of the change entry key.</summary>
            public byte KeyNoCek { get; set; }

            /// <summary>Key version of the change entry key.</summary>
            public byte KeyNoVcek { get; set; }

            /// <summary>Number of the associated key usage counter.</summary>
            public byte RefNoKuc { get; set; }

            /// <summary>Raw configuration settings of the key entry (SET).</summary>
            public ushort Settings { get; set; }
        }

        #endregion
    }
}
