/*
 * Created by SharpDevelop.
 * Date: 12.10.2017
 * Time: 11:21
 *
 */

using System;

namespace Elatec.NET
{
    // change key using mk = 0 enum
    // allow change mk = 1 or-ing
    // listing without mk = 2 or-ing
    // create del without mk = 4 or-ing
    // config changeable = 8 or-ing
    // default setting = 11
    // change using keyno = 224 enum
    // change frozen = 240 enum

    public static class Constants
    {
        public const int MAX_WAIT_INSERTION = 200; //timeout for chip response in ms
        public const string TITLE_SUFFIX = "DEVELOPER PREVIEW"; //turns out special app versions
    }

    /// <summary>
    ///
    /// </summary>
    public enum FileType_MifareDesfireFileType
    {
        StdDataFile,
        BackupFile,
        ValueFile,
        CyclicRecordFile,
        LinearRecordFile
    }

    /// <summary>
    /// Select DataBlock in Data Explorer
    /// </summary>
    [Flags]
    public enum DataExplorer_DataBlock
    {
        Block0 = 0,
        Block1 = 1,
        Block2 = 2,
        Block3 = 3
    }

    /// <summary>
    /// Select DataBlock in Sector Trailer Access Bits
    /// </summary>
    public enum SectorTrailer_DataBlock
    {
        Block0 = 0,
        Block1 = 1,
        Block2 = 2,
        BlockAll = 3
    }

    [Flags]
    public enum SectorTrailer_AccessType
    {
        WriteKeyB = 1,
        ReadKeyB = 2,
        WriteAccessBits = 4,
        ReadAccessBits = 8,
        WriteKeyA = 16,
        ReadKeyA = 32
    }

    /// <summary>
    ///
    /// </summary>
    public enum AccessCondition_MifareClassicSectorTrailer
    {
        NotApplicable,
        NotAllowed,
        Allowed_With_KeyA,
        Allowed_With_KeyB,
        Allowed_With_KeyA_Or_KeyB
    }

    /// <summary>
    /// UID and Type of Cardtechnology
    /// </summary>
    public struct CARD_INFO
    {
        public CARD_INFO(CARD_TYPE _type, string _uid)
        {
            CardType = _type;
            uid = _uid;
        }

        public string uid;
        public CARD_TYPE CardType;
    }

    /// <summary>
    /// Available Cardtechnologies
    /// </summary>
    public enum CARD_TYPE
    {
        Unspecified,
        ISO15693,
        Mifare1K,
        Mifare2K,
        Mifare4K,
        DESFire,
        DESFireEV1,
        DESFireEV2,
        MifarePlus_SL3_1K,
        MifarePlus_SL3_2K,
        MifarePlus_SL3_4K,
        MifareUltralight
    };

    /// <summary>
    /// 
    /// </summary>
    public enum MifareKeyType
    {
        KT_KEY_A,
        KT_KEY_B
    }

    /// <summary>
    /// 
    /// </summary>
    public enum DESfireKeyType
    {
        DF_KEY_DES,
        DF_KEY_3K3DES,
        DF_KEY_AES
    }

    /// <summary>
    /// 
    /// </summary>
    public enum DESFireKeySettings
    {
        KS_ALLOW_CHANGE_MK,
        KS_DEFAULT,
        KS_FREE_CREATE_DELETE_WITHOUT_MK
    }

    /// <summary>
    /// 
    /// </summary>
    public enum DESFireAccessRights
    {
        DF_KS_DES,
        DF_KS_3K3DES,
        DF_KS_AES
    }

    /// <summary>
    /// 
    /// </summary>
    public struct SectorAccessBits
    {
        int sab_a;
    }

    /// <summary>
    /// 
    /// </summary>
    public enum EncryptionMode
    {
        CM_PLAIN,
        CM_ENCRYPT,
        DF_KS_AES
    }

    /// <summary>
    /// Error Messages
    /// </summary>
    public enum Result
    {
        Empty,
        NoError,
        AuthenticationError,
        DeviceNotReadyError,
        IOError
    }

    public enum DESFireKeyType
    {
        DF_KEY_3K3DES,
        DF_KEY_AES,
        DF_KEY_DES
    }

    public enum TaskAccessRights
    {
        DF_KEY0,
        DF_KEY1,
        DF_KEY2
    }

    public struct MifareDesfireDefaultKeys
    {
        public MifareDesfireDefaultKeys(DESFireKeyType _encryptionType, string _key)
        {
            EncryptionType = _encryptionType;
            Key = _key;
        }

        public DESFireKeyType EncryptionType;
        public string Key;
    }

    public struct MifareClassicDefaultKeys
    {
        public MifareClassicDefaultKeys(int _keyNumber, string _accessBits)
        {
            KeyNumber = _keyNumber;
            accessBits = _accessBits;
        }

        private string accessBits;

        public int KeyNumber;
        public string AccessBits { get => accessBits; set => accessBits = value; }
    }
}