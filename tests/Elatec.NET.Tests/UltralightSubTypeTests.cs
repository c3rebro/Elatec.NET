using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Elatec.NET.Cards.Mifare;
using Elatec.NET.Helpers.ByteArrayHelper.Extensions;
using Xunit;

namespace Elatec.NET.Tests
{
    /// <summary>
    ///     MIFARE Ultralight and NTAG chips share ATQA 0x0044 and SAK 0x00, so <see cref="TWN4ReaderDevice.GetSingleChipAsync"/> asks for
    ///     GET_VERSION to tell them apart. Only the NTAG215 answer comes from a real chip, the other answers are written after the NXP data
    ///     sheets (the EV1 example is the one of the Simple Protocol documentation) and still have to be checked on chips.
    /// </summary>
    public class UltralightSubTypeTests
    {
        private static readonly byte[] Uid = { 0x04, 0xA1, 0xB2, 0xC3, 0xD4, 0xE5, 0x80 };
        private static readonly byte[] OtherUid = { 0x04, 0x11, 0x22, 0x33, 0x44, 0x55, 0x66 };

        private const string SearchTagFrame = "0500FF";
        private const string GetVersionFrame = "0C09";

        [Theory]
        [InlineData("0004040201001103", MifareChipSubType.NTAG_215)] // read from a real NTAG215
        [InlineData("0004040201000F03", MifareChipSubType.NTAG_213)]
        [InlineData("0004040201001303", MifareChipSubType.NTAG_216)]
        [InlineData("0004040101000B03", MifareChipSubType.NTAG_210)]
        [InlineData("0004040101000E03", MifareChipSubType.NTAG_212)]
        [InlineData("0004030101000E03", MifareChipSubType.MifareUltralight_EV1_MF0UL21)] // example of the Simple Protocol documentation
        [InlineData("0004030101000B03", MifareChipSubType.MifareUltralight_EV1_MF0UL11)]
        [InlineData("0004030201000B03", MifareChipSubType.MifareUltralight_EV1_MF0UL11)] // 50 pF variant
        [InlineData("0004030101001103", MifareChipSubType.MifareUltralight_EV1)] // EV1 of an unknown size
        public void ClassifyUltralightFamily_RecognisesTheChipByTypeSubtypeAndSize(string versionHex, MifareChipSubType expected)
        {
            var version = ByteArrayConverter.GetBytesFrom(versionHex);

            Assert.Equal(expected, TWN4ReaderDevice.ClassifyUltralightFamily(version));
        }

        [Theory]
        [InlineData("0004040502011303")] // NTAG with a product subtype that is not known: the size alone is not enough
        [InlineData("0004040201001503")] // NTAG of a size that is not known
        [InlineData("0004050201000F03")] // product type that is not known
        [InlineData("0104040201001103")] // wrong header
        [InlineData("0003040201001103")] // vendor is not NXP
        [InlineData("00040402010011")] // too short
        public void ClassifyUltralightFamily_KeepsMifareUltralightForAnswersItDoesNotKnow(string versionHex)
        {
            var version = ByteArrayConverter.GetBytesFrom(versionHex);

            Assert.Equal(MifareChipSubType.MifareUltralight, TWN4ReaderDevice.ClassifyUltralightFamily(version));
        }

        [Fact]
        public void ClassifyUltralightFamily_KeepsMifareUltralightWithoutAnAnswer()
        {
            Assert.Equal(MifareChipSubType.MifareUltralight, TWN4ReaderDevice.ClassifyUltralightFamily(null));
        }

        [Fact]
        public async Task GetSingleChipAsync_RecognisesAnNtag215()
        {
            var (device, transport) = CreateDevice("COM90");
            QueueChipIdentification(transport);
            transport.QueueResponseBytes(0x00, 0x01, 0x00, 0x04, 0x04, 0x02, 0x01, 0x00, 0x11, 0x03);

            var chip = (MifareChip)await device.GetSingleChipAsync();

            Assert.Equal(MifareChipSubType.NTAG_215, chip.SubType);
            Assert.Contains(GetVersionFrame, transport.WrittenLines);
            Assert.Equal(1, CountSearches(transport)); // the chip answered, it is still active
            Assert.Empty(transport.Responses);
        }

        [Fact]
        public async Task GetSingleChipAsync_RecognisesAnUltralightEv1()
        {
            var (device, transport) = CreateDevice("COM91");
            QueueChipIdentification(transport);
            transport.QueueResponseBytes(0x00, 0x01, 0x00, 0x04, 0x03, 0x01, 0x01, 0x00, 0x0E, 0x03);

            var chip = (MifareChip)await device.GetSingleChipAsync();

            Assert.Equal(MifareChipSubType.MifareUltralight_EV1_MF0UL21, chip.SubType);
            Assert.Equal(1, CountSearches(transport));
            Assert.Empty(transport.Responses);
        }

        [Fact]
        public async Task GetSingleChipAsync_FindsAChipWithoutGetVersionAgain()
        {
            var (device, transport) = CreateDevice("COM92");
            QueueChipIdentification(transport);
            transport.QueueResponseBytes(0x00, 0x00); // NAK: Ultralight and Ultralight C do not know GET_VERSION
            QueueSearchTag(transport, Uid);

            var chip = (MifareChip)await device.GetSingleChipAsync();

            Assert.Equal(MifareChipSubType.MifareUltralight, chip.SubType);
            Assert.Equal(2, CountSearches(transport)); // the chip is idle after the NAK and has to be found again
            Assert.Empty(transport.Responses);
        }

        [Fact]
        public async Task GetSingleChipAsync_SearchesAgainWhenAnotherTagAnswersFirst()
        {
            var (device, transport) = CreateDevice("COM93");
            QueueChipIdentification(transport);
            transport.QueueResponseBytes(0x00, 0x00);
            QueueSearchTag(transport, OtherUid);
            QueueSearchTag(transport, Uid);

            var chip = (MifareChip)await device.GetSingleChipAsync();

            Assert.Equal(MifareChipSubType.MifareUltralight, chip.SubType);
            Assert.Equal(3, CountSearches(transport));
            Assert.Empty(transport.Responses);
        }

        [Fact]
        public async Task GetSingleChipAsync_StopsSearchingWhenTheChipIsGone()
        {
            var (device, transport) = CreateDevice("COM94");
            QueueChipIdentification(transport);
            transport.QueueResponseBytes(0x00, 0x00);
            transport.QueueResponseBytes(0x00, 0x00); // the search finds nothing

            var chip = (MifareChip)await device.GetSingleChipAsync();

            Assert.Equal(MifareChipSubType.MifareUltralight, chip.SubType);
            Assert.Equal(2, CountSearches(transport));
            Assert.Empty(transport.Responses);
        }

        [Fact]
        public async Task GetSingleChipAsync_TreatsAReaderWithoutTheFunctionLikeAChipWithoutAnswer()
        {
            var (device, transport) = CreateDevice("COM95");
            QueueChipIdentification(transport);
            transport.QueueResponseBytes((byte)ResponseError.UnknownFunction);
            QueueSearchTag(transport, Uid);

            var chip = (MifareChip)await device.GetSingleChipAsync();

            Assert.Equal(MifareChipSubType.MifareUltralight, chip.SubType);
            Assert.Equal(2, CountSearches(transport));
            Assert.Empty(transport.Responses);
        }

        private static (TWN4ReaderDevice Device, FakeReaderTransport Transport) CreateDevice(string port)
        {
            var transport = new FakeReaderTransport(port);
            return (new TWN4ReaderDevice(port, _ => transport), transport);
        }

        /// <summary>
        ///     The answers up to the Ultralight branch: SearchTag, then ATQA, SAK 0x00 and no ATS.
        /// </summary>
        private static void QueueChipIdentification(FakeReaderTransport transport)
        {
            QueueSearchTag(transport, Uid);
            transport.QueueResponseBytes(0x00, 0x01, 0x44, 0x00);
            transport.QueueResponseBytes(0x00, 0x01, 0x00);
            transport.QueueResponseBytes(0x00, 0x00);
        }

        private static void QueueSearchTag(FakeReaderTransport transport, byte[] uid)
        {
            // [error 00][found 01][ChipType MIFARE 80][ID bit count][ID length][ID]
            var response = new List<byte> { 0x00, 0x01, 0x80, (byte)(uid.Length * 8), (byte)uid.Length };
            response.AddRange(uid);
            transport.QueueResponseBytes(response.ToArray());
        }

        private static int CountSearches(FakeReaderTransport transport)
        {
            return transport.WrittenLines.Count(line => line == SearchTagFrame);
        }
    }
}
