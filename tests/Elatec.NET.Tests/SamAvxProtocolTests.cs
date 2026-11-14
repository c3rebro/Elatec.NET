using System;
using System.Threading.Tasks;
using Xunit;

namespace Elatec.NET.Tests
{
    public class SamAvxProtocolTests
    {
        private const string TestKeyHex = "00112233445566778899AABBCCDDEEFF";

        private static readonly byte[] TestKey =
        {
            0x00, 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88, 0x99, 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF
        };

        private static (TWN4ReaderDevice Device, FakeReaderTransport Transport) CreateDevice(string port)
        {
            var transport = new FakeReaderTransport(port);
            return (new TWN4ReaderDevice(port, _ => transport), transport);
        }

        [Fact]
        public void ApiIdentifier_Is0x22()
        {
            Assert.Equal(0x22, TWN4ReaderDevice.API_SAMAVX);
        }

        [Fact]
        public async Task AuthenticateHostAsync_UsesDocumentedExampleFrame()
        {
            var (device, transport) = CreateDevice("COM40");
            transport.QueueResponseBytes(0x00, 0x01);

            await device.SAMAVx_AuthenticateHostAsync(0, new byte[16], SamAvxKeyType.TripleDes);

            // DocRev25 1.5.28.1: CryptoEnv 00, KeyNo 00, Key 16 x 00, KeyType 00
            Assert.Single(transport.WrittenLines, "22000000100000000000000000000000000000000000");
        }

        [Fact]
        public async Task AuthenticateHostAsync_EncodesCryptoEnvKeyNoKeyAndKeyType()
        {
            var (device, transport) = CreateDevice("COM41");
            transport.QueueResponseBytes(0x00, 0x01);

            await device.SAMAVx_AuthenticateHostAsync(5, TestKey, SamAvxKeyType.Aes, cryptoEnv: 2);

            // [2200][CryptoEnv 02][KeyNo 05][Len 10][Key][KeyType 02]
            Assert.Single(transport.WrittenLines, "2200020510" + TestKeyHex + "02");
        }

        [Fact]
        public async Task AuthenticateHostAsync_HexOverloadSendsSameFrameAsByteOverload()
        {
            var (device, transport) = CreateDevice("COM42");
            transport.QueueResponseBytes(0x00, 0x01);
            transport.QueueResponseBytes(0x00, 0x01);

            await device.SAMAVx_AuthenticateHostAsync(5, TestKey, SamAvxKeyType.Aes, 2);
            await device.SAMAVx_AuthenticateHostAsync(5, "00 11 22 33 44 55 66 77 88 99 aa bb cc dd ee ff", SamAvxKeyType.Aes, 2);

            Assert.Equal(2, transport.WrittenLines.Count);
            Assert.Equal(transport.WrittenLines[0], transport.WrittenLines[1]);
        }

        [Fact]
        public async Task AuthenticateHostAsync_ThrowsReaderExceptionWhenReaderReportsFailure()
        {
            var (device, transport) = CreateDevice("COM43");
            transport.QueueResponseBytes(0x00, 0x00);

            await Assert.ThrowsAsync<ReaderException>(() =>
                device.SAMAVx_AuthenticateHostAsync(0, TestKey, SamAvxKeyType.Aes));
        }

        [Fact]
        public async Task AuthenticateHostAsync_RejectsInvalidArgumentsWithoutTalkingToTheReader()
        {
            var (device, transport) = CreateDevice("COM44");

            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                device.SAMAVx_AuthenticateHostAsync(0, (byte[])null, SamAvxKeyType.Aes));
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                device.SAMAVx_AuthenticateHostAsync(0, (string)null, SamAvxKeyType.Aes));
            await Assert.ThrowsAsync<ArgumentException>(() =>
                device.SAMAVx_AuthenticateHostAsync(0, new byte[15], SamAvxKeyType.Aes));
            await Assert.ThrowsAsync<ArgumentException>(() =>
                device.SAMAVx_AuthenticateHostAsync(0, new byte[24], SamAvxKeyType.Aes));
            await Assert.ThrowsAsync<ArgumentException>(() =>
                device.SAMAVx_AuthenticateHostAsync(0, "0011", SamAvxKeyType.Aes));
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
                device.SAMAVx_AuthenticateHostAsync(0, TestKey, SamAvxKeyType.Aes, cryptoEnv: 4));
            // 3K3DES (1) is not a valid SAM key type.
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
                device.SAMAVx_AuthenticateHostAsync(0, TestKey, (SamAvxKeyType)1));

            Assert.Empty(transport.WrittenLines);
        }

        [Fact]
        public async Task GetKeyEntryAsync_ParsesDocumentedExample()
        {
            var (device, transport) = CreateDevice("COM45");
            // DocRev25 1.5.28.2: response 000100010200000000000000FF0C00
            transport.Responses.Enqueue("000100010200000000000000FF0C00");

            var entry = await device.SAMAVx_GetKeyEntryAsync(1);

            Assert.Single(transport.WrittenLines, "220101");
            Assert.NotNull(entry);
            Assert.Equal(0, entry.VersionKeyA);
            Assert.Equal(1, entry.VersionKeyB);
            Assert.Equal(2, entry.VersionKeyC);
            Assert.Equal(0u, entry.DfAid);
            Assert.Equal(0, entry.DfKeyNo);
            Assert.Equal(0, entry.KeyNoCek);
            Assert.Equal(0, entry.KeyNoVcek);
            Assert.Equal(0xFF, entry.RefNoKuc);
            Assert.Equal((ushort)0x000C, entry.Settings);
        }

        [Fact]
        public async Task GetKeyEntryAsync_DecodesMultiByteFieldsLeastSignificantByteFirst()
        {
            var (device, transport) = CreateDevice("COM46");
            transport.QueueResponseBytes(0x00, 0x01,
                0x0A, 0x0B, 0x0C,          // VersionKeyA/B/C
                0x78, 0x56, 0x34, 0x12,    // DF_AID
                0x07, 0x08, 0x09, 0x0D,    // DF_KeyNo, KeyNoCEK, KeyNoVCEK, RefNoKUC
                0x34, 0x12);               // SET

            var entry = await device.SAMAVx_GetKeyEntryAsync(0xFE);

            Assert.Single(transport.WrittenLines, "2201FE");
            Assert.Equal(0x0A, entry.VersionKeyA);
            Assert.Equal(0x0B, entry.VersionKeyB);
            Assert.Equal(0x0C, entry.VersionKeyC);
            Assert.Equal(0x12345678u, entry.DfAid);
            Assert.Equal(0x07, entry.DfKeyNo);
            Assert.Equal(0x08, entry.KeyNoCek);
            Assert.Equal(0x09, entry.KeyNoVcek);
            Assert.Equal(0x0D, entry.RefNoKuc);
            Assert.Equal((ushort)0x1234, entry.Settings);
        }

        [Fact]
        public async Task GetKeyEntryAsync_ReturnsNullWhenReaderReportsFailure()
        {
            var (device, transport) = CreateDevice("COM47");
            transport.QueueResponseBytes(0x00, 0x00);

            var entry = await device.SAMAVx_GetKeyEntryAsync(1);

            Assert.Null(entry);
        }
    }
}
