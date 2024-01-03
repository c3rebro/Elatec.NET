using ByteArrayHelper.Extensions;

using Log4CSharp;

using System;
using System.IO.Ports;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

using Elatec.NET.Model;

using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;

/*
* Elatec.NET is a C# library to easily Talk to Elatec's TWN4 Devices
* 
* Boolean "Results": Success = true, Failed = false
* 
* Some TWN4 Specific "Special" information:
* 
* Getting the ATS on different Readers works differently.
* 
*/

namespace Elatec.NET
{
    public class TWN4ReaderDevice : IDisposable
    {
        private const bool RESULT_SUCCESS = true;
        private const bool RESULT_FAILED = false;

        //private protected int portNumber;        

        private bool _disposed;
        private static readonly object syncRoot = new object();
        private static TWN4ReaderDevice instance;

        #region ELATEC COMMANDS
        private const string GET_LASTERR = "000A";

        private const string BEEP_CMD = "0407";
        private const string LEDINIT_CMD = "0410";
        private const string LEDON_CMD = "0411";
        private const string LEDOFF_CMD = "0412";

        private const string MIFARE_CLASSIC_LOGIN = "0B00";
        private const string MIFARE_CLASSIC_READBLOCK = "0B01";
        private const string MIFARE_CLASSIC_WRITEBLOCK = "0B02";

        private const string MIFARE_DESFIRE_GETAPPIDS = "0F00";
        private const string MIFARE_DESFIRE_CREATEAPP = "0F01";
        private const string MIFARE_DESFIRE_DELETEAPP = "0F02";
        private const string MIFARE_DESFIRE_SELECTAPP = "0F03";
        private const string MIFARE_DESFIRE_AUTH = "0F04";
        private const string MIFARE_DESFIRE_GETKEYSETTINGS = "0F05";
        private const string MIFARE_DESFIRE_GETFILEIDS = "0F06";
        private const string MIFARE_DESFIRE_GETFILESETTINGS = "0F07";
        private const string MIFARE_DESFIRE_READDATA = "0F08";
        private const string MIFARE_DESFIRE_GETFREEMEMORY = "0F0E";
        private const string MIFARE_DESFIRE_FORMATTAG = "0F0F";
        private const string MIFARE_DESFIRE_CREATE_STDDATAFILE = "0F10";
        private const string MIFARE_DESFIRE_DELETEFILE = "0F13";

        private const string MIFARE_DESFIRE_CHANGEKEYSETTINGS = "0F18";
        private const string MIFARE_DESFIRE_CHANGEKEY = "0F19";

        private const string ISO14443_GET_ATS = "1200";
        private const string ISO14443_4_TXD = "1203";
        private const string ISO14443_3_TXD = "1207";

        private const string ISO_CMD_RATS = "E050BCA5FFFF00";

        #endregion

        public static TWN4ReaderDevice Instance
        {
            get
            {
                lock (TWN4ReaderDevice.syncRoot)
                {
                    if (instance == null)
                    {
                        instance = DeviceManager.GetAvailableReaders().FirstOrDefault();
                        //instance = new TWN4ReaderDevice();
                        return instance;

                    }
                    else
                    {
                        return instance;
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public TWN4ReaderDevice()
        {
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="portName"></param>
        public TWN4ReaderDevice(string portName)
        {
            PortName = portName;
        }

        /// <summary>
        /// 
        /// </summary>
        public bool PortAccessDenied { get; private set; }

        #region Low level APIs

        #region API_SYS / System Functions

        private const int API_SYS = 0;

        // TODO: SYSFUNC(API_SYS, 0, bool SysCall(TEnvSysCall* Env))
        // TODO: SYSFUNC(API_SYS, 1, void Reset(void))
        // TODO: SYSFUNC(API_SYS, 2, void StartBootloader(void))
        // TODO: SYSFUNC(API_SYS, 3, unsigned long GetSysTicks(void))
        // TODO: SYSFUNC(API_SYS, 4, int GetVersionString(char* VersionString, int MaxLen))
        // TODO: SYSFUNC(API_SYS, 5, int GetUSBType(void))
        // TODO: SYSFUNC(API_SYS, 6, int GetDeviceType(void))
        // TODO: SYSFUNC(API_SYS, 7, int Sleep(unsigned long Ticks, unsigned long Flags))
        // TODO: SYSFUNC(API_SYS, 8, void GetDeviceUID(byte* UID))
        // TODO: SYSFUNC(API_SYS, 9, bool SetParameters(const byte* TLV,int ByteCount))
        // TODO: SYSFUNC(API_SYS,10, unsigned int GetLastError(void))
        // TODO: SYSFUNC(API_SYS,11, int Diagnostic(int Mode,const void* In,int InLen,void* Out,int* OutLen,int MaxOutLen))

        // TODO: SYSFUNC(API_SYS,13, int GetProdSerNo(byte* SerNo, int MaxLen))
        // TODO: SYSFUNC(API_SYS,14, bool SetInterruptHandler(TInterruptHandler InterruptHandler, int IntNo))
        // TODO: SYSFUNC(API_SYS,15, void GetVersionInfo(TVersionInfo* VersionInfo))
        // TODO: SYSFUNC(API_SYS,16, bool ReadInfoValue(int Index, int FilterType, int* Type, int* Length, byte* Value, int MaxLength))
        // TODO: SYSFUNC(API_SYS,17, bool WriteInfoValue(int Type, int Length,const byte* Value))
        // TODO: SYSFUNC(API_SYS,18, bool GetCustomKeyID(byte* CustomKeyID, int* Length, int MaxLength))
        // TODO: SYSFUNC(API_SYS,19, bool GetParameters(const byte* Types,int TypeCount,byte* TLVBytes,int* TLVByteCount,int TLVMaxByteCount))

        #endregion

        #region API_PERIPH / Periphery Functions

        public const int API_PERIPH = 4;

        /// <summary>
        ///     Use this function to configure one or several GPIOs as output. Each output can be configured to have an
        ///     integrated pull-up or pull-down resistor.The output driver characteristic is either Push-Pull or Open Drain.
        /// </summary>
        /// <param name="bits">Specify the GPIOs that shall be configured for output. Several GPIOs can
        ///     be configured simultaneously by using the bitwise or-operator (|).</param>
        /// <param name="pullUpDown">Specify the behaviour of the internal weak pull-up/down resistor.</param>
        /// <param name="outputType">Specify the output driver characteristic.</param>
        /// <returns></returns>
        public async Task GpioConfigureOutputsAsync(Gpios bits, GpioPullType pullUpDown, GpioOutputType outputType)
        {
            await DoTXRXAsync(new byte[] { API_PERIPH, 0, (byte)bits, (byte)pullUpDown, (byte)outputType });
        }

        /// <summary>
        ///     Use this function to configure one or several GPIOs as input. Each output can be configured to have an
        ///     integrated pull-up or pull-down resistor, alternatively it can be left floating.
        /// </summary>
        /// <param name="bits">Specify the GPIOs that shall be configured for input. Several GPIOs can
        ///     be configured simultaneously by using the bitwise or-operator (|).</param>
        /// <param name="pullUpDown">Specify the behaviour of the internal weak pull-up/down resistor.</param>
        /// <returns></returns>
        public async Task GpioConfigureInputsAsync(Gpios bits, GpioPullType pullUpDown)
        {
            await DoTXRXAsync(new byte[] { API_PERIPH, 1, (byte)bits, (byte)pullUpDown });
        }

        /// <summary>
        ///     Use this function to set one or several GPIOs to logical high level.
        ///     The respective ports must have been configured to output in advance.
        /// </summary>
        /// <param name="bits">Specify the GPIOs that shall be set to a logical level. Several GPIOs can
        ///     be handled simultaneously by using the bitwise or-operator (|).</param>
        /// <returns></returns>
        public async Task GpioSetBitsAsync(Gpios bits)
        {
            await DoTXRXAsync(new byte[] { API_PERIPH, 2, (byte)bits });
        }

        /// <summary>
        ///     Use this function to set one or several GPIOs to logical low level.
        ///     The respective ports must have been configured to output in advance.
        /// </summary>
        /// <param name="bits">Specify the GPIOs that shall be set to a logical level. Several GPIOs can
        ///     be handled simultaneously by using the bitwise or-operator (|).</param>
        /// <returns></returns>
        public async Task GpioClearBitsAsync(Gpios bits)
        {
            await DoTXRXAsync(new byte[] { API_PERIPH, 3, (byte)bits });
        }

        /// <summary>
        ///     Use this function to toggle the logical level of one or several GPIOs.
        ///     The respective ports must have been configured to output in advance.
        /// </summary>
        /// <param name="bits">Specify the GPIOs that shall be set to a logical level. Several GPIOs can
        ///     be handled simultaneously by using the bitwise or-operator (|).</param>
        /// <returns></returns>
        public async Task GpioToggleBitsAsync(Gpios bits)
        {
            await DoTXRXAsync(new byte[] { API_PERIPH, 4, (byte)bits });
        }

        // TODO: SYSFUNC(API_PERIPH, 5, void GPIOBlinkBits(int Bits, int TimeHi, int TimeLo))
        // TODO: SYSFUNC(API_PERIPH, 6, int GPIOTestBit(int Bit))

        /// <summary>
        /// Play a beep on the device.
        /// </summary>
        /// <param name="volume">Specify the volume in percent from 0 to 100.</param>
        /// <param name="frequency">Specify the frequency in Hertz from 500 to 10000.</param>
        /// <param name="onTime">Specify the duration of the beep in milliseconds from 0 to 10000000.</param>
        /// <param name="offTime">Specify the length of the pause after the beep in milliseconds from 0 to
        ///     10000000. This is useful for generating melodies.If this is not required, the
        ///     parameter may have the value 0.</param>
        /// <returns></returns>
        public async Task BeepAsync(byte volume, int frequency, int onTime, int offTime)
        {
            List<byte> bytes = new List<byte>() { API_PERIPH, 7 };
            bytes.Add(volume);
            bytes.AddWord(frequency);
            bytes.AddWord(onTime);
            bytes.AddWord(offTime);
            await DoTXRXAsync(bytes.ToArray());
        }

        public async Task DiagLedOnAsync()
        {
            await DoTXRXAsync(new byte[] { API_PERIPH, 8 });
        }

        public async Task DiagLedOffAsync()
        {
            await DoTXRXAsync(new byte[] { API_PERIPH, 9 });
        }

        public async Task DiagLedToggleAsync()
        {
            await DoTXRXAsync(new byte[] { API_PERIPH, 10 });
        }

        // TODO: SYSFUNC(API_PERIPH,11, bool DiagLEDIsOn(void))
        // TODO: SYSFUNC(API_PERIPH,12, void SendWiegand(int GPIOData0, int GPIOData1, int PulseTime, int IntervalTime,const byte* Bits,int BitCount))
        // TODO: SYSFUNC(API_PERIPH,13, void SendOmron(int GPIOClock, int GPIOData, int T1, int T2, int T3,const byte* Bits,int BitCount))
        // TODO: SYSFUNC(API_PERIPH,14, bool GPIOPlaySequence(const int* NewSequence,int ByteCount))
        // TODO: SYSFUNC(API_PERIPH,15, void GPIOStopSequence(void))

        /// <summary>
        /// Use this function to initialize the respective GPIOs to drive LEDs.
        /// </summary>
        /// <param name="leds">Specify the GPIOs that shall be configured for LED operation.</param>
        /// <returns></returns>
        public async Task InitLedsAsync(Leds leds = Leds.All)
        {
            await DoTXRXAsync(new byte[] { API_PERIPH, 16, (byte)leds });
        }

        /// <summary>
        /// Use this function to set one or several LEDs on.
        /// </summary>
        /// <param name="leds">Specify the LEDs that shall be set on.</param>
        /// <returns></returns>
        public async Task LedOnAsync(Leds leds)
        {
            await DoTXRXAsync(new byte[] { API_PERIPH, 17, (byte)leds });
        }

        /// <summary>
        /// Use this function to set one or several LEDs off.
        /// </summary>
        /// <param name="leds">Specify the LEDs that shall be set off.</param>
        /// <returns></returns>
        public async Task LedOffAsync(Leds leds)
        {
            await DoTXRXAsync(new byte[] { API_PERIPH, 18, (byte)leds });
        }

        /// <summary>
        /// Use this function to toggle one or several LEDs.
        /// </summary>
        /// <param name="leds">Specify the LEDs that shall be toggled.</param>
        /// <returns></returns>
        public async Task LedToggleAsync(Leds leds)
        {
            await DoTXRXAsync(new byte[] { API_PERIPH, 19, (byte)leds });
        }

        /// <summary>
        /// Use this function to let one or several LEDs blink.
        /// </summary>
        /// <param name="leds">Specify the LEDs that shall blink.</param>
        /// <param name="onTime">Specify the on-time in milliseconds.</param>
        /// <param name="offTime">Specify the off-time in milliseconds.</param>
        /// <returns></returns>
        public async Task LedBlinkAsync(Leds leds, int onTime, int offTime)
        {
            List<byte> bytes = new List<byte>() { API_PERIPH, 20 };
            bytes.Add((byte)leds);
            bytes.AddWord(onTime);
            bytes.AddWord(offTime);
            await DoTXRXAsync(bytes.ToArray());
        }

        // TODO: SYSFUNC(API_PERIPH,21,bool GPIOConfigureInterrupt(int GPIOBits,bool Enable,int Edge))

        /// <summary>
        /// Turn on beep with infinite length.
        /// </summary>
        /// <param name="volume">Specify the volume in percent from 0 to 100.</param>
        /// <param name="frequency">Specify the frequency in Hertz from 500 to 10000.</param>
        /// <returns></returns>
        public async Task BeepOnAsync(byte volume, int frequency)
        {
            List<byte> bytes = new List<byte>() { API_PERIPH, 22 };
            bytes.Add(volume);
            bytes.AddWord(frequency);
            await DoTXRXAsync(bytes.ToArray());
        }

        /// <summary>
        /// Turn off beep.
        /// </summary>
        /// <returns></returns>
        public async Task BeepOffAsync()
        {
            await DoTXRXAsync(new byte[] { API_PERIPH, 23 });
        }

        // TODO: SYSFUNC(API_PERIPH,24,void PlayMelody(const byte *Melody,int MelodyLength))

        #endregion

        #region API_RF

        public const int API_RF = 5;

        /// <summary>
        ///     Use this function to search a transponder in the reading range of TWN4. TWN4 is searching for all types
        ///     of transponders, which have been specified via function SetTagTypes. If a transponder has been found,
        ///     tag type, length of ID and ID data itself are returned.
        /// </summary>
        /// <param name="maxIDBytes">A value, which specifies the buffer size of ID.</param>
        /// <returns>A SearchTagResult. If tag.Found == true, ChipType and ID are returned.</returns>
        public async Task<SearchTagResult> SearchTagAsync(byte maxIDBytes = byte.MaxValue)
        {
            var result = await DoTXRXAsync(new byte[] { API_RF, 0, maxIDBytes });
            var parser = new ResponseParser(result.ToList());

            var errorCode = parser.ParseByte(); // TODO: move to DoTXRXAsync()
            var tag = new SearchTagResult();
            tag.Found = parser.ParseBool();
            if (tag.Found)
            {
                tag.ChipType = (ChipType)parser.ParseByte();
                tag.IDBitCount = parser.ParseByte();
                tag.IDBytes = parser.ParseVarByteArray();
            }
            return tag;
        }

        public class SearchTagResult
        {
            public bool Found { get; set; }
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
                    return ByteConverter.GetStringFrom(IDBytes);
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
            await DoTXRXAsync(new byte[] { API_RF, 1 });
        }

        /// <summary>
        /// Use this function to configure the transponders, which are searched by function SearchTag.
        /// </summary>
        /// <param name="lfTagTypes"></param>
        /// <param name="hfTagTypes"></param>
        /// <returns></returns>
        public async Task SetTagTypes(LFTagTypes lfTagTypes, HFTagTypes hfTagTypes)
        {
            List<byte> bytes = new List<byte>() { API_RF, 2 };
            bytes.AddLong((uint)lfTagTypes);
            bytes.AddLong((uint)hfTagTypes);
            await DoTXRXAsync(bytes.ToArray());
        }

        /// <summary>
        ///     This function returns the transponder types currently being searched for by function SearchTag separated
        ///     by frequency (LF and HF).
        /// </summary>
        /// <returns>Tag types.</returns>
        public async Task<GetTagTypesResult> GetTagTypesAsync()
        {
            var result = await DoTXRXAsync(new byte[] { API_RF, 3 });
            var parser = new ResponseParser(result.ToList());

            var errorCode = parser.ParseByte(); // TODO: move to DoTXRXAsync()
            var lf = parser.ParseLong();
            var hf = parser.ParseLong();

            return new GetTagTypesResult() { LFTagTypes = (LFTagTypes)lf, HFTagTypes = (HFTagTypes)hf };
        }

        public class GetTagTypesResult
        {
            public LFTagTypes LFTagTypes { get; internal set; }
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
            var result = await DoTXRXAsync(new byte[] { API_RF, 4 });
            var parser = new ResponseParser(result.ToList());

            var errorCode = parser.ParseByte(); // TODO: move to DoTXRXAsync()
            var lf = parser.ParseLong();
            var hf = parser.ParseLong();

            return new GetSupportedTagTypesResult() { LFTagTypes = (LFTagTypes)lf, HFTagTypes = (HFTagTypes)hf };
        }

        public class GetSupportedTagTypesResult
        {
            public LFTagTypes LFTagTypes { get; internal set; }
            public HFTagTypes HFTagTypes { get; internal set; }
        }

        #endregion

        #region API_ISO14443 / ISO14443 Transparent Transponder Access Functions

        public const int API_ISO14443 = 18;

        // TODO: SYSFUNC(API_ISO14443,  0, bool ISO14443A_GetATS(byte* ATS, int* ATSByteCnt, int MaxATSByteCnt))
        // TODO: SYSFUNC(API_ISO14443,  1, bool ISO14443B_GetATQB(byte* ATQB, int* ATQBByteCnt, int MaxATQBByteCnt))
        // TODO: SYSFUNC(API_ISO14443,  2, bool ISO14443_4_CheckPresence(void))
        // TODO: SYSFUNC(API_ISO14443,  3, bool ISO14443_4_TDX(byte* TXRX, int TXByteCnt, int* RXByteCnt, int MaxRXByteCnt))
        // TODO: SYSFUNC(API_ISO14443,  4, bool ISO14443A_GetATQA(byte* ATQA))
        // TODO: SYSFUNC(API_ISO14443,  5, bool ISO14443A_GetSAK(byte* SAK))
        // TODO: SYSFUNC(API_ISO14443,  6, bool ISO14443B_GetAnswerToATTRIB(byte* AnswerToATTRIB, int* AnswerToATTRIBByteCnt, int MaxAnswerToATTRIBByteCnt))
        // TODO: SYSFUNC(API_ISO14443,  7, bool ISO14443_3_TDX(byte* TXRX, int TXByteCnt, int* RXByteCnt, int MaxRXByteCnt, int Timeout))

        /// <summary>
        /// Use this function to search the RF field for ISO14443A transponders. The result is a list of the UID of the respective transponders.
        /// </summary>
        /// <param name="maxIDBytes"></param>
        /// <returns>A list containing the UIDs of all transponders.</returns>
        /// <remarks>SYSFUNC(API_ISO14443,  8, bool ISO14443A_SearchMultiTag(int* UIDCnt, int* UIDListByteCnt, byte* UIDList, int MaxUIDListByteCnt))</remarks>
        public async Task<List<byte[]>> ISO14443A_SearchMultiTagAsync(byte maxIDBytes = byte.MaxValue)
        {
            var result = await DoTXRXAsync(new byte[] { API_ISO14443, 8, maxIDBytes });
            var parser = new ResponseParser(result.ToList());
            var tagList = new List<byte[]>();

            var errorCode = parser.ParseByte(); // TODO: move to DoTXRXAsync()
            var found = parser.ParseBool();
            if (found)
            {
                var count = parser.ParseByte();
                for (int i = 0; i < count; i++)
                {
                    var tag = parser.ParseVarByteArray();
                    tagList.Add(tag);
                }
            }

            return tagList;
        }


        // TODO: SYSFUNC(API_ISO14443,  9, bool ISO14443A_SelectTag(const byte* UID, int UIDByteCnt))
        // TODO: SYSFUNC(API_ISO14443, 10, bool preISO14443B_GetATR(byte* ATR, int* ATRByteCnt, int MaxATRByteCnt))
        // TODO: SYSFUNC(API_ISO14443, 11, bool ISO14443A_Reselect(void))


        #endregion

        #endregion

        #region Common

        /// <summary>
        /// 
        /// </summary>
        public void Beep()
        {
            BeepAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="iterations"></param>
        /// <param name="length"></param>
        /// <param name="freq"></param>
        /// <param name="vol"></param>
        //public void Beep(ushort iterations, ushort length, ushort freq, byte vol)
        //{
        //    BeepAsync(iterations, length, freq, vol).GetAwaiter().GetResult();
        //}

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public async Task BeepAsync()
        {
            Result = await DoTXRXAsync(new byte[] { 0x04, 0x07, 0x64, 0x60, 0x09, 0x54, 0x01, 0xF4, 0x01 }); 
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="iterations"></param>
        /// <param name="length"></param>
        /// <param name="freq"></param>
        /// <param name="vol"></param>
        /// <returns></returns>
        //public async Task BeepAsync(ushort iterations, ushort length, ushort freq, byte vol)
        //{
        //    for (uint i = 0; i < iterations; i++)
        //    {
        //        Result = await DoTXRXAsync(
        //            ByteConverter.GetBytesFrom(BEEP_CMD +
        //            ByteConverter.GetStringFrom(vol) +
        //            ByteConverter.GetStringFrom(BitConverter.GetBytes(freq)) +
        //            ByteConverter.GetStringFrom(BitConverter.GetBytes(length)) +
        //            ByteConverter.GetStringFrom(BitConverter.GetBytes(length)))
        //            );
        //    }
        //}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="On"></param>
        public void GreenLED(bool On)
        {
            GreenLEDAsync(On).GetAwaiter().GetResult();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="On"></param>
        /// <returns></returns>
        public async Task GreenLEDAsync(bool On)
        {
            if (On)
            {
                await LedOnAsync(Leds.Green);
            }
            else
            {
                await LedOffAsync(Leds.Green);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="On"></param>
        public void RedLED(bool On)
        {
            RedLEDAsync(On).GetAwaiter().GetResult();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="On"></param>
        /// <returns></returns>
        public async Task RedLEDAsync(bool On)
        {
            if (On)
            {
                await LedOnAsync(Leds.Red);
            }
            else
            {
                await LedOffAsync(Leds.Red);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="hf"></param>
        /// <returns></returns>
        public ChipModel GetSingleChip(bool hf)
        {
            return GetSingleChipAsync(hf, false).Result;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="hf"></param>
        /// <returns></returns>
        public async Task<ChipModel> GetSingleChipAsync(bool hf)
        {
            return await GetSingleChipAsync(hf, false);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="hf"></param>
        /// <param name="legicOnly"></param>
        /// <returns></returns>
        public ChipModel GetSingleChip(bool hf, bool legicOnly)
        {
            return GetSingleChipAsync(hf, legicOnly).Result;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="hf"></param>
        /// <param name="legicOnly"></param>
        /// <returns></returns>
        public async Task<ChipModel> GetSingleChipAsync(bool hf, bool legicOnly)
        {
            try
            {
                var currentChip = new ChipModel();

                SAK = 0x00;
                ATS = new byte[1] {0x00};

                //if (hf)
                //{
                //    Result = await DoTXRXAsync(new byte[] { 0x05, 0x02, 0x00, 0x00, 0x00, 0x00, 0xF7, 0xFF, 0xFF, 0xFF }); //SetChipTypes (HF Only)
                //}
                //else
                //{
                //    Result = await DoTXRXAsync(new byte[] { 0x05, 0x02, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00 }); //Set Chip Types (LF Only)
                //}

                //if (legicOnly)
                //{
                //    Result = await DoTXRXAsync(new byte[] { 0x05, 0x02, 0x00, 0x00, 0x00, 0x00, 0x08, 0x00, 0x00, 0x00 }); //SetChipTypes (Legic Only)
                //}

                //Result = await DoTXRXAsync(new byte[] { 0x05, 0x02, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF }); //Set Chip Types (All LF + HF)

                var tag = await SearchTagAsync();
                if (tag.Found)
                {
                    currentChip.CardType = tag.ChipType;
                    currentChip.UID = tag.IDHexString;
                }
                else
                {
                    //GetChip UID if SearchTagAsync failed (SmartMX Elatec workaround)
                    var multiTags = await ISO14443A_SearchMultiTagAsync();
                    if (multiTags.Count > 0)
                    {
                        currentChip.UID = ByteConverter.GetStringFrom(multiTags[0]);
                    }
                }

                switch (currentChip.CardType)
                {
                    case ChipType.NOTAG:
                    case ChipType.MIFARE: //Start Mifare Identification Process

                        Result = await DoTXRXAsync(new byte[] { 0x12, 0x05 }); //GetSAK

                        if (Result?.Length == 3 && Result[2] != 0x00)
                        {
                            SAK = Result[2];
                            
                            // Start MIFARE identification
                            if ((SAK & 0x02) == 0x02)
                            {
                                currentChip.CardType = ChipType.Unspecified;
                            } // RFU bit set (RFU = Reserved for Future Use)

                            else
                            {
                                if ((SAK & 0x08) == 0x08)
                                {
                                    if ((SAK & 0x10) == 0x10)
                                    {
                                        if ((SAK & 0x01) == 0x01)
                                        {
                                            currentChip.CardType = ChipType.Mifare2K;
                                        } // // SAK b1 = 1 ? >> Mifare Classic 2K
                                        else
                                        {
                                            if ((SAK & 0x20) == 0x20)
                                            {
                                                currentChip.CardType = ChipType.SmartMX_Mifare_4K;
                                            } // SAK b6 = 1 ?  >> SmartMX Classic 4K
                                            else
                                            {
                                                //Get ATS - Switch to L4 ?
                                                var response = await DoTXRXAsync(
                                                    ByteConverter.GetBytesFrom(
                                                        ISO14443_3_TXD +
                                                        "04" +
                                                        ISO_CMD_RATS
                                                    ));

                                                if (response != null && response.Length <= 4)
                                                {
                                                    response = await DoTXRXAsync(ByteConverter.GetBytesFrom(ISO14443_GET_ATS + "20"));
                                                    ATS = new byte[response.Length - 2];
                                                }
                                                else if (response != null && response.Length >= 5)
                                                {
                                                    ATS = new byte[response.Length - 2];
                                                }
                                                else
                                                {
                                                    ATS = new byte[1] { 0x00 };
                                                }

                                                Buffer.BlockCopy(response, 2, ATS, 0, response.Length - 2);

                                                if (ATS.Length > 4)
                                                {
                                                    if (ByteConverter.SearchBytePattern(ATS, new byte[] { 0xC1, 0x05, 0x2F, 0x2F, 0x00, 0x35, 0xC7 }) != 0) //MF PlusS 4K in SL1
                                                    {
                                                        currentChip.CardType = ChipType.MifarePlus_SL1_4K;
                                                    }

                                                    else if (ByteConverter.SearchBytePattern(ATS, new byte[] { 0xC1, 0x05, 0x2F, 0x2F, 0x01, 0xBC, 0xD6 }) != 0) //MF PlusX 4K in SL1
                                                    {
                                                        currentChip.CardType = ChipType.MifarePlus_SL1_4K;
                                                    }

                                                } // Mifare Plus S / Plus X 4K

                                                else
                                                {
                                                    currentChip.CardType = ChipType.Mifare4K;
                                                } //Error on ATS = Mifare Classic 4K
                                                break;
                                            }
                                        }
                                    } // SAK b5 = 1 ?
                                    else
                                    {
                                        if ((SAK & 0x01) == 0x01)
                                        {
                                            currentChip.CardType = ChipType.MifareMini;
                                        } // // SAK b1 = 1 ? >> Mifare Mini
                                        else
                                        {
                                            if ((SAK & 0x20) == 0x20)
                                            {
                                                currentChip.CardType = ChipType.SmartMX_Mifare_1K;
                                            } // // SAK b6 = 1 ? >> SmartMX Classic 1K
                                            else
                                            {
                                                //Get ATS - Switch to L4 ?
                                                var response = await DoTXRXAsync(
                                                    ByteConverter.GetBytesFrom(
                                                        ISO14443_3_TXD +
                                                        "04" +
                                                        ISO_CMD_RATS
                                                    ));

                                                if (response != null && response.Length <= 4)
                                                {
                                                    response = await DoTXRXAsync(ByteConverter.GetBytesFrom(ISO14443_GET_ATS + "20"));
                                                    ATS = new byte[response.Length - 2];
                                                }
                                                else if (response != null && response.Length >= 5)
                                                {
                                                    ATS = new byte[response.Length - 2];
                                                }
                                                else
                                                {
                                                    ATS = new byte[1] { 0x00 };
                                                }
                                                Buffer.BlockCopy(response, 2, ATS, 0, response.Length - 2);

                                                if (ATS.Length > 4)
                                                {
                                                    if (ByteConverter.SearchBytePattern(ATS, new byte[] { 0xC1, 0x05, 0x2F, 0x2F, 0x00, 0x35, 0xC7 }) != 0) //MF PlusS 4K in SL1
                                                    {
                                                        currentChip.CardType = ChipType.MifarePlus_SL1_2K;
                                                    }

                                                    else if (ByteConverter.SearchBytePattern(ATS, new byte[] { 0xC1, 0x05, 0x2F, 0x2F, 0x01, 0xBC, 0xD6 }) != 0) //MF PlusX 4K in SL1
                                                    {
                                                        currentChip.CardType = ChipType.MifarePlus_SL1_2K;
                                                    }

                                                    else if (ByteConverter.SearchBytePattern(ATS, new byte[] { 0xC1, 0x05, 0x21, 0x30, 0x00, 0xF6, 0xD1 }) != 0) //MF PlusSE 1K
                                                    {
                                                        currentChip.CardType = ChipType.MifarePlus_SL0_1K;
                                                    }

                                                    else
                                                    {
                                                        currentChip.CardType = ChipType.MifarePlus_SL1_1K;
                                                    }
                                                } // Mifare Plus S / Plus X 4K

                                                else
                                                {
                                                    currentChip.CardType = ChipType.Mifare1K;
                                                } //Error on ATS = Mifare Classic 1K
                                            } // Mifare Plus; Historical Bytes ?
                                        }
                                    }
                                } // SAK b4 = 1 ?
                                else
                                {
                                    if ((SAK & 0x10) == 0x10)
                                    {
                                        if ((Result[2] & 0x01) == 0x01)
                                        {
                                            currentChip.CardType = ChipType.MifarePlus_SL2_4K;
                                        } // Mifare Plus 4K in SL2
                                        else
                                        {
                                            currentChip.CardType = ChipType.MifarePlus_SL2_2K;
                                        } // Mifare Plus 2K in SL2
                                    }
                                    else
                                    {
                                        if ((SAK & 0x01) == 0x01) // SAK b1 = 1 ?
                                        {

                                        } // Chip is "TagNPlay"
                                        else
                                        {
                                            if ((SAK & 0x20) == 0x20)
                                            {
                                                //Get ATS - Switch to L4 ?
                                                var response = await DoTXRXAsync(
                                                    ByteConverter.GetBytesFrom(
                                                        ISO14443_3_TXD +
                                                        "04" +
                                                        ISO_CMD_RATS
                                                    ));

                                                if (response != null && response.Length <= 4)
                                                {
                                                    response = await DoTXRXAsync(ByteConverter.GetBytesFrom(ISO14443_GET_ATS + "20"));
                                                    ATS = new byte[response.Length - 2];
                                                }
                                                else if (response != null && response.Length >= 5)
                                                {
                                                    ATS = new byte[response.Length - 2];
                                                }
                                                else
                                                {
                                                    ATS = new byte[1] { 0x00 };
                                                }

                                                Buffer.BlockCopy(response, 2, ATS, 0, response.Length - 2);
                                                Result = await DoTXRXAsync(new byte[] { 0x05, 0x00, 0x20 }); //GetChip
                                                var getVersion = await DoTXRXAsync(new byte[] { 0x12, 0x03, 0x01, 0x60, 0x20 }); //issue GetVersion

                                                if (getVersion?.Length > 4 && getVersion?[3] == 0xAF)
                                                {
                                                    L4VERSION = new byte[getVersion.Length - 2];
                                                    Buffer.BlockCopy(getVersion, 2, L4VERSION, 0, getVersion.Length - 2);

                                                    // Mifare Plus EV1/2 || DesFire || NTAG
                                                    if (getVersion?.Length > 1 && (getVersion?[5] == 0x01)) // DESFIRE
                                                    {
                                                        switch (getVersion?[7] & 0x0F) // Desfire(Sub)Type by lower Nibble of Major Version
                                                        {
                                                            case 0: //DESFIRE EV0
                                                                currentChip.CardType = ChipType.DESFire;

                                                                switch (getVersion?[9])
                                                                {
                                                                    case 0x10:
                                                                        currentChip.CardType = ChipType.DESFire_256; // DESFIRE 256B
                                                                        break;
                                                                    case 0x16:
                                                                        currentChip.CardType = ChipType.DESFire_2K; // DESFIRE 2K
                                                                        break;
                                                                    case 0x18:
                                                                        currentChip.CardType = ChipType.DESFire_4K; // 4K
                                                                        break;
                                                                    default:
                                                                        break;
                                                                } // Size ?
                                                                break;

                                                            case 1: // DESFIRE EV1
                                                                currentChip.CardType = ChipType.DESFireEV1;

                                                                switch (getVersion?[9])
                                                                {
                                                                    case 0x10:
                                                                        currentChip.CardType = ChipType.DESFireEV1_256; //DESFIRE 256B
                                                                        break;
                                                                    case 0x16:
                                                                        currentChip.CardType = ChipType.DESFireEV1_2K; // DESFIRE 2K
                                                                        break;
                                                                    case 0x18:
                                                                        currentChip.CardType = ChipType.DESFireEV1_4K; // 4K
                                                                        break;
                                                                    case 0x1A:
                                                                        currentChip.CardType = ChipType.DESFireEV1_8K; // 8K
                                                                        break;
                                                                    default:
                                                                        break;
                                                                } // Size ?
                                                                break;

                                                            case 2: // EV2
                                                                currentChip.CardType = ChipType.DESFireEV2;

                                                                switch (getVersion?[9])
                                                                {
                                                                    case 0x16:
                                                                        currentChip.CardType = ChipType.DESFireEV2_2K; // DESFIRE 2K
                                                                        break;
                                                                    case 0x18:
                                                                        currentChip.CardType = ChipType.DESFireEV2_4K; // 4K
                                                                        break;
                                                                    case 0x1A:
                                                                        currentChip.CardType = ChipType.DESFireEV2_8K; // 8K
                                                                        break;
                                                                    case 0x1C:
                                                                        currentChip.CardType = ChipType.DESFireEV2_16K; // 16K
                                                                        break;
                                                                    case 0x1E:
                                                                        currentChip.CardType = ChipType.DESFireEV2_32K; // 32K
                                                                        break;
                                                                    default:
                                                                        break;
                                                                } // SIZE ?
                                                                break;

                                                            case 3: // EV3
                                                                currentChip.CardType = ChipType.DESFireEV3;

                                                                switch (getVersion?[9])
                                                                {
                                                                    case 0x16:
                                                                        currentChip.CardType = ChipType.DESFireEV3_2K; // DESFIRE 2K
                                                                        break;
                                                                    case 0x18:
                                                                        currentChip.CardType = ChipType.DESFireEV3_4K; // 4K
                                                                        break;
                                                                    case 0x1A:
                                                                        currentChip.CardType = ChipType.DESFireEV3_8K; // 8K
                                                                        break;
                                                                    case 0x1C:
                                                                        currentChip.CardType = ChipType.DESFireEV3_16K; // 16K
                                                                        break;
                                                                    case 0x1E:
                                                                        currentChip.CardType = ChipType.DESFireEV3_32K; // 32K
                                                                        break;
                                                                    default:
                                                                        break;
                                                                } // SIZE ?
                                                                break;

                                                            default:
                                                                currentChip.CardType = ChipType.Unspecified;

                                                                break;
                                                        }
                                                    }
                                                    else if (getVersion?.Length > 1 && getVersion?[5] == 0x81) // Emulated e.g. SmartMX
                                                    {
                                                        switch (getVersion?[7] & 0x0F) // Desfire(Sub)Type by lower Nibble of Major Version
                                                        {
                                                            case 0: //DESFIRE EV0
                                                                currentChip.CardType = ChipType.SmartMX_DESFire_Generic;

                                                                switch (getVersion?[9])
                                                                {
                                                                    case 0x10:
                                                                        currentChip.CardType = ChipType.SmartMX_DESFire_Generic; // DESFIRE 256B
                                                                        break;
                                                                    case 0x16:
                                                                        currentChip.CardType = ChipType.SmartMX_DESFire_2K; // DESFIRE 2K
                                                                        break;
                                                                    case 0x18:
                                                                        currentChip.CardType = ChipType.SmartMX_DESFire_4K; // 4K
                                                                        break;
                                                                    default:
                                                                        break;
                                                                } // Size ?
                                                                break;

                                                            case 1: // DESFIRE EV1
                                                                currentChip.CardType = ChipType.SmartMX_DESFire_Generic;

                                                                switch (getVersion?[9])
                                                                {
                                                                    case 0x10:
                                                                        currentChip.CardType = ChipType.SmartMX_DESFire_Generic; //DESFIRE 256B
                                                                        break;
                                                                    case 0x16:
                                                                        currentChip.CardType = ChipType.SmartMX_DESFire_2K; // DESFIRE 2K
                                                                        break;
                                                                    case 0x18:
                                                                        currentChip.CardType = ChipType.SmartMX_DESFire_4K; // 4K
                                                                        break;
                                                                    case 0x1A:
                                                                        currentChip.CardType = ChipType.SmartMX_DESFire_8K; // 8K
                                                                        break;
                                                                    default:
                                                                        break;
                                                                } // Size ?
                                                                break;

                                                            case 2: // EV2
                                                                currentChip.CardType = ChipType.SmartMX_DESFire_Generic;

                                                                switch (getVersion?[9])
                                                                {
                                                                    case 0x16:
                                                                        currentChip.CardType = ChipType.SmartMX_DESFire_2K; // DESFIRE 2K
                                                                        break;
                                                                    case 0x18:
                                                                        currentChip.CardType = ChipType.SmartMX_DESFire_4K; // 4K
                                                                        break;
                                                                    case 0x1A:
                                                                        currentChip.CardType = ChipType.SmartMX_DESFire_8K; // 8K
                                                                        break;
                                                                    case 0x1C:
                                                                        currentChip.CardType = ChipType.SmartMX_DESFire_16K; // 16K
                                                                        break;
                                                                    case 0x1E:
                                                                        currentChip.CardType = ChipType.SmartMX_DESFire_32K; // 32K
                                                                        break;
                                                                    default:
                                                                        break;
                                                                } // SIZE ?
                                                                break;

                                                            case 3: // EV3
                                                                currentChip.CardType = ChipType.SmartMX_DESFire_Generic;

                                                                switch (getVersion?[9])
                                                                {
                                                                    case 0x16:
                                                                        currentChip.CardType = ChipType.SmartMX_DESFire_2K; // DESFIRE 2K
                                                                        break;
                                                                    case 0x18:
                                                                        currentChip.CardType = ChipType.SmartMX_DESFire_4K; // 4K
                                                                        break;
                                                                    case 0x1A:
                                                                        currentChip.CardType = ChipType.SmartMX_DESFire_8K; // 8K
                                                                        break;
                                                                    case 0x1C:
                                                                        currentChip.CardType = ChipType.SmartMX_DESFire_16K; // 16K
                                                                        break;
                                                                    case 0x1E:
                                                                        currentChip.CardType = ChipType.SmartMX_DESFire_32K; // 32K
                                                                        break;
                                                                    default:
                                                                        break;
                                                                } // SIZE ?
                                                                break;

                                                            default:
                                                                currentChip.CardType = ChipType.Unspecified;

                                                                break;
                                                        }
                                                    }
                                                } // Get Version L4 Failed

                                                else
                                                {
                                                    if (ATS.Length > 4)
                                                    {
                                                        if (ByteConverter.SearchBytePattern(ATS, new byte[] { 0xC1, 0x05, 0x2F, 0x2F, 0x00, 0x35, 0xC7 }) != 0) //MF PlusS 4K in SL1
                                                        {
                                                            currentChip.CardType = ChipType.MifarePlus_SL3_4K;
                                                        }

                                                        else if (ByteConverter.SearchBytePattern(ATS, new byte[] { 0xC1, 0x05, 0x2F, 0x2F, 0x01, 0xBC, 0xD6 }) != 0) //MF PlusX 4K in SL1
                                                        {
                                                            currentChip.CardType = ChipType.MifarePlus_SL3_4K;
                                                        }
                                                        else
                                                        {
                                                            currentChip.CardType = ChipType.Unspecified;
                                                        }
                                                    }
                                                    else
                                                    {
                                                        currentChip.CardType = ChipType.SmartMX_Mifare_4K;
                                                    }
                                                } // Mifare Plus
                                            } // SAK b6 = 1 ?
                                            else
                                            {
                                                currentChip.CardType = ChipType.MifareUltralight;
                                            } // Ultralight || NTAG
                                        }
                                    } // SAK b5 = 1 ?
                                } // SAK b5 = 1 ?
                            }


                        }
                        break;

                    default:

                        break;
                } // Get Tag

                genericChipModel = 
                    new ChipModel(
                        currentChip.UID, 
                        currentChip.CardType, 
                        ByteConverter.GetStringFrom(SAK), 
                        ByteConverter.GetStringFrom(ATS), 
                        ByteConverter.GetStringFrom(L4VERSION)
                    );
            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(e);
            }

            if (genericChipModel?.CardType != ChipType.NOTAG)
            {

            }
            else
            {

            }

            return genericChipModel;
        }
        private ChipModel genericChipModel;

        #endregion

        #region Reader Communication

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public bool Connect()
        {
            return ConnectAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public async Task<bool> ConnectAsync()
        {
            return (await DoTXRXAsync(new byte[] { 0 }))?[0] == 0x01; 
        }

        #region Tools for Simple Protocol

        private byte[] GetByteArrayfromPRS(string PRSString)
        {
            // Is string length = 0?
            if (PRSString.Length < 1)
            {
                // Yes, return null
                return null;
            }
            // Initialize byte array, half string length
            byte[] buffer = new byte[PRSString.Length / 2];
            // Get byte array from PRS string
            for (int i = 0; i < (buffer.Length); i++)
            {
                // Convert PRS Chars to byte array buffer
                buffer[i] = byte.Parse(PRSString.Substring((i * 2), 2), NumberStyles.HexNumber);
            }
            // Return byte array
            return buffer;
        }// End of PRStoByteArray
        private string GetPRSfromByteArray(byte[] PRSStream)
        {
            // Is length of PRS stream = 0
            if (PRSStream.Length < 1)
            {
                // Yes, return null
                return null;
            }
            // Iinitialize PRS buffer
            string buffer = null;
            // Convert byte to PRS string
            for (int i = 0; i < PRSStream.Length; i++)
            {
                // convert byte to characters
                buffer += PRSStream[i].ToString("X2");
            }
            // return buffer
            return buffer;
        }// End of GetPRSfromByteArray
        #endregion

        /// <summary>
        /// 
        /// </summary>
        /// <param name="CMD"></param>
        /// <returns></returns>
        private byte[] DoTXRX(byte[] CMD)
        {
            return DoTXRXAsync(CMD).GetAwaiter().GetResult();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="CMD"></param>
        /// <returns></returns>
        private async Task<byte[]> DoTXRXAsync(byte[] CMD)
        {
            try
            {
                using (SerialPort twnPort = new SerialPort())
                {
                    // Initialize serial port
                    twnPort.PortName = PortName; //GetTWNPortName(portNumber);
                    twnPort.BaudRate = 9600;
                    twnPort.DataBits = 8;
                    twnPort.StopBits = System.IO.Ports.StopBits.One;
                    twnPort.Parity = System.IO.Ports.Parity.None;
                    // NFC functions are known to take less than 2 second to execute.
                    twnPort.ReadTimeout = 2000;
                    twnPort.WriteTimeout = 2000;
                    twnPort.NewLine = "\r";
                    twnPort.ErrorReceived += TXRXErr;

                    Func<Task> toDo = async () =>
                    {
                        try
                        {
                            // Open TWN4 com port
                            twnPort.Open();
                            PortAccessDenied = false;
                            return;
                        }
                        catch (Exception e)
                        {
                            //Port Busy? Try Again
                            if (e is UnauthorizedAccessException)
                            {
                                for (int i = 0; i <= 2; i++)
                                {
                                    await Task.Delay(1000).ConfigureAwait(false);
                                    // Open TWN4 com port
                                    try
                                    {
                                        twnPort.Open();
                                        PortAccessDenied = false;
                                        break;
                                    }
                                    // Force Open
                                    catch (Exception e2)
                                    {
                                        PortAccessDenied = true;
                                        LogWriter.CreateLogEntry(e2);
                                    };
                                }  
                            }
                        }
                    };
                    await toDo().ConfigureAwait(false);

                    IsConnected = twnPort.IsOpen;

                    if (CMD?[0] != 0 && IsConnected)
                    {
                        // Discard com port inbuffer
                        twnPort.DiscardInBuffer();
                        // Generate simple protocol string and send command
                        twnPort.WriteLine(GetPRSfromByteArray(CMD));
                        // Read simple protocoll string and convert to byte array
                        var ret = GetByteArrayfromPRS(twnPort.ReadLine());

                        twnPort.Close();

                        return ret;
                    }

                    else
                    {
                        return new byte[] { 0x01 };
                    }
                }
            }

            catch
            {
                this.Dispose();
                return new byte[] { 0x00 }; ;
            }

        }// End of DoTXRXAsync

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TXRXErr(object sender, EventArgs e)
        {
            Debug.WriteLine(e.ToString());
            return;
        }

        #region Tools for connect TWN4


        #endregion

        #endregion

        #region Public Properties

        public string PortName { get; private set; }

        public byte[] Result
        {
            get; set;
        }

        public byte KeySettings
        {
            get; set;
        }

        public byte KeyType
        {
            get; set;
        }

        public byte NumberOfKeys
        {
            get; set;
        }

        public bool IsConnected
        {
            get; private set;
        }

        public byte[] ATS
        {
            get; set;
        }

        public byte SAK
        {
            get; private set;
        }

        public byte[] L4VERSION
        {
            get; private set;
        }

        public byte[] ChipIdentifier
        {
            get; private set;
        }
        #endregion

        #region ClassicCommands

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <param name="keyType"></param>
        /// <param name="sectorNumber"></param>
        /// <returns></returns>
        public bool MifareClassicLogin(string key, byte keyType, byte sectorNumber)
        {
            return MifareClassicLoginAsync(key, keyType, sectorNumber).GetAwaiter().GetResult();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key">The Key. Format: "FFFFFFFFFFFF"</param>
        /// <param name="keyType">The KeyType. Keytype: KEY_A = 0, KEY_B = 1</param>
        /// <param name="sectorNumber"></param>
        /// <returns>Success = true, false otherwise</returns>
        public async Task<bool> MifareClassicLoginAsync(string key, byte keyType, byte sectorNumber)
        {
            try
            {
                Result = await DoTXRXAsync(new byte[] { 0x05, 0x00, 0x20 }); //GetChip
                if (Result.Length > 2 && Result[1] == 0x01 ? true : false)
                {
                    var cmd = ByteConverter.GetBytesFrom(MIFARE_CLASSIC_LOGIN + key + keyType.ToString("X2") + sectorNumber.ToString("X2"));
                    Result = await DoTXRXAsync(cmd);
                }
                else
                {
                    return RESULT_FAILED;
                }
            }
            catch
            {
                throw new ArgumentException();
            }

            return Result[1] == 0x01 ? RESULT_SUCCESS : RESULT_FAILED;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="blockNumber"></param>
        /// <returns></returns>
        public byte[] MifareClassicReadBlock(byte blockNumber)
        {
            return MifareClassicReadBlockAsync(blockNumber).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Read Data from Classic Chip
        /// </summary>
        /// <param name="blockNumber">DataBlock Number</param>
        /// <returns>DATA</returns>
        public async Task<byte[]> MifareClassicReadBlockAsync(byte blockNumber)
        {
            Result = await DoTXRXAsync(ByteConverter.GetBytesFrom(MIFARE_CLASSIC_READBLOCK + blockNumber.ToString("X2")));
            if(Result.Length > 2)
            {
                return ByteConverter.Trim(Result, 2, Result.Length - 2);
            }
            else
            {
                return new byte[] { 0x00 };
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="data"></param>
        /// <param name="blockNumber"></param>
        /// <returns></returns>
        public bool MifareClassicWriteBlock(byte[] data, byte blockNumber)
        {
            return MifareClassicWriteBlockAsync(data, blockNumber).GetAwaiter().GetResult();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="data"></param>
        /// <param name="blockNumber"></param>
        /// <returns></returns>
        public async Task<bool> MifareClassicWriteBlockAsync(byte[] data, byte blockNumber)
        {
            if(data.Length == 16)
            {
                Result = await DoTXRXAsync(ByteConverter.GetBytesFrom(MIFARE_CLASSIC_WRITEBLOCK + blockNumber.ToString("X2") + ByteConverter.GetStringFrom(data)));

                if (Result.Length == 2)
                {
                    return Result[1] == 0x01 ? RESULT_SUCCESS : RESULT_FAILED;
                }
                else
                {
                    return RESULT_FAILED;
                }
            }

            else
            {
                return RESULT_FAILED;
            }
        }

        #endregion

        #region DesFireCommands

        public UInt32[] GetDesfireAppIDs()
        {
            return GetDesfireAppIDsAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Retrieve the Available Application IDs after selecing PICC (App 0), Authentication is needed - depending on the security config
        /// </summary>
        /// <returns>a uint32[] of the available appids with 4bytes each, null if no apps are available or on error</returns>
        public async Task<UInt32[]> GetDesfireAppIDsAsync()
        {
            try
            {
                Result = await DoTXRXAsync(ByteConverter.GetBytesFrom(MIFARE_DESFIRE_GETAPPIDS + "00" + "1C"));

                UInt32[] appids = new UInt32[1];

                if (Result.Length > 2)
                {
                    appids = new UInt32[Result[2]];

                    for (int i = 0; i < Result[2]; i++)
                    {
                        appids[i] = 0x00000000;

                        for (int j = 6 + (i * 4); j > 2 + (i * 4); j--)
                        {
                            appids[i] = appids[i] << 8;
                            appids[i] |= (byte)(Result[j]);
                        }
                    }
                    return appids;
                }
                else
                {
                    return null;
                }
            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(e);
                return null;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="appID"></param>
        /// <returns></returns>
        public bool DesfireSelectApplication(uint appID)
        {
            return DesfireSelectApplicationAsync(appID).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Select a desfire Application
        /// </summary>
        /// <param name="appID">The Application ID to select</param>
        /// <returns>true if Application could be selected, false otherwise</returns>
        public async Task<bool> DesfireSelectApplicationAsync(uint appID)
        {
            try
            {
                Result = await DoTXRXAsync(new byte[] { 0x05, 0x00, 0x20 }); //GetChip
                Result = await DoTXRXAsync(ByteConverter.GetBytesFrom(MIFARE_DESFIRE_SELECTAPP + "00" + ByteConverter.GetStringFrom(BitConverter.GetBytes(appID))));

                if (Result?.Length == 2)
                {
                    return Result[1] == 0x01 ? true : false;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(e);
                return false;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public UInt32? GetDesfireFreeMemory()
        {
            return GetDesfireFreeMemoryAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Get the free Memory of a desfire. 
        /// </summary>
        /// <returns>a uint32 of the available memory if supported, null if freemem could not be read out</returns>
        public async Task<UInt32?> GetDesfireFreeMemoryAsync()
        {
            try
            {
                Result = await DoTXRXAsync(ByteConverter.GetBytesFrom(MIFARE_DESFIRE_GETFREEMEMORY + "00"));

                if (Result?.Length > 2)
                {
                    UInt32 freemem = 0x00000000;

                    for (uint i = 3; i >= 2; i--)
                    {
                        freemem = (freemem << 8);
                        freemem |= (byte)(Result[i]);
                    }
                    return freemem;
                }

                else
                {
                    return null;
                }
            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(e);
                return null;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <param name="keyNo"></param>
        /// <param name="keyType"></param>
        /// <param name="authMode"></param>
        /// <returns></returns>
        public bool DesfireAuthenticate(string key, byte keyNo, byte keyType, byte authMode)
        {
            return DesfireAuthenticateAsync(key, keyNo, keyType, authMode).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Authenticate to a previously selected desfire application
        /// </summary>
        /// <param name="key">string: a 16 bytes key e.g. 00000000000000000000000000000000</param>
        /// <param name="keyNo">byte: the keyNo to use</param>
        /// <param name="keyType">byte: 0 = 3DES, 1 = 3K3DES, 2 = AES</param>
        /// <param name="authMode">byte: 1 = EV1 Mode, 0 = EV0 Mode</param>
        /// <returns>true if Authentication was successful, false otherwise</returns>
        public async Task<bool> DesfireAuthenticateAsync(string key, byte keyNo, byte keyType, byte authMode)
        {
            try
            {
                Result = await DoTXRXAsync(ByteConverter.GetBytesFrom(MIFARE_DESFIRE_AUTH + "00" //CryptoEnv
                                                               + keyNo.ToString("X2")
                                                               + "10" // keyLength ?
                                                               + key
                                                               + keyType.ToString("X2")
                                                               + authMode.ToString("X2"))); // EV1-ISO Mode = 1, compatible = 0

                if (Result?.Length == 2)
                {
                    return Result[1] == 0x01 ? true : false;
                }

                else
                {
                    return false;
                }
            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(e);
                return false;
            }

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="_keySettingsTarget"></param>
        /// <param name="_keyTypeTargetApplication"></param>
        /// <param name="_maxNbKeys"></param>
        /// <param name="_appID"></param>
        /// <returns></returns>
        public bool DesfireCreateApplication(DESFireKeySettings _keySettingsTarget, DESFireKeyType _keyTypeTargetApplication, int _maxNbKeys, int _appID)
        {
            return DesfireCreateApplicationAsync(_keySettingsTarget, _keyTypeTargetApplication, _maxNbKeys, _appID).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Creates a new Application
        /// </summary>
        /// <param name="_keySettingsTarget">byte: KS_CHANGE_KEY_WITH_MK = 0, KS_ALLOW_CHANGE_MK = 1, KS_FREE_LISTING_WITHOUT_MK = 2, KS_FREE_CREATE_DELETE_WITHOUT_MK = 4, KS_CONFIGURATION_CHANGEABLE = 8, KS_DEFAULT = 11, KS_CHANGE_KEY_WITH_TARGETED_KEYNO = 224, KS_CHANGE_KEY_FROZEN = 240</param>
        /// <param name="_keyTypeTargetApplication">byte: 0 = 3DES, 1 = 3K3DES, 2 = AES</param>
        /// <param name="_maxNbKeys">int max. number of keys</param>
        /// <param name="_appID">int application id</param>
        /// <returns>true if the Operation was successful, false otherwise</returns>
        public async Task<bool> DesfireCreateApplicationAsync(DESFireKeySettings _keySettingsTarget, DESFireKeyType _keyTypeTargetApplication, int _maxNbKeys, int _appID)
        {
            try
            {
                Result = await DoTXRXAsync(ByteConverter.GetBytesFrom(MIFARE_DESFIRE_CREATEAPP + "00" //CryptoEnv
                                                                   + ByteConverter.GetStringFrom(ByteConverter.Reverse(ByteConverter.GetBytesFrom(_appID.ToString("X8"))))
                                                                   + ((byte)_keySettingsTarget).ToString("X2")
                                                                   + _maxNbKeys.ToString("D2")
                                                                   + "000000"
                                                                   + ((int)_keyTypeTargetApplication).ToString("D2")
                                                                   + "000000"));

                if (Result?.Length == 2)
                {
                    return Result[1] == 0x01 ? true : false;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(e);
                return false;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="appID"></param>
        /// <returns></returns>
        public bool DesfireDeleteApplication(uint appID)
        {
            return DesfireDeleteApplicationAsync(appID).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Select a desfire Application
        /// </summary>
        /// <param name="appID">The Application ID to delete</param>
        /// <returns>true if Application could be deleted, false otherwise</returns>
        public async Task<bool> DesfireDeleteApplicationAsync(uint appID)
        {
            try
            {
                Result = await DoTXRXAsync(ByteConverter.GetBytesFrom(MIFARE_DESFIRE_DELETEAPP + "00" + ByteConverter.GetStringFrom(BitConverter.GetBytes(appID))));

                if (Result?.Length == 2)
                {
                    return Result[1] == 0x01 ? true : false;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(e);
                return false;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public byte[] GetDesfireFileIDs()
        {
            return GetDesfireFileIDsAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Retrieve the Available File IDs after selecing App and Authenticating
        /// </summary>
        /// <returns>byte[] array of available file ids. null on error</returns>
        public async Task<byte[]> GetDesfireFileIDsAsync()
        {
            try
            {
                Result = await DoTXRXAsync(ByteConverter.GetBytesFrom(MIFARE_DESFIRE_GETFILEIDS + "00" + "FF"));

                var fids = new byte[1];

                if (Result?.Length > 2)
                {
                    fids = new byte[Result[2]];
                    for (var i = 3; i < Result.Length; i++)
                    {
                        fids[i - 3] = (byte)Result[i];
                    }
                    return fids;
                }
                else
                {
                    return null;
                }
            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(e);
                return null;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public bool GetDesFireKeySettings()
        {
            return GetDesFireKeySettingsAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Get the KeySettings (Properties: KeySettings, NumberOfKeys, KeyType) of the selected Application. Authentication is needed - depending on the security config
        /// </summary>
        /// <returns>true if the Operation was successful, false otherwise</returns>
        public async Task<bool> GetDesFireKeySettingsAsync()
        {
            try
            {
                Result = await DoTXRXAsync(ByteConverter.GetBytesFrom(MIFARE_DESFIRE_GETKEYSETTINGS + "00"));

                if (Result?.Length >= 3 && (Result[1] == 0x01 ? true : false))
                {
                    KeySettings = Result[2];
                    NumberOfKeys = Result[3];
                    KeyType = Result[7];

                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(e);
                return false;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileNo"></param>
        /// <returns></returns>
        public byte[] GetDesFireFileSettings(byte fileNo)
        {
            return GetDesFireFileSettingsAsync(fileNo).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Get the filesettings of a fileid
        /// </summary>
        /// <param name="fileNo">id of the desired file</param>
        /// <returns>byte[] array of the file settings. null on error. content: FileType = fileSettings[2]; comSett = fileSettings[3]; accessRights[0] = fileSettings[4]; accessRights[1] = fileSettings[5];</returns>
        public async Task<byte[]> GetDesFireFileSettingsAsync(byte fileNo)
        {
            try
            {
                Result = await DoTXRXAsync(ByteConverter.GetBytesFrom(MIFARE_DESFIRE_GETFILESETTINGS + "00" + ByteConverter.GetStringFrom(fileNo)));

                if (Result?.Length >= 3 && (Result[1] == 0x01 ? true : false))
                {
                    return Result;
                }
                else
                {
                    return null;
                }
            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(e);
                return null;
            }

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileNo"></param>
        /// <param name="fileType"></param>
        /// <param name="comSet"></param>
        /// <param name="accessRights"></param>
        /// <param name="fileSize"></param>
        /// <returns></returns>
        public bool DesfireCreateFile(byte fileNo, byte fileType, byte comSet, UInt16 accessRights, UInt32 fileSize)
        {
            return DesfireCreateFileAsync(fileNo, fileType, comSet, accessRights, fileSize).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Read out Data on a Desfire
        /// </summary>
        /// <param name="fileNo">byte: filenumber: 0x00 - 0x14</param>
        /// <param name="length">int: filesize to read</param>
        /// <param name="comSet">byte: 0 = Plain, 1 = CMAC, 2 = Encrypted</param>
        /// <returns>byte[] of data, null on error</returns>
        public async Task<bool> DesfireCreateFileAsync(byte fileNo, byte fileType, byte comSet, UInt16 accessRights, UInt32 fileSize)
        {
            try
            {
                Result = await DoTXRXAsync(ByteConverter.GetBytesFrom(MIFARE_DESFIRE_CREATE_STDDATAFILE + "00" //CryptoEnv
                                                                   + fileNo.ToString("X2")
                                                                   + fileType.ToString("X2")
                                                                   + comSet.ToString("X2")
                                                                   + ByteConverter.GetStringFrom(ByteConverter.GetBytesFrom(accessRights.ToString("X4")))
                                                                   + ByteConverter.GetStringFrom(ByteConverter.Reverse(ByteConverter.GetBytesFrom(fileSize.ToString("X8"))))
                                                                   + "000000000000000000000000"));



                return Result?.Length >= 3 && (Result[1] == 0x01 ? true : false);

            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(e);
                return false;
            }

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileNo"></param>
        /// <param name="length"></param>
        /// <param name="comSet"></param>
        /// <returns></returns>
        public byte[] DesfireReadData(byte fileNo, int length, byte comSet)
        {
            return DesfireReadDataAsync(fileNo, length, comSet).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Read out Data on a Desfire
        /// </summary>
        /// <param name="fileNo">byte: filenumber: 0x00 - 0x14</param>
        /// <param name="length">int: filesize to read</param>
        /// <param name="comSet">byte: 0 = Plain, 1 = CMAC, 2 = Encrypted</param>
        /// <returns>byte[] of data, null on error</returns>
        public async Task<byte[]> DesfireReadDataAsync(byte fileNo, int length, byte comSet)
        {
            try
            {
                var data = new byte[length];
                var iterations = (length / 0xFF) == 0 ? 1 : (length / 0xFF);
                var dataLengthToRead = length;

                for (var i = 0; i < iterations; i++)
                {
                    Result = await DoTXRXAsync(ByteConverter.GetBytesFrom(MIFARE_DESFIRE_READDATA + "00" //CryptoEnv
                                                                   + fileNo.ToString("X2")
                                                                   + ByteConverter.GetStringFrom(ByteConverter.Reverse(ByteConverter.GetBytesFrom((i*0xFF).ToString("X4"))))
                                                                   + (dataLengthToRead >= 0xFF ? 0xFF : length).ToString("X2")
                                                                   + (comSet).ToString("X2")));
                    
                    Array.Copy(ByteConverter.Trim(Result, 3, Result[2]),0, data,(i*0xFF), (dataLengthToRead >= 0xFF ? 0xFF : length));
                }
                

                if (Result?.Length >= 3 && (Result[1] == 0x01 ? true : false))
                {
                    return data;
                }
                else
                {
                    return null;
                }
            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(e);
                return null;
            }

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="keySettings"></param>
        /// <param name="numberOfKeys"></param>
        /// <param name="keyType"></param>
        /// <returns></returns>
        public bool DesfireChangeKeySettings(byte keySettings, UInt32 numberOfKeys, UInt32 keyType)
        {
            return DesfireChangeKeySettingsAsync(keySettings, numberOfKeys, keyType).GetAwaiter().GetResult();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="keySettings"></param>
        /// <param name="numberOfKeys"></param>
        /// <param name="keyType"></param>
        /// <returns></returns>
        public async Task<bool> DesfireChangeKeySettingsAsync(byte keySettings, UInt32 numberOfKeys, UInt32 keyType)
        {
            try
            {
                Result = await DoTXRXAsync(ByteConverter.GetBytesFrom(MIFARE_DESFIRE_CHANGEKEYSETTINGS + "00" 
                    + keySettings.ToString("X2") 
                    + numberOfKeys.ToString("X8") 
                    + keyType.ToString("X8") ));

                return Result?.Length == 2 && (Result[1] == 0x01 ? true : false);
            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(e);
                return false;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="oldKey"></param>
        /// <param name="newKey"></param>
        /// <param name="keyVersion"></param>
        /// <param name="accessRights"></param>
        /// <param name="keyNo"></param>
        /// <param name="numberOfKeys"></param>
        /// <param name="keyType"></param>
        /// <returns></returns>
        public bool DesfireChangeKey(string oldKey, string newKey, byte keyVersion, byte accessRights, byte keyNo, UInt32 numberOfKeys, UInt32 keyType)
        {
            return DesfireChangeKeyAsync(oldKey, newKey, keyVersion, accessRights, keyNo, numberOfKeys, keyType).GetAwaiter().GetResult();
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
        public async Task<bool> DesfireChangeKeyAsync(string oldKey, string newKey, byte keyVersion, byte accessRights, byte keyNo, UInt32 numberOfKeys, UInt32 keyType)
        {
            try
            {
                Result = await DoTXRXAsync(ByteConverter.GetBytesFrom(MIFARE_DESFIRE_CHANGEKEY + "00" 
                    + keyNo.ToString("X2") 
                    + "10" + oldKey 
                    + "10" + newKey 
                    + keyVersion.ToString("X2") 
                    + accessRights.ToString("X2")
                    + ByteConverter.GetStringFrom(ByteConverter.Reverse(ByteConverter.GetBytesFrom(numberOfKeys.ToString("X8"))))
                    + ByteConverter.GetStringFrom(ByteConverter.Reverse(ByteConverter.GetBytesFrom(keyType.ToString("X8"))))));

                return Result?.Length == 2 && (Result[1] == 0x01 ? true : false);
            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(e);
                return false;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileNo"></param>
        /// <returns></returns>
        public bool DesfireDeleteFile(byte fileNo)
        {
            return DesfireDeleteFileAsync(fileNo).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Delete a File
        /// </summary>
        /// <param name="fileNo">byte: Filenumber to delete</param>
        /// <returns>true if the Operation was successful, false otherwise</returns>
        public async Task<bool> DesfireDeleteFileAsync(byte fileNo)
        {
            try
            {
                Result = await DoTXRXAsync(ByteConverter.GetBytesFrom(MIFARE_DESFIRE_DELETEFILE + "00" + fileNo.ToString("X2")));

                if (Result?.Length == 2)
                {
                    return Result[1] == 0x01 ? true : false;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(e);
                return false;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public bool DesfireFormatTag()
        {
            return DesfireFormatTagAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Format a Chip
        /// </summary>
        /// <returns>true if the Operation was successful, false otherwise</returns>
        public async Task<bool> DesfireFormatTagAsync()
        {
            try
            {
                Result = await DoTXRXAsync(ByteConverter.GetBytesFrom(MIFARE_DESFIRE_FORMATTAG + "00" ));

                if (Result?.Length == 2)
                {
                    return Result[1] == 0x01 ? true : false;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(e);
                return false;
            }
        }
        #endregion

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    IsConnected = false;
                    
                    // Dispose any managed objects
                    // ...
                }

                Thread.Sleep(20);
                _disposed = true;
            }
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            //GreenLED(false);
            Dispose(true);
        }

    }


}
