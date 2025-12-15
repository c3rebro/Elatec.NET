using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using Elatec.NET.Interfaces;

namespace Elatec.NET.Tests
{
    /// <summary>
    ///     Minimal mock implementation of <see cref="IReaderTransport"/> to exercise protocol behavior without hardware.
    ///     Responses are provided as Simple Protocol hex strings so they can be parsed via the production converter helpers.
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

        /// <summary>
        /// Enqueue a response using raw bytes that will be converted to the PRS hex representation expected by the reader.
        /// </summary>
        /// <param name="bytes">Bytes that should be returned on the next read.</param>
        public void QueueResponseBytes(params byte[] bytes)
        {
            Responses.Enqueue(ToHex(bytes));
        }

        private static string ToHex(IEnumerable<byte> bytes)
        {
            var builder = new StringBuilder();
            foreach (var value in bytes)
            {
                builder.Append(value.ToString("X2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }
    }
}
