using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Elatec.NET.Interfaces;

namespace Elatec.NET
{
    public class DeviceManager
    {
        private static IDeviceEnumerator _deviceEnumerator = CreatePlatformEnumerator();

        /// <summary>
        /// Gets or sets the platform specific device enumerator. Primarily intended for testing.
        /// </summary>
        public static IDeviceEnumerator DeviceEnumerator
        {
            get => _deviceEnumerator;
            set => _deviceEnumerator = value ?? throw new ArgumentNullException(nameof(value));
        }

        public static List<TWN4ReaderDevice> GetAvailableReaders()
        {
            return DeviceEnumerator.GetAvailableReaders() ?? new List<TWN4ReaderDevice>();
        }

        private static IDeviceEnumerator CreatePlatformEnumerator()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return new WindowsDeviceEnumerator();
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return new MacOsDeviceEnumerator();
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return new LinuxDeviceEnumerator();
            }

            return new NullDeviceEnumerator();
        }
    }
}
