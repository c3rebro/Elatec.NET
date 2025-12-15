using System.Collections.Generic;
using System.Diagnostics;
using Elatec.NET.Interfaces;

namespace Elatec.NET
{
    /// <summary>
    /// Placeholder for future Linux support using udev or serial port scanning.
    /// </summary>
    public class LinuxDeviceEnumerator : IDeviceEnumerator
    {
        public List<TWN4ReaderDevice> GetAvailableReaders()
        {
            Trace.TraceInformation("Linux device enumeration is not implemented yet. Returning an empty list.");
            return new List<TWN4ReaderDevice>();
        }
    }
}
