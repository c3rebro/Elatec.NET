using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Elatec.NET.Sam;
using Xunit;
using static Elatec.NET.Tests.SamAv3KnownAnswerTests;

namespace Elatec.NET.Tests
{
    public class Twn4SamApduChannelTests
    {
        private static (TWN4ReaderDevice Device, FakeReaderTransport Transport) CreateDevice(string port)
        {
            var transport = new FakeReaderTransport(port);
            return (new TWN4ReaderDevice(port, _ => transport), transport);
        }

        /// <summary>Queues what the reader returns for ISO7816_ExchangeAPDU: [00][Result][RXData, 2 byte length][SW, LSB first].</summary>
        private static void QueueApduResponse(FakeReaderTransport transport, string dataHex, ushort statusWord)
        {
            var data = Hex(dataHex);
            var bytes = new List<byte> { 0x00, 0x01, (byte)data.Length, (byte)(data.Length >> 8) };
            bytes.AddRange(data);
            bytes.Add((byte)statusWord);
            bytes.Add((byte)(statusWord >> 8));
            transport.QueueResponseBytes(bytes.ToArray());
        }

        [Fact]
        public async Task TransmitAsync_SendsAnExchangeApduWithLeAndParsesTheResponse()
        {
            var (device, transport) = CreateDevice("COM80");
            QueueApduResponse(transport, "2509C7B09F2DA8FF6D76578B", 0x90AF);
            var channel = new Twn4SamApduChannel(device, Iso7816Channel.Sam1);

            var response = await channel.TransmitAsync(new SamApdu(0x80, 0xA4, 0x00, 0x00, Hex("050102"), 0x00));

            // [1005][Channel 20][CLA INS P1 P2][Lc 0003][Le 0000][Flags 01: Le present][TXData: 0003 | 050102][MaxRX 0x0100]
            Assert.Single(transport.WrittenLines, "1005" + "20" + "80A40000" + "0300" + "0000" + "01" + "0300" + "050102" + "0001");
            Assert.Equal((ushort)0x90AF, response.StatusWord);
            Assert.Equal("2509C7B09F2DA8FF6D76578B", ToHex(response.Data));
        }

        [Fact]
        public async Task TransmitAsync_WithoutLe_ClearsTheLePresentFlag()
        {
            var (device, transport) = CreateDevice("COM81");
            QueueApduResponse(transport, "AA60E01E86561A6F", 0x9000);
            var channel = new Twn4SamApduChannel(device, Iso7816Channel.Sam2, 64);

            await channel.TransmitAsync(new SamApdu(0x80, 0xC1, 0x17, 0xFF, new byte[72]));

            // channel 21, Lc 0x48, Le 0 not present, MaxRX 64
            Assert.Single(transport.WrittenLines, "1005" + "21" + "80C117FF" + "4800" + "0000" + "00" + "4800" + new string('0', 144) + "4000");
        }

        [Fact]
        public async Task TransmitAsync_WhenTheReaderReportsFailure_ThrowsSamException()
        {
            var (device, transport) = CreateDevice("COM82");
            transport.QueueResponseBytes(0x00, 0x00);
            var channel = new Twn4SamApduChannel(device, Iso7816Channel.Sam1);

            await Assert.ThrowsAsync<SamException>(() => channel.TransmitAsync(new SamApdu(0x80, 0xA4, 0x00, 0x00)));
        }

        [Fact]
        public void Constructor_RejectsNullReader()
        {
            Assert.Throws<ArgumentNullException>(() => new Twn4SamApduChannel(null, Iso7816Channel.Sam1));
        }

        [Fact]
        public async Task HostAuthentication_Table2_WorksEndToEndThroughTheReader()
        {
            var (device, transport) = CreateDevice("COM83");
            QueueApduResponse(transport, "2509C7B09F2DA8FF6D76578B", 0x90AF);
            QueueApduResponse(transport, "E89F438446F5177E" + "03322788AE6DB98C963E12C6DF1F4019", 0x90AF);
            QueueApduResponse(transport, "F261C8E49E275A46E210899B3EFD0D58", 0x9000);
            var randoms = new Queue<byte[]>(new[] { Hex("000102030405060708090A0B"), Hex("000102030405060708090A0B0C0D0E0F") });
            var session = new SamAv3HostSession(new Twn4SamApduChannel(device, Iso7816Channel.Sam1), count => randoms.Dequeue());

            await session.AuthenticateHostAsync(0x05, 0x01, Hex("000102030405060708090A0B0C0D0E0F"));

            Assert.True(session.IsAuthenticated);
            Assert.Equal("F7B5D7E05FCDA9F12D6F106CB483B66A", ToHex(session.SecureMessaging.SessionKeyEnc));
            Assert.Equal(3, transport.WrittenLines.Count);
            Assert.StartsWith("1005" + "20" + "80A40000" + "0300", transport.WrittenLines[0]);
            Assert.StartsWith("1005" + "20" + "80A40000" + "1400", transport.WrittenLines[1]); // Lc 0x14: CMAC + Rnd1
            Assert.StartsWith("1005" + "20" + "80A40000" + "2000", transport.WrittenLines[2]); // Lc 0x20: Ek(RndA | RndB')
        }
    }
}
