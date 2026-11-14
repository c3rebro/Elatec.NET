using System;
using System.IO;
using System.Threading.Tasks;
using Elatec.NET.Cards.Mifare;
using Xunit;

namespace Elatec.NET.Tests
{
    /// <summary>
    ///     <see cref="ReaderException.ErrorNumber"/> has to be set at every throw site, with the same error the message names.
    ///     One test per source file that throws, plus the different errors of the Core file.
    /// </summary>
    public class ReaderExceptionTests
    {
        private static (TWN4ReaderDevice Device, FakeReaderTransport Transport) CreateDevice(string port)
        {
            var transport = new FakeReaderTransport(port);
            return (new TWN4ReaderDevice(port, _ => transport), transport);
        }

        /// <summary>
        ///     A card command the reader answers with Result = false, which is all the Simple Protocol reports.
        /// </summary>
        private static (TWN4ReaderDevice Device, FakeReaderTransport Transport) CreateDeviceReportingFailure(string port)
        {
            var (device, transport) = CreateDevice(port);
            transport.QueueResponseBytes(0x00, 0x00);
            return (device, transport);
        }

        [Fact]
        public async Task MifareUltralight_WritePage_ReportsAccessDenied()
        {
            var (device, _) = CreateDeviceReportingFailure("COM70");

            var exception = await Assert.ThrowsAsync<ReaderException>(() =>
                device.MifareUltralight_WritePageAsync(135, new byte[4]));

            Assert.Equal(ReaderError.AccessDenied, exception.ErrorNumber);
            // Callers parse the text, it must not change.
            Assert.Equal("Call was not successfull, error AccessDenied", exception.Message);
        }

        [Fact]
        public async Task MifareUltralightEV1_PwdAuth_ReportsAccessDenied()
        {
            var (device, _) = CreateDeviceReportingFailure("COM71");

            var exception = await Assert.ThrowsAsync<ReaderException>(() =>
                device.MifareUltralightEV1_PwdAuthAsync(new byte[4], new byte[2]));

            Assert.Equal(ReaderError.AccessDenied, exception.ErrorNumber);
        }

        [Fact]
        public async Task MifareClassic_Login_ReportsAccessDenied()
        {
            var (device, _) = CreateDeviceReportingFailure("COM72");

            var exception = await Assert.ThrowsAsync<ReaderException>(() =>
                device.MifareClassic_LoginAsync("FFFFFFFFFFFF", 0, 0));

            Assert.Equal(ReaderError.AccessDenied, exception.ErrorNumber);
        }

        [Fact]
        public async Task MifareClassic_WriteValueBlock_ReportsAccessDenied()
        {
            // One of the throw sites that used to span two lines.
            var (device, _) = CreateDeviceReportingFailure("COM73");

            var exception = await Assert.ThrowsAsync<ReaderException>(() =>
                device.MifareClassic_WriteValueBlockAsync(4, 1));

            Assert.Equal(ReaderError.AccessDenied, exception.ErrorNumber);
            Assert.Equal("Call was not successfull, error AccessDenied", exception.Message);
        }

        [Fact]
        public async Task MifareDesfire_DeleteFile_ReportsAccessDenied()
        {
            var (device, _) = CreateDeviceReportingFailure("COM74");

            var exception = await Assert.ThrowsAsync<ReaderException>(() =>
                device.MifareDesfire_DeleteFileAsync(1));

            Assert.Equal(ReaderError.AccessDenied, exception.ErrorNumber);
        }

        [Fact]
        public async Task MifareDesfire_GetVersion_ReportsNotSupported()
        {
            var (device, _) = CreateDeviceReportingFailure("COM75");

            var exception = await Assert.ThrowsAsync<ReaderException>(() =>
                device.MifareDesfire_GetVersionAsync());

            Assert.Equal(ReaderError.NotSupported, exception.ErrorNumber);
        }

        [Fact]
        public async Task MifareDesfire_ReadData_ReportsDataWhenTheChunkLengthDiffers()
        {
            var (device, transport) = CreateDevice("COM76");
            // Result = true, but only 2 of the 4 requested bytes.
            transport.QueueResponseBytes(0x00, 0x01, 0x02, 0xAA, 0xBB);

            var exception = await Assert.ThrowsAsync<ReaderException>(() =>
                device.MifareDesfire_ReadDataAsync(1, 0, 4, EncryptionMode.CM_PLAIN));

            Assert.Equal(ReaderError.Data, exception.ErrorNumber);
        }

        [Fact]
        public async Task Iso7816_SetCommSettings_ReportsGeneral()
        {
            var (device, _) = CreateDeviceReportingFailure("COM77");

            var exception = await Assert.ThrowsAsync<ReaderException>(() => device.ISO7816_SetCommSettingsAsync(
                Iso7816Channel.Sam1,
                new TWN4ReaderDevice.Iso7816CommSettings { Protocol = Iso7816Protocol.T0, T0 = new TWN4ReaderDevice.Iso7816ProtocolDataT0() }));

            Assert.Equal(ReaderError.General, exception.ErrorNumber);
        }

        [Fact]
        public async Task SamAvx_AuthenticateHost_ReportsAccessDenied()
        {
            var (device, _) = CreateDeviceReportingFailure("COM78");

            var exception = await Assert.ThrowsAsync<ReaderException>(() =>
                device.SAMAVx_AuthenticateHostAsync(0, new byte[16], SamAvxKeyType.Aes));

            Assert.Equal(ReaderError.AccessDenied, exception.ErrorNumber);
        }

        [Fact]
        public async Task Core_CommunicationFailure_ReportsNotOpenAndKeepsTheCause()
        {
            // The fake transport throws when no response is queued.
            var (device, _) = CreateDevice("COM79");

            var exception = await Assert.ThrowsAsync<ReaderException>(() =>
                device.CallFunctionRawAsync(new byte[] { 0x00, 0x03 }));

            Assert.Equal(ReaderError.NotOpen, exception.ErrorNumber);
            Assert.Equal("Call was not successfull, error NotOpen", exception.Message);
            Assert.IsType<InvalidOperationException>(exception.InnerException);
        }

        [Fact]
        public async Task Core_Connect_ReportsNotOpenWhenThePortIsBusy()
        {
            var cause = new UnauthorizedAccessException();
            var transport = new FailingTransport("COM80", connectFailure: cause);
            var device = new TWN4ReaderDevice("COM80", _ => transport);

            var exception = await Assert.ThrowsAsync<ReaderException>(() => device.ConnectAsync());

            Assert.Equal(ReaderError.NotOpen, exception.ErrorNumber);
            Assert.Same(cause, exception.InnerException);
        }

        [Fact]
        public async Task Core_Connect_ReportsNotInitWhenTheTransportCannotConnect()
        {
            var cause = new InvalidOperationException();
            var transport = new FailingTransport("COM81", connectFailure: cause);
            var device = new TWN4ReaderDevice("COM81", _ => transport);

            var exception = await Assert.ThrowsAsync<ReaderException>(() => device.ConnectAsync());

            Assert.Equal(ReaderError.NotInit, exception.ErrorNumber);
            Assert.Same(cause, exception.InnerException);
        }

        [Fact]
        public async Task Core_Disconnect_ReportsNotOpenWhenThePortIsBusy()
        {
            var cause = new UnauthorizedAccessException();
            var device = await CreateConnectedDeviceAsync("COM82", new FailingTransport("COM82", disconnectFailure: cause));

            var exception = await Assert.ThrowsAsync<ReaderException>(() => device.DisconnectAsync());

            Assert.Equal(ReaderError.NotOpen, exception.ErrorNumber);
            Assert.Same(cause, exception.InnerException);
        }

        [Fact]
        public async Task Core_Disconnect_ReportsNotInitOnIoFailure()
        {
            var cause = new IOException();
            var device = await CreateConnectedDeviceAsync("COM83", new FailingTransport("COM83", disconnectFailure: cause));

            var exception = await Assert.ThrowsAsync<ReaderException>(() => device.DisconnectAsync());

            Assert.Equal(ReaderError.NotInit, exception.ErrorNumber);
            Assert.Same(cause, exception.InnerException);
        }

        [Fact]
        public void For_DerivesMessageAndErrorNumberFromTheSameError()
        {
            var cause = new InvalidOperationException();

            var exception = ReaderException.For(ReaderError.NotSupported, cause);

            Assert.Equal(ReaderError.NotSupported, exception.ErrorNumber);
            Assert.Equal("Call was not successfull, error NotSupported", exception.Message);
            Assert.Same(cause, exception.InnerException);
        }

        /// <summary>
        ///     Creates the transport of a device by sending one command, so the following disconnect has an open transport to close.
        /// </summary>
        private static async Task<TWN4ReaderDevice> CreateConnectedDeviceAsync(string port, FailingTransport transport)
        {
            transport.QueueResponseBytes(0x00);
            var device = new TWN4ReaderDevice(port, _ => transport);
            await device.CallFunctionRawAsync(new byte[] { 0x00, 0x03 });
            return device;
        }

        /// <summary>
        ///     Fake transport that fails to connect or to disconnect with the given exception.
        /// </summary>
        private sealed class FailingTransport : FakeReaderTransport
        {
            private readonly Exception _connectFailure;
            private readonly Exception _disconnectFailure;

            public FailingTransport(string portName, Exception connectFailure = null, Exception disconnectFailure = null) : base(portName)
            {
                _connectFailure = connectFailure;
                _disconnectFailure = disconnectFailure;
            }

            public override Task ConnectAsync()
            {
                if (_connectFailure != null)
                {
                    throw _connectFailure;
                }

                return base.ConnectAsync();
            }

            public override Task DisconnectAsync()
            {
                if (_disconnectFailure != null)
                {
                    throw _disconnectFailure;
                }

                return base.DisconnectAsync();
            }
        }
    }
}
