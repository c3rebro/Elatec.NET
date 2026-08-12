using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Elatec.NET.Cards.Mifare;
using Elatec.NET.Helpers.ByteArrayHelper;
using Elatec.NET.Helpers.ByteArrayHelper.Extensions;

namespace Elatec.NET
{
    public partial class TWN4ReaderDevice
    {
        #region API_MIFAREDESFIRE / Mifare Desfire Functions

        public static readonly byte API_MIFAREDESFIRE = 15;

        private static readonly byte CRYPTO_ENV = 0;
        private static readonly byte DESFIRE_AUTHMODE_COMPATIBLE = 0;
        private static readonly byte DESFIRE_AUTHMODE_EV1 = 1;
        private static readonly byte DESFIRE_KEYLENGTH = 0x10;
        private static readonly byte DESFIRE_MAX_FILEIDS = 0xFF;

        private static readonly byte MIFARE_DESFIRE_GETAPPIDS = 0;
        private static readonly byte MIFARE_DESFIRE_CREATEAPP = 1;
        private static readonly byte MIFARE_DESFIRE_DELETEAPP = 2;
        private static readonly byte MIFARE_DESFIRE_SELECTAPP = 3;
        private static readonly byte MIFARE_DESFIRE_AUTH = 4;
        private static readonly byte MIFARE_DESFIRE_GETKEYSETTINGS = 5;
        private static readonly byte MIFARE_DESFIRE_GETFILEIDS = 6;
        private static readonly byte MIFARE_DESFIRE_GETFILESETTINGS = 7;
        private static readonly byte MIFARE_DESFIRE_READDATA = 8;
        private static readonly byte MIFARE_DESFIRE_WRITEDATA = 9;
        private static readonly byte MIFARE_DESFIRE_GETVALUE = 10;
        private static readonly byte MIFARE_DESFIRE_CREDIT = 11;
        private static readonly byte MIFARE_DESFIRE_DEBIT = 12;
        private static readonly byte MIFARE_DESFIRE_LIMITEDCREDIT = 13;
        private static readonly byte MIFARE_DESFIRE_GETFREEMEMORY = 14;
        private static readonly byte MIFARE_DESFIRE_FORMATTAG = 15;
        private static readonly byte MIFARE_DESFIRE_CREATE_STDDATAFILE = 16;
        private static readonly byte MIFARE_DESFIRE_CREATE_VALUEFILE = 17;
        private static readonly byte MIFARE_DESFIRE_GETVERSION = 18;
        private static readonly byte MIFARE_DESFIRE_DELETEFILE = 19;
        private static readonly byte MIFARE_DESFIRE_COMMITTRANSACTION = 20;
        private static readonly byte MIFARE_DESFIRE_ABORTTRANSACTION = 21;
        private static readonly byte MIFARE_DESFIRE_GETUID = 22;
        private static readonly byte MIFARE_DESFIRE_GETKEYVERSION = 23;
        private static readonly byte MIFARE_DESFIRE_CHANGEKEYSETTINGS = 24;
        private static readonly byte MIFARE_DESFIRE_CHANGEKEY = 25;
        private static readonly byte MIFARE_DESFIRE_CHANGEFILESETTINGS = 26;
        private static readonly byte MIFARE_DESFIRE_DISABLEFORMATCARD = 27;
        private static readonly byte MIFARE_DESFIRE_ENABLERANDOMID = 28;
        private static readonly byte MIFARE_DESFIRE_SETDEFAULTKEY = 29;
        private static readonly byte MIFARE_DESFIRE_SETATS = 30;
        private static readonly byte MIFARE_DESFIRE_CREATERECORDFILE = 31;
        private static readonly byte MIFARE_DESFIRE_READRECORDS = 32;
        private static readonly byte MIFARE_DESFIRE_WRITERECORD = 33;
        private static readonly byte MIFARE_DESFIRE_CLEARRECORDFILE = 34;

        /// <summary>
        /// Retrieve the Available Application IDs after selecing PICC (App 0), Authentication is needed - depending on the security config
        /// </summary>
        /// <returns>a uint32[] of the available appids with 4bytes each, null if no apps are available or on error</returns>
        public async Task<UInt32[]> MifareDesfire_GetAppIDsAsync()
        {
            return await MifareDesfire_GetAppIDsAsync(28);
        }

        /// <summary>
        /// Retrieve the Available Application IDs after selecing PICC (App 0), Authentication is needed - depending on the security config
        /// </summary>
        /// <param name="maxAppIDCnt"></param>
        /// <returns>a uint32[] of the available appids with 4bytes each, null if no apps are available or on error</returns>
        /// <exception cref="ReaderException"></exception>
        public async Task<UInt32[]> MifareDesfire_GetAppIDsAsync(byte maxAppIDCnt)
        {
            List<byte> bytes = new List<byte> { API_MIFAREDESFIRE, MIFARE_DESFIRE_GETAPPIDS , CRYPTO_ENV , maxAppIDCnt};

            var parser = await CallFunctionAsync(bytes.ToArray());
            var success = parser.ParseBool();

            if (success)
            {
                var appIDCnt = parser.ParseByte();

                var appids = new UInt32[appIDCnt];

                for (var i = 0; i < appIDCnt; i++)
                {
                    appids[i] = parser.ParseUInt32();
                }

                return appids;
            }
            else
            {
                throw new ReaderException("Call was not successfull, error " + Enum.GetName(typeof(ReaderError), ReaderError.AccessDenied), null);
            }
        }

        /// <summary>
        /// Creates a new Application
        /// </summary>
        /// <param name="_keySettingsTarget">byte: KS_CHANGE_KEY_WITH_MK = 0, KS_ALLOW_CHANGE_MK = 1, KS_FREE_LISTING_WITHOUT_MK = 2, KS_FREE_CREATE_DELETE_WITHOUT_MK = 4, KS_CONFIGURATION_CHANGEABLE = 8, KS_DEFAULT = 11, KS_CHANGE_KEY_WITH_TARGETED_KEYNO = 224, KS_CHANGE_KEY_FROZEN = 240</param>
        /// <param name="_keyTypeTargetApplication">byte: 0 = 3DES, 1 = 3K3DES, 2 = AES</param>
        /// <param name="_maxNbKeys">int max. number of keys</param>
        /// <param name="_appID">int application id</param>
        /// <returns></returns>
        /// <exception cref="ReaderException"></exception>
        public async Task MifareDesfire_CreateApplicationAsync(DESFireAppAccessRights keySettingsTarget, DESFireKeyType keyTypeTargetApplication, int maxNbKeys, int appID)
        {
            List<byte> bytes = new List<byte> { API_MIFAREDESFIRE, MIFARE_DESFIRE_CREATEAPP, CRYPTO_ENV };
            bytes.AddUInt32((UInt32)appID);
            bytes.Add((byte)keySettingsTarget);
            bytes.AddUInt32((UInt32)maxNbKeys);
            bytes.AddUInt32((UInt32)keyTypeTargetApplication);

            var parser = await CallFunctionAsync(bytes.ToArray());
            var success = parser.ParseBool();

            if (!success)
            {
                throw new ReaderException("Call was not successfull, error " + Enum.GetName(typeof(ReaderError), ReaderError.AccessDenied), null);
            }
        }

        /// <summary>
        /// Select a Desfire Application
        /// </summary>
        /// <param name="appID"></param>
        /// <returns></returns>
        /// <exception cref="ReaderException"></exception>
        public async Task MifareDesfire_DeleteApplicationAsync(uint appID)
        {
            List<byte> bytes = new List<byte> { API_MIFAREDESFIRE, MIFARE_DESFIRE_DELETEAPP, CRYPTO_ENV };
            bytes.AddUInt32((UInt32)appID);

            var parser = await CallFunctionAsync(bytes.ToArray());
            var success = parser.ParseBool();

            if (!success)
            {
                throw new ReaderException("Call was not successfull, error " + Enum.GetName(typeof(ReaderError), ReaderError.AccessDenied), null);
            }
        }

        /// <summary>
        /// Select a Mifare Desfire Application
        /// </summary>
        /// <param name="appID">The Application ID to select</param>
        /// <returns>true if Application could be selected, false otherwise</returns>
        /// <exception cref="ReaderException"></exception>
        public async Task MifareDesfire_SelectApplicationAsync(uint appID)
        {          
            List<byte> bytes = new List<byte> { API_MIFAREDESFIRE, MIFARE_DESFIRE_SELECTAPP, CRYPTO_ENV };
            bytes.AddUInt32((UInt32)appID);

            var parser = await CallFunctionAsync(bytes.ToArray());
            var success = parser.ParseBool();

            if (!success)
            {
                throw new ReaderException("Call was not successfull, error " + Enum.GetName(typeof(ReaderError), ReaderError.AccessDenied), null);
            }
        }

        /// <summary>
        /// Authenticate to a previously selected desfire application
        /// </summary>
        /// <param name="key">string: a 16 bytes key e.g. 00000000000000000000000000000000</param>
        /// <param name="keyNo">byte: the keyNo to use</param>
        /// <param name="keyType">byte: 0 = 3DES, 1 = 3K3DES, 2 = AES</param>
        /// <param name="authMode">byte: 1 = EV1 Mode, 0 = EV0 Mode</param>
        /// <returns></returns>
        /// <exception cref="ReaderException"></exception>
        public async Task MifareDesfire_AuthenticateAsync(string key, byte keyNo, byte keyType, byte authMode)
        {
            var keyBytes = ByteArrayConverter.GetBytesFrom(key);
            List<byte> bytes = new List<byte> { API_MIFAREDESFIRE, MIFARE_DESFIRE_AUTH, CRYPTO_ENV, keyNo, (byte)keyBytes.Length };
            bytes.AddRange(keyBytes);
            bytes.Add(keyType);
            bytes.Add(authMode);

            var parser = await CallFunctionAsync(bytes.ToArray());
            var success = parser.ParseBool();

            if (!success)
            {
                throw new ReaderException("Call was not successfull, error " + Enum.GetName(typeof(ReaderError), ReaderError.AccessDenied), null);
            }
        }

        /// <summary>
        /// Get the KeySettings (Properties: KeySettings, NumberOfKeys, KeyType) of the selected Application. Authentication is needed - depending on the security config
        /// </summary>
        /// <returns></returns>
        /// <exception cref="ReaderException"></exception>
        public async Task<DESFireKeySettings> MifareDesfire_GetKeySettingsAsync()
        {
            List<byte> bytes = new List<byte> { API_MIFAREDESFIRE, MIFARE_DESFIRE_GETKEYSETTINGS, CRYPTO_ENV};

            var parser = await CallFunctionAsync(bytes.ToArray());
            var success = parser.ParseBool();

            if (success)
            {
                var keySettings = new DESFireKeySettings();

                keySettings.AccessRights = (DESFireAppAccessRights)parser.ParseByte();
                keySettings.NumberOfKeys = parser.ParseUInt32();
                keySettings.KeyType = (DESFireKeyType)parser.ParseUInt32();

                return keySettings;
            }
            else
            {
                throw new ReaderException("Call was not successfull, error " + Enum.GetName(typeof(ReaderError), ReaderError.AccessDenied), null);
            }
        }

        /// <summary>
        /// Retrieve the available file IDs after selecing app and authenticating to app
        /// </summary>
        /// <returns>byte[] array of available file ids</returns>
        /// <exception cref="ReaderException"></exception>
        public async Task<byte[]> MifareDesfire_GetFileIDsAsync()
        {
            List<byte> bytes = new List<byte> { API_MIFAREDESFIRE, MIFARE_DESFIRE_GETFILEIDS, CRYPTO_ENV , DESFIRE_MAX_FILEIDS};

            var parser = await CallFunctionAsync(bytes.ToArray());
            var success = parser.ParseBool();

            if (success)
            {
                var filesCount = parser.ParseByte();
                var fids = new byte[filesCount];

                for (var i = 0; i < filesCount; i++)
                {
                    fids[i] = parser.ParseByte();
                }

                return fids;
            }
            else
            {
                throw new ReaderException("Call was not successfull, error " + Enum.GetName(typeof(ReaderError), ReaderError.AccessDenied), null);
            }
        }

        /// <summary>
        /// Get the filesettings of a fileid
        /// </summary>
        /// <param name="fileNo">ID of the desired file</param>
        /// <returns><see cref="DESFireFileSettings"/></returns>
        /// <exception cref="ReaderException"></exception>
        public async Task<DESFireFileSettings> MifareDesfire_GetFileSettingsAsync(byte fileNo)
        {
            List<byte> bytes = new List<byte> { API_MIFAREDESFIRE, MIFARE_DESFIRE_GETFILESETTINGS, CRYPTO_ENV, fileNo };

            var parser = await CallFunctionAsync(bytes.ToArray());
            var success = parser.ParseBool();

            if (success)
            {
                var fileSettings = new DESFireFileSettings();

                fileSettings.FileType = (DESFireFileType)parser.ParseByte();
                fileSettings.ComSett = parser.ParseByte();

                var ar = parser.ParseUInt16();
                fileSettings.accessRights = DESFireFileAccessRights.FromAccessRightsWord(ar);

                switch (fileSettings.FileType)
                {
                    case DESFireFileType.DF_FT_STDDATAFILE:
                    case DESFireFileType.DF_FT_BACKUPDATAFILE:
                        fileSettings.DataFileSetting = fileSettings.DataFileSetting ?? new DataFileSetting();
                        fileSettings.DataFileSetting.FileSize = parser.ParseUInt32();
                        break;

                    case DESFireFileType.DF_FT_VALUEFILE:
                        fileSettings.ValueFileSetting.LowerLimit = parser.ParseUInt32();
                        fileSettings.ValueFileSetting.UpperLimit = parser.ParseUInt32();
                        fileSettings.ValueFileSetting.LimitedCreditValue = parser.ParseUInt32();
                        fileSettings.ValueFileSetting.LimitedCreditEnabled = parser.ParseByte();
                        fileSettings.ValueFileSetting.FreeGetValue = parser.ParseByte();
                        fileSettings.ValueFileSetting.RFU = parser.ParseByte();
                        break;

                    case DESFireFileType.DF_FT_CYCLICRECORDFILE:
                    case DESFireFileType.DF_FT_LINEARRECORDFILE:
                        fileSettings.RecordFileSetting = fileSettings.RecordFileSetting ?? new RecordFileSetting();
                        fileSettings.RecordFileSetting.RecordSize = parser.ParseUInt32();
                        fileSettings.RecordFileSetting.MaxNumberOfRecords = parser.ParseUInt32();
                        fileSettings.RecordFileSetting.CurrentNumberOfRecords = parser.ParseUInt32();
                        break;

                    default:

                        break;
                }

                return fileSettings;
            }
            else
            {
                throw new ReaderException("Call was not successfull, error " + Enum.GetName(typeof(ReaderError), ReaderError.AccessDenied), null);
            }
        }

        /// <summary>
        /// Read out Data on a Desfire
        /// </summary>
        /// <param name="fileNo">byte: filenumber: 0x00 - 0x14</param>
        /// <param name="length">int: filesize to read</param>
        /// <param name="comSet">byte: 0 = Plain, 1 = CMAC, 2 = Encrypted</param>
        /// <returns>byte[] of data</returns>
        /// <exception cref="ReaderException"></exception>
        public Task<byte[]> MifareDesfire_ReadDataAsync(byte fileNo, int length, EncryptionMode mode)
        {
            // Simple Protocol: DESFire_ReadData [0F08][Byte: CryptoEnv][Byte: FileNo][UInt16: Offset][Byte: Length][Byte: CommSet]
            // Length is a single byte, so reads > 255 bytes must be segmented.
            return MifareDesfire_ReadDataAsync(fileNo, 0, length, mode);
        }

        /// <summary>
        /// Read out Data on a DESFire (Simple Protocol: DESFire_ReadData / 0x0F08)
        /// </summary>
        /// <param name="fileNo">byte: filenumber</param>
        /// <param name="offset">UInt16: start offset</param>
        /// <param name="length">int: number of bytes to read</param>
        /// <param name="mode">byte: 0 = Plain, 1 = CMAC, 2 = Encrypted</param>
        /// <returns>byte[] of data</returns>
        /// <remarks>
        /// Simple Protocol limits Offset to UInt16 and Length to one byte (max 255). This method transparently
        /// segments reads larger than 255 bytes by issuing multiple DESFire_ReadData calls with increasing offsets.
        /// </remarks>
        /// <exception cref="ReaderException"></exception>
        public async Task<byte[]> MifareDesfire_ReadDataAsync(byte fileNo, ushort offset, int length, EncryptionMode mode)
        {
            if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
            if (length == 0) return new byte[0];

            // Offset is UInt16 in Simple Protocol, therefore offset + length must stay within 0..65535.
            if ((uint)offset + (uint)length > 0x10000)
                throw new ArgumentOutOfRangeException(nameof(length), "Simple Protocol uses a 16-bit offset; offset + length must be <= 65536.");

            var data = new byte[length];
            var remaining = length;
            var dstIndex = 0;
            var currentOffset = offset;

            while (remaining > 0)
            {
                var chunkLen = (byte)Math.Min(0xFF, remaining);

                List<byte> bytes = new List<byte> { API_MIFAREDESFIRE, MIFARE_DESFIRE_READDATA, CRYPTO_ENV, fileNo };
                bytes.AddUInt16(currentOffset);
                bytes.Add(chunkLen);
                bytes.Add((byte)mode);

                var parser = await CallFunctionAsync(bytes.ToArray());
                var success = parser.ParseBool();

                if (!success)
                    throw new ReaderException("Call was not successfull, error " + Enum.GetName(typeof(ReaderError), ReaderError.AccessDenied), null);

                var chunk = parser.ParseVarByteArray();
                if (chunk.Length != chunkLen)
                    throw new ReaderException("Unexpected DESFire_ReadData length. Expected " + chunkLen + " bytes, got " + chunk.Length + " bytes.", null);

                Array.Copy(chunk, 0, data, dstIndex, chunkLen);

                dstIndex += chunkLen;
                remaining -= chunkLen;
                currentOffset = (ushort)(currentOffset + chunkLen);
            }

            return data;
        }

        /// <summary>
        /// Write Data to a Desfire File
        /// </summary>
        /// <param name="fileNo">The file number to read</param>
        /// <param name="data"></param>
        /// <param name="mode"><see cref="EncryptionMode"/></param>
        /// <returns></returns>
        /// <exception cref="ReaderException"></exception>
        public Task MifareDesfire_WriteDataAsync(byte fileNo, byte[] data, EncryptionMode mode)
        {
            // Simple Protocol: DESFire_WriteData [0F09][Byte: CryptoEnv][Byte: FileNo][UInt16: Offset][Byte Array(Var): Data][Byte: CommSet]
            // The 'Data' field is length-prefixed (one byte), so writes > 255 bytes must be segmented.
            return MifareDesfire_WriteDataAsync(fileNo, 0, data, mode);
        }

        /// <summary>
        /// Write Data to a DESFire (Simple Protocol: DESFire_WriteData / 0x0F09)
        /// </summary>
        /// <param name="fileNo">byte: filenumber</param>
        /// <param name="offset">UInt16: start offset</param>
        /// <param name="data">byte[]: data to write</param>
        /// <param name="mode">byte: 0 = Plain, 1 = CMAC, 3 = Fully Encrypted</param>
        /// <remarks>
        /// In Simple Protocol the data payload is a Byte Array(Var), i.e. it starts with a single length byte.
        /// Therefore each command can transport at most 255 data bytes. This method transparently segments larger
        /// payloads by issuing multiple DESFire_WriteData calls with increasing offsets.
        /// </remarks>
        public async Task MifareDesfire_WriteDataAsync(byte fileNo, ushort offset, byte[] data, EncryptionMode mode)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (data.Length == 0) return;

            // Offset is UInt16 in Simple Protocol, therefore offset + data.Length must stay within 0..65535.
            if ((uint)offset + (uint)data.Length > 0x10000)
                throw new ArgumentOutOfRangeException(nameof(data), "Simple Protocol uses a 16-bit offset; offset + data.Length must be <= 65536.");

            var remaining = data.Length;
            var srcIndex = 0;
            var currentOffset = offset;

            while (remaining > 0)
            {
                var chunkLen = Math.Min(0xFF, remaining);

                // Build: [API][FUNC][CryptoEnv][FileNo][UInt16 Offset][VarLen][Data...][CommSet]
                List<byte> bytes = new List<byte> { API_MIFAREDESFIRE, MIFARE_DESFIRE_WRITEDATA, CRYPTO_ENV, fileNo };
                bytes.AddUInt16(currentOffset);
                bytes.Add((byte)chunkLen); // Var array length byte

                var chunk = new byte[chunkLen];
                Array.Copy(data, srcIndex, chunk, 0, chunkLen);
                bytes.AddRange(chunk);

                bytes.Add((byte)mode);

                var parser = await CallFunctionAsync(bytes.ToArray());
                var success = parser.ParseBool();

                if (!success)
                    throw new ReaderException("Call was not successfull, error " + Enum.GetName(typeof(ReaderError), ReaderError.AccessDenied), null);

                srcIndex += chunkLen;
                remaining -= chunkLen;
                currentOffset = (ushort)(currentOffset + chunkLen);
            }
        }

                /// <summary>
        /// Get the current value of a DESFire value file.
        /// </summary>
        /// <param name="fileNo">File number (value file)</param>
        /// <param name="mode">Communication setting (Plain/CMAC/Encrypted)</param>
        /// <returns>The current value.</returns>
        /// <exception cref="ReaderException"></exception>
        public async Task<UInt32> MifareDesfire_GetValueAsync(byte fileNo, EncryptionMode mode)
        {
            List<byte> bytes = new List<byte> { API_MIFAREDESFIRE, MIFARE_DESFIRE_GETVALUE, CRYPTO_ENV, fileNo, (byte)mode };

            var parser = await CallFunctionAsync(bytes.ToArray());
            var success = parser.ParseBool();

            if (success)
            {
                return parser.ParseUInt32();
            }

            throw new ReaderException("Call was not successfull, error " + Enum.GetName(typeof(ReaderError), ReaderError.AccessDenied), null);
        }

        /// <summary>
        /// Credit (add) a value to a DESFire value file (transactional).
        /// </summary>
        /// <param name="fileNo">File number (value file)</param>
        /// <param name="value">Value to credit</param>
        /// <param name="mode">Communication setting (Plain/CMAC/Encrypted)</param>
        /// <exception cref="ReaderException"></exception>
        public async Task MifareDesfire_CreditAsync(byte fileNo, UInt32 value, EncryptionMode mode)
        {
            List<byte> bytes = new List<byte> { API_MIFAREDESFIRE, MIFARE_DESFIRE_CREDIT, CRYPTO_ENV, fileNo };

            bytes.AddUInt32(value);
            bytes.Add((byte)mode);

            var parser = await CallFunctionAsync(bytes.ToArray());
            var success = parser.ParseBool();

            if (!success)
            {
                throw new ReaderException("Call was not successfull, error " + Enum.GetName(typeof(ReaderError), ReaderError.AccessDenied), null);
            }
        }

        /// <summary>
        /// Debit (subtract) a value from a DESFire value file (transactional).
        /// </summary>
        /// <param name="fileNo">File number (value file)</param>
        /// <param name="value">Value to debit</param>
        /// <param name="mode">Communication setting (Plain/CMAC/Encrypted)</param>
        /// <exception cref="ReaderException"></exception>
        public async Task MifareDesfire_DebitAsync(byte fileNo, UInt32 value, EncryptionMode mode)
        {
            List<byte> bytes = new List<byte> { API_MIFAREDESFIRE, MIFARE_DESFIRE_DEBIT, CRYPTO_ENV, fileNo };

            bytes.AddUInt32(value);
            bytes.Add((byte)mode);

            var parser = await CallFunctionAsync(bytes.ToArray());
            var success = parser.ParseBool();

            if (!success)
            {
                throw new ReaderException("Call was not successfull, error " + Enum.GetName(typeof(ReaderError), ReaderError.AccessDenied), null);
            }
        }

        /// <summary>
        /// Limited credit a value to a DESFire value file (transactional).
        /// </summary>
        /// <param name="fileNo">File number (value file)</param>
        /// <param name="value">Value to credit</param>
        /// <param name="mode">Communication setting (Plain/CMAC/Encrypted)</param>
        /// <exception cref="ReaderException"></exception>
        public async Task MifareDesfire_LimitedCreditAsync(byte fileNo, UInt32 value, EncryptionMode mode)
        {
            List<byte> bytes = new List<byte> { API_MIFAREDESFIRE, MIFARE_DESFIRE_LIMITEDCREDIT, CRYPTO_ENV, fileNo };

            bytes.AddUInt32(value);
            bytes.Add((byte)mode);

            var parser = await CallFunctionAsync(bytes.ToArray());
            var success = parser.ParseBool();

            if (!success)
            {
                throw new ReaderException("Call was not successfull, error " + Enum.GetName(typeof(ReaderError), ReaderError.AccessDenied), null);
            }
        }

        /// <summary>
        /// Get the free Memory of a desfire. 
        /// </summary>
        /// <returns>a uint32 of the available memory if supported</returns>
        /// <exception cref="ReaderException"></exception>
        public async Task<UInt16> MifareDesfire_GetFreeMemoryAsync()
        {
            List<byte> bytes = new List<byte> { API_MIFAREDESFIRE, MIFARE_DESFIRE_GETFREEMEMORY, CRYPTO_ENV };

            var parser = await CallFunctionAsync(bytes.ToArray());
            var success = parser.ParseBool();

            if (success)
            {
                return parser.ParseUInt16();
            }
            else
            {
                throw new ReaderException("Call was not successfull, error " + Enum.GetName(typeof(ReaderError), ReaderError.AccessDenied), null);
            }
        }

        /// <summary>
        /// Format a Chip
        /// </summary>
        /// <returns></returns>
        /// <exception cref="ReaderException"></exception>
        public async Task MifareDesfire_FormatTagAsync()
        {
            List<byte> bytes = new List<byte> { API_MIFAREDESFIRE, MIFARE_DESFIRE_FORMATTAG, CRYPTO_ENV };

            var parser = await CallFunctionAsync(bytes.ToArray());
            var success = parser.ParseBool();

            if (!success)
            {
                throw new ReaderException("Call was not successfull, error " + Enum.GetName(typeof(ReaderError), ReaderError.AccessDenied), null);
            }
        }

        /// <summary>
        /// Create a std data file on a desfire
        /// </summary>
        /// <param name="fileNo"></param>
        /// <param name="fileType"><see cref="DESFireFileType"/></param>
        /// <param name="mode"><see cref="EncryptionMode"/></param>
        /// <param name="accessRights"><see cref="DESFireFileAccessRights"/></param>
        /// <param name="fileSize"></param>
        /// <returns></returns>
        /// <exception cref="ReaderException"></exception>
        public async Task MifareDesfire_CreateStdDataFileAsync(byte fileNo, DESFireFileType fileType, EncryptionMode mode, DESFireFileAccessRights accessRights, UInt32 fileSize)
        {
            List<byte> bytes = new List<byte> { API_MIFAREDESFIRE, MIFARE_DESFIRE_CREATE_STDDATAFILE, CRYPTO_ENV, fileNo, (byte)fileType, (byte)mode };

            UInt16 fileAccessRights = accessRights.ToAccessRightsWord();

            bytes.AddUInt16(fileAccessRights);
            bytes.AddUInt32(fileSize);
            bytes.AddRange(new byte[] { 0,0,0,0, 0,0,0,0, 0,0,0,0 });

            var parser = await CallFunctionAsync(bytes.ToArray());
            var success = parser.ParseBool();

            if (!success)
            {
                throw new ReaderException("Call was not successfull, error " + Enum.GetName(typeof(ReaderError), ReaderError.AccessDenied), null);
            }
        }
        /// <summary>
        /// Create a value file on a DESFire.
        /// </summary>
        /// <param name="fileNo"></param>
        /// <param name="fileType"><see cref="DESFireFileType"/></param>
        /// <param name="mode"><see cref="EncryptionMode"/></param>
        /// <param name="accessRights"><see cref="DESFireFileAccessRights"/></param>
        /// <param name="lowerLimit"></param>
        /// <param name="upperLimit"></param>
        /// <param name="limitedCreditValue"></param>
        /// <param name="freeGetValue">If true, GetValue is allowed without authentication (bit 0).</param>
        /// <param name="limitedCreditEnabled">If true, LimitedCredit is enabled (bit 1).</param>
        /// <exception cref="ReaderException"></exception>
        public async Task MifareDesfire_CreateValueFileAsync(
            byte fileNo,
            DESFireFileType fileType,
            EncryptionMode mode,
            DESFireFileAccessRights accessRights,
            UInt32 lowerLimit,
            UInt32 upperLimit,
            UInt32 limitedCreditValue,
            bool freeGetValue,
            bool limitedCreditEnabled)
        {
            List<byte> bytes = new List<byte> { API_MIFAREDESFIRE, MIFARE_DESFIRE_CREATE_VALUEFILE, CRYPTO_ENV, fileNo, (byte)fileType, (byte)mode };

            UInt16 fileAccessRights = accessRights.ToAccessRightsWord();

            bytes.AddUInt16(fileAccessRights);
            bytes.AddUInt32(lowerLimit);
            bytes.AddUInt32(upperLimit);
            bytes.AddUInt32(limitedCreditValue);

            UInt32 flags = 0;
            if (freeGetValue) flags |= 0x01;
            if (limitedCreditEnabled) flags |= 0x02;
            bytes.AddUInt32(flags);

            var parser = await CallFunctionAsync(bytes.ToArray());
            var success = parser.ParseBool();

            if (!success)
            {
                throw new ReaderException("Call was not successfull, error " + Enum.GetName(typeof(ReaderError), ReaderError.AccessDenied), null);
            }
        }
        /// <summary>
        /// Get version of a desfire.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="ReaderException"></exception>
        public async Task<byte[]> MifareDesfire_GetVersionAsync()
        {
            {
                List<byte> bytes = new List<byte> { API_MIFAREDESFIRE, MIFARE_DESFIRE_GETVERSION, CRYPTO_ENV };

                var parser = await CallFunctionAsync(bytes.ToArray());
                var success = parser.ParseBool();

                if (success)
                {
                    return parser.ParseVarByteArray();
                }
                else
                {
                    throw new ReaderException("Call was not successfull, error " + Enum.GetName(typeof(ReaderError), ReaderError.NotSupported), null);
                }
            }
        }

        /// <summary>
        /// Delete a file in a desfire app
        /// </summary>
        /// <param name="fileNo">byte: Filenumber to delete</param>
        /// <returns>true if the Operation was successful, false otherwise</returns>
        /// <exception cref="ReaderException"></exception>
        public async Task MifareDesfire_DeleteFileAsync(byte fileNo)
        {
            {
                List<byte> bytes = new List<byte> { API_MIFAREDESFIRE, MIFARE_DESFIRE_DELETEFILE, CRYPTO_ENV, fileNo };

                var parser = await CallFunctionAsync(bytes.ToArray());
                var success = parser.ParseBool();

                if (!success)
                {
                    throw new ReaderException("Call was not successfull, error " + Enum.GetName(typeof(ReaderError), ReaderError.AccessDenied), null);
                }
            }
        }

        /// <summary>
        /// Commit a DESFire transaction (e.g., after Credit/Debit/LimitedCredit).
        /// </summary>
        /// <exception cref="ReaderException"></exception>
        public async Task MifareDesfire_CommitTransactionAsync()
        {
            List<byte> bytes = new List<byte> { API_MIFAREDESFIRE, MIFARE_DESFIRE_COMMITTRANSACTION, CRYPTO_ENV };

            var parser = await CallFunctionAsync(bytes.ToArray());
            var success = parser.ParseBool();

            if (!success)
            {
                throw new ReaderException("Call was not successfull, error " + Enum.GetName(typeof(ReaderError), ReaderError.AccessDenied), null);
            }
        }

        /// <summary>
        /// Abort a DESFire transaction.
        /// </summary>
        /// <exception cref="ReaderException"></exception>
        public async Task MifareDesfire_AbortTransactionAsync()
        {
            List<byte> bytes = new List<byte> { API_MIFAREDESFIRE, MIFARE_DESFIRE_ABORTTRANSACTION, CRYPTO_ENV };

            var parser = await CallFunctionAsync(bytes.ToArray());
            var success = parser.ParseBool();

            if (!success)
            {
                throw new ReaderException("Call was not successfull, error " + Enum.GetName(typeof(ReaderError), ReaderError.AccessDenied), null);
            }
        }

        /// <summary>
        /// Get the UID from the currently selected DESFire card.
        /// </summary>
        /// <param name="bufferSize">Maximum UID bytes to return (default: 0xFF)</param>
        /// <returns>UID byte array</returns>
        /// <exception cref="ReaderException"></exception>
        public async Task<byte[]> MifareDesfire_GetUidAsync(byte bufferSize = 0xFF)
        {
            List<byte> bytes = new List<byte> { API_MIFAREDESFIRE, MIFARE_DESFIRE_GETUID, CRYPTO_ENV, bufferSize };

            var parser = await CallFunctionAsync(bytes.ToArray());
            var success = parser.ParseBool();

            if (success)
            {
                return parser.ParseVarByteArray();
            }

            throw new ReaderException("Call was not successfull, error " + Enum.GetName(typeof(ReaderError), ReaderError.AccessDenied), null);
        }

        /// <summary>
        /// Get the key version of a DESFire key.
        /// </summary>
        /// <param name="keyNo">Key number</param>
        /// <returns>Key version</returns>
        /// <exception cref="ReaderException"></exception>
        public async Task<byte> MifareDesfire_GetKeyVersionAsync(byte keyNo)
        {
            List<byte> bytes = new List<byte> { API_MIFAREDESFIRE, MIFARE_DESFIRE_GETKEYVERSION, CRYPTO_ENV, keyNo };

            var parser = await CallFunctionAsync(bytes.ToArray());
            var success = parser.ParseBool();

            if (success)
            {
                return parser.ParseByte();
            }

            throw new ReaderException("Call was not successfull, error " + Enum.GetName(typeof(ReaderError), ReaderError.AccessDenied), null);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="keySettings"></param>
        /// <param name="numberOfKeys"></param>
        /// <param name="keyType"></param>
        /// <returns></returns>
        public async Task MifareDesfire_ChangeKeySettingsAsync(DESFireAppAccessRights keySettings, UInt32 numberOfKeys, DESFireKeyType keyType)
        {
            List<byte> bytes = new List<byte> { API_MIFAREDESFIRE, MIFARE_DESFIRE_CHANGEKEYSETTINGS, CRYPTO_ENV};

            bytes.Add((byte)keySettings);
            bytes.AddUInt32(numberOfKeys);
            bytes.AddUInt32((byte)keyType);

            var parser = await CallFunctionAsync(bytes.ToArray());
            var success = parser.ParseBool();

            if (!success)
            {
                throw new ReaderException("Call was not successfull, error " + Enum.GetName(typeof(ReaderError), ReaderError.AccessDenied), null);
            }
        }

        /// <summary>
        /// Changes a Key
        /// </summary>
        /// <param name="oldKey"></param>
        /// <param name="newKey"></param>
        /// <param name="keyVersion"></param>
        /// <param name="accessRights"></param>
        /// <param name="keyNo"></param>
        /// <param name="numberOfKeys"></param>
        /// <param name="keyType">The Type of the new Key</param>
        /// <returns></returns>
        public async Task MifareDesfire_ChangeKeyAsync(string oldKey, string newKey, byte keyVersion, byte accessRights, byte keyNo, UInt32 numberOfKeys, DESFireKeyType keyType)
        {
            List<byte> bytes = new List<byte> { API_MIFAREDESFIRE, MIFARE_DESFIRE_CHANGEKEY, CRYPTO_ENV, keyNo };
            bytes.Add(DESFIRE_KEYLENGTH);
            bytes.AddRange(ByteArrayConverter.GetBytesFrom(oldKey));
            bytes.Add(DESFIRE_KEYLENGTH);
            bytes.AddRange(ByteArrayConverter.GetBytesFrom(newKey));
            bytes.Add(keyVersion);
            bytes.Add(accessRights);
            bytes.AddUInt32(numberOfKeys);
            bytes.AddUInt32((byte)keyType);

            var parser = await CallFunctionAsync(bytes.ToArray());
            var success = parser.ParseBool();

            if (!success)
            {
                throw new ReaderException("Call was not successfull, error " + Enum.GetName(typeof(ReaderError), ReaderError.AccessDenied), null);
            }
        }

        /// <summary>
        /// Change a DESFire file's communication settings and access rights.
        /// </summary>
        /// <param name="fileNo"></param>
        /// <param name="newCommSet"><see cref="EncryptionMode"/></param>
        /// <param name="oldAccessRights"><see cref="DESFireFileAccessRights"/></param>
        /// <param name="newAccessRights"><see cref="DESFireFileAccessRights"/></param>
        /// <exception cref="ReaderException"></exception>
        public async Task MifareDesfire_ChangeFileSettingsAsync(byte fileNo, EncryptionMode newCommSet, DESFireFileAccessRights oldAccessRights, DESFireFileAccessRights newAccessRights)
        {
            List<byte> bytes = new List<byte> { API_MIFAREDESFIRE, MIFARE_DESFIRE_CHANGEFILESETTINGS, CRYPTO_ENV, fileNo, (byte)newCommSet };

            UInt16 oldAr = oldAccessRights.ToAccessRightsWord();
            UInt16 newAr = newAccessRights.ToAccessRightsWord();

            bytes.AddUInt16(oldAr);
            bytes.AddUInt16(newAr);

            var parser = await CallFunctionAsync(bytes.ToArray());
            var success = parser.ParseBool();

            if (!success)
            {
                throw new ReaderException("Call was not successfull, error " + Enum.GetName(typeof(ReaderError), ReaderError.AccessDenied), null);
            }
        }

        /// <summary>
        /// Disable FormatCard for the currently selected DESFire card.
        /// </summary>
        /// <exception cref="ReaderException"></exception>
        public async Task MifareDesfire_DisableFormatCardAsync()
        {
            List<byte> bytes = new List<byte> { API_MIFAREDESFIRE, MIFARE_DESFIRE_DISABLEFORMATCARD, CRYPTO_ENV };

            var parser = await CallFunctionAsync(bytes.ToArray());
            var success = parser.ParseBool();

            if (!success)
            {
                throw new ReaderException("Call was not successfull, error " + Enum.GetName(typeof(ReaderError), ReaderError.AccessDenied), null);
            }
        }

        /// <summary>
        /// Enable random UID (RandomID) for the currently selected DESFire card.
        /// </summary>
        /// <exception cref="ReaderException"></exception>
        public async Task MifareDesfire_EnableRandomIdAsync()
        {
            List<byte> bytes = new List<byte> { API_MIFAREDESFIRE, MIFARE_DESFIRE_ENABLERANDOMID, CRYPTO_ENV };

            var parser = await CallFunctionAsync(bytes.ToArray());
            var success = parser.ParseBool();

            if (!success)
            {
                throw new ReaderException("Call was not successfull, error " + Enum.GetName(typeof(ReaderError), ReaderError.AccessDenied), null);
            }
        }

        /// <summary>
        /// Set the default DESFire key (master key) and its version.
        /// </summary>
        /// <param name="key">Key material (variable length)</param>
        /// <param name="keyVersion">Key version byte</param>
        /// <exception cref="ReaderException"></exception>
        public async Task MifareDesfire_SetDefaultKeyAsync(byte[] key, byte keyVersion)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (key.Length > 0xFF) throw new ArgumentOutOfRangeException(nameof(key), "Key is too long for a Var Byte Array (max 255).");

            List<byte> bytes = new List<byte> { API_MIFAREDESFIRE, MIFARE_DESFIRE_SETDEFAULTKEY, CRYPTO_ENV, (byte)key.Length };

            bytes.AddRange(key);
            bytes.Add(keyVersion);

            var parser = await CallFunctionAsync(bytes.ToArray());
            var success = parser.ParseBool();

            if (!success)
            {
                throw new ReaderException("Call was not successfull, error " + Enum.GetName(typeof(ReaderError), ReaderError.AccessDenied), null);
            }
        }

        /// <summary>
        /// Set the ATS (Answer To Select) for the currently selected DESFire card.
        /// </summary>
        /// <param name="ats">ATS bytes</param>
        /// <exception cref="ReaderException"></exception>
        public async Task MifareDesfire_SetAtsAsync(byte[] ats)
        {
            if (ats == null) throw new ArgumentNullException(nameof(ats));
            if (ats.Length > 0xFF) throw new ArgumentOutOfRangeException(nameof(ats), "ATS is too long for a Var Byte Array (max 255).");

            List<byte> bytes = new List<byte> { API_MIFAREDESFIRE, MIFARE_DESFIRE_SETATS, CRYPTO_ENV, (byte)ats.Length };

            bytes.AddRange(ats);

            var parser = await CallFunctionAsync(bytes.ToArray());
            var success = parser.ParseBool();

            if (!success)
            {
                throw new ReaderException("Call was not successfull, error " + Enum.GetName(typeof(ReaderError), ReaderError.AccessDenied), null);
            }
        }

        /// <summary>
        /// Create a record file on a DESFire.
        /// </summary>
        /// <param name="fileNo"></param>
        /// <param name="fileType"><see cref="DESFireFileType"/> (Linear or Cyclic record file)</param>
        /// <param name="mode"><see cref="EncryptionMode"/></param>
        /// <param name="accessRights"><see cref="DESFireFileAccessRights"/></param>
        /// <param name="recordSize"></param>
        /// <param name="maxNumberOfRecords"></param>
        /// <exception cref="ReaderException"></exception>
        public async Task MifareDesfire_CreateRecordFileAsync(byte fileNo, DESFireFileType fileType, EncryptionMode mode, DESFireFileAccessRights accessRights, UInt32 recordSize, UInt32 maxNumberOfRecords)
        {
            List<byte> bytes = new List<byte> { API_MIFAREDESFIRE, MIFARE_DESFIRE_CREATERECORDFILE, CRYPTO_ENV, fileNo, (byte)fileType, (byte)mode };

            UInt16 fileAccessRights = accessRights.ToAccessRightsWord();

            bytes.AddUInt16(fileAccessRights);
            bytes.AddUInt32(recordSize);
            bytes.AddUInt32(maxNumberOfRecords);

            // appending 0's (8 bytes) per Simple Protocol
            bytes.AddRange(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 });

            var parser = await CallFunctionAsync(bytes.ToArray());
            var success = parser.ParseBool();

            if (!success)
            {
                throw new ReaderException("Call was not successfull, error " + Enum.GetName(typeof(ReaderError), ReaderError.AccessDenied), null);
            }
        }

        /// <summary>
        /// Read records from a DESFire record file.
        /// </summary>
        /// <param name="fileNo"></param>
        /// <param name="offset"></param>
        /// <param name="numberOfRecords"></param>
        /// <param name="recordSize"></param>
        /// <param name="mode"><see cref="EncryptionMode"/></param>
        /// <returns>Record data</returns>
        /// <exception cref="ReaderException"></exception>
        public async Task<byte[]> MifareDesfire_ReadRecordsAsync(byte fileNo, UInt16 offset, byte numberOfRecords, byte recordSize, EncryptionMode mode)
        {
            List<byte> bytes = new List<byte> { API_MIFAREDESFIRE, MIFARE_DESFIRE_READRECORDS, CRYPTO_ENV, fileNo };

            bytes.AddUInt16(offset);
            bytes.Add(numberOfRecords);
            bytes.Add(recordSize);
            bytes.Add((byte)mode);

            var parser = await CallFunctionAsync(bytes.ToArray());
            var success = parser.ParseBool();

            if (success)
            {
                return parser.ParseVarByteArray();
            }

            throw new ReaderException("Call was not successfull, error " + Enum.GetName(typeof(ReaderError), ReaderError.AccessDenied), null);
        }

        /// <summary>
        /// Write a record to a DESFire record file.
        /// </summary>
        /// <param name="fileNo"></param>
        /// <param name="offset"></param>
        /// <param name="data"></param>
        /// <param name="mode"><see cref="EncryptionMode"/></param>
        /// <exception cref="ReaderException"></exception>
        public async Task MifareDesfire_WriteRecordAsync(byte fileNo, UInt16 offset, byte[] data, EncryptionMode mode)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (data.Length > 0xFF) throw new ArgumentOutOfRangeException(nameof(data), "Data is too long for a Var Byte Array (max 255).");

            List<byte> bytes = new List<byte> { API_MIFAREDESFIRE, MIFARE_DESFIRE_WRITERECORD, CRYPTO_ENV, fileNo };

            bytes.AddUInt16(offset);
            bytes.Add((byte)data.Length);
            bytes.AddRange(data);
            bytes.Add((byte)mode);

            var parser = await CallFunctionAsync(bytes.ToArray());
            var success = parser.ParseBool();

            if (!success)
            {
                throw new ReaderException("Call was not successfull, error " + Enum.GetName(typeof(ReaderError), ReaderError.AccessDenied), null);
            }
        }

        /// <summary>
        /// Clear a DESFire record file.
        /// </summary>
        /// <param name="fileNo"></param>
        /// <exception cref="ReaderException"></exception>
        public async Task MifareDesfire_ClearRecordFileAsync(byte fileNo)
        {
            List<byte> bytes = new List<byte> { API_MIFAREDESFIRE, MIFARE_DESFIRE_CLEARRECORDFILE, CRYPTO_ENV, fileNo };

            var parser = await CallFunctionAsync(bytes.ToArray());
            var success = parser.ParseBool();

            if (!success)
            {
                throw new ReaderException("Call was not successfull, error " + Enum.GetName(typeof(ReaderError), ReaderError.AccessDenied), null);
            }
        }
        #endregion

    }
}
