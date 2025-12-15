using System.Collections.Generic;
using Elatec.NET.Interfaces;

namespace Elatec.NET
{
    internal class NullDeviceEnumerator : IDeviceEnumerator
    {
        public List<TWN4ReaderDevice> GetAvailableReaders()
        {
            return new List<TWN4ReaderDevice>();
        }
    }
}
