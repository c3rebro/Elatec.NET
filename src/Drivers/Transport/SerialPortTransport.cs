using System;
using System.IO;
using System.IO.Ports;
using System.Threading.Tasks;
using System.Threading;
using Elatec.NET.Interfaces;

namespace Elatec.NET.System
{
    /// <summary>
    /// Serial port based implementation of <see cref="IReaderTransport"/>.
    /// </summary>
    public class SerialPortTransport : IReaderTransport
    {
        private readonly ISerialPortAdapter _serialPort;
        private readonly SemaphoreSlim _connectionLock = new SemaphoreSlim(1, 1);
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="SerialPortTransport"/> class for the given port.
        /// </summary>
        /// <param name="portName">Name of the serial port used to reach the reader.</param>
        public SerialPortTransport(string portName)
            : this(CreateConfiguredPort(portName))
        {
        }

        internal SerialPortTransport(ISerialPortAdapter serialPort)
        {
            if (serialPort == null)
            {
                throw new ArgumentNullException(nameof(serialPort));
            }

            _serialPort = serialPort;
            _serialPort.ErrorReceived += OnErrorReceived;
        }

        /// <inheritdoc />
        public string PortName => _serialPort.PortName;

        /// <inheritdoc />
        public int ReadTimeout
        {
            get => _serialPort.ReadTimeout;
            set => _serialPort.ReadTimeout = value;
        }

        /// <inheritdoc />
        public int WriteTimeout
        {
            get => _serialPort.WriteTimeout;
            set => _serialPort.WriteTimeout = value;
        }

        /// <inheritdoc />
        public bool IsOpen => _serialPort.IsOpen;

        /// <inheritdoc />
        public event EventHandler<Exception> ErrorReceived;

        /// <inheritdoc />
        public async Task ConnectAsync()
        {
            ThrowIfDisposed();

            await _connectionLock.WaitAsync().ConfigureAwait(false);
            try
            {
                ThrowIfDisposed();
                if (!_serialPort.IsOpen)
                {
                    _serialPort.Open();
                }
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        /// <inheritdoc />
        public async Task DisconnectAsync()
        {
            ThrowIfDisposed();

            await _connectionLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_serialPort.IsOpen)
                {
                    _serialPort.DiscardInBuffer();
                    _serialPort.DiscardOutBuffer();
                    _serialPort.Close();
                }
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        /// <inheritdoc />
        public void DiscardInBuffer()
        {
            ThrowIfDisposed();
            _serialPort.DiscardInBuffer();
        }

        /// <inheritdoc />
        public void DiscardOutBuffer()
        {
            ThrowIfDisposed();
            _serialPort.DiscardOutBuffer();
        }

        /// <inheritdoc />
        public async Task<string> ReadLineAsync()
        {
            ThrowIfDisposed();
            return await Task.Run(() => _serialPort.ReadLine()).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task WriteLineAsync(string data)
        {
            ThrowIfDisposed();
            await Task.Run(() => _serialPort.WriteLine(data)).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _connectionLock.Wait();
            try
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _serialPort.ErrorReceived -= OnErrorReceived;

                if (_serialPort.IsOpen)
                {
                    _serialPort.DiscardInBuffer();
                    _serialPort.DiscardOutBuffer();
                    _serialPort.Close();
                }

                _serialPort.Dispose();
            }
            finally
            {
                _connectionLock.Release();
                _connectionLock.Dispose();
            }
        }

        private void OnErrorReceived(object sender, SerialErrorReceivedEventArgs e)
        {
            ErrorReceived?.Invoke(this, new IOException($"Serial port error: {e.EventType}"));
        }

        private static ISerialPortAdapter CreateConfiguredPort(string portName)
        {
            if (string.IsNullOrWhiteSpace(portName))
            {
                throw new ArgumentException("Port name must be provided.", nameof(portName));
            }

            return new SerialPortAdapter(portName);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(SerialPortTransport));
            }
        }
    }
}
