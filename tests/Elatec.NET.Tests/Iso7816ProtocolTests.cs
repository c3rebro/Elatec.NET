using System;
using System.Threading.Tasks;
using Xunit;

namespace Elatec.NET.Tests
{
    public class Iso7816ProtocolTests
    {
        private static (TWN4ReaderDevice Device, FakeReaderTransport Transport) CreateDevice(string port)
        {
            var transport = new FakeReaderTransport(port);
            return (new TWN4ReaderDevice(port, _ => transport), transport);
        }

        [Fact]
        public void ApiIdentifierAndChannels_MatchVendorDocumentation()
        {
            Assert.Equal(0x10, TWN4ReaderDevice.API_ISO7816);
            // The Simple Protocol examples use channel 0x20 for ISO7816 (SAM1) and 0x28 for SLE44xx cards (SC1).
            Assert.Equal(0x20, (byte)Iso7816Channel.Sam1);
            Assert.Equal(0x28, (byte)Iso7816Channel.Sc1);
        }

        // ---- ISO7816_GetSlotStatus [1000] ----

        [Fact]
        public async Task GetSlotStatusAsync_UsesDocumentedExample()
        {
            var (device, transport) = CreateDevice("COM50");
            // DocRev25 1.5.15.1: command 100020, response 0001000000
            transport.Responses.Enqueue("0001000000");

            var status = await device.ISO7816_GetSlotStatusAsync(Iso7816Channel.Sam1);

            Assert.Single(transport.WrittenLines, "100020");
            Assert.NotNull(status);
            Assert.Equal(Iso7816IccStatus.PresentAndActive, status.IccStatus);
            Assert.Equal(0, status.CommandStatus);
            Assert.Equal(0, status.Error);
            Assert.Equal(Iso7816ClockStatus.Running, status.ClockStatus);
        }

        [Fact]
        public async Task GetSlotStatusAsync_DecodesStatusRegisterBitFields()
        {
            var (device, transport) = CreateDevice("COM51");
            // bStatus 0x41: bmICCStatus (bits 0-1) = 1, bmCommandStatus (bits 6-7) = 1
            transport.QueueResponseBytes(0x00, 0x01, 0x41, 0x05, 0x02);
            // bStatus 0x82: bmICCStatus = 2, bmCommandStatus = 2
            transport.QueueResponseBytes(0x00, 0x01, 0x82, 0x00, 0x00);

            var first = await device.ISO7816_GetSlotStatusAsync(Iso7816Channel.Sc1);
            var second = await device.ISO7816_GetSlotStatusAsync(Iso7816Channel.Sc1);

            Assert.Equal("100028", transport.WrittenLines[0]);
            Assert.Equal(Iso7816IccStatus.PresentAndInactive, first.IccStatus);
            Assert.Equal(1, first.CommandStatus);
            Assert.Equal(5, first.Error);
            Assert.Equal(Iso7816ClockStatus.StoppedHigh, first.ClockStatus);
            Assert.Equal(Iso7816IccStatus.NoIccPresent, second.IccStatus);
            Assert.Equal(2, second.CommandStatus);
        }

        [Fact]
        public async Task GetSlotStatusAsync_ReturnsNullWhenReaderReportsFailure()
        {
            var (device, transport) = CreateDevice("COM52");
            transport.QueueResponseBytes(0x00, 0x00);

            Assert.Null(await device.ISO7816_GetSlotStatusAsync(Iso7816Channel.Sam1));
        }

        // ---- ISO7816_IccPowerOn [1001] ----

        [Fact]
        public async Task IccPowerOnAsync_UsesDocumentedExample()
        {
            var (device, transport) = CreateDevice("COM53");
            // DocRev25 1.5.15.2: command 100120FF00, response 00010F3B959680B1FE551FC7477261636513 + bStatus 00 + bError 00
            transport.Responses.Enqueue("00010F3B959680B1FE551FC74772616365130000");

            var result = await device.ISO7816_IccPowerOnAsync(Iso7816Channel.Sam1);

            Assert.Single(transport.WrittenLines, "100120FF00");
            Assert.NotNull(result);
            Assert.Equal("3B959680B1FE551FC7477261636513", result.AtrHexString);
            Assert.Equal(15, result.Atr.Length);
            Assert.Equal(Iso7816IccStatus.PresentAndActive, result.IccStatus);
            Assert.Equal(0, result.CommandStatus);
            Assert.Equal(0, result.Error);
        }

        [Fact]
        public async Task IccPowerOnAsync_EncodesChannelMaxAtrAndPowerSelect()
        {
            var (device, transport) = CreateDevice("COM54");
            transport.QueueResponseBytes(0x00, 0x01, 0x02, 0x3B, 0x00, 0x00, 0x00);

            await device.ISO7816_IccPowerOnAsync(Iso7816Channel.Sc1, Iso7816PowerSelect.V3, 0x20);

            // [1001][Channel 28][MaxATRByteCnt 20][bPowerSelect 02]
            Assert.Single(transport.WrittenLines, "1001282002");
        }

        [Fact]
        public async Task IccPowerOnAsync_ReturnsNullWhenReaderReportsFailure()
        {
            var (device, transport) = CreateDevice("COM55");
            transport.QueueResponseBytes(0x00, 0x00);

            Assert.Null(await device.ISO7816_IccPowerOnAsync(Iso7816Channel.Sam1));
        }

        // ---- ISO7816_IccPowerOff [1002] ----

        [Fact]
        public async Task IccPowerOffAsync_UsesDocumentedExample()
        {
            var (device, transport) = CreateDevice("COM56");
            // DocRev25 1.5.15.3: command 100220, response 0001010000
            transport.Responses.Enqueue("0001010000");

            var status = await device.ISO7816_IccPowerOffAsync(Iso7816Channel.Sam1);

            Assert.Single(transport.WrittenLines, "100220");
            Assert.Equal(Iso7816IccStatus.PresentAndInactive, status.IccStatus);
        }

        // ---- ISO7816_SetCommSettings [1003] ----

        [Fact]
        public async Task SetCommSettingsAsync_T1_UsesDocumentedExample()
        {
            var (device, transport) = CreateDevice("COM57");
            transport.Responses.Enqueue("0001");

            await device.ISO7816_SetCommSettingsAsync(Iso7816Channel.Sam1, new TWN4ReaderDevice.Iso7816CommSettings
            {
                Protocol = Iso7816Protocol.T1,
                Frequency = Iso7816Frequency.Hz1000000,
                F = 0x0174,
                D = 1,
                T1 = new TWN4ReaderDevice.Iso7816ProtocolDataT1
                {
                    FindexDindex = 0x00,
                    Tccks = 0x00,
                    GuardTime = 0xFF,
                    WaitingIntegers = 0x55,
                    ClockStop = 0x00,
                    Ifsc = 0xFE,
                    NadValue = 0x00,
                    Wtx = 0x00
                }
            });

            // DocRev25 1.5.15.4: 1003 20 0100740101000000FF5500FE0000
            Assert.Single(transport.WrittenLines, "1003200100740101000000FF5500FE0000");
        }

        [Fact]
        public async Task SetCommSettingsAsync_T0_PadsProtocolDataTo8Bytes()
        {
            var (device, transport) = CreateDevice("COM58");
            transport.QueueResponseBytes(0x00, 0x01);

            await device.ISO7816_SetCommSettingsAsync(Iso7816Channel.Sam1, new TWN4ReaderDevice.Iso7816CommSettings
            {
                Protocol = Iso7816Protocol.T0,
                Frequency = Iso7816Frequency.Hz5000000,
                F = 0x0174,
                D = 1,
                T0 = new TWN4ReaderDevice.Iso7816ProtocolDataT0
                {
                    FindexDindex = 0x11,
                    Tccks = 0x00,
                    GuardTime = 0x00,
                    WaitingInteger = 0x0A,
                    ClockStop = 0x00
                }
            });

            // [Protocol 00][Freq 05][F 7401][D 0100][11 00 00 0A 00][3 x padding]
            Assert.Single(transport.WrittenLines, "1003" + "20" + "0005" + "7401" + "0100" + "1100000A00" + "000000");
        }

        [Fact]
        public async Task SetCommSettingsAsync_ThrowsReaderExceptionWhenReaderReportsFailure()
        {
            var (device, transport) = CreateDevice("COM59");
            transport.QueueResponseBytes(0x00, 0x00);

            await Assert.ThrowsAsync<ReaderException>(() => device.ISO7816_SetCommSettingsAsync(
                Iso7816Channel.Sam1,
                new TWN4ReaderDevice.Iso7816CommSettings { Protocol = Iso7816Protocol.T0, T0 = new TWN4ReaderDevice.Iso7816ProtocolDataT0() }));
        }

        [Fact]
        public async Task SetCommSettingsAsync_RejectsInvalidSettingsWithoutTalkingToTheReader()
        {
            var (device, transport) = CreateDevice("COM60");

            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                device.ISO7816_SetCommSettingsAsync(Iso7816Channel.Sam1, null));
            // T=1 selected, but only T=0 data given
            await Assert.ThrowsAsync<ArgumentException>(() => device.ISO7816_SetCommSettingsAsync(
                Iso7816Channel.Sam1,
                new TWN4ReaderDevice.Iso7816CommSettings { Protocol = Iso7816Protocol.T1, T0 = new TWN4ReaderDevice.Iso7816ProtocolDataT0() }));
            await Assert.ThrowsAsync<ArgumentException>(() => device.ISO7816_SetCommSettingsAsync(
                Iso7816Channel.Sam1,
                new TWN4ReaderDevice.Iso7816CommSettings { Protocol = (Iso7816Protocol)2 }));

            Assert.Empty(transport.WrittenLines);
        }

        // ---- ISO7816_Transceive [1004] ----

        [Fact]
        public async Task TransceiveAsync_UsesDocumentedExample()
        {
            var (device, transport) = CreateDevice("COM61");
            // DocRev25 1.5.15.5: command 100420050000C10120E0FF, response 000102006E00
            transport.Responses.Enqueue("000102006E00");

            var rx = await device.ISO7816_TransceiveAsync(Iso7816Channel.Sam1, new byte[] { 0x00, 0xC1, 0x01, 0x20, 0xE0 });

            Assert.Single(transport.WrittenLines, "100420050000C10120E0FF");
            Assert.Equal(new byte[] { 0x6E, 0x00 }, rx);
        }

        [Fact]
        public async Task TransceiveAsync_ReturnsNullWhenReaderReportsFailure()
        {
            var (device, transport) = CreateDevice("COM62");
            transport.QueueResponseBytes(0x00, 0x00);

            Assert.Null(await device.ISO7816_TransceiveAsync(Iso7816Channel.Sam1, new byte[] { 0xFF, 0x10, 0xEF }));
        }

        [Fact]
        public async Task TransceiveAsync_RejectsNullData()
        {
            var (device, transport) = CreateDevice("COM63");

            await Assert.ThrowsAsync<ArgumentNullException>(() => device.ISO7816_TransceiveAsync(Iso7816Channel.Sam1, null));
            Assert.Empty(transport.WrittenLines);
        }

        // ---- ISO7816_ExchangeAPDU [1005] ----

        [Fact]
        public async Task ExchangeAPDUAsync_UsesDocumentedExample()
        {
            var (device, transport) = CreateDevice("COM64");
            // DocRev25 1.5.15.6: command 10052000A40004020000000102003F008000, response 00010000006E
            transport.Responses.Enqueue("00010000006E");

            var response = await device.ISO7816_ExchangeAPDUAsync(Iso7816Channel.Sam1, 0x00, 0xA4, 0x00, 0x04,
                data: new byte[] { 0x3F, 0x00 }, le: 0, maxRxByteCount: 128);

            Assert.Single(transport.WrittenLines, "10052000A40004020000000102003F008000");
            Assert.NotNull(response);
            Assert.Empty(response.Data);
            Assert.Equal((ushort)0x6E00, response.StatusWord); // 28160 in the documentation
            Assert.Equal(0x6E, response.Sw1);
            Assert.Equal(0x00, response.Sw2);
            Assert.False(response.IsSuccess);
        }

        [Fact]
        public async Task ExchangeAPDUAsync_ParsesResponseDataAndStatusWord()
        {
            var (device, transport) = CreateDevice("COM65");
            transport.QueueResponseBytes(0x00, 0x01, 0x03, 0x00, 0xAA, 0xBB, 0xCC, 0x00, 0x90);

            var response = await device.ISO7816_ExchangeAPDUAsync(Iso7816Channel.Sam1, 0x80, 0x60, 0x00, 0x00);

            Assert.Equal(new byte[] { 0xAA, 0xBB, 0xCC }, response.Data);
            Assert.Equal((ushort)0x9000, response.StatusWord);
            Assert.True(response.IsSuccess);
        }

        [Fact]
        public async Task ExchangeAPDUAsync_WithoutDataAndLe_SendsEmptyDataFieldAndLePresentCleared()
        {
            var (device, transport) = CreateDevice("COM66");
            transport.QueueResponseBytes(0x00, 0x01, 0x00, 0x00, 0x00, 0x90);

            await device.ISO7816_ExchangeAPDUAsync(Iso7816Channel.Sam1, 0x80, 0x00, 0x00, 0x00);

            // [1005][20][CLA 80 INS 00 P1 00 P2 00][Lc 0000][Le 0000][Flags 00][TXData len 0000][MaxRX 0x0100 -> 0001]
            Assert.Single(transport.WrittenLines, "1005" + "20" + "80000000" + "0000" + "0000" + "00" + "0000" + "0001");
        }

        [Fact]
        public async Task ExchangeAPDUAsync_EncodesLeAndFlags()
        {
            var (device, transport) = CreateDevice("COM67");
            transport.QueueResponseBytes(0x00, 0x01, 0x00, 0x00, 0x00, 0x90);
            transport.QueueResponseBytes(0x00, 0x01, 0x00, 0x00, 0x00, 0x90);

            await device.ISO7816_ExchangeAPDUAsync(Iso7816Channel.Sam1, 0x80, 0xAA, 0x01, 0x02, le: 0x1234);
            await device.ISO7816_ExchangeAPDUAsync(Iso7816Channel.Sam1, 0x80, 0xAA, 0x01, 0x02, le: 0x1234, extendedApdu: true);

            // Flags: bit 0 = LePresent (confirmed by the vendor example), bit 1 = ExtendedAPDU
            Assert.Equal("1005" + "20" + "80AA0102" + "0000" + "3412" + "01" + "0000" + "0001", transport.WrittenLines[0]);
            Assert.Equal("1005" + "20" + "80AA0102" + "0000" + "3412" + "03" + "0000" + "0001", transport.WrittenLines[1]);
        }

        [Fact]
        public async Task ExchangeAPDUAsync_LimitsDataLengthByApduFormat()
        {
            var (device, transport) = CreateDevice("COM68");
            transport.QueueResponseBytes(0x00, 0x01, 0x00, 0x00, 0x00, 0x90);

            await Assert.ThrowsAsync<ArgumentException>(() =>
                device.ISO7816_ExchangeAPDUAsync(Iso7816Channel.Sam1, 0x80, 0xAA, 0x00, 0x00, data: new byte[256]));
            Assert.Empty(transport.WrittenLines);

            // 255 bytes still fit a short APDU, 256 bytes fit an extended one.
            await device.ISO7816_ExchangeAPDUAsync(Iso7816Channel.Sam1, 0x80, 0xAA, 0x00, 0x00, data: new byte[255]);
            Assert.Single(transport.WrittenLines);
            Assert.StartsWith("1005" + "20" + "80AA0000" + "FF00", transport.WrittenLines[0]);

            transport.QueueResponseBytes(0x00, 0x01, 0x00, 0x00, 0x00, 0x90);
            await device.ISO7816_ExchangeAPDUAsync(Iso7816Channel.Sam1, 0x80, 0xAA, 0x00, 0x00, data: new byte[256], extendedApdu: true);
            Assert.StartsWith("1005" + "20" + "80AA0000" + "0001", transport.WrittenLines[1]);
        }

        [Fact]
        public async Task ExchangeAPDUAsync_ReturnsNullWhenReaderReportsFailure()
        {
            var (device, transport) = CreateDevice("COM69");
            transport.QueueResponseBytes(0x00, 0x00);

            Assert.Null(await device.ISO7816_ExchangeAPDUAsync(Iso7816Channel.Sam1, 0x80, 0x00, 0x00, 0x00));
        }

        // ---- ISO7816_T0_TPDU [1006] ----

        [Fact]
        public async Task T0TpduAsync_UsesDocumentedExample()
        {
            var (device, transport) = CreateDevice("COM70");
            // DocRev25 1.5.15.7: command 10062000A400040202003F008000, response 00010000006E
            transport.Responses.Enqueue("00010000006E");

            var response = await device.ISO7816_T0_TPDUAsync(Iso7816Channel.Sam1, 0x00, 0xA4, 0x00, 0x04, 0x02,
                data: new byte[] { 0x3F, 0x00 }, maxRxByteCount: 128);

            Assert.Single(transport.WrittenLines, "10062000A400040202003F008000");
            Assert.Empty(response.Data);
            Assert.Equal((ushort)0x6E00, response.StatusWord);
        }

        [Fact]
        public async Task T0TpduAsync_ReturnsNullWhenReaderReportsFailure()
        {
            var (device, transport) = CreateDevice("COM71");
            transport.QueueResponseBytes(0x00, 0x00);

            Assert.Null(await device.ISO7816_T0_TPDUAsync(Iso7816Channel.Sam1, 0x00, 0xC0, 0x00, 0x00, 0x10));
        }

        // ---- ISO7816_CheckWellKnownCards [1007] ----

        [Fact]
        public async Task CheckWellKnownCardsAsync_UsesDocumentedExample()
        {
            var (device, transport) = CreateDevice("COM72");
            // DocRev25 1.5.15.8: command 100720, response 000110000000
            transport.Responses.Enqueue("000110000000");

            var cardType = await device.ISO7816_CheckWellKnownCardsAsync(Iso7816Channel.Sam1);

            Assert.Single(transport.WrittenLines, "100720");
            Assert.Equal(new byte[] { 0x10, 0x00, 0x00, 0x00 }, cardType);
        }

        // ---- A typical SAM flow ----

        [Fact]
        public async Task SamFlow_PowerOnExchangeApduPowerOff_UsesSimpleProtocolFrames()
        {
            var (device, transport) = CreateDevice("COM73");
            transport.QueueResponseBytes(0x00, 0x01, 0x00, 0x00, 0x00);                         // slot status: present and active
            transport.QueueResponseBytes(0x00, 0x01, 0x02, 0x3B, 0x90, 0x00, 0x00);            // power on: ATR 3B90
            transport.QueueResponseBytes(0x00, 0x01, 0x02, 0x00, 0xCA, 0xFE, 0x00, 0x90);      // APDU: CAFE + 9000
            transport.QueueResponseBytes(0x00, 0x01, 0x01, 0x00, 0x00);                         // power off

            var slot = await device.ISO7816_GetSlotStatusAsync(Iso7816Channel.Sam1);
            Assert.Equal(Iso7816IccStatus.PresentAndActive, slot.IccStatus);

            var atr = await device.ISO7816_IccPowerOnAsync(Iso7816Channel.Sam1);
            Assert.Equal("3B90", atr.AtrHexString);

            var response = await device.ISO7816_ExchangeAPDUAsync(Iso7816Channel.Sam1, 0x80, 0x60, 0x00, 0x00, le: 0);
            Assert.True(response.IsSuccess);
            Assert.Equal(new byte[] { 0xCA, 0xFE }, response.Data);

            var off = await device.ISO7816_IccPowerOffAsync(Iso7816Channel.Sam1);
            Assert.Equal(Iso7816IccStatus.PresentAndInactive, off.IccStatus);

            Assert.Equal(new[]
            {
                "100020",
                "100120FF00",
                "1005" + "20" + "80600000" + "0000" + "0000" + "01" + "0000" + "0001",
                "100220"
            }, transport.WrittenLines);
        }
    }
}
