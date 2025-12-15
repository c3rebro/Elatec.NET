using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Elatec.NET.Tests
{
    public class WindowsDeviceEnumeratorTests
    {
        [Fact]
        public void FindUsbDevices_ValidatesArguments()
        {
            var enumerator = new WindowsDeviceEnumerator();

            Assert.Throws<ArgumentException>(() => enumerator.FindUsbDevices(null, "vid", "pid"));
            Assert.Throws<ArgumentException>(() => enumerator.FindUsbDevices("svc", null, "pid"));
            Assert.Throws<ArgumentException>(() => enumerator.FindUsbDevices("svc", "vid", null));
        }

        [Fact]
        public void FilterDevices_ReturnsOnlyMatchingUsbDevices()
        {
            var values = new object[]
            {
                "USB\\VID_09D8&PID_0420\\7&123456&0&3",
                "USB\\VID_09D8&PID_0420\\7&9999&0&4",
                "USB\\VID_0000&PID_0000\\1&1&0&1",
                42,
                null
            };

            var matches = WindowsDeviceEnumerator.FilterDevices(values, "USB\\VID_09D8&PID_0420\\").ToList();

            Assert.Equal(2, matches.Count);
            Assert.All(matches, match => Assert.StartsWith("USB\\VID_09D8&PID_0420\\", match));
        }

        [Fact]
        public void FilterDevices_IgnoresNullCollections()
        {
            var matches = WindowsDeviceEnumerator.FilterDevices(null, "USB\\VID_09D8&PID_0420\\").ToList();

            Assert.Empty(matches);
        }
    }
}
