using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Elatec.NET.Tests
{
    /// <summary>
    ///     <c>DESFire_GetVersion</c> answers <c>[00][Bool: Result][Byte Array(34): Version]</c>: the 34 bytes follow the result without
    ///     a length byte. The method used to read them with a length byte, took the vendor byte <c>04</c> for the length and returned
    ///     4 bytes (<c>81012101</c> for a SmartMX).
    /// </summary>
    public class MifareDesfireGetVersionTests
    {
        private static (TWN4ReaderDevice Device, FakeReaderTransport Transport) CreateDevice(string port)
        {
            var transport = new FakeReaderTransport(port);
            return (new TWN4ReaderDevice(port, _ => transport), transport);
        }

        private static byte[] AnswerWith(byte[] version) =>
            new byte[] { 0x00, 0x01 }.Concat(version).ToArray();

        [Theory]
        // The example of the Simple Protocol documentation (DocRev25, DESFire_GetVersion).
        [InlineData("040101010000100000050401010103001000000500000000000000BA14D0A7103110", 0x01)]
        // An EV3 of 8 KB, native: the type is 01. The production data is made up.
        [InlineData("0401023300002000000504010103000020000005" + "04010203040506" + "0000000000" + "2524", 0x01)]
        // A SmartMX that emulates an EV1 of 8 KB: the type is 81. The production data is made up.
        [InlineData("0481012101002000000504810301020020000005" + "04010203040506" + "0000000000" + "2421", 0x81)]
        public async Task MifareDesfire_GetVersion_ReturnsAllBytesOfTheAnswer(string versionHex, byte expectedType)
        {
            var version = Convert.FromHexString(versionHex);
            var (device, transport) = CreateDevice("COM80");
            transport.QueueResponseBytes(AnswerWith(version));

            var result = await device.MifareDesfire_GetVersionAsync();

            Assert.Equal(34, result.Length);
            Assert.Equal(version, result);
            Assert.Equal(expectedType, result[1]);
            Assert.Equal("0F1200", Assert.Single(transport.WrittenLines));
        }

        [Fact]
        public async Task MifareDesfire_GetVersion_DoesNotPassOnAnAnswerThatIsTooShort()
        {
            // What the method used to return for the SmartMX above is no version.
            var (device, transport) = CreateDevice("COM81");
            transport.QueueResponseBytes(AnswerWith(new byte[] { 0x81, 0x01, 0x21, 0x01 }));

            await Assert.ThrowsAsync<ApplicationException>(() => device.MifareDesfire_GetVersionAsync());
        }
    }
}
