using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Elatec.NET.Helpers.ByteArrayHelper.Extensions;

namespace Elatec.NET
{
    public partial class TWN4ReaderDevice
    {
        #region API_IO / Host I/O Functions

        public static readonly byte API_IO = 1;

        /// <summary>
        /// Writes a single byte to the requested host channel.
        /// </summary>
        /// <param name="channel">Communication endpoint to write to.</param>
        /// <param name="value">Byte value to send.</param>
        public async Task WriteByteAsync(IoChannel channel, byte value)
        {
            await CallFunctionAsync(new byte[] { API_IO, 0, (byte)channel, value });
        }

        /// <summary>
        /// Reads a single byte from the requested host channel.
        /// </summary>
        /// <param name="channel">Communication endpoint to read from.</param>
        /// <returns>The byte read from the reader buffer.</returns>
        public async Task<byte> ReadByteAsync(IoChannel channel)
        {
            var parser = await CallFunctionAsync(new byte[] { API_IO, 1, (byte)channel });
            return parser.ParseByte();
        }

        /// <summary>
        /// Checks whether the requested channel buffer is empty for the given direction.
        /// </summary>
        /// <param name="channel">Communication endpoint to inspect.</param>
        /// <param name="direction">Direction of the buffer to test.</param>
        /// <returns><see langword="true"/> if the buffer is empty; otherwise <see langword="false"/>.</returns>
        public async Task<bool> TestEmptyAsync(IoChannel channel, IoDirection direction)
        {
            var parser = await CallFunctionAsync(new byte[] { API_IO, 2, (byte)channel, (byte)direction });
            return parser.ParseBool();
        }

        /// <summary>
        /// Checks whether the requested channel buffer is already full for the given direction.
        /// </summary>
        /// <param name="channel">Communication endpoint to inspect.</param>
        /// <param name="direction">Direction of the buffer to test.</param>
        /// <returns><see langword="true"/> if the buffer is full; otherwise <see langword="false"/>.</returns>
        public async Task<bool> TestFullAsync(IoChannel channel, IoDirection direction)
        {
            var parser = await CallFunctionAsync(new byte[] { API_IO, 3, (byte)channel, (byte)direction });
            return parser.ParseBool();
        }

        /// <summary>
        /// Retrieves the configured buffer size for the selected channel and direction.
        /// </summary>
        /// <param name="channel">Communication endpoint to query.</param>
        /// <param name="direction">Direction of the buffer to query.</param>
        /// <returns>The buffer size in bytes.</returns>
        public async Task<ushort> GetBufferSizeAsync(IoChannel channel, IoDirection direction)
        {
            var parser = await CallFunctionAsync(new byte[] { API_IO, 4, (byte)channel, (byte)direction });
            return parser.ParseUInt16();
        }

        /// <summary>
        /// Retrieves the number of bytes currently stored in the requested channel buffer.
        /// </summary>
        /// <param name="channel">Communication endpoint to query.</param>
        /// <param name="direction">Direction of the buffer to query.</param>
        /// <returns>The amount of buffered data in bytes.</returns>
        public async Task<ushort> GetByteCountAsync(IoChannel channel, IoDirection direction)
        {
            var parser = await CallFunctionAsync(new byte[] { API_IO, 5, (byte)channel, (byte)direction });
            return parser.ParseUInt16();
        }

        /// <summary>
        /// Applies serial framing parameters for the selected COM channel and direction.
        /// </summary>
        /// <param name="channel">Communication endpoint to configure.</param>
        /// <param name="direction">Direction for which the parameters apply.</param>
        /// <param name="baudRate">Baud rate to configure.</param>
        /// <param name="parity">Parity constant as defined by the TWN4 firmware.</param>
        /// <param name="stopBits">Stop bits constant as defined by the TWN4 firmware.</param>
        /// <param name="dataBits">Number of data bits.</param>
        /// <returns><see langword="true"/> if the configuration was accepted; otherwise <see langword="false"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="dataBits"/> is zero.</exception>
        public async Task<bool> SetComParametersAsync(IoChannel channel, IoDirection direction, uint baudRate, byte parity, byte stopBits, byte dataBits)
        {
            if (dataBits == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(dataBits));
            }

            var payload = new List<byte> { API_IO, 6, (byte)channel, (byte)direction };
            payload.AddUInt32(baudRate);
            payload.Add(parity);
            payload.Add(stopBits);
            payload.Add(dataBits);

            var parser = await CallFunctionAsync(payload.ToArray());
            return parser.ParseBool();
        }

        /// <summary>
        /// Returns the current USB device state.
        /// </summary>
        /// <returns>The USB device state reported by the firmware.</returns>
        public async Task<UsbDeviceState> GetUsbDeviceStateAsync()
        {
            var parser = await CallFunctionAsync(new byte[] { API_IO, 7 });
            return (UsbDeviceState)parser.ParseByte();
        }

        /// <summary>
        /// Returns the host channel the reader currently uses for upstream communication.
        /// </summary>
        /// <returns>The configured host channel.</returns>
        public async Task<IoChannel> GetHostChannelAsync()
        {
            var parser = await CallFunctionAsync(new byte[] { API_IO, 8 });
            return (IoChannel)parser.ParseByte();
        }

        /// <summary>
        /// Triggers a remote wakeup on the USB host.
        /// </summary>
        public async Task UsbRemoteWakeupAsync()
        {
            await CallFunctionAsync(new byte[] { API_IO, 9 });
        }

        /// <summary>
        /// Writes multiple bytes to the requested host channel.
        /// </summary>
        /// <param name="channel">Communication endpoint to write to.</param>
        /// <param name="data">Payload to send.</param>
        /// <returns><see langword="true"/> when the reader acknowledged the frame; otherwise <see langword="false"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="data"/> is <see langword="null"/>.</exception>
        public async Task<bool> WriteBytesAsync(IoChannel channel, byte[] data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            var payload = new List<byte> { API_IO, 10, (byte)channel, (byte)data.Length };
            payload.AddRange(data);
            var parser = await CallFunctionAsync(payload.ToArray());
            return parser.ParseBool();
        }

        /// <summary>
        /// Reads the requested number of bytes from the specified host channel.
        /// </summary>
        /// <param name="channel">Communication endpoint to read from.</param>
        /// <param name="length">Maximum number of bytes to read.</param>
        /// <returns>Acknowledgement and payload returned by the reader.</returns>
        public async Task<ReadBytesResult> ReadBytesAsync(IoChannel channel, byte length)
        {
            var parser = await CallFunctionAsync(new byte[] { API_IO, 11, (byte)channel, length });
            var acknowledged = parser.ParseBool();
            var data = parser.ParseFlexByteArray();

            return new ReadBytesResult
            {
                Acknowledged = acknowledged,
                Data = data
            };
        }

        public class ReadBytesResult
        {
            /// <summary>
            /// Indicates whether the firmware accepted the request.
            /// </summary>
            public bool Acknowledged { get; set; }

            /// <summary>
            /// Data returned by the reader.
            /// </summary>
            public byte[] Data { get; set; }
        }

        #endregion
    }
}
