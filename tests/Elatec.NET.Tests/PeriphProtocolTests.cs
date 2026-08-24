using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Elatec.NET.Tests
{
    public class PeriphProtocolTests
    {
        [Fact]
        public async Task BeepAsync_UsesDocumented0407Frame()
        {
            var transport = new FakeReaderTransport("COM24");
            transport.QueueResponseBytes(0x00);
            var device = new TWN4ReaderDevice("COM24", _ => transport);

            await device.BeepAsync(100, 2400, 500, 500);

            Assert.Equal(0x04, TWN4ReaderDevice.API_PERIPH);
            Assert.Single(transport.WrittenLines, "0407646009F401F401");
        }

        [Fact]
        public async Task ReadmeSample_BeepLedAndPlayMelody_UseSimpleProtocolFrames()
        {
            var transport = new FakeReaderTransport("COM24");
            transport.QueueResponseBytes(0x00); // Beep
            transport.QueueResponseBytes(0x00); // LED init
            transport.QueueResponseBytes(0x00); // LED blink
            transport.QueueResponseBytes(0x00); // Melody tone

            var device = new TWN4ReaderDevice("COM24", _ => transport);

            // Keep this sequence aligned with README.md and c3rebro/Elatec.Net.SampleApp.
            await device.BeepAsync(100, 1500, 500, 100);
            await device.LedInitAsync();
            await device.LedBlinkAsync(Leds.All, 100, 300);
            await device.PlayMelody(90, new List<TWN4ReaderDevice.Tone>
            {
                new TWN4ReaderDevice.Tone { Value = 4, Pitch = NotePitch.C3 }
            });

            Assert.Equal(new[]
            {
                "040764DC05F4016400",
                "041007",
                "04140764002C01",
                "04073C1704A5000000"
            }, transport.WrittenLines);
        }

        [Fact]
        public async Task GpioCommands_UseDocumented0400To0406Frames()
        {
            var transport = new FakeReaderTransport("COM24");
            for (var i = 0; i < 6; i++)
            {
                transport.QueueResponseBytes(0x00);
            }
            transport.QueueResponseBytes(0x00, 0x01);

            var device = new TWN4ReaderDevice("COM24", _ => transport);

            await device.GpioConfigureOutputsAsync(Gpios.GPIO0, GpioPullType.NoPull, GpioOutputType.PushPull);
            await device.GpioConfigureInputsAsync(Gpios.GPIO0, GpioPullType.NoPull);
            await device.GpioSetBitsAsync(Gpios.GPIO0);
            await device.GpioClearBitsAsync(Gpios.GPIO0);
            await device.GpioToggleBitsAsync(Gpios.GPIO0);
            await device.GpioBlinkBitsAsync(Gpios.GPIO0, 100, 100);
            var isSet = await device.GpioTestBitAsync(Gpios.GPIO0);

            Assert.True(isSet);
            Assert.Equal(new[]
            {
                "0400010000",
                "04010100",
                "040201",
                "040301",
                "040401",
                "04050164006400",
                "040601"
            }, transport.WrittenLines);
        }

        [Fact]
        public async Task DiagnosticLedLedAndContinuousBeep_UseDocumentedFrames()
        {
            var transport = new FakeReaderTransport("COM24");
            transport.QueueResponseBytes(0x00);       // Diag LED on
            transport.QueueResponseBytes(0x00);       // Diag LED off
            transport.QueueResponseBytes(0x00);       // Diag LED toggle
            transport.QueueResponseBytes(0x00, 0x01); // Diag LED is on
            transport.QueueResponseBytes(0x00);       // LED init
            transport.QueueResponseBytes(0x00);       // LED on
            transport.QueueResponseBytes(0x00);       // LED off
            transport.QueueResponseBytes(0x00);       // LED toggle
            transport.QueueResponseBytes(0x00);       // LED blink
            transport.QueueResponseBytes(0x00);       // Beep on
            transport.QueueResponseBytes(0x00);       // Beep off

            var device = new TWN4ReaderDevice("COM24", _ => transport);

            await device.DiagLedOnAsync();
            await device.DiagLedOffAsync();
            await device.DiagLedToggleAsync();
            var diagLedIsOn = await device.DiagLedIsOnAsync();
            await device.LedInitAsync();
            await device.LedOnAsync(Leds.Red);
            await device.LedOffAsync(Leds.Red);
            await device.LedToggleAsync(Leds.Red);
            await device.LedBlinkAsync(Leds.Red, 500, 500);
            await device.BeepOnAsync(100, 2400);
            await device.BeepOffAsync();

            Assert.True(diagLedIsOn);
            Assert.Equal(new[]
            {
                "0408",
                "0409",
                "040A",
                "040B",
                "041007",
                "041101",
                "041201",
                "041301",
                "041401F401F401",
                "0416646009",
                "0417"
            }, transport.WrittenLines);
        }

        [Fact]
        public async Task WiegandAndOmron_UseDocumentedFrames()
        {
            var transport = new FakeReaderTransport("COM24");
            transport.QueueResponseBytes(0x00);
            transport.QueueResponseBytes(0x00);
            var device = new TWN4ReaderDevice("COM24", _ => transport);

            await device.SendWiegandAsync(Gpios.GPIO3, Gpios.GPIO4, 100, 1000, new byte[] { 0xAA }, 8);
            await device.SendOmronAsync(Gpios.GPIO3, Gpios.GPIO4, 500, 500, 500, new byte[] { 0xAA }, 8);

            Assert.Equal(new[]
            {
                "040C08106400E80301AA08",
                "040D0810F401F401F40101AA08"
            }, transport.WrittenLines);
        }

        [Fact]
        public async Task AppApiOnlyPeriphMethods_DoNotSendMisleadingSimpleProtocolFrames()
        {
            var transport = new FakeReaderTransport("COM24");
            var device = new TWN4ReaderDevice("COM24", _ => transport);

#pragma warning disable CS0618
            await Assert.ThrowsAsync<NotSupportedException>(() => device.SetSerialModeAsync(SerialMode.Uart, 9600));
            await Assert.ThrowsAsync<NotSupportedException>(() => device.PlaySoundAsync(100));
#pragma warning restore CS0618

            Assert.Empty(transport.WrittenLines);
        }
    }
}
