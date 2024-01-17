using System;

namespace Elatec.NET
{
    public static class Constants
    {
        public const int MAX_WAIT_INSERTION = 200; //timeout for chip response in ms
        public const string TITLE_SUFFIX = "DEVELOPER PREVIEW"; //turns out special app versions
    }

    /// <summary>
    /// Type of a chip, as returned by the reader.
    /// </summary>
    public enum ChipType
    {
        NOTAG = 0,
        // LF Tags
        EM4102 = 0x40,    // "EM4x02/CASI-RUSCO" (aka IDRO_A)
        HITAG1S = 0x41,   // "HITAG 1/HITAG S"   (aka IDRW_B)
        HITAG2 = 0x42,    // "HITAG 2"           (aka IDRW_C)
        EM4150 = 0x43,    // "EM4x50"            (aka IDRW_D)
        AT5555 = 0x44,    // "T55x7"             (aka IDRW_E)
        ISOFDX = 0x45,    // "ISO FDX-B"         (aka IDRO_G)
        EM4026 = 0x46,    // N/A                 (aka IDRO_H)
        HITAGU = 0x47,    // N/A                 (aka IDRW_I)
        EM4305 = 0x48,    // "EM4305"            (aka IDRW_K)
        HIDPROX = 0x49,	// "HID Prox"
        TIRIS = 0x4A,	    // "ISO HDX/TIRIS"
        COTAG = 0x4B,	    // "Cotag"
        IOPROX = 0x4C,	// "ioProx"
        INDITAG = 0x4D,	// "Indala"
        HONEYTAG = 0x4E,	// "NexWatch"
        AWID = 0x4F,	    // "AWID"
        GPROX = 0x50,	    // "G-Prox"
        PYRAMID = 0x51,	// "Pyramid"
        KERI = 0x52,	    // "Keri"
        DEISTER = 0x53,	// "Deister"
        CARDAX = 0x54,	// "Cardax"
        NEDAP = 0x55,	    // "Nedap"
        PAC = 0x56,	    // "PAC"
        IDTECK = 0x57,	// "IDTECK"
        ULTRAPROX = 0x58,	// "UltraProx"
        ICT = 0x59,	    // "ICT"
        ISONAS = 0x5A,	// "Isonas"
        // HF Tags
        MIFARE = 0x80,	// "ISO14443A/MIFARE"
        ISO14443B = 0x81,	// "ISO14443B"
        ISO15693 = 0x82,	// "ISO15693"
        LEGIC = 0x83,	    // "LEGIC"
        HIDICLASS = 0x84,	// "HID iCLASS"
        FELICA = 0x85,	// "FeliCa"
        SRX = 0x86,	    // "SRX"
        NFCP2P = 0x87,	// "NFC Peer-to-Peer"
        BLE = 0x88,	    // "Bluetooth Low Energy"
        TOPAZ = 0x89,     // "Topaz"
        CTS = 0x8A,       // "CTS256 / CTS512"
        BLELC = 0x8B,     // "Bluetooth Low Energy LEGIC Connect"

    }

    /// <summary>
    /// Type of Mifare from NXP AN10833
    /// </summary>
    public enum MifareChipSubType
    {
        // Custom
        Unspecified = 0xB0,
        NTAG = 0xB1,
        MifareMini = 0xB2,
        Mifare1K = 0xB3,
        Mifare2K = 0xB4,
        Mifare4K = 0xB5,
        SAM_AV1 = 0xB6,
        SAM_AV2 = 0xB7,
        MifarePlus_SL0_1K = 0xB9,
        MifarePlus_SL0_2K = 0xBA,
        MifarePlus_SL0_4K = 0xBB,
        MifarePlus_SL1_1K = 0xBC,
        MifarePlus_SL1_2K = 0xBD,
        MifarePlus_SL1_4K = 0xBE,
        MifarePlus_SL2_1K = 0xBF,
        MifarePlus_SL2_2K = 0xC0,
        MifarePlus_SL2_4K = 0xC1,
        MifarePlus_SL3_1K = 0xC2,
        MifarePlus_SL3_2K = 0xC3,
        MifarePlus_SL3_4K = 0xC4,
        DESFire = 0xC5,
        DESFireEV1 = 0xC6,
        DESFireEV2 = 0xC7,
        DESFireEV3 = 0xC8,
        SmartMX_DESFire_Generic = 0xC9,
        SmartMX_DESFire_2K = 0xCA,
        SmartMX_DESFire_4K = 0xCB,
        SmartMX_DESFire_8K = 0xCC,
        SmartMX_DESFire_16K = 0xCD,
        SmartMX_DESFire_32K = 0xCE,
        DESFire_256 = 0xD0,
        DESFire_2K = 0xD1,
        DESFire_4K = 0xD2,
        DESFireEV1_256 = 0xD3,
        DESFireEV1_2K = 0xD4,
        DESFireEV1_4K = 0xD5,
        DESFireEV1_8K = 0xD6,
        DESFireEV2_2K = 0xD7,
        DESFireEV2_4K = 0xD8,
        DESFireEV2_8K = 0xD9,
        DESFireEV2_16K = 0xDA,
        DESFireEV2_32K = 0xDB,
        DESFireEV3_2K = 0xDC,
        DESFireEV3_4K = 0xDD,
        DESFireEV3_8K = 0xDE,
        DESFireEV3_16K = 0xDF,
        DESFireEV3_32K = 0xE0,
        DESFireLight = 0xE1,
        SmartMX_Mifare_1K = 0xF9,
        SmartMX_Mifare_4K = 0xFA,
        MifareUltralight = 0xFB,
        MifareUltralightC = 0xFC,
        GENERIC_T_CL_A = 0xFF
    }

    [Flags]
    public enum LFTagTypes : uint
    {
        NOTAG = 0,
        // LF Tags
        EM4102 = 1 << 0,    // "EM4x02/CASI-RUSCO" (aka IDRO_A)
        HITAG1S = 1 << 1,   // "HITAG 1/HITAG S"   (aka IDRW_B)
        HITAG2 = 1 << 2,    // "HITAG 2"           (aka IDRW_C)
        EM4150 = 1 << 3,    // "EM4x50"            (aka IDRW_D)
        AT5555 = 1 << 4,    // "T55x7"             (aka IDRW_E)
        ISOFDX = 1 << 5,    // "ISO FDX-B"         (aka IDRO_G)
        EM4026 = 1 << 6,    // N/A                 (aka IDRO_H)
        HITAGU = 1 << 7,    // N/A                 (aka IDRW_I)
        EM4305 = 1 << 8,    // "EM4305"            (aka IDRW_K)
        HIDPROX = 1 << 9,	// "HID Prox"
        TIRIS = 1 << 0xA,	    // "ISO HDX/TIRIS"
        COTAG = 1 << 0xB,	    // "Cotag"
        IOPROX = 1 << 0xC,	// "ioProx"
        INDITAG = 1 << 0xD,	// "Indala"
        HONEYTAG = 1 << 0xE,	// "NexWatch"
        AWID = 1 << 0xF,	    // "AWID"
        GPROX = 1 << 0x10,	    // "G-Prox"
        PYRAMID = 1 << 0x11,	// "Pyramid"
        KERI = 1 << 0x12,	    // "Keri"
        DEISTER = 1 << 0x13,	// "Deister"
        CARDAX = 1 << 0x14,	// "Cardax"
        NEDAP = 1 << 0x15,	    // "Nedap"
        PAC = 1 << 0x16,	    // "PAC"
        IDTECK = 1 << 0x17,	// "IDTECK"
        ULTRAPROX = 1 << 0x18,	// "UltraProx"
        ICT = 1 << 0x19,	    // "ICT"
        ISONAS = 1 << 0x1A,	// "Isonas"

        AllLFTags = 0xFFFFFFFF,
    }

    [Flags]
    public enum HFTagTypes : uint
    {
        NOTAG = 0,
        // HF Tags
        MIFARE = 1 << 0,	// "ISO14443A/MIFARE"
        ISO14443B = 1 << 1,	// "ISO14443B"
        ISO15693 = 1 << 2,	// "ISO15693"
        LEGIC = 1 << 3,	    // "LEGIC"
        HIDICLASS = 1 << 4,	// "HID iCLASS"
        FELICA = 1 << 5,	// "FeliCa"
        SRX = 1 << 6,	    // "SRX"
        NFCP2P = 1 << 7,	// "NFC Peer-to-Peer"
        BLE = 1 << 8,	    // "Bluetooth Low Energy"
        TOPAZ = 1 << 9,     // "Topaz"
        CTS = 1 << 0xA,       // "CTS256 / CTS512"
        BLELC = 1 << 0xB,     // "Bluetooth Low Energy LEGIC Connect"

        AllHFTags = 0xFFFFFFFF,
    }

    /// <summary>
    /// A response to a TWN Simple Protocol command always starts with a byte, which reflects execution of the command on protocol level.
    /// </summary>
    public enum ResponseError : byte
    {
        None = 0,
        UnknownFunction = 1,
        MissingParameter = 2,
        UnusedParameters = 3,
        InvalidFunction = 4,
        ParserError = 5,
    }

    /// <summary>
    ///     Values returned by <see cref="TWN4ReaderDevice.GetLastErrorAsync"/>, at least theoretically.
    ///     In practice, the method also returns undocumented error codes.
    /// </summary>
    public enum ReaderError : uint
    {
        None = 0,

        // --- General Errors ---
        OutOfMemory = 1,
        IsAlreadyInit = 2,
        NotInit = 3,
        IsAlreadyOpen = 4,
        NotOpen = 5,
        Range = 6,
        Parameter = 7,
        General = 8,
        NotSupported = 9,
        State = 10,
        Compatibility = 11,
        Data = 12,

        // --- Storage Errors ---
        UnknownStorageID = 100,
        WrongIndex = 101,
        FlashErase = 102,
        FlashWrite = 103,
        SectorNotFound = 104,
        StorageFull = 105,
        StorageInvalid = 106,
        TransactionLimit = 107,

        // --- File Errors ---
        UnknownFS = 200,
        FileNotFound = 201,
        FileAlreadyExists = 202,
        EndOfFile = 203,
        StorageNotFound = 204,
        StorageAlreadyMounted = 205,
        AccessDenied = 206,
        FileCorrupt = 207,
        InvalidFileEnv = 208,
        InvalidFileID = 209,
        ResourceLimit = 210,

        // --- I2C Errors ---
        Timeout = 300,
        PecErr = 301,
        Ovr = 302,
        /// <summary>
        /// Acknowledge Error
        /// </summary>
        AF = 303,
        Arlo = 304,
        /// <summary>
        /// Bus Error
        /// </summary>
        BErr = 305
    }

    #region GPIOs

    /// <summary>
    /// Bitmasks of GPIOs
    /// </summary>
    [Flags]
    public enum Gpios : byte
    {
        GPIO0 = 0x0001,
        GPIO1 = 0x0002,
        GPIO2 = 0x0004,
        GPIO3 = 0x0008,
        GPIO4 = 0x0010,
        GPIO5 = 0x0020,
        GPIO6 = 0x0040,
        GPIO7 = 0x0080,
    }

    /// <summary>
    /// GPIO Pullup/Pulldown
    /// </summary>
    public enum GpioPullType : byte
    {
        NoPull = 0,
        PullUp = 1,
        PullDown = 2
    }

    /// <summary>
    /// GPIO Output Type
    /// </summary>
    public enum GpioOutputType : byte
    {
        PushPull = 0,
        OpenDrain = 1
    }

    #endregion

    /// <summary>
    ///     Colored LEDs
    /// </summary>
    /// <remarks>
    ///     REDLED = GPIO0,
    ///     GREENLED = GPIO1,
    ///     YELLOWLED = GPIO2,
    ///     BLUELED = GPIO2.
    ///     Attention: Yellow and Blue have the same id!
    /// </remarks>
    [Flags]
    public enum Leds : byte
    {
        Red = Gpios.GPIO0,
        Green = Gpios.GPIO1,
        Yellow = Gpios.GPIO2,
        Blue = Gpios.GPIO2,
        All = Red | Green | Yellow | Blue
    }

    #region Mifare KeySettings

    /// <summary>
    /// 
    /// </summary>
    public enum MifareKeyType
    {
        KT_KEY_A,
        KT_KEY_B
    }

    /// <summary>
    /// Access Rights <see cref="DESFireAppAccessRights"/>
    /// Number of Keys
    /// KeyType <see cref="DESFireKeyType"/>
    /// </summary>
    public class DESFireKeySettings
    {
        private DESFireAppAccessRights accessRights;
        private DESFireKeyType keyType;
        private UInt32 numberOfKeys;

        public DESFireAppAccessRights AccessRights { get => accessRights; set => accessRights = value; }
        public UInt32 NumberOfKeys { get => numberOfKeys; set => numberOfKeys = value; }
        public DESFireKeyType KeyType { get => keyType; set => keyType = value; }
    }

    /// <summary>
    /// CRYPTOMODE_AES128 = 2,
    /// CRYPTOMODE_3DES = 0
    /// CRYPTOMODE_3K3DES = 1
    /// </summary>    
    public enum DESFireKeyType
    {
        DF_KEY_DES = 0,
        DF_KEY_3K3DES = 1,
        DF_KEY_AES = 2
    }

    /// <summary>
    /// 
    /// </summary>
    public class DESFireFileSettings
    {
        private DESFireFileAccessRights fileAccessRights;
        private DESFireFileType fileType;
        private byte comSett;
        private DataFileSetting dataFileSetting;
        private RecordFileSetting recordFileSetting;
        private ValueFileSetting valueFileSetting;

        public DESFireFileSettings()
        {
            accessRights = new DESFireFileAccessRights();
        }

        public DESFireFileAccessRights accessRights { get => fileAccessRights; set => fileAccessRights = value; }
        public DESFireFileType FileType { get => fileType; set => fileType = value; }
        public byte ComSett { get => comSett; set => comSett = value; }
        public DataFileSetting DataFileSetting { get => dataFileSetting; set => dataFileSetting = value; }
        public RecordFileSetting RecordFileSetting { get => recordFileSetting; set => recordFileSetting = value; }
        public ValueFileSetting ValueFileSetting { get => valueFileSetting; set => valueFileSetting = value; }
    }

    /// <summary>
    /// DESF_FILETYPE_STDDATAFILE           0
    /// DESF_FILETYPE_BACKUPDATAFILE        1
    /// DESF_FILETYPE_VALUEFILE             2
    /// DESF_FILETYPE_LINEARRECORDFILE      3
    /// DESF_FILETYPE_CYCLICRECORDFILE      4
    /// </summary>
    public enum DESFireFileType
    {
        DF_FT_STDDATAFILE           = 0,
        DF_FT_BACKUPDATAFILE        = 1,
        DF_FT_VALUEFILE             = 2,
        DF_FT_LINEARRECORDFILE      = 3,
        DF_FT_CYCLICRECORDFILE      = 4
    }

    public class DataFileSetting
    {
        private UInt32 fileSize;
        public UInt32 FileSize { get => fileSize; set => fileSize = value; }
    }

    public class RecordFileSetting
    {
        private UInt32 recordSize;
        private UInt32 maxNumberOfRecords;
        private UInt32 currentNumberOfRecords;

        public UInt32 RecordSize { get => recordSize; set => recordSize = value; }
        public UInt32 MaxNumberOfRecords { get => maxNumberOfRecords; set => maxNumberOfRecords = value; }
        public UInt32 CurrentNumberOfRecords { get => currentNumberOfRecords; set => currentNumberOfRecords = value; }
    }

    public class ValueFileSetting
    {
        private UInt32 upperLimit;
        private UInt32 lowerLimit;
        private UInt32 limitedCreditValue;

        private byte limitedCreditEnabled;
        private byte freeGetValue;
        private byte rFU;

        public UInt32 UpperLimit { get => upperLimit; set => upperLimit = value; }
        public UInt32 LowerLimit { get => lowerLimit; set => lowerLimit = value; }
        public UInt32 LimitedCreditValue { get => limitedCreditValue; set => limitedCreditValue = value; }

        public byte LimitedCreditEnabled { get => limitedCreditEnabled; set => limitedCreditEnabled = value; }
        public byte FreeGetValue { get => freeGetValue; set => freeGetValue = value; }
        public byte RFU { get => rFU; set => rFU = value; }
    }

    /// <summary>
    /// KS_CHANGE_KEY_WITH_MK = 0,
    /// KS_ALLOW_CHANGE_MK = 1,
    /// KS_FREE_LISTING_WITHOUT_MK = 2,
    /// KS_FREE_CREATE_DELETE_WITHOUT_MK = 4,
    /// KS_CONFIGURATION_CHANGEABLE = 8,
    /// KS_DEFAULT = 11,
    /// KS_CHANGE_KEY_WITH_TARGETED_KEYNO = 224,
    /// KS_CHANGE_KEY_FROZEN = 240
    /// </summary>
    [Flags]
    public enum DESFireAppAccessRights
    {
        KS_CHANGE_KEY_WITH_MK = 0,
        KS_ALLOW_CHANGE_MK = 1,
        KS_FREE_LISTING_WITHOUT_MK = 2,
        KS_FREE_CREATE_DELETE_WITHOUT_MK = 4,
        KS_CONFIGURATION_CHANGEABLE = 8,
        KS_DEFAULT = 11,
        KS_CHANGE_KEY_WITH_TARGETED_KEYNO = 224,
        KS_CHANGE_KEY_FROZEN = 240
    }

    /// <summary>
    /// AccessRights to access a file. Stored in the app keysettings
    /// </summary>
    public class DESFireFileAccessRights
    {
        private byte readKeyNo;
        private byte writeKeyNo;
        private byte readWriteKeyNo;
        private byte changeKeyNo;

        public byte ReadKeyNo { get => readKeyNo; set => readKeyNo = value; }
        public byte WriteKeyNo { get => writeKeyNo; set => writeKeyNo = value; }
        public byte ReadWriteKeyNo { get => readWriteKeyNo; set => readWriteKeyNo = value; }
        public byte ChangeKeyNo { get => changeKeyNo; set => changeKeyNo = value; }    
    }

    /// <summary>
    /// 
    /// </summary>
    public enum EncryptionMode
    {
        CM_PLAIN = 0,
        CM_MAC = 1,
        CM_ENCRYPT = 3
    }

    public struct AccessBits
    {
        public short c1;
        public short c2;
        public short c3;
    }

    public struct SectorAccessBits
    {
        public int Cx;

        public AccessBits d_data_block0_access_bits;
        public AccessBits d_data_block1_access_bits;
        public AccessBits d_data_block2_access_bits;
        public AccessBits d_sector_trailer_access_bits;
    }

    public struct MifareDesfireKey
    {
        public MifareDesfireKey(DESFireKeyType _encryptionType, string _key)
        {
            EncryptionType = _encryptionType;
            Key = _key;
        }

        public DESFireKeyType EncryptionType;
        public string Key;
    }

    public struct MifareClassicKey
    {
        public MifareClassicKey(int _keyNumber, string _accessBits)
        {
            KeyNumber = _keyNumber;
            accessBits = _accessBits;
        }

        private string accessBits;

        public int KeyNumber;
        public string AccessBits { get => accessBits; set => accessBits = value; }
    }

    #endregion

    /// <summary>
    /// Pitch of Readerpiezo to be used with Beep() or Note() used by PlayMusic().
    /// </summary>
    public enum NotePitch
    {
        PAUSE = 0,

        C3 = 1047,
        CIS3 = 1109,
        DES3 = 1109,
        D3 = 1175,
        DIS3 = 1245,
        ES3 = 1245,
        E3 = 1319,
        F3 = 1397,
        FIS3 = 1480,
        GES3 = 1480,
        G3 = 1568,
        GIS3 = 1661,
        AES3 = 1661,
        A3 = 1760,
        B3 = 1865,
        H3 = 1976,
        C4 = 2093,
        CIS4 = 2217,
        DES4 = 2217,
        D4 = 2349,
        DIS4 = 2489,
        ES4 = 2489,
        E4 = 2637,
        F4 = 2794,
        FIS4 = 2960,
        GES4 = 2960,
        G4 = 3136,
        GIS4 = 3322,
        AES4 = 3322,
        A4 = 3520,
        AIS4 = 3729,
        B4 = 3729,
        H4 = 3951,
        C5 = 4186,
        CIS5 = 4435,
        DES5 = 4435,
        D5 = 4699,
        DIS5 = 4978,
        ES5 = 4978,
        E5 = 5274,
        F5 = 5588,

        LOW = 2057,
        HIGH = 2400
    }

    /// <summary>
    ///     TODO: Elatec references some constants in TWN4 API reference, but without their values:
    ///     USBTYPE_CCID_HID: CCID + HID (compound device),
    ///     USBTYPE_REPORTS: CCID + HID reports,
    ///     USBTYPE_CCID_CDC: CCID + CDC (compound device),
    ///     USBTYPE_CCID: CCID
    /// </summary>
    public enum UsbType : byte
    {
        None = 0,
        /// <summary>
        /// CDC device (virtual COM port)
        /// </summary>
        CDC = 1,
        Keyboard = 4,
    }

    public enum DeviceType : byte
    {
        LegicNfc = 10,
        MifareNfc = 11,
        Legic63 = 12,
    }
}