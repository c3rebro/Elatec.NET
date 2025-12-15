using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Elatec.NET.Interfaces;

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

    /// <summary>
    /// Minimal fake transport used to validate transport interactions without hardware.
    /// </summary>
    public class FakeReaderTransport : IReaderTransport
    {
        public FakeReaderTransport(string portName)
        {
            PortName = portName;
        }

        public string PortName { get; }

        public int ReadTimeout { get; set; }

        public int WriteTimeout { get; set; }

        public bool IsOpen { get; private set; }

        public bool ConnectCalled { get; private set; }

        public Queue<string> Responses { get; } = new Queue<string>();

        public List<string> WrittenLines { get; } = new List<string>();

        public event EventHandler<Exception> ErrorReceived;

        public Task ConnectAsync()
        {
            ConnectCalled = true;
            IsOpen = true;
            return Task.CompletedTask;
        }

        public Task DisconnectAsync()
        {
            IsOpen = false;
            return Task.CompletedTask;
        }

        public void DiscardInBuffer()
        {
        }

        public void DiscardOutBuffer()
        {
        }

        public Task<string> ReadLineAsync()
        {
            if (Responses.Count == 0)
            {
                throw new InvalidOperationException("No responses configured.");
            }

            return Task.FromResult(Responses.Dequeue());
        }

        public Task WriteLineAsync(string data)
        {
            WrittenLines.Add(data);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            IsOpen = false;
        }
    }
}
