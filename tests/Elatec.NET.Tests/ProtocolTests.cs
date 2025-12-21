using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Elatec.NET;

namespace Elatec.NET.Tests
{
    public class ProtocolTests
    {
        [Fact]
        public void BuildBinaryFrame_IncludesLengthPrefixAndCrc()
        {
            var payload = new byte[] { 0x01, 0x02, 0x03 };
            var crc = SimpleProtocolFrameCodec.ComputeCrc(payload);

            var frame = SimpleProtocolFrameCodec.BuildBinaryFrame(payload, includeCrc: true);

            Assert.Equal(new byte[]
            {
                0x03, 0x00, // length prefix (LSB first)
                0x01, 0x02, 0x03,
                (byte)(crc & 0xFF),
                (byte)((crc >> 8) & 0xFF)
            }, frame);
        }

        [Fact]
        public void TryParseBinaryFrames_ParsesMultiPartResponses()
        {
            var payloadA = new byte[] { 0xAA, 0xBB };
            var payloadB = new byte[] { 0xCC };
            var frameA = SimpleProtocolFrameCodec.BuildBinaryFrame(payloadA, includeCrc: false);
            var frameB = SimpleProtocolFrameCodec.BuildBinaryFrame(payloadB, includeCrc: false);
            var buffer = new byte[frameA.Length + frameB.Length];
            Array.Copy(frameA, 0, buffer, 0, frameA.Length);
            Array.Copy(frameB, 0, buffer, frameA.Length, frameB.Length);

            var status = SimpleProtocolFrameCodec.TryParseBinaryFrames(buffer, hasCrc: false, out var frames, out var consumed);

            Assert.Equal(SimpleProtocolFrameStatus.Ok, status);
            Assert.Equal(buffer.Length, consumed);
            Assert.Equal(2, frames.Count);
            Assert.Equal(payloadA, frames[0].Payload);
            Assert.Equal(payloadB, frames[1].Payload);
        }

        [Fact]
        public void ParseUInt16LengthPrefixedByteArray_ReadsPayload()
        {
            var payload = new byte[] { 0x10, 0x11, 0x12 };
            var bytes = new List<byte>();
            bytes.AddUInt16((ushort)payload.Length);
            bytes.AddRange(payload);

            var parser = new ResponseParser(bytes);

            Assert.Equal(payload, parser.ParseUInt16LengthPrefixedByteArray());
        }

        [Fact]
        public async Task CallFunctionParserAsync_ConvertsHexAndParsesPayload()
        {
            var transport = new FakeReaderTransport("COM3");
            transport.Responses.Enqueue("00341203414243");
            var device = new TWN4ReaderDevice("COM3", _ => transport);

            var parser = await device.CallFunctionParserAsync(new byte[] { 0x10, 0x22 });

            Assert.Single(transport.WrittenLines, "1022");
            Assert.Equal(ResponseError.None, parser.ParseResponseError());
            Assert.Equal(0x1234, parser.ParseUInt16());
            Assert.Equal("ABC", parser.ParseAsciiString());
        }

        [Fact]
        public async Task CallFunctionAsync_WhenDeviceReturnsError_Throws()
        {
            var transport = new FakeReaderTransport("COM4");
            transport.Responses.Enqueue("02");
            var device = new TWN4ReaderDevice("COM4", _ => transport);

            var exception = await Assert.ThrowsAsync<TwnException>(() => device.CallFunctionAsync(new byte[] { 0x20 }));

            Assert.Equal(ResponseError.MissingParameter, exception.ErrorNumber);
            Assert.Single(transport.WrittenLines, "20");
        }

        [Theory]
        [InlineData("STD/1.00/B", true)]
        [InlineData("STD/1.00/X", false)]
        public async Task GetVersionStringAsync_SetsLegicFlagBasedOnVersion(string version, bool expectedLegic)
        {
            var encodedVersion = EncodeAsciiResponse(version);
            var transport = new FakeReaderTransport("COM5");
            transport.Responses.Enqueue(encodedVersion);
            var device = new TWN4ReaderDevice("COM5", _ => transport);

            var returnedVersion = await device.GetVersionStringAsync();

            Assert.Equal(version, returnedVersion);
            Assert.Equal(expectedLegic, device.IsTWN4LegicReader);
            Assert.Single(transport.WrittenLines, "0004FF");
        }

        private static string EncodeAsciiResponse(string value)
        {
            var bytes = global::System.Text.Encoding.ASCII.GetBytes(value);
            var response = new byte[bytes.Length + 2];
            response[0] = 0x00; // ResponseError.None
            response[1] = (byte)bytes.Length;
            Array.Copy(bytes, 0, response, 2, bytes.Length);
            return BytesToHex(response);
        }

        private static string BytesToHex(byte[] bytes)
        {
            var hex = new global::System.Text.StringBuilder();
            foreach (var value in bytes)
            {
                hex.Append(value.ToString("X2"));
            }

            return hex.ToString();
        }
    }
}
