using System;
using System.Linq;
using System.Threading.Tasks;
using Elatec.NET.Cards.Mifare;
using Xunit;

namespace Elatec.NET.Tests
{
    /// <summary>
    ///     A MIFARE Plus in security level 1 answers like a Classic chip (SAK 08 or 18) and a TWN4 sends it no RATS during SearchTag, so
    ///     <see cref="TWN4ReaderDevice.GetSingleChipAsync"/> had no ATS and took it for a Classic chip. It now sends a RATS by hand.
    ///     The answer of a Plus S 4K in security level 1 comes from a real chip on a TWN4/B1.64/NCF5.07; the other ATS values are made
    ///     after the patterns that the library has always compared with (NXP AN10833) and still have to be checked on chips.
    /// </summary>
    public class MifarePlusSl1Tests
    {
        private static readonly byte[] Uid = { 0x04, 0xA1, 0xB2, 0xC3, 0xD4, 0xE5, 0x80 };

        private const string SearchTagFrame = "0500FF";

        // ISO14443_3_Tdx: E0 80 and its CRC_A (31 73), at most 20 ms to answer.
        private const string RatsFrame = "120704E0803173FF1400";

        // The answer of a Plus S 4K in security level 1: the ATS (TL = 12, T0, TA(1), TB(1), TC(1), historical bytes C1 05 2F 2F 00 35 C7) and its CRC_A.
        private const string PlusS4KAnswer = "0C75778002C1052F2F0035C760D3";

        private static readonly byte[] PlusS = { 0xC1, 0x05, 0x2F, 0x2F, 0x00, 0x35, 0xC7 };
        private static readonly byte[] PlusX = { 0xC1, 0x05, 0x2F, 0x2F, 0x01, 0xBC, 0xD6 };
        private static readonly byte[] SomethingElse = { 0xC1, 0x05, 0x21, 0x30, 0x10, 0xF6, 0xD1 };

        [Theory]
        [InlineData("E080", "3173")] // the RATS of this library, and what a TWN4 accepted
        [InlineData("E050", "BCA5")] // the RATS with FSD 64, answered by the same chip
        [InlineData("0000", "A01E")] // ISO/IEC 14443-3, annex B
        [InlineData("1234", "26CF")] // ISO/IEC 14443-3, annex B
        public void CrcA_MatchesTheKnownValues(string dataHex, string expectedHex)
        {
            Assert.Equal(Convert.FromHexString(expectedHex), TWN4ReaderDevice.CrcA(Convert.FromHexString(dataHex)));
        }

        [Fact]
        public void ExtractAts_TakesTheAtsOfARealChipAndLeavesOutTheCrc()
        {
            var ats = TWN4ReaderDevice.ExtractAts(Convert.FromHexString(PlusS4KAnswer));

            Assert.Equal(Convert.FromHexString("0C75778002C1052F2F0035C7"), ats);
        }

        [Theory]
        [InlineData("0C75778002C1052F2F0035C760D4")] // wrong CRC
        [InlineData("0C75778002C1052F2F0035C760")] // CRC cut off
        [InlineData("0D75778002C1052F2F0035C760D3")] // TL says 13, there are 12
        [InlineData("0075778002C1052F2F0035C760D3")] // TL 0
        [InlineData("0C75")] // too short
        public void ExtractAts_RefusesWhatIsNoAts(string answerHex)
        {
            Assert.Null(TWN4ReaderDevice.ExtractAts(Convert.FromHexString(answerHex)));
        }

        [Fact]
        public void ExtractAts_RefusesNoAnswer()
        {
            Assert.Null(TWN4ReaderDevice.ExtractAts(null));
        }

        [Theory]
        [InlineData(0x08, true)] // Classic 1K, Plus 2K in security level 1
        [InlineData(0x18, true)] // Classic 4K, Plus 4K in security level 1
        [InlineData(0x09, false)] // Mini
        [InlineData(0x19, false)] // Classic 2K
        [InlineData(0x28, false)] // SmartMX Classic 1K: talks ISO/IEC 14443-4, the reader sends the RATS
        [InlineData(0x38, false)] // SmartMX Classic 4K
        [InlineData(0x20, false)] // DESFire, Plus in security level 3
        [InlineData(0x10, false)] // Plus in security level 2
        [InlineData(0x00, false)] // Ultralight, NTAG
        [InlineData(0x0A, false)] // RFU bit
        public void AnswersLikeClassic_IsTrueForSak08And18Only(byte sak, bool expected)
        {
            Assert.Equal(expected, TWN4ReaderDevice.AnswersLikeClassic(sak));
        }

        [Fact]
        public async Task GetSingleChipAsync_RecognisesAPlusS4KInSecurityLevel1ByItsAts()
        {
            var (device, transport) = CreateDevice("COM100");
            QueueChip(transport, 0x18);
            QueueRatsAnswer(transport, Convert.FromHexString(PlusS4KAnswer));
            QueueSearchTag(transport);

            var chip = (MifareChip)await device.GetSingleChipAsync();

            Assert.Equal(MifareChipSubType.MifarePlus_SL1_4K, chip.SubType);
            Assert.Equal(Convert.FromHexString("0C75778002C1052F2F0035C7"), chip.ATS);
            Assert.Contains(RatsFrame, transport.WrittenLines);
            Assert.Equal(2, CountSearches(transport)); // the chip is in layer 4 after the RATS and has to be found again
            Assert.Empty(transport.Responses);
        }

        [Theory]
        [InlineData(nameof(PlusS), MifareChipSubType.MifarePlus_SL1_2K)]
        [InlineData(nameof(PlusX), MifareChipSubType.MifarePlus_SL1_2K)]
        [InlineData(nameof(SomethingElse), MifareChipSubType.MifarePlus_SL1_1K)] // answers a RATS, so no Classic chip
        public async Task GetSingleChipAsync_RecognisesAPlusWithSak08ByItsHistoricalBytes(string historical, MifareChipSubType expected)
        {
            var (device, transport) = CreateDevice("COM101");
            QueueChip(transport, 0x08);
            QueueRatsAnswer(transport, WithCrc(Ats(HistoricalBytes(historical))));
            QueueSearchTag(transport);

            var chip = (MifareChip)await device.GetSingleChipAsync();

            Assert.Equal(expected, chip.SubType);
            Assert.Equal(2, CountSearches(transport));
            Assert.Empty(transport.Responses);
        }

        [Fact]
        public async Task GetSingleChipAsync_RecognisesAPlus4KWhoseHistoricalBytesItDoesNotKnow()
        {
            var (device, transport) = CreateDevice("COM102");
            QueueChip(transport, 0x18);
            QueueRatsAnswer(transport, WithCrc(Ats(SomethingElse)));
            QueueSearchTag(transport);

            var chip = (MifareChip)await device.GetSingleChipAsync();

            // It was Mifare4K before the RATS, and a chip that answers a RATS is no Classic chip; with no type at all it would be lost.
            Assert.Equal(MifareChipSubType.MifarePlus_SL1_4K, chip.SubType);
            Assert.Empty(transport.Responses);
        }

        [Theory]
        [InlineData(0x08, MifareChipSubType.Mifare1K)]
        [InlineData(0x18, MifareChipSubType.Mifare4K)]
        public async Task GetSingleChipAsync_KeepsAClassicChipThatDoesNotAnswerTheRats(byte sak, MifareChipSubType expected)
        {
            var (device, transport) = CreateDevice("COM103");
            QueueChip(transport, sak);
            QueueRatsAnswer(transport, null);
            QueueSearchTag(transport); // the chip is idle after the RATS that it did not understand

            var chip = (MifareChip)await device.GetSingleChipAsync();

            Assert.Equal(expected, chip.SubType);
            Assert.Null(chip.ATS);
            Assert.Equal(2, CountSearches(transport));
            Assert.Empty(transport.Responses);
        }

        [Fact]
        public async Task GetSingleChipAsync_KeepsAClassicChipWhenTheAnswerIsNoAts()
        {
            var (device, transport) = CreateDevice("COM104");
            QueueChip(transport, 0x18);
            QueueRatsAnswer(transport, Convert.FromHexString("0C75778002C1052F2F0035C760D4")); // wrong CRC: noise
            QueueSearchTag(transport);

            var chip = (MifareChip)await device.GetSingleChipAsync();

            Assert.Equal(MifareChipSubType.Mifare4K, chip.SubType);
            Assert.Null(chip.ATS);
            Assert.Empty(transport.Responses);
        }

        [Fact]
        public async Task GetSingleChipAsync_TreatsAReaderWithoutTheFunctionLikeAChipWithoutAnswer()
        {
            var (device, transport) = CreateDevice("COM105");
            QueueChip(transport, 0x18);
            transport.QueueResponseBytes((byte)ResponseError.UnknownFunction);
            QueueSearchTag(transport);

            var chip = (MifareChip)await device.GetSingleChipAsync();

            Assert.Equal(MifareChipSubType.Mifare4K, chip.SubType);
            Assert.Equal(2, CountSearches(transport));
            Assert.Empty(transport.Responses);
        }

        [Fact]
        public async Task GetSingleChipAsync_SendsNoRatsWhenTheReaderGaveTheAts()
        {
            // A Legic capable reader sends the RATS itself during SearchTag, and then there is an ATS.
            var (device, transport) = CreateDevice("COM106");
            QueueChip(transport, 0x18, Ats(PlusS));

            var chip = (MifareChip)await device.GetSingleChipAsync();

            Assert.Equal(MifareChipSubType.MifarePlus_SL1_4K, chip.SubType);
            Assert.DoesNotContain(RatsFrame, transport.WrittenLines);
            Assert.Equal(1, CountSearches(transport));
            Assert.Empty(transport.Responses);
        }

        [Theory]
        [InlineData(0x09, MifareChipSubType.MifareMini)]
        [InlineData(0x19, MifareChipSubType.Mifare2K)]
        public async Task GetSingleChipAsync_SendsNoRatsToAMiniOrA2KChip(byte sak, MifareChipSubType expected)
        {
            var (device, transport) = CreateDevice("COM107");
            QueueChip(transport, sak);

            var chip = (MifareChip)await device.GetSingleChipAsync();

            Assert.Equal(expected, chip.SubType);
            Assert.DoesNotContain(RatsFrame, transport.WrittenLines);
            Assert.Equal(1, CountSearches(transport));
            Assert.Empty(transport.Responses);
        }

        private static (TWN4ReaderDevice Device, FakeReaderTransport Transport) CreateDevice(string port)
        {
            var transport = new FakeReaderTransport(port);
            return (new TWN4ReaderDevice(port, _ => transport), transport);
        }

        private static byte[] HistoricalBytes(string name) => name switch
        {
            nameof(PlusS) => PlusS,
            nameof(PlusX) => PlusX,
            _ => SomethingElse,
        };

        /// <summary>An ATS with TA(1), TB(1) and TC(1) as the Plus S answered them, and the given historical bytes: TL first, no CRC.</summary>
        private static byte[] Ats(byte[] historical)
        {
            var body = new byte[] { 0x00, 0x75, 0x77, 0x80, 0x02 }.Concat(historical).ToArray();
            body[0] = (byte)body.Length;

            return body;
        }

        private static byte[] WithCrc(byte[] ats) => ats.Concat(TWN4ReaderDevice.CrcA(ats)).ToArray();

        /// <summary>The answers up to the ATS: SearchTag, ATQA, SAK and the ATS that the reader has (none, or the given one).</summary>
        private static void QueueChip(FakeReaderTransport transport, byte sak, byte[] atsOfTheReader = null)
        {
            QueueSearchTag(transport);
            transport.QueueResponseBytes(0x00, 0x01, 0x42, 0x00);
            transport.QueueResponseBytes(0x00, 0x01, sak);
            QueueRatsAnswer(transport, atsOfTheReader);
        }

        // [error 00][result 01][length][bytes], or [error 00][result 00] when there is none. The same shape for the ATS of the reader and
        // for the answer to the RATS.
        private static void QueueRatsAnswer(FakeReaderTransport transport, byte[] bytes)
        {
            if (bytes == null)
            {
                transport.QueueResponseBytes(0x00, 0x00);
                return;
            }

            transport.QueueResponseBytes(new byte[] { 0x00, 0x01, (byte)bytes.Length }.Concat(bytes).ToArray());
        }

        private static void QueueSearchTag(FakeReaderTransport transport)
        {
            // [error 00][found 01][ChipType MIFARE 80][ID bit count][ID length][ID]
            transport.QueueResponseBytes(new byte[] { 0x00, 0x01, 0x80, (byte)(Uid.Length * 8), (byte)Uid.Length }.Concat(Uid).ToArray());
        }

        private static int CountSearches(FakeReaderTransport transport)
        {
            return transport.WrittenLines.Count(line => line == SearchTagFrame);
        }
    }
}
