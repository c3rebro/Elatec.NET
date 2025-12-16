using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Elatec.NET.Helpers.ByteArrayHelper.Extensions;

namespace Elatec.NET
{
    public partial class TWN4ReaderDevice
    {
        #region API_SYS / System Functions

        public static readonly byte API_SYS = 0;

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
            var subVersion = version.Split('/');
            IsTWN4LegicReader = subVersion.Length >= 3 && subVersion[2].Contains("B");
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
            List<byte> bytes = new List<byte> { API_SYS, 7 };
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
            List<byte> bytes = new List<byte> { API_SYS, 9 };
            bytes.Add((byte)(TLV.Length + 1));
            bytes.AddRange(TLV);
            bytes.Add(0); // TLV_END
            var parser = await CallFunctionAsync(bytes.ToArray());
            var result = parser.ParseBool();
            return result;
        }

        /// <summary>
        /// This function is used to retrieve internal system errors of the reader. Do not deduce protocol or communication errors from this function call.
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

        public static readonly byte API_PERIPH = 2;

        // Not supported: SYSFUNC(API_PERIPH, 0, bool SysSetGpioConfig(byte bits, byte pull_up_down, byte output_type))

        /// <summary>
        /// Set the polarity and the output type (open-drain or push-pull) of each GPIO pin.
        /// </summary>
        /// <param name="bits">GPIO pins to set. This is a bitmask, where the bits represent the GPIO pins, see TwnGpioEnum.</param>
        /// <param name="pullUpDown">Input pin resistors: PullUp, PullDown, or None</param>
        /// <param name="outputType">Output pin types: PushPull or OpenDrain.</param>
        /// <returns></returns>
        /// <remarks>SYSFUNC(API_PERIPH, 1, bool GpioSetConfig(byte bits, byte pull_up_down, byte output_type))</remarks>
        public async Task SetGpioConfigAsync(Gpios bits, PullResistor pullUpDown, OutputType outputType)
        {
            await CallFunctionAsync(new byte[] { API_PERIPH, 0, (byte)bits, (byte)pullUpDown, (byte)outputType });
        }

        /// <summary>
        /// Set the pull up/down of each GPIO pin.
        /// </summary>
        /// <param name="bits">GPIO pins to set. This is a bitmask, where the bits represent the GPIO pins, see TwnGpioEnum.</param>
        /// <param name="pullUpDown">Input pin resistors: PullUp, PullDown, or None</param>
        /// <returns></returns>
        /// <remarks>SYSFUNC(API_PERIPH, 2, bool GpioSetPullUpDown(byte bits, byte pull_up_down))</remarks>
        public async Task SetGpioPullUpDownAsync(Gpios bits, PullResistor pullUpDown)
        {
            await CallFunctionAsync(new byte[] { API_PERIPH, 1, (byte)bits, (byte)pullUpDown });
        }

        /// <summary>
        /// Set GPIO pins to PushPull.
        /// </summary>
        /// <param name="bits">GPIO pins to set. This is a bitmask, where the bits represent the GPIO pins, see TwnGpioEnum.</param>
        /// <returns></returns>
        /// <remarks>SYSFUNC(API_PERIPH, 3, bool GpioSetPushPull(byte bits))</remarks>
        public async Task SetGpioPushPullAsync(Gpios bits)
        {
            await CallFunctionAsync(new byte[] { API_PERIPH, 2, (byte)bits });
        }

        /// <summary>
        /// Set GPIO pins to OpenDrain.
        /// </summary>
        /// <param name="bits">GPIO pins to set. This is a bitmask, where the bits represent the GPIO pins, see TwnGpioEnum.</param>
        /// <returns></returns>
        /// <remarks>SYSFUNC(API_PERIPH, 4, bool GpioSetOpenDrain(byte bits))</remarks>
        public async Task SetGpioOpenDrainAsync(Gpios bits)
        {
            await CallFunctionAsync(new byte[] { API_PERIPH, 3, (byte)bits });
        }

        /// <summary>
        /// Set the state of the desired GPIO pins.
        /// </summary>
        /// <param name="bits">GPIO pins to set. This is a bitmask, where the bits represent the GPIO pins, see TwnGpioEnum.</param>
        /// <returns></returns>
        /// <remarks>SYSFUNC(API_PERIPH, 5, bool GpioSetBits(byte bits))</remarks>
        public async Task SetGpioBitsAsync(Gpios bits)
        {
            await CallFunctionAsync(new byte[] { API_PERIPH, 4, (byte)bits });
        }

        /// <summary>
        /// Clear the state of the desired GPIO pins.
        /// </summary>
        /// <param name="bits">GPIO pins to clear. This is a bitmask, where the bits represent the GPIO pins, see TwnGpioEnum.</param>
        /// <returns></returns>
        /// <remarks>SYSFUNC(API_PERIPH, 6, bool GpioClearBits(byte bits))</remarks>
        public async Task ClearGpioBitsAsync(Gpios bits)
        {
            await CallFunctionAsync(new byte[] { API_PERIPH, 5, (byte)bits });
        }

        /// <summary>
        /// Set the state of the desired GPIO pins.
        /// </summary>
        /// <param name="setbits">GPIO pins to set. This is a bitmask, where the bits represent the GPIO pins, see TwnGpioEnum.</param>
        /// <param name="clearbits">GPIO pins to clear. This is a bitmask, where the bits represent the GPIO pins, see TwnGpioEnum.</param>
        /// <param name="togglebits">GPIO pins to toggle the state of. This is a bitmask, where the bits represent the GPIO pins, see TwnGpioEnum.</param>
        /// <returns></returns>
        /// <remarks>SYSFUNC(API_PERIPH, 7, bool GpioWriteBits(byte setbits, byte clearbits, byte togglebits))</remarks>
        public async Task WriteGpioBitsAsync(Gpios setbits, Gpios clearbits, Gpios togglebits)
        {
            List<byte> bytes = new List<byte> { API_PERIPH, 6 };
            bytes.Add((byte)setbits);
            bytes.Add((byte)clearbits);
            bytes.Add((byte)togglebits);
            await CallFunctionAsync(bytes.ToArray());
        }

        /// <summary>
        /// Get the state of the desired GPIO pins.
        /// </summary>
        /// <param name="bit">GPIO pin to check. See TwnGpioEnum.</param>
        /// <returns>Returns true, if the GPIO pin is set, false otherwise.</returns>
        /// <remarks>SYSFUNC(API_PERIPH, 8, bool GpioGetBit(byte bit))</remarks>
        public async Task<bool> GetGpioBitAsync(Gpios bit)
        {
            var parser = await CallFunctionAsync(new byte[] { API_PERIPH, 6, (byte)bit });
            var result = parser.ParseBool();
            return result;
        }

        /// <summary>
        /// This function initializes UART0 in the specified mode. It occupies GPIO pins 1 and 4 (RX and TX).
        /// </summary>
        /// <param name="Mode">The UART mode selection. See TwnUartModeEnum.</param>
        /// <param name="Baudrate">UART0 baudrate in Bauds</param>
        /// <returns></returns>
        /// <remarks>SYSFUNC(API_PERIPH,10, bool SerialSetMode(byte Mode, unsigned int Baudrate))</remarks>
        public async Task SetSerialModeAsync(SerialMode Mode, uint Baudrate)
        {
            List<byte> bytes = new List<byte> { API_PERIPH, 8 };
            bytes.Add((byte)Mode);
            bytes.AddUInt32(Baudrate);
            await CallFunctionAsync(bytes.ToArray());
        }

        /// <summary>
        /// Writes to UART0.
        /// </summary>
        /// <param name="Data">Data to write to UART0.</param>
        /// <returns></returns>
        /// <remarks>SYSFUNC(API_PERIPH,11, bool SerialWrite(const byte* Data, int ByteCount))</remarks>
        public async Task SerialWriteAsync(byte[] Data)
        {
            List<byte> bytes = new List<byte> { API_PERIPH, 9 };
            bytes.Add((byte)Data.Length);
            bytes.AddRange(Data);
            await CallFunctionAsync(bytes.ToArray());
        }

        /// <summary>
        /// Reads from UART0 into a buffer.
        /// </summary>
        /// <param name="MaxBytes">Maximum number of bytes to read from UART0.</param>
        /// <returns></returns>
        /// <remarks>SYSFUNC(API_PERIPH,12, int SerialRead(byte* Data,int MaxByteCount))</remarks>
        public async Task<byte[]> SerialReadAsync(byte MaxBytes)
        {
            List<byte> bytes = new List<byte> { API_PERIPH, 10 };
            bytes.Add(MaxBytes);
            var parser = await CallFunctionAsync(bytes.ToArray());
            var result = parser.ParseFlexByteArray();
            return result;
        }

        /// <summary>
        /// Reads and writes to UART0.
        /// </summary>
        /// <param name="writeData">Data to write to UART0.</param>
        /// <param name="readMaxBytes">Maximum number of bytes to read from UART0.</param>
        /// <returns></returns>
        /// <remarks>SYSFUNC(API_PERIPH,13, int SerialReadWrite(const byte* writeData,int writeByteCount,byte* readData,int readMaxByteCount))</remarks>
        public async Task<byte[]> SerialReadWriteAsync(byte[] writeData, byte readMaxBytes)
        {
            List<byte> bytes = new List<byte> { API_PERIPH, 11 };
            bytes.Add((byte)writeData.Length);
            bytes.AddRange(writeData);
            bytes.Add(readMaxBytes);
            var parser = await CallFunctionAsync(bytes.ToArray());
            var result = parser.ParseFlexByteArray();
            return result;
        }

        /// <summary>
        /// Causes the device to emit an acoustic tone using buzzer or speaker for the desired duration.
        /// </summary>
        /// <param name="duration">Milliseconds of tone output</param>
        /// <remarks>SYSFUNC(API_PERIPH,16, void PlaySound(int duration))</remarks>
        public async Task PlaySoundAsync(short duration)
        {
            await CallFunctionAsync(new byte[] { API_PERIPH, 16, (byte)duration });
        }

        /// <summary>
        /// Causes the device to emit two acoustic tones using buzzer or speaker for the desired duration.
        /// </summary>
        /// <param name="duration">Milliseconds of tone output</param>
        /// <remarks>SYSFUNC(API_PERIPH,17, void PlaySound1(int duration))</remarks>
        public async Task PlaySound1Async(short duration)
        {
            await CallFunctionAsync(new byte[] { API_PERIPH, 17, (byte)duration });
        }

        /// <summary>
        /// Causes the device to emit two acoustic tones using buzzer or speaker for the desired duration.
        /// </summary>
        /// <param name="duration">Milliseconds of tone output</param>
        /// <remarks>SYSFUNC(API_PERIPH,18, void PlaySound2(int duration))</remarks>
        public async Task PlaySound2Async(short duration)
        {
            await CallFunctionAsync(new byte[] { API_PERIPH, 18, (byte)duration });
        }

        /// <summary>
        /// Causes the device to emit an acoustic tone using buzzer or speaker for the desired duration.
        /// </summary>
        /// <param name="tone1Duration">Duration of first tone in milliseconds.</param>
        /// <param name="tone2Duration">Duration of second tone in milliseconds.</param>
        /// <remarks>SYSFUNC(API_PERIPH,19, void PlaySound4(int tone1Duration, int tone2Duration))</remarks>
        public async Task PlaySound4Async(short tone1Duration, short tone2Duration)
        {
            await CallFunctionAsync(new byte[] { API_PERIPH, 19, (byte)tone1Duration, (byte)tone2Duration });
        }

        /// <summary>
        /// Causes the device to emit a melody.
        /// </summary>
        /// <param name="tones">The desired melody: tones.</param>
        /// <param name="durations">The desired melody: durations. This represents a percentage with 255=100%.</param>
        /// <remarks>SYSFUNC(API_PERIPH,20, void PlayMelody(const byte* tones,const byte* durations,int Count,int RepeatCount))</remarks>
        public async Task PlayMelodyAsync(byte[] tones, byte[] durations)
        {
            List<byte> bytes = new List<byte> { API_PERIPH, 20 };
            bytes.Add((byte)tones.Length);
            bytes.AddRange(tones);
            bytes.Add((byte)durations.Length);
            bytes.AddRange(durations);
            await CallFunctionAsync(bytes.ToArray());
        }

        /// <summary>
        /// Causes the device to emit a melody.
        /// </summary>
        /// <param name="tones">The desired melody: tones.</param>
        /// <param name="durations">The desired melody: durations. This represents a percentage with 255=100%.</param>
        /// <param name="repeatCount">Repeat the melody repeatCount times.</param>
        /// <remarks>SYSFUNC(API_PERIPH,21, void PlayMelody(const byte* tones,const byte* durations,int Count,int RepeatCount))</remarks>
        public async Task PlayMelodyAsync(byte[] tones, byte[] durations, byte repeatCount)
        {
            List<byte> bytes = new List<byte> { API_PERIPH, 20 };
            bytes.Add((byte)tones.Length);
            bytes.AddRange(tones);
            bytes.Add((byte)durations.Length);
            bytes.AddRange(durations);
            bytes.Add(repeatCount);
            await CallFunctionAsync(bytes.ToArray());
        }

        /// <summary>
        /// Stops an acoustic tone or melody produced by a previous PlaySound or PlayMelody call.
        /// </summary>
        /// <remarks>SYSFUNC(API_PERIPH,22, void StopSound())</remarks>
        public async Task StopSoundAsync()
        {
            await CallFunctionAsync(new byte[] { API_PERIPH, 23 });
        }

        #endregion
    }
}
