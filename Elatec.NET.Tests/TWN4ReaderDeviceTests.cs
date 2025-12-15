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
    }
}
