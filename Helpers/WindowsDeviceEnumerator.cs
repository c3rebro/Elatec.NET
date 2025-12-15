using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Win32;
using Elatec.NET.Interfaces;

namespace Elatec.NET
{
    public class WindowsDeviceEnumerator : IDeviceEnumerator
    {
        public static readonly string VendorIdElatec = "09D8";
        public static readonly string ProductIdTWN4MultiTech2 = "0420";
        public static readonly string ServiceUsbSerial = "usbser";

        public List<TWN4ReaderDevice> GetAvailableReaders()
        {
            var readers = new List<TWN4ReaderDevice>();

            foreach (string deviceInstanceId in FindUsbDevices(ServiceUsbSerial, VendorIdElatec, ProductIdTWN4MultiTech2))
            {
                var portName = RegQuerySZ($"SYSTEM\\CurrentControlSet\\Enum\\{deviceInstanceId}\\Device Parameters", "PortName");
                if (string.IsNullOrWhiteSpace(portName))
                {
                    Trace.TraceWarning($"Port name not found for device '{deviceInstanceId}'. Ensure the TWN4 drivers are installed.");
                    continue;
                }

                var reader = new TWN4ReaderDevice(portName);
                readers.Add(reader);
            }

            return readers;
        }

        internal List<string> FindUsbDevices(string service, string vendorId, string productId)
        {
            if (string.IsNullOrWhiteSpace(service))
            {
                throw new ArgumentException("Service must be specified.", nameof(service));
            }

            if (string.IsNullOrWhiteSpace(vendorId))
            {
                throw new ArgumentException("Vendor ID must be specified.", nameof(vendorId));
            }

            if (string.IsNullOrWhiteSpace(productId))
            {
                throw new ArgumentException("Product ID must be specified.", nameof(productId));
            }

            RegistryKey registryKey = null;
            var devices = new List<string>();
            string devicePath = $"USB\\VID_{vendorId}&PID_{productId}\\";

            try
            {
                registryKey = Registry.LocalMachine.OpenSubKey($"SYSTEM\\CurrentControlSet\\Services\\{service}\\Enum");
                if (registryKey == null)
                {
                    Trace.TraceWarning($"USB serial driver for '{service}' was not found. Ensure the TWN4 drivers are installed.");
                    return devices;
                }

                IEnumerable<object> registryValues = registryKey.GetValueNames().Select(name => registryKey.GetValue(name));
                devices.AddRange(FilterDevices(registryValues, devicePath));
            }
            finally
            {
                registryKey?.Close();

            }

            return devices;
        }

        internal static IEnumerable<string> FilterDevices(IEnumerable<object> registryValues, string devicePathPrefix)
        {
            if (registryValues == null || string.IsNullOrEmpty(devicePathPrefix))
            {
                yield break;
            }

            foreach (object value in registryValues)
            {
                if (value is string device && device.StartsWith(devicePathPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    yield return device;
                }
            }
        }

        private static string RegQuerySZ(string subKeyName, string valueName)
        {
            RegistryKey registryKey = Registry.LocalMachine.OpenSubKey(subKeyName);
            if (registryKey == null)
            {
                return null;
            }

            object value = registryKey.GetValue(valueName);
            registryKey.Close();
            return value?.ToString();
        }
    }
}
