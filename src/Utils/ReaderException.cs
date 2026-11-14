using System;

namespace Elatec.NET
{
    /// <summary>
    /// Exception thrown when a call to the reader or to a card fails. <see cref="ErrorNumber"/> tells the failure categories apart.
    /// </summary>
    /// <remarks>
    /// <see cref="ErrorNumber"/> is chosen by the library, it is not read from the firmware. The Simple Protocol reports a card
    /// command only as success or failure, and GetLastError does not deliver card errors. A refused card command is therefore
    /// reported with the generic <see cref="ReaderError.AccessDenied"/>, for example a write to a page that does not exist as well as a
    /// wrong password. <see cref="ReaderError.NotOpen"/> and <see cref="ReaderError.NotInit"/> mean that the reader or its port
    /// could not be used.
    /// </remarks>
    public class ReaderException : Exception
    {
        /// <summary>
        /// The error the library reports for the failure, see the remarks of <see cref="ReaderException"/>.
        /// </summary>
        public ReaderError ErrorNumber { get; internal set; }

        public ReaderException()
        {
        }

        public ReaderException(string message) : base(message)
        {
        }

        public ReaderException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public ReaderException(string message, ReaderError errorNumber) : base(message)
        {
            ErrorNumber = errorNumber;
        }

        public ReaderException(string message, ReaderError errorNumber, Exception innerException) : base(message, innerException)
        {
            ErrorNumber = errorNumber;
        }

        /// <summary>
        /// Creates the exception for a failed call. The message and <see cref="ErrorNumber"/> are both derived from <paramref name="error"/>.
        /// </summary>
        internal static ReaderException For(ReaderError error, Exception innerException = null)
        {
            return new ReaderException("Call was not successfull, error " + error, error, innerException);
        }

    }
}
