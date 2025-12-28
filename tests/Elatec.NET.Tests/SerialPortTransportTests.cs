using System;
using System.IO.Ports;
using System.Threading.Tasks;
using Elatec.NET.System;
using Xunit;

namespace Elatec.NET.Tests
{
    public class SerialPortTransportTests
    {
        [Fact]
        public async Task ConnectAsync_OpensPortOnce()
        {
            var adapter = new FakeSerialPortAdapter();
            using var transport = new SerialPortTransport(adapter);

            await transport.ConnectAsync();
            await transport.ConnectAsync();

            Assert.True(adapter.IsOpen);
            Assert.Equal(1, adapter.OpenCallCount);
        }

        [Fact]
        public async Task DisconnectAsync_ClosesAndDiscardsBuffers()
        {
            var adapter = new FakeSerialPortAdapter();
            using var transport = new SerialPortTransport(adapter);

            await transport.ConnectAsync();
            await transport.DisconnectAsync();

            Assert.False(adapter.IsOpen);
            Assert.Equal(1, adapter.DiscardInBufferCallCount);
            Assert.Equal(1, adapter.DiscardOutBufferCallCount);
            Assert.Equal(1, adapter.CloseCallCount);
        }

        [Fact]
        public async Task Dispose_ClosesOpenPort()
        {
            var adapter = new FakeSerialPortAdapter();
            var transport = new SerialPortTransport(adapter);

            await transport.ConnectAsync();
            transport.Dispose();

            Assert.False(adapter.IsOpen);
            Assert.Equal(1, adapter.DiscardInBufferCallCount);
            Assert.Equal(1, adapter.DiscardOutBufferCallCount);
            Assert.Equal(1, adapter.CloseCallCount);
        }

        [Fact]
        public async Task ConnectAsync_ThrowsWhenDisposed()
        {
            var adapter = new FakeSerialPortAdapter();
            var transport = new SerialPortTransport(adapter);

            transport.Dispose();

            await Assert.ThrowsAsync<ObjectDisposedException>(() => transport.ConnectAsync());
        }

        private sealed class FakeSerialPortAdapter : ISerialPortAdapter
        {
            public string PortName { get; set; } = "COM1";

            public int BaudRate { get; set; }

            public int DataBits { get; set; }

            public StopBits StopBits { get; set; }

            public Parity Parity { get; set; }

            public string NewLine { get; set; } = "\r";

            public int ReadTimeout { get; set; }

            public int WriteTimeout { get; set; }

            public bool IsOpen { get; private set; }

            public int OpenCallCount { get; private set; }

            public int CloseCallCount { get; private set; }

            public int DiscardInBufferCallCount { get; private set; }

            public int DiscardOutBufferCallCount { get; private set; }

#pragma warning disable CS0067
            public event SerialErrorReceivedEventHandler ErrorReceived;
#pragma warning restore CS0067

            public void Open()
            {
                OpenCallCount++;
                IsOpen = true;
            }

            public void Close()
            {
                CloseCallCount++;
                IsOpen = false;
            }

            public void DiscardInBuffer()
            {
                DiscardInBufferCallCount++;
            }

            public void DiscardOutBuffer()
            {
                DiscardOutBufferCallCount++;
            }

            public string ReadLine()
            {
                return "OK";
            }

            public void WriteLine(string data)
            {
            }

            public void Dispose()
            {
                IsOpen = false;
            }
        }
    }
}
