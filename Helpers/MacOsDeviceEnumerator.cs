using System.Collections.Generic;
using System.Diagnostics;
using Elatec.NET.Interfaces;

namespace Elatec.NET
{
    /// <summary>
    /// Placeholder for future macOS support using serial port scanning.
    /// </summary>
    public class MacOsDeviceEnumerator : IDeviceEnumerator
    {
        public List<TWN4ReaderDevice> GetAvailableReaders()
        {
            Trace.TraceInformation("macOS device enumeration is not implemented yet. Returning an empty list.");
            return new List<TWN4ReaderDevice>();
        }
    }
}
