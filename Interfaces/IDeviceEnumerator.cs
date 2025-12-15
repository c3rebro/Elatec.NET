using System.Collections.Generic;

namespace Elatec.NET.Interfaces
{
    /// <summary>
    /// Abstraction for enumerating available reader devices on a platform.
    /// </summary>
    public interface IDeviceEnumerator
    {
        /// <summary>
        /// Discover available TWN4 reader devices.
        /// </summary>
        /// <returns>List of connected reader devices.</returns>
        List<TWN4ReaderDevice> GetAvailableReaders();
    }
}
