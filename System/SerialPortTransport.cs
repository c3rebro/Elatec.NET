using System;
using System.IO;
using System.IO.Ports;
using System.Threading.Tasks;
using Elatec.NET.Interfaces;

namespace Elatec.NET
{
    /// <summary>
    /// Serial port based implementation of <see cref="IReaderTransport"/>.
    /// </summary>
    public class SerialPortTransport : IReaderTransport
    {
        private readonly SerialPort _serialPort;

        /// <summary>
        /// Initializes a new instance of the <see cref="SerialPortTransport"/> class for the given port.
        /// </summary>
        /// <param name="portName">Name of the serial port used to reach the reader.</param>
        public SerialPortTransport(string portName)
        {
            if (string.IsNullOrWhiteSpace(portName))
            {
                throw new ArgumentException("Port name must be provided.", nameof(portName));
            }

            _serialPort = new SerialPort
            {
                PortName = portName,
                BaudRate = 9600,
                DataBits = 8,
                StopBits = StopBits.One,
                Parity = Parity.None,
                NewLine = "\r"
            };

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
            await Task.Run(() =>
            {
                if (!_serialPort.IsOpen)
                {
                    _serialPort.Open();
                }
            }).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task DisconnectAsync()
        {
            await Task.Run(() =>
            {
                if (_serialPort.IsOpen)
                {
                    _serialPort.DiscardInBuffer();
                    _serialPort.DiscardOutBuffer();
                    _serialPort.Close();
                }
            }).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public void DiscardInBuffer()
        {
            _serialPort.DiscardInBuffer();
        }

        /// <inheritdoc />
        public void DiscardOutBuffer()
        {
            _serialPort.DiscardOutBuffer();
        }

        /// <inheritdoc />
        public async Task<string> ReadLineAsync()
        {
            return await Task.Run(() => _serialPort.ReadLine()).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task WriteLineAsync(string data)
        {
            await Task.Run(() => _serialPort.WriteLine(data)).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _serialPort.ErrorReceived -= OnErrorReceived;

            if (_serialPort.IsOpen)
            {
                _serialPort.Close();
            }

            _serialPort.Dispose();
        }

        private void OnErrorReceived(object sender, SerialErrorReceivedEventArgs e)
        {
            ErrorReceived?.Invoke(this, new IOException($"Serial port error: {e.EventType}"));
        }
    }
}
