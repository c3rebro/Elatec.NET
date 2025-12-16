using System.Threading.Tasks;
using Xunit;

namespace Elatec.NET.Tests
{
    public class TWN4ReaderDeviceTests
    {
        [Fact]
        public async Task CallFunctionRawAsync_UsesTransportToSendAndReceive()
        {
            var fakeTransport = new FakeReaderTransport("COM1");
            fakeTransport.Responses.Enqueue("0001");
            var device = new TWN4ReaderDevice("COM1", _ => fakeTransport);

            var response = await device.CallFunctionRawAsync(new byte[] { 0xAA });

            Assert.True(fakeTransport.ConnectCalled);
            Assert.Single(fakeTransport.WrittenLines, "AA");
            Assert.Equal(new byte[] { 0x00, 0x01 }, response);
        }

        [Fact]
        public async Task DisconnectAsync_InvokesTransportClose()
        {
            var fakeTransport = new FakeReaderTransport("COM2");
            fakeTransport.Responses.Enqueue("00");
            var device = new TWN4ReaderDevice("COM2", _ => fakeTransport);

            await device.CallFunctionRawAsync(new byte[] { 0x00 });
            var result = await device.DisconnectAsync();

            Assert.True(result);
            Assert.False(fakeTransport.IsOpen);
        }

        [Fact]
        public async Task GetVersionStringAsync_ParsesResponseAndSetsLegicFlag()
        {
            const string versionString = "FW/1.0/B1";
            var fakeTransport = new FakeReaderTransport("COM3");
            fakeTransport.QueueResponseBytes(0x00, (byte)versionString.Length,
                0x46, 0x57, 0x2F, 0x31, 0x2E, 0x30, 0x2F, 0x42, 0x31);

            var device = new TWN4ReaderDevice("COM3", _ => fakeTransport);

            var result = await device.GetVersionStringAsync();

            Assert.Equal("0004FF", Assert.Single(fakeTransport.WrittenLines));
            Assert.Equal(versionString, result);
            Assert.True(device.IsTWN4LegicReader);
        }

        [Fact]
        public async Task GetBufferSizeAsync_UsesIoApiAndParsesUInt16()
        {
            var transport = new FakeReaderTransport("COM4");
            transport.QueueResponseBytes(0x00, 0x10, 0x27);
            var device = new TWN4ReaderDevice("COM4", _ => transport);

            var result = await device.GetBufferSizeAsync(IoChannel.Com1, IoDirection.In);

            Assert.Equal((ushort)10000, result);
            Assert.Single(transport.WrittenLines, "01040101");
        }

        [Fact]
        public async Task WriteBytesAsync_SendsPayloadLength()
        {
            var transport = new FakeReaderTransport("COM5");
            transport.QueueResponseBytes(0x00, 0x01);
            var device = new TWN4ReaderDevice("COM5", _ => transport);

            var acked = await device.WriteBytesAsync(IoChannel.Usb, new byte[] { 0xAA, 0xBB, 0xCC });

            Assert.True(acked);
            Assert.Single(transport.WrittenLines, "010A0003AABBCC");
        }

        [Fact]
        public async Task ReadBytesAsync_ParsesAcknowledgementAndPayload()
        {
            var transport = new FakeReaderTransport("COM6");
            transport.QueueResponseBytes(0x00, 0x01, 0x02, 0xAA, 0xBB);
            var device = new TWN4ReaderDevice("COM6", _ => transport);

            var result = await device.ReadBytesAsync(IoChannel.Usb, 0x02);

            Assert.True(result.Acknowledged);
            Assert.Equal(new byte[] { 0xAA, 0xBB }, result.Data);
            Assert.Single(transport.WrittenLines, "010B0002");
        }

        [Fact]
        public async Task SleepAsync_UsesSysApi()
        {
            var transport = new FakeReaderTransport("COM7");
            transport.QueueResponseBytes(0x00, 0x01);
            var device = new TWN4ReaderDevice("COM7", _ => transport);

            var result = await device.SleepAsync(5, 1);

            Assert.Equal(0x01, result);
            Assert.Single(transport.WrittenLines, "00070500000001000000");
        }
    }
}
