using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Elatec.NET.Sam;
using Xunit;
using static Elatec.NET.Tests.SamAv3KnownAnswerTests;

namespace Elatec.NET.Tests
{
    /// <summary>
    /// Tests for the activation of a new SAM (SAM_LockUnlock, P1 = 0x03). There is no NXP test vector for it, the
    /// expectations follow an open-source SAM AV2 library (command bytes) and the three-pass authentication of AN12704.
    /// </summary>
    public class SamActivationTests
    {
        private static readonly byte[] ZeroKey = new byte[16];

        private static FakeSamAv3 NewUnactivatedSam()
        {
            var sam = new FakeSamAv3(requireActivation: true);
            sam.AddHostKey(0, 0, ZeroKey);
            return sam;
        }

        [Fact]
        public async Task Activate_SendsTheThreeCommandsOfTheReferenceImplementation()
        {
            var sam = NewUnactivatedSam();
            sam.AddHostKey(7, 2, ZeroKey);
            var session = new SamAv3HostSession(sam);

            await session.ActivateSamAsync(7, 2, ZeroKey);

            Assert.Equal(3, sam.Sent.Count);
            // 80 10 [P1 03] 00 [Lc 05] [KeyNo] [KeyVer] [MaxChainBlocks 00 00 00] [Le 00]
            Assert.Equal("80100300" + "05" + "0702" + "000000" + "00", sam.Sent[0]);
            // 80 10 00 00 [Lc 14] [CMAC 8] [Rnd1 12] [Le 00]
            Assert.StartsWith("80100000" + "14", sam.Sent[1]);
            Assert.Equal((4 + 1 + 20 + 1) * 2, sam.Sent[1].Length);
            Assert.EndsWith("00", sam.Sent[1]);
            // 80 10 00 00 [Lc 20] [Ek(RndA | RndB')] [Le 00]
            Assert.StartsWith("80100000" + "20", sam.Sent[2]);
            Assert.Equal((4 + 1 + 32 + 1) * 2, sam.Sent[2].Length);
            Assert.EndsWith("00", sam.Sent[2]);
            Assert.True(sam.IsActivated);
        }

        [Fact]
        public async Task Activate_Part2ContainsTheCmacOverRnd2AndTheLockUnlockMode()
        {
            // The CMAC input is Rnd2 | 03 | 000000 (the mode byte is 03, not the host mode 02 of SAM_AuthenticateHost).
            var channel = new ScriptedSamChannel();
            channel.Enqueue("2509C7B09F2DA8FF6D76578B", 0x90AF);
            channel.Enqueue("0000000000000000" + new string('0', 32), 0x90AF); // content irrelevant: the host stops at the CMAC check
            var session = new SamAv3HostSession(channel, count => Hex("000102030405060708090A0B"));

            await Assert.ThrowsAsync<SamException>(() => session.ActivateSamAsync(0, 0, Hex("000102030405060708090A0B0C0D0E0F")));

            var full = AesCmac.Compute(Hex("000102030405060708090A0B0C0D0E0F"), Hex("2509C7B09F2DA8FF6D76578B03000000"));
            var expectedCmac = ToHex(Enumerable.Range(0, 8).Select(i => full[2 * i + 1]).ToArray());
            Assert.Equal("80100000" + "14" + expectedCmac + "000102030405060708090A0B" + "00", channel.Sent[1]);
        }

        // ---- the sequence for a new SAM ----

        [Fact]
        public async Task NewSam_RefusesHostAuthenticationUntilItIsActivated_ThenAcceptsAKeyWrite()
        {
            var sam = NewUnactivatedSam();
            var session = new SamAv3HostSession(sam);

            // NXP UM11316: a new SAM is unactivated and cannot be host-authenticated.
            var refused = await Assert.ThrowsAsync<SamException>(() => session.AuthenticateHostAsync(0, 0, ZeroKey));
            Assert.Equal(0x6985, refused.StatusWord);
            Assert.False(sam.IsActivated);

            await session.ActivateSamAsync(0, 0, ZeroKey);
            Assert.True(sam.IsActivated);
            Assert.False(session.IsAuthenticated); // activation does not open a session

            await session.AuthenticateHostAsync(0, 0, ZeroKey);
            var newKey = Hex("00112233445566778899AABBCCDDEEFF");
            await session.ChangeKeyEntryAsync(1, SamAv3KeyEntry.CreateAes128PiccKey(newKey, 0));

            Assert.Equal(newKey, sam.WrittenEntries[1].Take(16).ToArray());
        }

        [Fact]
        public async Task Activate_WithTheWrongKey_DoesNotActivate()
        {
            var sam = NewUnactivatedSam();
            var session = new SamAv3HostSession(sam);

            var exception = await Assert.ThrowsAsync<SamException>(() =>
                session.ActivateSamAsync(0, 0, Hex("FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF")));

            Assert.Equal(0x6982, exception.StatusWord);
            Assert.False(sam.IsActivated);
            Assert.Equal(2, sam.Sent.Count); // refused at part 2, the final command was never sent
        }

        [Fact]
        public async Task Activate_WithAnUnknownKeyEntry_ReportsTheStatusOfTheSam()
        {
            var session = new SamAv3HostSession(NewUnactivatedSam());

            var exception = await Assert.ThrowsAsync<SamException>(() => session.ActivateSamAsync(9, 0, ZeroKey));

            Assert.Equal(0x6A88, exception.StatusWord);
        }

        [Fact]
        public async Task Activate_DiscardsAnExistingSession()
        {
            var sam = new FakeSamAv3(); // already activated
            sam.AddHostKey(0, 0, ZeroKey);
            var session = new SamAv3HostSession(sam);
            await session.AuthenticateHostAsync(0, 0, ZeroKey);
            Assert.True(session.IsAuthenticated);

            await session.ActivateSamAsync(0, 0, ZeroKey);

            Assert.False(session.IsAuthenticated);
        }

        // ---- what the final response looks like is not documented for AV3 ----

        [Fact]
        public async Task Activate_AcceptsAFinalResponseWithoutProof()
        {
            var sam = NewUnactivatedSam();
            sam.ActivationReturnsProof = false;
            var session = new SamAv3HostSession(sam);

            await session.ActivateSamAsync(0, 0, ZeroKey);

            Assert.True(sam.IsActivated);
        }

        [Fact]
        public async Task Activate_WithAnInvalidProof_StatesThatTheSamMayBeActivatedNevertheless()
        {
            var sam = NewUnactivatedSam();
            sam.CorruptActivationProof = true;
            var session = new SamAv3HostSession(sam);

            var exception = await Assert.ThrowsAsync<SamException>(() => session.ActivateSamAsync(0, 0, ZeroKey));

            Assert.Contains("may nevertheless have taken effect", exception.Message);
            Assert.True(sam.IsActivated); // exactly the situation the message warns about
        }

        [Fact]
        public async Task Activate_RejectsASamThatCannotProveTheKey_BeforeSendingTheFinalCommand()
        {
            var channel = new ScriptedSamChannel();
            channel.Enqueue("2509C7B09F2DA8FF6D76578B", 0x90AF);
            channel.Enqueue("E99F438446F5177E" + "03322788AE6DB98C963E12C6DF1F4019", 0x90AF); // wrong CMAC
            var session = new SamAv3HostSession(channel, count => Hex("000102030405060708090A0B"));

            var exception = await Assert.ThrowsAsync<SamException>(() => session.ActivateSamAsync(0, 0, Hex("000102030405060708090A0B0C0D0E0F")));

            Assert.Contains("CMAC mismatch", exception.Message);
            Assert.Equal(2, channel.Sent.Count);
        }

        [Fact]
        public async Task Activate_ValidatesTheKey()
        {
            var session = new SamAv3HostSession(NewUnactivatedSam());

            await Assert.ThrowsAsync<ArgumentNullException>(() => session.ActivateSamAsync(0, 0, null));
            await Assert.ThrowsAsync<ArgumentException>(() => session.ActivateSamAsync(0, 0, new byte[8]));
        }
    }
}
