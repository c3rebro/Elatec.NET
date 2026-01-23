using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Elatec.NET.Interfaces;

namespace Elatec.NET.Tests
{
    /// <summary>
    /// Transport stub that detects overlapping Write/Read cycles to ensure calls are serialized.
    /// </summary>
    public class ConcurrentReaderTransport : IReaderTransport
    {
        private int _inFlight;

        public ConcurrentReaderTransport(string portName)
        {
            PortName = portName;
        }

        public string PortName { get; }

        public int ReadTimeout { get; set; }

        public int WriteTimeout { get; set; }

        public bool IsOpen { get; private set; }

        public bool OverlapDetected { get; private set; }

        public Queue<string> Responses { get; } = new Queue<string>();

        public List<string> WrittenLines { get; } = new List<string>();

        public TaskCompletionSource<bool> FirstWriteSeen { get; } = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> AllowRead { get; } = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        public event EventHandler<Exception> ErrorReceived;

        public Task ConnectAsync()
        {
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

        public async Task<string> ReadLineAsync()
        {
            await AllowRead.Task.ConfigureAwait(false);
            Interlocked.Exchange(ref _inFlight, 0);

            if (Responses.Count == 0)
            {
                throw new InvalidOperationException("No responses configured.");
            }

            return Responses.Dequeue();
        }

        public Task WriteLineAsync(string data)
        {
            if (Interlocked.CompareExchange(ref _inFlight, 1, 0) == 1)
            {
                OverlapDetected = true;
            }

            WrittenLines.Add(data);
            FirstWriteSeen.TrySetResult(true);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            IsOpen = false;
        }
    }
}
