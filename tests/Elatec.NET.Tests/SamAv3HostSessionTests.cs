using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Elatec.NET.Sam;
using Xunit;
using static Elatec.NET.Tests.SamAv3KnownAnswerTests;

namespace Elatec.NET.Tests
{
    public class SamAv3HostSessionTests
    {
        private static readonly byte[] MasterKey = new byte[16]; // the factory master key of a new SAM AV3 is all zeros (NXP UM11316)

        private static FakeSamAv3 NewSam()
        {
            var sam = new FakeSamAv3();
            sam.AddHostKey(0, 0, MasterKey);
            return sam;
        }

        private static readonly byte[] NewKey = Hex("00112233445566778899AABBCCDDEEFF");

        // ---- documentation consistency ----

        [Fact]
        public async Task ChangeKeyEntry_Table3_KeyVcekOf01WouldNotMatchThePublishedCiphertext()
        {
            // Step 14 of AN12704 table 3 prints KeyVCEK as 01, steps 7 and 18 as 00. The published ciphertext and CMAC
            // belong to 00 (see SamAv3KnownAnswerTests), so the 01 in step 14 is a typo of the document.
            var channel = new ScriptedSamChannel();
            channel.Enqueue("AA60E01E86561A6F", 0x9000);
            var session = new SamAv3HostSession(channel);
            InstallSession(session, "092D5F2AA78F5A22B5F5A01F931A83FB", "2CA7ADBD4969DD3F22BEC6B5C39952CA");
            var entry = new SamAv3KeyEntry
            {
                KeyA = Hex("01020304050607080910111213141516"), KeyB = Hex("00112233445566778899AABBCCDDEEFF"), VersionB = 1,
                KeyC = Hex("ABCDEF012345678990817263545E740F"), VersionC = 2, KeyNoCek = 0, KeyVersionCek = 0x01,
                RefNoKuc = 2, Set = 0x0120
            };

            await session.ChangeKeyEntryAsync(0x17, entry);

            Assert.NotEqual("80C117FF48" + Table3EncryptedAndMac, channel.Sent.Single());
        }

        // ---- the flow for a new SAM, against the software SAM ----

        [Fact]
        public async Task NewSamFlow_AuthenticateWithMasterKeyThenWriteKey()
        {
            var sam = NewSam();
            using (var session = new SamAv3HostSession(sam))
            {
                await session.AuthenticateHostAsync(0, 0, MasterKey);
                await session.ChangeKeyEntryAsync(1, SamAv3KeyEntry.CreateAes128PiccKey(NewKey, 3));

                Assert.True(session.IsAuthenticated);
            }

            var written = sam.WrittenEntries[1];
            Assert.Equal(61, written.Length);
            Assert.Equal(NewKey, written.Take(16).ToArray());
            // position B and C repeat position A, with the same version
            Assert.Equal(NewKey, written.Skip(16).Take(16).ToArray());
            Assert.Equal(NewKey, written.Skip(32).Take(16).ToArray());
            Assert.Equal("000000" + "00" + "00" + "00" + "FF" + "2000" + "030303" + "01", ToHex(written.Skip(48).ToArray()));
        }

        [Fact]
        public async Task SessionKeepsCountingAcrossSeveralCommands()
        {
            var sam = NewSam();
            var session = new SamAv3HostSession(sam);
            await session.AuthenticateHostAsync(0, 0, MasterKey);

            await session.ChangeKeyEntryAsync(1, SamAv3KeyEntry.CreateAes128PiccKey(NewKey, 0));
            await session.ChangeKeyEntryAsync(2, SamAv3KeyEntry.CreateAes128PiccKey(NewKey, 0));
            var echoed = await session.ExecuteSecureAsync(0x7F, 0x00, 0x00, Hex("0102030405"), 0x00);
            var echoedFullBlock = await session.ExecuteSecureAsync(0x7F, 0x00, 0x00, Hex("000102030405060708090A0B0C0D0E0F"), 0x00);

            Assert.Equal(4u, sam.Counter);
            Assert.Equal(4u, session.SecureMessaging.Counter);
            Assert.Equal(Hex("0102030405"), echoed);
            Assert.Equal(Hex("000102030405060708090A0B0C0D0E0F"), echoedFullBlock); // exactly one block still round-trips
            Assert.True(sam.WrittenEntries.ContainsKey(1) && sam.WrittenEntries.ContainsKey(2));
        }

        [Fact]
        public async Task AuthenticatingAgainStartsANewSession()
        {
            var sam = NewSam();
            var session = new SamAv3HostSession(sam);
            await session.AuthenticateHostAsync(0, 0, MasterKey);
            await session.ChangeKeyEntryAsync(1, SamAv3KeyEntry.CreateAes128PiccKey(NewKey, 0));
            var firstKe = session.SecureMessaging.SessionKeyEnc;

            await session.AuthenticateHostAsync(0, 0, MasterKey);

            Assert.Equal(0u, session.SecureMessaging.Counter);
            Assert.NotEqual(firstKe, session.SecureMessaging.SessionKeyEnc); // fresh random numbers -> fresh session keys
        }

        [Fact]
        public async Task WrongHostKey_FailsAndLeavesNoSession()
        {
            var sam = NewSam();
            var session = new SamAv3HostSession(sam);

            var exception = await Assert.ThrowsAsync<SamException>(() => session.AuthenticateHostAsync(0, 0, NewKey));

            Assert.Equal(0x6982, exception.StatusWord); // the software SAM rejects the CMAC of part 2
            Assert.False(session.IsAuthenticated);
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                session.ChangeKeyEntryAsync(1, SamAv3KeyEntry.CreateAes128PiccKey(NewKey, 0)));
        }

        [Fact]
        public async Task UnknownKeyEntry_FailsWithTheStatusWordOfTheSam()
        {
            var session = new SamAv3HostSession(NewSam());

            var exception = await Assert.ThrowsAsync<SamException>(() => session.AuthenticateHostAsync(7, 0, MasterKey));

            Assert.Equal(0x6A88, exception.StatusWord);
        }

        // The Table 2 transcript with one response of the SAM altered: the host must not accept an impostor SAM.

        private static ScriptedSamChannel Table2Channel(string part2Response, string part3Response)
        {
            var channel = new ScriptedSamChannel();
            channel.Enqueue("2509C7B09F2DA8FF6D76578B", 0x90AF);
            channel.Enqueue(part2Response, 0x90AF);
            channel.Enqueue(part3Response, 0x9000);
            return channel;
        }

        private static SamAv3HostSession Table2Session(ScriptedSamChannel channel)
        {
            var randoms = new Queue<byte[]>(new[] { Hex("000102030405060708090A0B"), Hex("000102030405060708090A0B0C0D0E0F") });
            return new SamAv3HostSession(channel, count => randoms.Dequeue());
        }

        private static readonly byte[] Table2Key = Hex("000102030405060708090A0B0C0D0E0F");

        [Fact]
        public async Task ImpostorSam_WithWrongProofInPart2_IsRejected()
        {
            // first byte of the SAM's CMAC changed (E8 -> E9)
            var channel = Table2Channel("E99F438446F5177E" + "03322788AE6DB98C963E12C6DF1F4019", "F261C8E49E275A46E210899B3EFD0D58");
            var session = Table2Session(channel);

            var exception = await Assert.ThrowsAsync<SamException>(() => session.AuthenticateHostAsync(0x05, 0x01, Table2Key));

            Assert.Contains("CMAC mismatch", exception.Message);
            Assert.False(session.IsAuthenticated);
            Assert.Equal(2, channel.Sent.Count); // part 3 was never sent
        }

        [Fact]
        public async Task ImpostorSam_WithWrongProofInPart3_IsRejected()
        {
            // last byte of the SAM's final response changed (58 -> 59)
            var channel = Table2Channel("E89F438446F5177E" + "03322788AE6DB98C963E12C6DF1F4019", "F261C8E49E275A46E210899B3EFD0D59");
            var session = Table2Session(channel);

            var exception = await Assert.ThrowsAsync<SamException>(() => session.AuthenticateHostAsync(0x05, 0x01, Table2Key));

            Assert.Contains("RndA mismatch", exception.Message);
            Assert.False(session.IsAuthenticated);
        }

        [Theory]
        [InlineData(0x6982)]
        [InlineData(0x6A88)]
        [InlineData(0x9000)] // success without the expected "additional frame" is not acceptable in part 1
        public async Task UnexpectedStatusInPart1_IsReported(ushort status)
        {
            var channel = new ScriptedSamChannel();
            channel.Enqueue("", status);
            var session = new SamAv3HostSession(channel);

            var exception = await Assert.ThrowsAsync<SamException>(() => session.AuthenticateHostAsync(0, 0, Table2Key));

            Assert.Equal(status, exception.StatusWord);
            Assert.False(session.IsAuthenticated);
        }

        [Fact]
        public async Task ManipulatedResponse_IsDetectedAndDiscardsTheSession()
        {
            var sam = NewSam();
            var session = new SamAv3HostSession(sam);
            await session.AuthenticateHostAsync(0, 0, MasterKey);
            sam.CorruptNextResponseMac = true;

            var exception = await Assert.ThrowsAsync<SamException>(() =>
                session.ChangeKeyEntryAsync(1, SamAv3KeyEntry.CreateAes128PiccKey(NewKey, 0)));

            Assert.Contains("CMAC", exception.Message);
            Assert.False(session.IsAuthenticated);
        }

        [Fact]
        public async Task ErrorStatusFromTheSam_IsReportedAndDiscardsTheSession()
        {
            var sam = NewSam();
            var session = new SamAv3HostSession(sam);
            await session.AuthenticateHostAsync(0, 0, MasterKey);
            sam.ChangeKeyEntryStatus = 0x6985;

            var exception = await Assert.ThrowsAsync<SamException>(() =>
                session.ChangeKeyEntryAsync(1, SamAv3KeyEntry.CreateAes128PiccKey(NewKey, 0)));

            Assert.Equal(0x6985, exception.StatusWord);
            Assert.False(session.IsAuthenticated);
        }

        [Fact]
        public async Task Dispose_DiscardsTheSession()
        {
            var sam = NewSam();
            var session = new SamAv3HostSession(sam);
            await session.AuthenticateHostAsync(0, 0, MasterKey);

            session.Dispose();

            Assert.False(session.IsAuthenticated);
        }

        // ---- safeguards ----

        [Fact]
        public async Task ChangeKeyEntry_RequiresAuthentication()
        {
            var session = new SamAv3HostSession(NewSam());

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                session.ChangeKeyEntryAsync(1, SamAv3KeyEntry.CreateAes128PiccKey(NewKey, 0)));
        }

        [Fact]
        public async Task ChangeKeyEntry_RefusesTheMasterKeyEntryUnlessAllowed()
        {
            var sam = NewSam();
            var session = new SamAv3HostSession(sam);
            await session.AuthenticateHostAsync(0, 0, MasterKey);
            var commandsBefore = sam.Sent.Count;

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                session.ChangeKeyEntryAsync(0, SamAv3KeyEntry.CreateAes128HostKey(NewKey, 1)));
            Assert.Equal(commandsBefore, sam.Sent.Count); // nothing was sent
            Assert.True(session.IsAuthenticated);

            await session.ChangeKeyEntryAsync(0, SamAv3KeyEntry.CreateAes128HostKey(NewKey, 1), allowMasterKeyChange: true);
            Assert.True(sam.WrittenEntries.ContainsKey(0));
        }

        [Theory]
        [InlineData(0xFF)]
        [InlineData(0x80)]
        [InlineData(0xFD)]
        public async Task ChangeKeyEntry_RefusesAnIrreversibleLockUnlessAllowed(byte keyNoCek)
        {
            var sam = NewSam();
            var session = new SamAv3HostSession(sam);
            await session.AuthenticateHostAsync(0, 0, MasterKey);
            var entry = SamAv3KeyEntry.CreateAes128PiccKey(NewKey, 0);
            entry.KeyNoCek = keyNoCek;

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => session.ChangeKeyEntryAsync(1, entry));
            Assert.Contains("irreversibly", exception.Message);

            await session.ChangeKeyEntryAsync(1, entry, allowIrreversibleLock: true);
            Assert.Equal(keyNoCek, sam.WrittenEntries[1][52]); // KeyNoCEK
        }

        [Theory]
        [InlineData(0x00)]
        [InlineData(0x7F)]
        [InlineData(0xFE)] // free change: not irreversible
        public async Task ChangeKeyEntry_AllowsReversibleChangeConditionsWithoutOptIn(byte keyNoCek)
        {
            var sam = NewSam();
            var session = new SamAv3HostSession(sam);
            await session.AuthenticateHostAsync(0, 0, MasterKey);
            var entry = SamAv3KeyEntry.CreateAes128PiccKey(NewKey, 0);
            entry.KeyNoCek = keyNoCek;

            await session.ChangeKeyEntryAsync(1, entry);

            Assert.True(sam.WrittenEntries.ContainsKey(1));
        }

        [Fact]
        public async Task ChangeKeyEntry_ValidatesArgumentsBeforeSendingAnything()
        {
            var sam = NewSam();
            var session = new SamAv3HostSession(sam);
            await session.AuthenticateHostAsync(0, 0, MasterKey);
            var sentAfterAuthentication = sam.Sent.Count;

            await Assert.ThrowsAsync<ArgumentNullException>(() => session.ChangeKeyEntryAsync(1, null));
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
                session.ChangeKeyEntryAsync(128, SamAv3KeyEntry.CreateAes128PiccKey(NewKey, 0)));
            await Assert.ThrowsAsync<ArgumentException>(() =>
                session.ChangeKeyEntryAsync(1, SamAv3KeyEntry.CreateAes128PiccKey(new byte[15], 0)));
            await Assert.ThrowsAsync<ArgumentException>(() =>
                session.ChangeKeyEntryAsync(1, new SamAv3KeyEntry { KeyA = null, Set = SamAv3KeyEntry.SetKeyTypeAes128 }));
            await Assert.ThrowsAsync<ArgumentException>(() => // RefNoKUC out of range
                session.ChangeKeyEntryAsync(1, new SamAv3KeyEntry { KeyA = NewKey, Set = SamAv3KeyEntry.SetKeyTypeAes128, RefNoKuc = 16 }));
            await Assert.ThrowsAsync<NotSupportedException>(() => // 3DES / other key types are not supported
                session.ChangeKeyEntryAsync(1, new SamAv3KeyEntry { KeyA = NewKey, Set = 0x0000 }));

            Assert.Equal(sentAfterAuthentication, sam.Sent.Count);
            Assert.True(session.IsAuthenticated);
        }

        [Fact]
        public async Task AuthenticateHost_ValidatesTheKey()
        {
            var session = new SamAv3HostSession(NewSam());

            await Assert.ThrowsAsync<ArgumentNullException>(() => session.AuthenticateHostAsync(0, 0, null));
            await Assert.ThrowsAsync<ArgumentException>(() => session.AuthenticateHostAsync(0, 0, new byte[24]));
        }

        // ---- key entry model ----

        [Fact]
        public void KeyEntryFactories_SetTheDocumentedBits()
        {
            var host = SamAv3KeyEntry.CreateAes128HostKey(NewKey, 2);
            var picc = SamAv3KeyEntry.CreateAes128PiccKey(NewKey, 2);

            Assert.Equal(0x0120, host.Set); // AES-128 | host authentication key (the value of the NXP example)
            Assert.Equal(0x00, host.ExtSet);
            Assert.True(host.IsHostKey);
            Assert.Equal(0x0020, picc.Set);
            Assert.Equal(0x01, picc.ExtSet);
            Assert.False(picc.IsHostKey);
            Assert.Equal(0xFF, host.RefNoKuc); // no key usage counter unless asked for
            Assert.Equal(0x00, host.KeyNoCek); // changes need the master key by default
        }

        [Fact]
        public void KeyEntry_OnlyPositionAWritesIdenticalPositionsBAndC()
        {
            var data = SamAv3KeyEntry.CreateAes128PiccKey(NewKey, 7).ToCommandData();

            Assert.Equal(data.Take(16), data.Skip(16).Take(16));
            Assert.Equal(data.Take(16), data.Skip(32).Take(16));
            Assert.Equal(new byte[] { 7, 7, 7 }, data.Skip(57).Take(3).ToArray()); // Va Vb Vc
        }

        [Fact]
        public void KeyEntry_ExplicitPositionsKeepTheirOwnKeysAndVersions()
        {
            var keyB = Hex("11111111111111111111111111111111");
            var data = new SamAv3KeyEntry { KeyA = NewKey, VersionA = 1, KeyB = keyB, VersionB = 2, Set = SamAv3KeyEntry.SetKeyTypeAes128 }.ToCommandData();

            Assert.Equal(keyB, data.Skip(16).Take(16).ToArray());
            Assert.Equal(NewKey, data.Skip(32).Take(16).ToArray()); // C not given -> A
            Assert.Equal(new byte[] { 1, 2, 1 }, data.Skip(57).Take(3).ToArray());
        }
    }
}
