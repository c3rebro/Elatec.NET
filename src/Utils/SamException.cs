namespace Elatec.NET.Sam
{
    /// <summary>
    /// A SAM operation failed, e.g. because the SAM returned an error status word or a response failed verification.
    /// </summary>
    public class SamException : ReaderException
    {
        /// <summary>The status word returned by the SAM, 0 if the failure is not related to a status word.</summary>
        public ushort StatusWord { get; }

        public SamException(string message) : base(message)
        {
        }

        public SamException(string message, ushort statusWord) : base(message)
        {
            StatusWord = statusWord;
        }
    }
}
