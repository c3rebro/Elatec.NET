using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Elatec.NET.Helpers.ByteArrayHelper.Extensions;

namespace Elatec.NET
{
    public partial class TWN4ReaderDevice
    {
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