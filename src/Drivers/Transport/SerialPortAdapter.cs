using System;
using System.IO.Ports;

namespace Elatec.NET.System
{
    /// <summary>
    /// Provides an abstraction layer around <see cref="SerialPort"/> for testing.
    /// </summary>
    internal interface ISerialPortAdapter : IDisposable
    {
        string PortName { get; set; }

        int BaudRate { get; set; }

        int DataBits { get; set; }

        StopBits StopBits { get; set; }

        Parity Parity { get; set; }

        string NewLine { get; set; }

        int ReadTimeout { get; set; }

        int WriteTimeout { get; set; }

        bool IsOpen { get; }

        event SerialErrorReceivedEventHandler ErrorReceived;

        void Open();

        void Close();

        void DiscardInBuffer();

        void DiscardOutBuffer();

        string ReadLine();

        void WriteLine(string data);
    }

    internal sealed class SerialPortAdapter : ISerialPortAdapter
    {
        private readonly SerialPort _serialPort;

        public SerialPortAdapter(string portName)
        {
            _serialPort = new SerialPort
            {
                PortName = portName,
                BaudRate = 9600,
                DataBits = 8,
                StopBits = StopBits.One,
                Parity = Parity.None,
                NewLine = "\r"
            };
        }

        public string PortName
        {
            get => _serialPort.PortName;
            set => _serialPort.PortName = value;
        }

        public int BaudRate
        {
            get => _serialPort.BaudRate;
            set => _serialPort.BaudRate = value;
        }

        public int DataBits
        {
            get => _serialPort.DataBits;
            set => _serialPort.DataBits = value;
        }

        public StopBits StopBits
        {
            get => _serialPort.StopBits;
            set => _serialPort.StopBits = value;
        }

        public Parity Parity
        {
            get => _serialPort.Parity;
            set => _serialPort.Parity = value;
        }

        public string NewLine
        {
            get => _serialPort.NewLine;
            set => _serialPort.NewLine = value;
        }

        public int ReadTimeout
        {
            get => _serialPort.ReadTimeout;
            set => _serialPort.ReadTimeout = value;
        }

        public int WriteTimeout
        {
            get => _serialPort.WriteTimeout;
            set => _serialPort.WriteTimeout = value;
        }

        public bool IsOpen => _serialPort.IsOpen;

        public event SerialErrorReceivedEventHandler ErrorReceived
        {
            add => _serialPort.ErrorReceived += value;
            remove => _serialPort.ErrorReceived -= value;
        }

        public void Open()
        {
            _serialPort.Open();
        }

        public void Close()
        {
            _serialPort.Close();
        }

        public void DiscardInBuffer()
        {
            _serialPort.DiscardInBuffer();
        }

        public void DiscardOutBuffer()
        {
            _serialPort.DiscardOutBuffer();
        }

        public string ReadLine()
        {
            return _serialPort.ReadLine();
        }

        public void WriteLine(string data)
        {
            _serialPort.WriteLine(data);
        }

        public void Dispose()
        {
            _serialPort.Dispose();
        }
    }
}
