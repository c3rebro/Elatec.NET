using System;
using System.Collections.Generic;

namespace Elatec.NET
{
    /// <summary>
    /// Parse results for Simple Protocol binary frames (length-prefixed, optional CRC).
    /// See APIDocRev28 section 38.5 for Simple Protocol mode details.
    /// </summary>
    public enum SimpleProtocolFrameStatus
    {
        /// <summary>
        /// Frame parsed successfully.
        /// </summary>
        Ok = 0,

        /// <summary>
        /// Not enough bytes have been received to parse the complete frame.
        /// </summary>
        IncompleteFrame = 1,

        /// <summary>
        /// The length prefix is inconsistent with the available buffer.
        /// </summary>
        InvalidLengthPrefix = 2,

        /// <summary>
        /// CRC validation failed for the received payload.
        /// </summary>
        InvalidChecksum = 3,
    }

    /// <summary>
    /// Represents a parsed Simple Protocol binary frame payload.
    /// </summary>
    public readonly struct SimpleProtocolFrame
    {
        public SimpleProtocolFrame(byte[] payload)
        {
            Payload = payload ?? throw new ArgumentNullException(nameof(payload));
        }

        /// <summary>
        /// Raw payload bytes (length prefix and CRC removed).
        /// </summary>
        public byte[] Payload { get; }
    }

    /// <summary>
    /// Helpers for Simple Protocol binary framing (length prefix + optional CRC).
    /// The CRC algorithm is based on the Simple Protocol framing described in DocRev25 (1.3.3) and
    /// referenced by APIDocRev28 section 38.5 (Simple Protocol).
    /// </summary>
    public static class SimpleProtocolFrameCodec
    {
        private const int LengthPrefixSize = 2;
        private const int CrcSize = 2;

        /// <summary>
        /// Build a length-prefixed Simple Protocol binary frame with optional CRC.
        /// </summary>
        /// <param name="payload">Payload bytes to send.</param>
        /// <param name="includeCrc">When true, append the Simple Protocol CRC after the payload.</param>
        /// <returns>Framed payload with length prefix and optional CRC.</returns>
        public static byte[] BuildBinaryFrame(byte[] payload, bool includeCrc)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            if (payload.Length > ushort.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(payload), "Payload too large for Simple Protocol length prefix.");

            var frame = new List<byte>(LengthPrefixSize + payload.Length + (includeCrc ? CrcSize : 0));
            frame.AddUInt16((ushort)payload.Length);
            frame.AddRange(payload);

            if (includeCrc)
            {
                var crc = ComputeCrc(payload);
                frame.AddUInt16(crc);
            }

            return frame.ToArray();
        }

        /// <summary>
        /// Try to parse a single length-prefixed Simple Protocol binary frame from the buffer.
        /// </summary>
        /// <param name="buffer">Buffer containing at least one frame.</param>
        /// <param name="hasCrc">Whether the frame is expected to include a CRC.</param>
        /// <param name="frame">The parsed frame if successful.</param>
        /// <param name="bytesConsumed">Number of bytes consumed from the buffer.</param>
        /// <returns>Status describing the parsing outcome.</returns>
        public static SimpleProtocolFrameStatus TryParseBinaryFrame(
            IReadOnlyList<byte> buffer,
            bool hasCrc,
            out SimpleProtocolFrame frame,
            out int bytesConsumed)
        {
            frame = default;
            bytesConsumed = 0;
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));

            if (buffer.Count < LengthPrefixSize)
            {
                return SimpleProtocolFrameStatus.IncompleteFrame;
            }

            var payloadLength = (ushort)(buffer[0] | (buffer[1] << 8));
            var totalLength = LengthPrefixSize + payloadLength + (hasCrc ? CrcSize : 0);
            if (totalLength < LengthPrefixSize)
            {
                return SimpleProtocolFrameStatus.InvalidLengthPrefix;
            }

            if (buffer.Count < totalLength)
            {
                return SimpleProtocolFrameStatus.IncompleteFrame;
            }

            var payload = new byte[payloadLength];
            for (var i = 0; i < payloadLength; i++)
            {
                payload[i] = buffer[LengthPrefixSize + i];
            }

            if (hasCrc)
            {
                var crcOffset = LengthPrefixSize + payloadLength;
                var receivedCrc = (ushort)(buffer[crcOffset] | (buffer[crcOffset + 1] << 8));
                var expectedCrc = ComputeCrc(payload);
                if (receivedCrc != expectedCrc)
                {
                    bytesConsumed = totalLength;
                    return SimpleProtocolFrameStatus.InvalidChecksum;
                }
            }

            frame = new SimpleProtocolFrame(payload);
            bytesConsumed = totalLength;
            return SimpleProtocolFrameStatus.Ok;
        }

        /// <summary>
        /// Try to parse multiple frames from a buffer, useful when responses arrive back-to-back.
        /// </summary>
        /// <param name="buffer">Buffer containing one or more frames.</param>
        /// <param name="hasCrc">Whether the frames are expected to include a CRC.</param>
        /// <param name="frames">Parsed frames in order.</param>
        /// <param name="bytesConsumed">Total number of bytes consumed from the buffer.</param>
        /// <returns>Status describing the parsing outcome.</returns>
        public static SimpleProtocolFrameStatus TryParseBinaryFrames(
            IReadOnlyList<byte> buffer,
            bool hasCrc,
            out List<SimpleProtocolFrame> frames,
            out int bytesConsumed)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));

            frames = new List<SimpleProtocolFrame>();
            bytesConsumed = 0;

            while (bytesConsumed < buffer.Count)
            {
                var slice = new BufferSlice(buffer, bytesConsumed);
                var status = TryParseBinaryFrame(slice, hasCrc, out var frame, out var consumed);
                if (status != SimpleProtocolFrameStatus.Ok)
                {
                    return status;
                }

                frames.Add(frame);
                bytesConsumed += consumed;
            }

            return SimpleProtocolFrameStatus.Ok;
        }

        /// <summary>
        /// Compute the CCITT CRC (reverse polynomial 0x8408) used by Simple Protocol.
        /// </summary>
        /// <param name="payload">Payload bytes to include in the CRC calculation.</param>
        /// <returns>CRC value.</returns>
        public static ushort ComputeCrc(byte[] payload)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));

            ushort crc = 0xFFFF;
            foreach (var value in payload)
            {
                crc = UpdateCrc(crc, value);
            }

            return crc;
        }

        private static ushort UpdateCrc(ushort crc, byte value)
        {
            var data = (byte)(value ^ crc);
            data ^= (byte)(data << 4);
            return (ushort)(((data << 8) | (crc >> 8)) ^ (data >> 4) ^ (data << 3));
        }

        private readonly struct BufferSlice : IReadOnlyList<byte>
        {
            private readonly IReadOnlyList<byte> _buffer;
            private readonly int _offset;

            public BufferSlice(IReadOnlyList<byte> buffer, int offset)
            {
                _buffer = buffer;
                _offset = offset;
            }

            public int Count => _buffer.Count - _offset;

            public byte this[int index] => _buffer[_offset + index];

            public IEnumerator<byte> GetEnumerator()
            {
                for (var i = 0; i < Count; i++)
                {
                    yield return _buffer[_offset + i];
                }
            }

            global::System.Collections.IEnumerator global::System.Collections.IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }
    }
}
