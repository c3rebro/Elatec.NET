using Log4CSharp;

using System;
using System.IO.Ports;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using Elatec.NET.Cards;
using Elatec.NET.Cards.Mifare;
using Elatec.NET.Helpers.ByteArrayHelper.Extensions;

/*
* Elatec.NET is a C# library to easily Talk to Elatec's TWN4 Devices
* 
* Some TWN4 Specific "Special" information:
* 
* Getting the ATS on different Readers works differently.
* 
*/

namespace Elatec.NET
{
    /// <summary>
    ///     This class offers communications methods with a TWN4 Reader device, e.g. TWN4 MultiTech 2.
    ///     The methods are on different abstraction levels:<br/>
    ///     <list type="number">
    ///     <item>High level<br/>
    ///     - e.g. <see cref="GetSingleChipAsync(bool)"/> providing detailed information</item>
    ///     <item>TWN4 Simple Protocol APIs<br/>
    ///     - e.g. <see cref="GpioSetBitsAsync(Gpios)"/></item>
    ///     <item>Low level TWN4 Simple Protocol APIs<br/>
    ///     - <see cref="CallFunctionAsync(byte[])"/> which takes a raw byte[] as input and returns a parser. Errors are thrown as TwnException.<br />
    ///     - See <see cref="CallFunctionParserAsync(byte[])"/> and <see cref="CallFunctionRawAsync(byte[])"/> for variants without error handling and parser.</item>
    ///     </list>
    /// </summary>
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

        public const int API_SYS = 0;

        // Not supported: SYSFUNC(API_SYS, 0, bool SysCall(TEnvSysCall* Env))

        /// <summary>
        /// This function is performing a reset of the firmware, which also includes a restart of the currently running App.
        /// </summary>
        /// <returns></returns>
        public async Task ResetAsync()
        {
            await CallFunctionAsync(new byte[] { API_SYS, 1 });
        }

        /// <summary>
        /// This function is performing a manual call of the boot loader. As a consequence the execution of the App is stopped.
        /// </summary>
        /// <returns></returns>
        public async Task StartBootloaderAsync()
        {
            await CallFunctionAsync(new byte[] { API_SYS, 2 });
        }

        /// <summary>
        /// Retrieve number of system ticks, specified in multiple of 1 milliseconds, since startup of the firmware.
        /// </summary>
        /// <returns></returns>
        public async Task<uint> GetSysTicksAsync()
        {
            var parser = await CallFunctionAsync(new byte[] { API_SYS, 3 });
            uint ticks = parser.ParseUInt32();
            return ticks;
        }

        /// <summary>
        /// Retrieve version information.
        /// </summary>
        /// <returns></returns>
        public async Task<string> GetVersionStringAsync()
        {
            var parser = await CallFunctionAsync(new byte[] { API_SYS, 4, /* maxLen */ byte.MaxValue });
            string version = parser.ParseAsciiString();
            return version;
        }

        /// <summary>
        ///     Retrieve type of USB communication. This could by keyboard emulation or CDC emulation or some other
        ///     value for future or custom implementations.
        /// </summary>
        /// <returns></returns>
        public async Task<UsbType> GetUsbTypeAsync()
        {
            var parser = await CallFunctionAsync(new byte[] { API_SYS, 5 });
            var type = (UsbType)parser.ParseByte();
            return type;
        }

        /// <summary>
        /// Retrieve type of underlying TWN4 hardware.
        /// </summary>
        /// <returns></returns>
        public async Task<DeviceType> GetDeviceTypeAsync()
        {
            var parser = await CallFunctionAsync(new byte[] { API_SYS, 6 });
            var type = (DeviceType)parser.ParseByte();
            return type;
        }

        /// <summary>
        ///     The device enters the sleep state for a specified time. During sleep state, the device reduces the current
        ///     consumption to a value, which depends on the mode of sleep.
        /// </summary>
        /// <param name="ticks">Time, specified in milliseconds, the device should enter the sleep state.</param>
        /// <param name="flags">See TWN4 API Reference.</param>
        /// <returns>See TWN4 API Reference.</returns>
        public async Task<byte> SleepAsync(uint ticks, uint flags)
        {
            List<byte> bytes = new List<byte>() { API_RF, 7 };
            bytes.AddUInt32(ticks);
            bytes.AddUInt32(flags);
            var parser = await CallFunctionAsync(bytes.ToArray());
            var result = parser.ParseByte();
            return result;
        }

        /// <summary>
        /// This function returns a UID, which is unique to the specific TWN4 device.
        /// </summary>
        /// <returns></returns>
        public async Task<byte[]> GetDeviceUidAsync()
        {
            var parser = await CallFunctionAsync(new byte[] { API_SYS, 8 });
            byte[] result = parser.ParseFixByteArray(12);
            return result;
        }

        /// <summary>
        ///     This function allows to set parameters, which influence the behaviour of the TWN4 firmware. See also
        ///     chapter System Parameters of TWN4 API Reference for a description of the TLV list and all available parameters.
        /// </summary>
        /// <param name="TLV">The raw bytes of the TLV list. Do not include TLV_END, as it is appended automatically!</param>
        /// <returns>The function returns true, if the parameters were set to the new value. Otherwise
        ///     the function returns false.</returns>
        /// <remarks>SYSFUNC(API_SYS, 9, bool SetParameters(const byte* TLV,int ByteCount))</remarks>
        public async Task<bool> SetParametersAsync(byte[] TLV)
        {
            List<byte> bytes = new List<byte>() { API_SYS, 9 };
            bytes.Add((byte)(TLV.Length + 1));
            bytes.AddRange(TLV);
            bytes.Add(0); // TLV_END
            var parser = await CallFunctionAsync(bytes.ToArray());
            var result = parser.ParseBool();
            return result;
        }

        /// <summary>
        /// This function allows to read the last error code, which was generated by any system function.
        /// </summary>
        /// <returns></returns>
        /// <remarks>SYSFUNC(API_SYS,10, unsigned int GetLastError(void))</remarks>
        public async Task<ReaderError> GetLastErrorAsync()
        {
            var parser = await CallFunctionAsync(new byte[] { API_SYS, 10 });
            var result = (ReaderError)parser.ParseUInt32();
            return result;
        }

        // Not supported: SYSFUNC(API_SYS,11, int Diagnostic(int Mode,const void* In,int InLen,void* Out,int* OutLen,int MaxOutLen))

        /// <summary>
        /// Get the product serial number of the TWN device.
        /// </summary>
        /// <returns></returns>
        public async Task<string> GetProdSerNoAsync()
        {
            var parser = await CallFunctionAsync(new byte[] { API_SYS, 13, /* maxBytes */ byte.MaxValue });
            string result = parser.ParseAsciiString();
            return result;
        }

        // Not supported: SYSFUNC(API_SYS,14, bool SetInterruptHandler(TInterruptHandler InterruptHandler, int IntNo))

        /// <summary>
        /// Retrieve version information.
        /// </summary>
        /// <returns></returns>
        /// <remarks>SYSFUNC(API_SYS,15, void GetVersionInfo(TVersionInfo* VersionInfo)).<br/>
        ///     This internal method is not documented in TWN4 API reference.
        /// </remarks>
        public async Task<VersionInfo> GetVersionInfoAsync()
        {
            var parser = await CallFunctionAsync(new byte[] { API_SYS, 15 });
            var info = new VersionInfo();
            info.Compatibility = parser.ParseUInt16();
            info.BootBranch = parser.ParseUInt16();
            var minor = parser.ParseByte();
            var major = parser.ParseByte();
            info.BootVersion = new Version(major, minor);
            info.FirmwareKeyType = parser.ParseUInt16();
            info.BranchNum = parser.ParseByte();
            info.BranchChar = (char)parser.ParseByte();
            minor = parser.ParseByte();
            major = parser.ParseByte();
            info.FirmwareVersion = new Version(major, minor);
            info.AppChars = parser.ParseFixByteArray(4);
            minor = parser.ParseByte();
            major = parser.ParseByte();
            info.AppVersion = new Version(major, minor);

            return info;
        }

        public class VersionInfo
        {
            public int Compatibility { get; set; }
            public int BootBranch { get; set; }
            public Version BootVersion { get; set; }
            public int FirmwareKeyType { get; set; }
            public byte BranchNum { get; set; }
            /// <summary>
            /// 'K' = Keyboard, 'C' = CDC
            /// </summary>
            public char BranchChar { get; set; }
            public Version FirmwareVersion { get; set; }
            /// <summary>
            /// e.g. "STD", "STDC", "PRS" = Simple Protocol
            /// </summary>
            public byte[] AppChars { get; set; }
            public Version AppVersion { get; set; }
        }

        // Not supported: SYSFUNC(API_SYS,16, bool ReadInfoValue(int Index, int FilterType, int* Type, int* Length, byte* Value, int MaxLength))
        // Not supported: SYSFUNC(API_SYS,17, bool WriteInfoValue(int Type, int Length,const byte* Value))
        // Not supported: SYSFUNC(API_SYS,18, bool GetCustomKeyID(byte* CustomKeyID, int* Length, int MaxLength))
        // Not supported: SYSFUNC(API_SYS,19, bool GetParameters(const byte* Types,int TypeCount,byte* TLVBytes,int* TLVByteCount,int TLVMaxByteCount))

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
            await CallFunctionAsync(new byte[] { API_PERIPH, 0, (byte)bits, (byte)pullUpDown, (byte)outputType });
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
            await CallFunctionAsync(new byte[] { API_PERIPH, 1, (byte)bits, (byte)pullUpDown });
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
            await CallFunctionAsync(new byte[] { API_PERIPH, 2, (byte)bits });
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
            await CallFunctionAsync(new byte[] { API_PERIPH, 3, (byte)bits });
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
            await CallFunctionAsync(new byte[] { API_PERIPH, 4, (byte)bits });
        }

        /// <summary>
        ///     Use this function to generate a pulse-width modulated square waveform with constant frequency on one
        ///     or several GPIOs. The respective ports must have been configured to output in advance.
        /// </summary>
        /// <param name="bits">Specify the GPIOs that shall generate the waveform.</param>
        /// <param name="timeHi">Specify the duration for logical high level in milliseconds.</param>
        /// <param name="timeLo">Specify the duration for logical low level in milliseconds.</param>
        /// <returns></returns>
        public async Task GpioBlinkBitsAsync(Gpios bits, ushort timeHi, ushort timeLo)
        {
            List<byte> bytes = new List<byte>() { API_PERIPH, 5 };
            bytes.Add((byte)bits);
            bytes.AddUInt16(timeHi);
            bytes.AddUInt16(timeLo);
            await CallFunctionAsync(bytes.ToArray());
        }

        /// <summary>
        /// Use this function to read the logical level of one GPIO that has been configured as input.
        /// </summary>
        /// <param name="bit">Specify the GPIO that shall be read.</param>
        /// <returns>If the GPIO has logical high level, the return value is 1, otherwise it is 0.</returns>
        public async Task<bool> GpioTestBitAsync(Gpios bit)
        {
            var parser = await CallFunctionAsync(new byte[] { API_PERIPH, 6, (byte)bit });
            var result = parser.ParseBool();
            return result;
        }

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
        public async Task BeepAsync(byte volume, ushort frequency, ushort onTime, ushort offTime)
        {
            List<byte> bytes = new List<byte>() { API_PERIPH, 7 };
            bytes.Add(volume);
            bytes.AddUInt16(frequency);
            bytes.AddUInt16(onTime);
            bytes.AddUInt16(offTime);
            await CallFunctionAsync(bytes.ToArray());
        }

        public async Task DiagLedOnAsync()
        {
            await CallFunctionAsync(new byte[] { API_PERIPH, 8 });
        }

        public async Task DiagLedOffAsync()
        {
            await CallFunctionAsync(new byte[] { API_PERIPH, 9 });
        }

        public async Task DiagLedToggleAsync()
        {
            await CallFunctionAsync(new byte[] { API_PERIPH, 10 });
        }

        public async Task<bool> DiagLedIsOnAsync()
        {
            var parser = await CallFunctionAsync(new byte[] { API_PERIPH, 11 });
            var result = parser.ParseBool();
            return result;
        }

        // TODO: SYSFUNC(API_PERIPH,12, void SendWiegand(int GPIOData0, int GPIOData1, int PulseTime, int IntervalTime,const byte* Bits,int BitCount))
        // TODO: SYSFUNC(API_PERIPH,13, void SendOmron(int GPIOClock, int GPIOData, int T1, int T2, int T3,const byte* Bits,int BitCount))
        // Not supported: SYSFUNC(API_PERIPH,14, bool GPIOPlaySequence(const int* NewSequence,int ByteCount))
        // Not supported: SYSFUNC(API_PERIPH,15, void GPIOStopSequence(void))

        /// <summary>
        /// Use this function to initialize the respective GPIOs to drive LEDs.
        /// </summary>
        /// <param name="leds">Specify the GPIOs that shall be configured for LED operation.</param>
        /// <returns></returns>
        public async Task LedInitAsync(Leds leds = Leds.All)
        {
            await CallFunctionAsync(new byte[] { API_PERIPH, 16, (byte)leds });
        }

        /// <summary>
        /// Use this function to set one or several LEDs on.
        /// </summary>
        /// <param name="leds">Specify the LEDs that shall be set on.</param>
        /// <returns></returns>
        public async Task LedOnAsync(Leds leds)
        {
            await CallFunctionAsync(new byte[] { API_PERIPH, 17, (byte)leds });
        }

        /// <summary>
        /// Use this function to set one or several LEDs off.
        /// </summary>
        /// <param name="leds">Specify the LEDs that shall be set off.</param>
        /// <returns></returns>
        public async Task LedOffAsync(Leds leds)
        {
            await CallFunctionAsync(new byte[] { API_PERIPH, 18, (byte)leds });
        }

        /// <summary>
        /// Use this function to toggle one or several LEDs.
        /// </summary>
        /// <param name="leds">Specify the LEDs that shall be toggled.</param>
        /// <returns></returns>
        public async Task LedToggleAsync(Leds leds)
        {
            await CallFunctionAsync(new byte[] { API_PERIPH, 19, (byte)leds });
        }

        /// <summary>
        /// Use this function to let one or several LEDs blink.
        /// </summary>
        /// <param name="leds">Specify the LEDs that shall blink.</param>
        /// <param name="onTime">Specify the on-time in milliseconds.</param>
        /// <param name="offTime">Specify the off-time in milliseconds.</param>
        /// <returns></returns>
        public async Task LedBlinkAsync(Leds leds, ushort onTime, ushort offTime)
        {
            List<byte> bytes = new List<byte>() { API_PERIPH, 20 };
            bytes.Add((byte)leds);
            bytes.AddUInt16(onTime);
            bytes.AddUInt16(offTime);
            await CallFunctionAsync(bytes.ToArray());
        }

        // Not supported: SYSFUNC(API_PERIPH,21,bool GPIOConfigureInterrupt(int GPIOBits,bool Enable,int Edge))

        /// <summary>
        /// Turn on beep with infinite length.
        /// </summary>
        /// <param name="volume">Specify the volume in percent from 0 to 100.</param>
        /// <param name="frequency">Specify the frequency in Hertz from 500 to 10000.</param>
        /// <returns></returns>
        public async Task BeepOnAsync(byte volume, ushort frequency)
        {
            List<byte> bytes = new List<byte>() { API_PERIPH, 22 };
            bytes.Add(volume);
            bytes.AddUInt16(frequency);
            await CallFunctionAsync(bytes.ToArray());
        }

        /// <summary>
        /// Turn off beep.
        /// </summary>
        /// <returns></returns>
        public async Task BeepOffAsync()
        {
            await CallFunctionAsync(new byte[] { API_PERIPH, 23 });
        }

        // Not supported: SYSFUNC(API_PERIPH,24,void PlayMelody(const byte *Melody,int MelodyLength))

        #endregion

        #region API_RF

        public const int API_RF = 5;

        /// <summary>
        ///     Use this function to search a transponder in the reading range of TWN4. TWN4 is searching for all types
        ///     of transponders, which have been specified via function SetTagTypes. If a transponder has been found,
        ///     tag type, length of ID and ID data itself are returned.
        /// </summary>
        /// <returns>A SearchTagResult or <see langword="null" /> if no tag was detected.</returns>
        public async Task<SearchTagResult> SearchTagAsync()
        {
            var parser = await CallFunctionAsync(new byte[] { API_RF, 0, /* maxIDBytes */ byte.MaxValue });
            var found = parser.ParseBool();
            if (found)
            {
                var tag = new SearchTagResult();
                tag.ChipType = (ChipType)parser.ParseByte();
                tag.IDBitCount = parser.ParseByte();
                tag.IDBytes = parser.ParseVarByteArray();
                return tag;
            }
            return null;
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
            await CallFunctionAsync(new byte[] { API_RF, 1 });
        }

        /// <summary>
        /// Use this function to configure the transponders, which are searched by function SearchTag.
        /// </summary>
        /// <param name="lfTagTypes"></param>
        /// <param name="hfTagTypes"></param>
        /// <returns></returns>
        public async Task SetTagTypesAsync(LFTagTypes lfTagTypes, HFTagTypes hfTagTypes)
        {
            List<byte> bytes = new List<byte>() { API_RF, 2 };
            bytes.AddUInt32((uint)lfTagTypes);
            bytes.AddUInt32((uint)hfTagTypes);
            await CallFunctionAsync(bytes.ToArray());
        }

        /// <summary>
        ///     This function returns the transponder types currently being searched for by function SearchTag separated
        ///     by frequency (LF and HF).
        /// </summary>
        /// <returns>Tag types.</returns>
        public async Task<GetTagTypesResult> GetTagTypesAsync()
        {
            var parser = await CallFunctionAsync(new byte[] { API_RF, 3 });
            var lf = parser.ParseUInt32();
            var hf = parser.ParseUInt32();

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
            var parser = await CallFunctionAsync(new byte[] { API_RF, 4 });
            var lf = parser.ParseUInt32();
            var hf = parser.ParseUInt32();

            return new GetSupportedTagTypesResult() { LFTagTypes = (LFTagTypes)lf, HFTagTypes = (HFTagTypes)hf };
        }

        public class GetSupportedTagTypesResult
        {
            public LFTagTypes LFTagTypes { get; internal set; }
            public HFTagTypes HFTagTypes { get; internal set; }
        }

        #endregion

        #region API_MIFAREULTRALIGHT / Mifare Ultralight Functions

        public const int API_MIFAREULTRALIGHT = 12;

        // TODO: SYSFUNC(API_MIFAREULTRALIGHT, 0, bool MifareUltralight_ReadPage(int Page, byte* Data))
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


        // TODO: SYSFUNC(API_MIFAREULTRALIGHT, 1, bool MifareUltralight_WritePage(int Page, const byte* Data))
        // TODO: SYSFUNC(API_MIFAREULTRALIGHT, 2, bool MifareUltralightC_Authenticate(const byte* Key))
        // TODO: SYSFUNC(API_MIFAREULTRALIGHT, 3, bool MifareUltralightC_SAMAuthenticate(int KeyNo, int KeyVersion, const byte* DIVInput, int DIVByteCnt))
        // TODO: SYSFUNC(API_MIFAREULTRALIGHT, 4, bool MifareUltralightC_WriteKeyFromSAM(int KeyNo, int KeyVersion, const byte* DIVInput, int DIVByteCnt))
        // TODO: SYSFUNC(API_MIFAREULTRALIGHT, 5, bool MifareUltralightEV1_FastRead(int StartPage, int NumberOfPages, byte* Data))
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


        // TODO: SYSFUNC(API_MIFAREULTRALIGHT, 6, bool MifareUltralightEV1_IncCounter(int CounterAddr, int IncrValue))
        // TODO: SYSFUNC(API_MIFAREULTRALIGHT, 7, bool MifareUltralightEV1_ReadCounter(int CounterAddr, int* CounterValue))
        // TODO: SYSFUNC(API_MIFAREULTRALIGHT, 8, bool MifareUltralightEV1_ReadSig(byte* ECCSig))
        // TODO: SYSFUNC(API_MIFAREULTRALIGHT, 9, bool MifareUltralightEV1_GetVersion(byte* Version))
        // TODO: SYSFUNC(API_MIFAREULTRALIGHT, 10, bool MifareUltralightEV1_PwdAuth(const byte* Password, const byte* PwdAck))
        // TODO: SYSFUNC(API_MIFAREULTRALIGHT, 11, bool MifareUltralightEV1_CheckTearingEvent(int CounterAddr, byte* ValidFlag))

        #endregion

        #region API_ISO14443 / ISO14443 Transparent Transponder Access Functions

        public const int API_ISO14443 = 18;

        /// <summary>
        /// This function delivers the ATS (Answer To Select) of a ISO14443A layer 4 transponder.
        /// </summary>
        /// <returns>The ATS if one is found, otherwise null.</returns>
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
            List<byte> bytes = new List<byte>() { API_ISO14443, 3 };
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
            List<byte> bytes = new List<byte>() { API_ISO14443, 7 };
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
        /// </summary>
        /// <param name="uid">Specify the UID of the transponder to be selected.</param>
        /// <returns>If the operation was successful, the return value is true, otherwise it is false.</returns>
        public async Task<bool> ISO14443A_SelectTagAsync(byte[] uid)
        {
            List<byte> bytes = new List<byte>() { API_ISO14443, 9 };
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
        //public BaseChip GetSingleChip(bool hf)
        //{
        //    return GetSingleChipAsync(hf, false).Result;
        //}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="hf"></param>
        /// <returns></returns>
        //public async Task<BaseChip> GetSingleChipAsync(bool hf)
        //{
        //    return await GetSingleChipAsync(hf, false);
        //}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="hf"></param>
        /// <param name="legicOnly"></param>
        /// <returns></returns>
        //public BaseChip GetSingleChip(bool hf, bool legicOnly)
        //{
        //    return GetSingleChipAsync(hf, legicOnly).Result;
        //}

        /// <summary>
        /// Get a single chip which is currently in the reading range of the device.
        /// If multiple chips are present, only the first one is returned.
        /// Use <see cref="ISO14443A_SearchMultiTagAsync"/> if you need to work with multiple chips.
        /// </summary>
        /// <returns>Depending on the ChipType a BaseChip or specialized class is returned, e.g. MifareChip.</returns>
        public async Task<BaseChip> GetSingleChipAsync()
        {
            var tag = await SearchTagAsync();
            if (tag != null)
            {
                var chip = await GetTypedChipInstance(tag.ChipType, tag.IDBytes);
                return chip;
            }
            else
            {
                //GetChip UID if SearchTagAsync failed (SmartMX Elatec workaround)
                var multiTags = await ISO14443A_SearchMultiTagAsync();
                if (multiTags.Count > 0)
                {
                    var uid = multiTags[0];
                    var chip = await GetTypedChipInstance(ChipType.MIFARE, uid); // MIFARE is the same as ISO14443A
                    return chip;
                }
            }

            return null;
        }

        private async Task<BaseChip> GetTypedChipInstance(ChipType chipType, byte[] uid)
        {
            BaseChip chip;
            switch (chipType)
            {
                case ChipType.MIFARE:
                    var mifareChip = new MifareChip(chipType, uid);
                    await DetectMifareSubType(mifareChip);
                    chip = mifareChip;
                    break;
                default:
                    chip = new BaseChip(chipType, uid);
                    break;
            }
            
            return chip;
        }

        private async Task DetectMifareSubType(MifareChip currentChip)
        {
            //Start Mifare Identification Process

            // If multiple tags were detected, select one.
            bool success = await ISO14443A_SelectTagAsync(currentChip.UID);
            if (!success)
            {
                var errorNumber = await GetLastErrorAsync();
                // returned error numbers are not properly documented, like 0x10000001. But the code execution usually continues successfully with GetAtqa.
                //return;
            }

            var atqa = await ISO14443A_GetAtqaAsync();
            if (!atqa.HasValue) 
                return;
            currentChip.ATQA = atqa.Value;

            var sakResult = await ISO14443A_GetSakAsync();
            if (!sakResult.HasValue) 
                return;

            var SAK = sakResult.Value;
            currentChip.SAK = SAK;

            byte[] ATS;

            // Start MIFARE identification
            if ((SAK & 0x02) == 0x02)
            {
                currentChip.SubType = MifareChipSubType.Unspecified;
            } // RFU bit set (RFU = Reserved for Future Use)

            else
            {
                if ((SAK & 0x08) == 0x08)
                {
                    if ((SAK & 0x10) == 0x10)
                    {
                        if ((SAK & 0x01) == 0x01)
                        {
                            currentChip.SubType = MifareChipSubType.Mifare2K;
                        } // // SAK b1 = 1 ? >> Mifare Classic 2K
                        else
                        {
                            if ((SAK & 0x20) == 0x20)
                            {
                                currentChip.SubType = MifareChipSubType.SmartMX_Mifare_4K;
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
                                        currentChip.SubType = MifareChipSubType.MifarePlus_SL1_4K;
                                    }

                                    else if (ByteConverter.SearchBytePattern(ATS, new byte[] { 0xC1, 0x05, 0x2F, 0x2F, 0x01, 0xBC, 0xD6 }) != 0) //MF PlusX 4K in SL1
                                    {
                                        currentChip.SubType = MifareChipSubType.MifarePlus_SL1_4K;
                                    }

                                } // Mifare Plus S / Plus X 4K

                                else
                                {
                                    currentChip.SubType = MifareChipSubType.Mifare4K;
                                } //Error on ATS = Mifare Classic 4K
                                //break;
                            }
                        }
                    } // SAK b5 = 1 ?
                    else
                    {
                        if ((SAK & 0x01) == 0x01)
                        {
                            currentChip.SubType = MifareChipSubType.MifareMini;
                        } // // SAK b1 = 1 ? >> Mifare Mini
                        else
                        {
                            if ((SAK & 0x20) == 0x20)
                            {
                                currentChip.SubType = MifareChipSubType.SmartMX_Mifare_1K;
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
                                        currentChip.SubType = MifareChipSubType.MifarePlus_SL1_2K;
                                    }

                                    else if (ByteConverter.SearchBytePattern(ATS, new byte[] { 0xC1, 0x05, 0x2F, 0x2F, 0x01, 0xBC, 0xD6 }) != 0) //MF PlusX 4K in SL1
                                    {
                                        currentChip.SubType = MifareChipSubType.MifarePlus_SL1_2K;
                                    }

                                    else if (ByteConverter.SearchBytePattern(ATS, new byte[] { 0xC1, 0x05, 0x21, 0x30, 0x00, 0xF6, 0xD1 }) != 0) //MF PlusSE 1K
                                    {
                                        currentChip.SubType = MifareChipSubType.MifarePlus_SL0_1K;
                                    }

                                    else
                                    {
                                        currentChip.SubType = MifareChipSubType.MifarePlus_SL1_1K;
                                    }
                                } // Mifare Plus S / Plus X 4K

                                else
                                {
                                    currentChip.SubType = MifareChipSubType.Mifare1K;
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
                            currentChip.SubType = MifareChipSubType.MifarePlus_SL2_4K;
                        } // Mifare Plus 4K in SL2
                        else
                        {
                            currentChip.SubType = MifareChipSubType.MifarePlus_SL2_2K;
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
                                    var L4VERSION = new byte[getVersion.Length - 2];
                                    Buffer.BlockCopy(getVersion, 2, L4VERSION, 0, getVersion.Length - 2);

                                    // Mifare Plus EV1/2 || DesFire || NTAG
                                    if (getVersion?.Length > 1 && (getVersion?[5] == 0x01)) // DESFIRE
                                    {
                                        switch (getVersion?[7] & 0x0F) // Desfire(Sub)Type by lower Nibble of Major Version
                                        {
                                            case 0: //DESFIRE EV0
                                                currentChip.SubType = MifareChipSubType.DESFire;

                                                switch (getVersion?[9])
                                                {
                                                    case 0x10:
                                                        currentChip.SubType = MifareChipSubType.DESFire_256; // DESFIRE 256B
                                                        break;
                                                    case 0x16:
                                                        currentChip.SubType = MifareChipSubType.DESFire_2K; // DESFIRE 2K
                                                        break;
                                                    case 0x18:
                                                        currentChip.SubType = MifareChipSubType.DESFire_4K; // 4K
                                                        break;
                                                    default:
                                                        break;
                                                } // Size ?
                                                break;

                                            case 1: // DESFIRE EV1
                                                currentChip.SubType = MifareChipSubType.DESFireEV1;

                                                switch (getVersion?[9])
                                                {
                                                    case 0x10:
                                                        currentChip.SubType = MifareChipSubType.DESFireEV1_256; //DESFIRE 256B
                                                        break;
                                                    case 0x16:
                                                        currentChip.SubType = MifareChipSubType.DESFireEV1_2K; // DESFIRE 2K
                                                        break;
                                                    case 0x18:
                                                        currentChip.SubType = MifareChipSubType.DESFireEV1_4K; // 4K
                                                        break;
                                                    case 0x1A:
                                                        currentChip.SubType = MifareChipSubType.DESFireEV1_8K; // 8K
                                                        break;
                                                    default:
                                                        break;
                                                } // Size ?
                                                break;

                                            case 2: // EV2
                                                currentChip.SubType = MifareChipSubType.DESFireEV2;

                                                switch (getVersion?[9])
                                                {
                                                    case 0x16:
                                                        currentChip.SubType = MifareChipSubType.DESFireEV2_2K; // DESFIRE 2K
                                                        break;
                                                    case 0x18:
                                                        currentChip.SubType = MifareChipSubType.DESFireEV2_4K; // 4K
                                                        break;
                                                    case 0x1A:
                                                        currentChip.SubType = MifareChipSubType.DESFireEV2_8K; // 8K
                                                        break;
                                                    case 0x1C:
                                                        currentChip.SubType = MifareChipSubType.DESFireEV2_16K; // 16K
                                                        break;
                                                    case 0x1E:
                                                        currentChip.SubType = MifareChipSubType.DESFireEV2_32K; // 32K
                                                        break;
                                                    default:
                                                        break;
                                                } // SIZE ?
                                                break;

                                            case 3: // EV3
                                                currentChip.SubType = MifareChipSubType.DESFireEV3;

                                                switch (getVersion?[9])
                                                {
                                                    case 0x16:
                                                        currentChip.SubType = MifareChipSubType.DESFireEV3_2K; // DESFIRE 2K
                                                        break;
                                                    case 0x18:
                                                        currentChip.SubType = MifareChipSubType.DESFireEV3_4K; // 4K
                                                        break;
                                                    case 0x1A:
                                                        currentChip.SubType = MifareChipSubType.DESFireEV3_8K; // 8K
                                                        break;
                                                    case 0x1C:
                                                        currentChip.SubType = MifareChipSubType.DESFireEV3_16K; // 16K
                                                        break;
                                                    case 0x1E:
                                                        currentChip.SubType = MifareChipSubType.DESFireEV3_32K; // 32K
                                                        break;
                                                    default:
                                                        break;
                                                } // SIZE ?
                                                break;

                                            default:
                                                currentChip.SubType = MifareChipSubType.Unspecified;

                                                break;
                                        }
                                    }
                                    else if (getVersion?.Length > 1 && getVersion?[5] == 0x81) // Emulated e.g. SmartMX
                                    {
                                        switch (getVersion?[7] & 0x0F) // Desfire(Sub)Type by lower Nibble of Major Version
                                        {
                                            case 0: //DESFIRE EV0
                                                currentChip.SubType = MifareChipSubType.SmartMX_DESFire_Generic;

                                                switch (getVersion?[9])
                                                {
                                                    case 0x10:
                                                        currentChip.SubType = MifareChipSubType.SmartMX_DESFire_Generic; // DESFIRE 256B
                                                        break;
                                                    case 0x16:
                                                        currentChip.SubType = MifareChipSubType.SmartMX_DESFire_2K; // DESFIRE 2K
                                                        break;
                                                    case 0x18:
                                                        currentChip.SubType = MifareChipSubType.SmartMX_DESFire_4K; // 4K
                                                        break;
                                                    default:
                                                        break;
                                                } // Size ?
                                                break;

                                            case 1: // DESFIRE EV1
                                                currentChip.SubType = MifareChipSubType.SmartMX_DESFire_Generic;

                                                switch (getVersion?[9])
                                                {
                                                    case 0x10:
                                                        currentChip.SubType = MifareChipSubType.SmartMX_DESFire_Generic; //DESFIRE 256B
                                                        break;
                                                    case 0x16:
                                                        currentChip.SubType = MifareChipSubType.SmartMX_DESFire_2K; // DESFIRE 2K
                                                        break;
                                                    case 0x18:
                                                        currentChip.SubType = MifareChipSubType.SmartMX_DESFire_4K; // 4K
                                                        break;
                                                    case 0x1A:
                                                        currentChip.SubType = MifareChipSubType.SmartMX_DESFire_8K; // 8K
                                                        break;
                                                    default:
                                                        break;
                                                } // Size ?
                                                break;

                                            case 2: // EV2
                                                currentChip.SubType = MifareChipSubType.SmartMX_DESFire_Generic;

                                                switch (getVersion?[9])
                                                {
                                                    case 0x16:
                                                        currentChip.SubType = MifareChipSubType.SmartMX_DESFire_2K; // DESFIRE 2K
                                                        break;
                                                    case 0x18:
                                                        currentChip.SubType = MifareChipSubType.SmartMX_DESFire_4K; // 4K
                                                        break;
                                                    case 0x1A:
                                                        currentChip.SubType = MifareChipSubType.SmartMX_DESFire_8K; // 8K
                                                        break;
                                                    case 0x1C:
                                                        currentChip.SubType = MifareChipSubType.SmartMX_DESFire_16K; // 16K
                                                        break;
                                                    case 0x1E:
                                                        currentChip.SubType = MifareChipSubType.SmartMX_DESFire_32K; // 32K
                                                        break;
                                                    default:
                                                        break;
                                                } // SIZE ?
                                                break;

                                            case 3: // EV3
                                                currentChip.SubType = MifareChipSubType.SmartMX_DESFire_Generic;

                                                switch (getVersion?[9])
                                                {
                                                    case 0x16:
                                                        currentChip.SubType = MifareChipSubType.SmartMX_DESFire_2K; // DESFIRE 2K
                                                        break;
                                                    case 0x18:
                                                        currentChip.SubType = MifareChipSubType.SmartMX_DESFire_4K; // 4K
                                                        break;
                                                    case 0x1A:
                                                        currentChip.SubType = MifareChipSubType.SmartMX_DESFire_8K; // 8K
                                                        break;
                                                    case 0x1C:
                                                        currentChip.SubType = MifareChipSubType.SmartMX_DESFire_16K; // 16K
                                                        break;
                                                    case 0x1E:
                                                        currentChip.SubType = MifareChipSubType.SmartMX_DESFire_32K; // 32K
                                                        break;
                                                    default:
                                                        break;
                                                } // SIZE ?
                                                break;

                                            default:
                                                currentChip.SubType = MifareChipSubType.Unspecified;

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
                                            currentChip.SubType = MifareChipSubType.MifarePlus_SL3_4K;
                                        }

                                        else if (ByteConverter.SearchBytePattern(ATS, new byte[] { 0xC1, 0x05, 0x2F, 0x2F, 0x01, 0xBC, 0xD6 }) != 0) //MF PlusX 4K in SL1
                                        {
                                            currentChip.SubType = MifareChipSubType.MifarePlus_SL3_4K;
                                        }
                                        else
                                        {
                                            currentChip.SubType = MifareChipSubType.Unspecified;
                                        }
                                    }
                                    else
                                    {
                                        currentChip.SubType = MifareChipSubType.SmartMX_Mifare_4K;
                                    }
                                } // Mifare Plus
                            } // SAK b6 = 1 ?
                            else
                            {
                                currentChip.SubType = MifareChipSubType.MifareUltralight;
                            } // Ultralight || NTAG
                        }
                    } // SAK b5 = 1 ?
                } // SAK b5 = 1 ?
            }



        }

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
            var version = await GetVersionStringAsync();
            return version.StartsWith("TWN4");
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
        /// Call a function of the TWN device in raw format. Sends the bytes provided by CMD and returns the response.
        /// The caller is responsible for providing the correct input sequence, parsing the result and handling the errorCode.
        /// 
        /// Example: 
        /// <code>
        ///     byte[] result = await CallFunctionRawAsync(new byte[] { API_SYS, 3 });
        ///     byte errorCode = result[0];
        /// </code>
        /// </summary>
        /// <param name="CMD">Command to send to the device.</param>
        /// <returns>The response of the device.</returns>
        public async Task<byte[]> CallFunctionRawAsync(byte[] CMD)
        {
            return await DoTXRXAsync(CMD);
        }

        /// <summary>
        /// Call a function of the TWN device in raw format. Sends the bytes provided by CMD and returns a ResponseParser wrapping the response.
        /// The caller is responsible for providing the correct input sequence and handling errors.
        /// 
        /// Example: 
        /// <code>
        ///     var parser = await CallFunctionParserAsync(new byte[] { API_SYS, 3 });
        ///     var errorCode = parser.ParseResponseError();
        ///     if (errorCode != ResponseError.None)
        ///         uint ticks = parser.ParseLong();
        /// </code>
        /// </summary>
        /// <param name="CMD">Command to send to the device.</param>
        /// <returns>A <see cref="ResponseParser"/> wrapping the response of the device.</returns>
        public async Task<ResponseParser> CallFunctionParserAsync(byte[] CMD)
        {
            var result = await CallFunctionRawAsync(CMD);
            var parser = new ResponseParser(result.ToList());
            return parser;
        }

        /// <summary>
        /// Call a function of the TWN device in raw format. Sends the bytes provided by CMD and returns a ResponseParser wrapping the response.
        /// The caller is responsible for providing the correct input sequence.
        /// If the device returns an error, it is thrown as a TwnException. The caller must NOT treat the first parser byte as an error code, as it has already been parsed.
        /// 
        /// Example: 
        /// <code>
        ///   try {
        ///     var parser = await CallFunctionAsync(new byte[] { API_SYS, 3 });
        ///     uint ticks = parser.ParseLong();
        ///   } catch (TwnException e) {...}
        /// </code>
        /// </summary>
        /// <param name="CMD">Command to send to the device.</param>
        /// <returns>A <see cref="ResponseParser"/> wrapping the response of the device.</returns>
        /// <exception cref="TwnException">Is thrown if a communication error occurs.</exception>
        public async Task<ResponseParser> CallFunctionAsync(byte[] CMD)
        {
            var parser = await CallFunctionParserAsync(CMD);
            var errorCode = parser.ParseResponseError();
            if (errorCode != ResponseError.None)
            {
                throw new TwnException("Reader error: " + errorCode, errorCode);
            }
            return parser;
        }

        // TODO This is example code for error handling
        //public async Task<byte[]> MifareClassicReadBlock2Async(byte blockNumber)
        //{
        //    var parser = await CallFunctionAsync(new byte[] { API_MIFARECLASSIC, 1, blockNumber });
        //    bool success = parser.ParseBool();
        //    if (await CheckSuccessOrThrowLastError(success))
        //    {
        //        byte[] content = parser.ParseFixByteArray(16);
        //        return content;
        //    }
        //    return null; // can never happen
        //}

        public async Task<bool> CheckSuccessOrThrowLastError(bool success)
        {
            if (success) { return true; }
            var errorCode = await GetLastErrorAsync();
            throw new ReaderException("Call was not successfull, error " + errorCode, errorCode);
        }


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

                    if (IsConnected) // if (CMD?[0] != 0 && IsConnected)
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
