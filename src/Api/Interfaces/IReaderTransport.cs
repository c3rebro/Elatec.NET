using System;
using System.Threading.Tasks;

namespace Elatec.NET.Interfaces
{
    /// <summary>
    /// Provides a transport abstraction for communicating with TWN reader hardware.
    /// Implementations are responsible for connecting, reading, writing and managing timeouts.
    /// </summary>
    public interface IReaderTransport : IDisposable
    {
        /// <summary>
        /// Gets the name of the underlying port or connection.
        /// </summary>
        string PortName { get; }

        /// <summary>
        /// Gets or sets the read timeout in milliseconds.
        /// </summary>
        int ReadTimeout { get; set; }

        /// <summary>
        /// Gets or sets the write timeout in milliseconds.
        /// </summary>
        int WriteTimeout { get; set; }

        /// <summary>
        /// Gets a value indicating whether the transport is connected.
        /// </summary>
        bool IsOpen { get; }

        /// <summary>
        /// Connect to the reader transport.
        /// </summary>
        Task ConnectAsync();

        /// <summary>
        /// Disconnect from the reader transport.
        /// </summary>
        Task DisconnectAsync();

        /// <summary>
        /// Read a line of data from the transport.
        /// </summary>
        Task<string> ReadLineAsync();

        /// <summary>
        /// Write a line of data to the transport.
        /// </summary>
        Task WriteLineAsync(string data);

        /// <summary>
        /// Discard any buffered incoming data.
        /// </summary>
        void DiscardInBuffer();

        /// <summary>
        /// Discard any buffered outgoing data.
        /// </summary>
        void DiscardOutBuffer();

        /// <summary>
        /// Raised when the transport encounters a communication error.
        /// </summary>
        event EventHandler<Exception> ErrorReceived;
    }
}
