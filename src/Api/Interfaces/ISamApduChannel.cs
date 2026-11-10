using System.Threading.Tasks;
using Elatec.NET.Sam;

namespace Elatec.NET.Interfaces
{
    /// <summary>
    /// A way to exchange APDUs with an activated SAM, e.g. <see cref="Twn4SamApduChannel"/> for a SAM
    /// in a slot of a TWN4 reader. Keeps the SAM protocol code independent from the transport.
    /// </summary>
    public interface ISamApduChannel
    {
        /// <summary>
        /// Sends a command APDU to the SAM and returns its response.
        /// </summary>
        /// <exception cref="SamException">The APDU could not be exchanged with the SAM.</exception>
        Task<SamApduResponse> TransmitAsync(SamApdu command);
    }
}
