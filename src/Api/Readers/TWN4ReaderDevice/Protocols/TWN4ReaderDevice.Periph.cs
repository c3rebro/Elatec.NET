using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Elatec.NET.Helpers.ByteArrayHelper.Extensions;

namespace Elatec.NET
{
    public partial class TWN4ReaderDevice
    {
        #region API_PERIPH / Periphery Functions

        /// <summary>
        /// TWN4 Simple Protocol API identifier for periphery functions (0x04xx).
        /// </summary>
        public static readonly byte API_PERIPH = 0x04;

        /// <summary>
        /// Configure one or more GPIOs as outputs.
        /// Simple Protocol command: 0x0400.
        /// </summary>
        public async Task GpioConfigureOutputsAsync(Gpios bits, GpioPullType pullUpDown, GpioOutputType outputType)
        {
            await CallFunctionAsync(new byte[] { API_PERIPH, 0x00, (byte)bits, (byte)pullUpDown, (byte)outputType });
        }

        /// <summary>
        /// Configure one or more GPIOs as inputs.
        /// Simple Protocol command: 0x0401.
        /// </summary>
        public async Task GpioConfigureInputsAsync(Gpios bits, GpioPullType pullUpDown)
        {
            await CallFunctionAsync(new byte[] { API_PERIPH, 0x01, (byte)bits, (byte)pullUpDown });
        }

        /// <summary>
        /// Set one or more GPIO outputs to logical high.
        /// The GPIOs must have been configured as outputs first.
        /// Simple Protocol command: 0x0402.
        /// </summary>
        public async Task GpioSetBitsAsync(Gpios bits)
        {
            await CallFunctionAsync(new byte[] { API_PERIPH, 0x02, (byte)bits });
        }

        /// <summary>
        /// Set one or more GPIO outputs to logical low.
        /// The GPIOs must have been configured as outputs first.
        /// Simple Protocol command: 0x0403.
        /// </summary>
        public async Task GpioClearBitsAsync(Gpios bits)
        {
            await CallFunctionAsync(new byte[] { API_PERIPH, 0x03, (byte)bits });
        }

        /// <summary>
        /// Toggle one or more GPIO outputs.
        /// The GPIOs must have been configured as outputs first.
        /// Simple Protocol command: 0x0404.
        /// </summary>
        public async Task GpioToggleBitsAsync(Gpios bits)
        {
            await CallFunctionAsync(new byte[] { API_PERIPH, 0x04, (byte)bits });
        }

        /// <summary>
        /// Blink one or more GPIO outputs.
        /// The GPIOs must have been configured as outputs first.
        /// Simple Protocol command: 0x0405.
        /// </summary>
        public async Task GpioBlinkBitsAsync(Gpios bits, ushort timeHigh, ushort timeLow)
        {
            var bytes = new List<byte> { API_PERIPH, 0x05, (byte)bits };
            bytes.AddUInt16(timeHigh);
            bytes.AddUInt16(timeLow);
            await CallFunctionAsync(bytes.ToArray());
        }

        /// <summary>
        /// Read the state of a GPIO.
        /// Simple Protocol command: 0x0406.
        /// </summary>
        public async Task<bool> GpioTestBitAsync(Gpios bit)
        {
            var parser = await CallFunctionAsync(new byte[] { API_PERIPH, 0x06, (byte)bit });
            return parser.ParseByte() != 0;
        }

        /// <summary>
        /// Switch the diagnostic LED on.
        /// Simple Protocol command: 0x0408.
        /// </summary>
        public async Task DiagLedOnAsync()
        {
            await CallFunctionAsync(new byte[] { API_PERIPH, 0x08 });
        }

        /// <summary>
        /// Switch the diagnostic LED off.
        /// Simple Protocol command: 0x0409.
        /// </summary>
        public async Task DiagLedOffAsync()
        {
            await CallFunctionAsync(new byte[] { API_PERIPH, 0x09 });
        }

        /// <summary>
        /// Toggle the diagnostic LED.
        /// Simple Protocol command: 0x040A.
        /// </summary>
        public async Task DiagLedToggleAsync()
        {
            await CallFunctionAsync(new byte[] { API_PERIPH, 0x0A });
        }

        /// <summary>
        /// Query the diagnostic LED state.
        /// Simple Protocol command: 0x040B.
        /// </summary>
        public async Task<bool> DiagLedIsOnAsync()
        {
            var parser = await CallFunctionAsync(new byte[] { API_PERIPH, 0x0B });
            return parser.ParseBool();
        }

        /// <summary>
        /// Send a Wiegand bit stream.
        /// Simple Protocol command: 0x040C.
        /// </summary>
        public async Task SendWiegandAsync(Gpios gpioData0, Gpios gpioData1, ushort pulseTime, ushort intervalTime, byte[] bits, byte bitCount)
        {
            if (bits == null)
            {
                throw new ArgumentNullException(nameof(bits));
            }

            if (bits.Length > byte.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(bits), "Simple Protocol Byte Array(Var) fields are limited to 255 bytes.");
            }

            var bytes = new List<byte> { API_PERIPH, 0x0C, (byte)gpioData0, (byte)gpioData1 };
            bytes.AddUInt16(pulseTime);
            bytes.AddUInt16(intervalTime);
            bytes.Add((byte)bits.Length);
            bytes.AddRange(bits);
            bytes.Add(bitCount);
            await CallFunctionAsync(bytes.ToArray());
        }

        /// <summary>
        /// Send an Omron bit stream.
        /// Simple Protocol command: 0x040D.
        /// </summary>
        public async Task SendOmronAsync(Gpios gpioClock, Gpios gpioData, ushort t1, ushort t2, ushort t3, byte[] bits, byte bitCount)
        {
            if (bits == null)
            {
                throw new ArgumentNullException(nameof(bits));
            }

            if (bits.Length > byte.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(bits), "Simple Protocol Byte Array(Var) fields are limited to 255 bytes.");
            }

            var bytes = new List<byte> { API_PERIPH, 0x0D, (byte)gpioClock, (byte)gpioData };
            bytes.AddUInt16(t1);
            bytes.AddUInt16(t2);
            bytes.AddUInt16(t3);
            bytes.Add((byte)bits.Length);
            bytes.AddRange(bits);
            bytes.Add(bitCount);
            await CallFunctionAsync(bytes.ToArray());
        }

        /// <summary>
        /// Initialize GPIOs for LED operation.
        /// Simple Protocol command: 0x0410.
        /// </summary>
        public async Task LedInitAsync(Leds leds = Leds.All)
        {
            await CallFunctionAsync(new byte[] { API_PERIPH, 0x10, (byte)leds });
        }

        /// <summary>
        /// Compatibility alias for <see cref="LedInitAsync(Leds)"/>.
        /// </summary>
        public Task InitLedsAsync(Leds leds = Leds.All)
        {
            return LedInitAsync(leds);
        }

        /// <summary>
        /// Switch one or more initialized LEDs on.
        /// Simple Protocol command: 0x0411.
        /// </summary>
        public async Task LedOnAsync(Leds leds)
        {
            await CallFunctionAsync(new byte[] { API_PERIPH, 0x11, (byte)leds });
        }

        /// <summary>
        /// Switch one or more initialized LEDs off.
        /// Simple Protocol command: 0x0412.
        /// </summary>
        public async Task LedOffAsync(Leds leds)
        {
            await CallFunctionAsync(new byte[] { API_PERIPH, 0x12, (byte)leds });
        }

        /// <summary>
        /// Toggle one or more initialized LEDs.
        /// Simple Protocol command: 0x0413.
        /// </summary>
        public async Task LedToggleAsync(Leds leds)
        {
            await CallFunctionAsync(new byte[] { API_PERIPH, 0x13, (byte)leds });
        }

        /// <summary>
        /// Blink one or more initialized LEDs.
        /// Simple Protocol command: 0x0414.
        /// </summary>
        public async Task LedBlinkAsync(Leds leds, ushort onTime, ushort offTime)
        {
            var bytes = new List<byte> { API_PERIPH, 0x14, (byte)leds };
            bytes.AddUInt16(onTime);
            bytes.AddUInt16(offTime);
            await CallFunctionAsync(bytes.ToArray());
        }

        /// <summary>
        /// Switch the beeper on continuously.
        /// Simple Protocol command: 0x0416.
        /// </summary>
        public async Task BeepOnAsync(byte volume, ushort frequency)
        {
            var bytes = new List<byte> { API_PERIPH, 0x16, volume };
            bytes.AddUInt16(frequency);
            await CallFunctionAsync(bytes.ToArray());
        }

        /// <summary>
        /// Switch the beeper off.
        /// Simple Protocol command: 0x0417.
        /// </summary>
        public async Task BeepOffAsync()
        {
            await CallFunctionAsync(new byte[] { API_PERIPH, 0x17 });
        }

        #region Compatibility aliases

        /// <summary>
        /// Compatibility alias for configuring GPIO outputs.
        /// </summary>
        public Task SetGpioConfigAsync(Gpios bits, PullResistor pullUpDown, OutputType outputType)
        {
            return GpioConfigureOutputsAsync(bits, (GpioPullType)pullUpDown, (GpioOutputType)outputType);
        }

        /// <summary>
        /// Compatibility alias for <see cref="GpioSetBitsAsync(Gpios)"/>.
        /// </summary>
        public Task SetGpioBitsAsync(Gpios bits)
        {
            return GpioSetBitsAsync(bits);
        }

        /// <summary>
        /// Compatibility alias for <see cref="GpioClearBitsAsync(Gpios)"/>.
        /// </summary>
        public Task ClearGpioBitsAsync(Gpios bits)
        {
            return GpioClearBitsAsync(bits);
        }

        /// <summary>
        /// Compatibility alias for <see cref="GpioTestBitAsync(Gpios)"/>.
        /// </summary>
        public Task<bool> GetGpioBitAsync(Gpios bit)
        {
            return GpioTestBitAsync(bit);
        }

        [Obsolete("GpioSetPullUpDown is part of the TWN4 App/API SYSFUNC interface and has no equivalent command in the stock Simple Protocol. Use GpioConfigureInputsAsync or GpioConfigureOutputsAsync instead.")]
        public Task SetGpioPullUpDownAsync(Gpios bits, PullResistor pullUpDown)
        {
            return UnsupportedSimpleProtocolCall(nameof(SetGpioPullUpDownAsync));
        }

        [Obsolete("GpioSetPushPull is part of the TWN4 App/API SYSFUNC interface and has no equivalent command in the stock Simple Protocol. Use GpioConfigureOutputsAsync instead.")]
        public Task SetGpioPushPullAsync(Gpios bits)
        {
            return UnsupportedSimpleProtocolCall(nameof(SetGpioPushPullAsync));
        }

        [Obsolete("GpioSetOpenDrain is part of the TWN4 App/API SYSFUNC interface and has no equivalent command in the stock Simple Protocol. Use GpioConfigureOutputsAsync instead.")]
        public Task SetGpioOpenDrainAsync(Gpios bits)
        {
            return UnsupportedSimpleProtocolCall(nameof(SetGpioOpenDrainAsync));
        }

        [Obsolete("GpioWriteBits is part of the TWN4 App/API SYSFUNC interface and has no atomic equivalent in the stock Simple Protocol. Use GpioSetBitsAsync, GpioClearBitsAsync and GpioToggleBitsAsync instead.")]
        public Task WriteGpioBitsAsync(Gpios setbits, Gpios clearbits, Gpios togglebits)
        {
            return UnsupportedSimpleProtocolCall(nameof(WriteGpioBitsAsync));
        }

        [Obsolete("SerialSetMode is part of the TWN4 App/API SYSFUNC interface and is not a stock Simple Protocol PERIPH command. Use the API IO Simple Protocol functions where appropriate.")]
        public Task SetSerialModeAsync(SerialMode mode, uint baudrate)
        {
            return UnsupportedSimpleProtocolCall(nameof(SetSerialModeAsync));
        }

        [Obsolete("SerialWrite is part of the TWN4 App/API SYSFUNC interface and is not a stock Simple Protocol PERIPH command. Use the API IO Simple Protocol functions where appropriate.")]
        public Task SerialWriteAsync(byte[] data)
        {
            return UnsupportedSimpleProtocolCall(nameof(SerialWriteAsync));
        }

        [Obsolete("SerialRead is part of the TWN4 App/API SYSFUNC interface and is not a stock Simple Protocol PERIPH command. Use the API IO Simple Protocol functions where appropriate.")]
        public Task<byte[]> SerialReadAsync(byte maxBytes)
        {
            return UnsupportedSimpleProtocolCall<byte[]>(nameof(SerialReadAsync));
        }

        [Obsolete("SerialReadWrite is part of the TWN4 App/API SYSFUNC interface and is not a stock Simple Protocol PERIPH command. Use the API IO Simple Protocol functions where appropriate.")]
        public Task<byte[]> SerialReadWriteAsync(byte[] writeData, byte readMaxBytes)
        {
            return UnsupportedSimpleProtocolCall<byte[]>(nameof(SerialReadWriteAsync));
        }

        [Obsolete("PlaySound is part of the TWN4 App/API SYSFUNC interface and is not a stock Simple Protocol command. Use BeepAsync instead.")]
        public Task PlaySoundAsync(short duration)
        {
            return UnsupportedSimpleProtocolCall(nameof(PlaySoundAsync));
        }

        [Obsolete("PlaySound1 is part of the TWN4 App/API SYSFUNC interface and is not a stock Simple Protocol command. Use BeepAsync instead.")]
        public Task PlaySound1Async(short duration)
        {
            return UnsupportedSimpleProtocolCall(nameof(PlaySound1Async));
        }

        [Obsolete("PlaySound2 is part of the TWN4 App/API SYSFUNC interface and is not a stock Simple Protocol command. Use BeepAsync instead.")]
        public Task PlaySound2Async(short duration)
        {
            return UnsupportedSimpleProtocolCall(nameof(PlaySound2Async));
        }

        [Obsolete("PlaySound4 is part of the TWN4 App/API SYSFUNC interface and is not a stock Simple Protocol command. Use BeepAsync instead.")]
        public Task PlaySound4Async(short tone1Duration, short tone2Duration)
        {
            return UnsupportedSimpleProtocolCall(nameof(PlaySound4Async));
        }

        [Obsolete("The TWN4 App/API PlayMelody SYSFUNC is not a stock Simple Protocol command. Use the high-level PlayMelody helper instead.")]
        public Task PlayMelodyAsync(byte[] tones, byte[] durations)
        {
            return UnsupportedSimpleProtocolCall(nameof(PlayMelodyAsync));
        }

        [Obsolete("The TWN4 App/API PlayMelody SYSFUNC is not a stock Simple Protocol command. Use the high-level PlayMelody helper instead.")]
        public Task PlayMelodyAsync(byte[] tones, byte[] durations, byte repeatCount)
        {
            return UnsupportedSimpleProtocolCall(nameof(PlayMelodyAsync));
        }

        /// <summary>
        /// Compatibility alias for stopping a currently active beeper.
        /// </summary>
        public Task StopSoundAsync()
        {
            return BeepOffAsync();
        }

        private static Task UnsupportedSimpleProtocolCall(string methodName)
        {
            throw new NotSupportedException(methodName + " is not available in the stock TWN4 Simple Protocol.");
        }

        private static Task<T> UnsupportedSimpleProtocolCall<T>(string methodName)
        {
            throw new NotSupportedException(methodName + " is not available in the stock TWN4 Simple Protocol.");
        }

        #endregion

        #endregion
    }
}
