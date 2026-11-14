using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Elatec.NET.Helpers.ByteArrayHelper.Extensions;
using Elatec.NET.Interfaces;
using Elatec.NET.Sam;
using Xunit;

namespace Elatec.NET.Tests
{
    /// <summary>
    /// Known-answer tests: the expected values are the published examples of NXP AN12704 (MIFARE SAM AV3 - Host
    /// Communication), tables 2 to 5, and the AES-CMAC test vectors of RFC 4493.
    /// </summary>
    public class SamAv3KnownAnswerTests
    {
        internal static byte[] Hex(string hexString)
        {
            return ByteArrayConverter.GetBytesFrom(hexString);
        }

        internal static string ToHex(byte[] bytes)
        {
            return ByteArrayConverter.GetStringFrom(bytes);
        }

        /// <summary>Plays back canned responses and records the commands as hex strings (CLA INS P1 P2 [Lc Data] [Le]).</summary>
        internal sealed class ScriptedSamChannel : ISamApduChannel
        {
            public Queue<SamApduResponse> Responses { get; } = new Queue<SamApduResponse>();

            public List<string> Sent { get; } = new List<string>();

            public void Enqueue(string dataHex, ushort statusWord)
            {
                Responses.Enqueue(new SamApduResponse(Hex(dataHex), statusWord));
            }

            public Task<SamApduResponse> TransmitAsync(SamApdu command)
            {
                Sent.Add(Format(command));
                return Task.FromResult(Responses.Dequeue());
            }

            internal static string Format(SamApdu command)
            {
                var text = new StringBuilder();
                text.Append(ToHex(new[] { command.Cla, command.Ins, command.P1, command.P2 }));
                if (command.Data.Length > 0)
                {
                    text.Append(command.Data.Length.ToString("X2")).Append(ToHex(command.Data));
                }

                if (command.Le.HasValue)
                {
                    text.Append(command.Le.Value.ToString("X2"));
                }

                return text.ToString();
            }
        }

        // ---- AES-CMAC, RFC 4493 ----

        [Theory]
        [InlineData("", "bb1d6929e95937287fa37d129b756746")]
        [InlineData("6bc1bee22e409f96e93d7e117393172a", "070a16b46b4d4144f79bdd9dd04a287c")]
        [InlineData("6bc1bee22e409f96e93d7e117393172aae2d8a571e03ac9c9eb76fac45af8e5130c81c46a35ce411", "dfa66747de9ae63030ca32611497c827")]
        [InlineData("6bc1bee22e409f96e93d7e117393172aae2d8a571e03ac9c9eb76fac45af8e5130c81c46a35ce411e5fbc1191a0a52eff69f2445df4f9b17ad2b417be66c3710", "51f0bebf7e3b9d92fc49741779363cfe")]
        public void AesCmac_MatchesRfc4493Vectors(string messageHex, string expectedHex)
        {
            var key = Hex("2b7e151628aed2a6abf7158809cf4f3c");

            var mac = AesCmac.Compute(key, Hex(messageHex));

            Assert.Equal(Hex(expectedHex), mac);
        }

        // ---- AN12704 table 4 / 5: secure messaging with full protection ----

        private static readonly byte[] Table4Ke = Hex("3056A1804B24B44386F5E1032AA206A9");
        private static readonly byte[] Table4Km = Hex("D03206A036FB41257A8093DB52A2DBC5");

        [Fact]
        public void SecureMessaging_Table4_CommandWithoutDataField()
        {
            var secure = new SamAv3SecureMessaging(Table4Ke, Table4Km, 0);

            // C-APDU 8026010000 (ISO14443-3_ActivateIdle) becomes 802601000804FD77D0FAFF11E500
            var apdu = secure.Protect(0x80, 0x26, 0x01, 0x00, new byte[0], 0x00);

            Assert.Equal("802601000804FD77D0FAFF11E500", ScriptedSamChannel.Format(apdu));
        }

        [Fact]
        public void SecureMessaging_Table4_ResponseIsVerifiedAndDecrypted()
        {
            var secure = new SamAv3SecureMessaging(Table4Ke, Table4Km, 0);

            var plain = secure.Unprotect(new SamApduResponse(Hex("4FE359F6A562BC2E51BA95ED48C9E9F4" + "432959D77D63B69A"), 0x9000));

            Assert.Equal("44032007049137C9922680", ToHex(plain));
            Assert.Equal(1u, secure.Counter);
        }

        [Fact]
        public void SecureMessaging_Table5_CommandWithDataAndResponse()
        {
            // The example continues with the counter at 1.
            var secure = new SamAv3SecureMessaging(Table4Ke, Table4Km, 1);

            // C-APDU 80E000000301000000 (ISO14443-4_RATS_PPS)
            var apdu = secure.Protect(0x80, 0xE0, 0x00, 0x00, Hex("010000"), 0x00);
            Assert.Equal("80E00000181917CFB3C9E585DFA822E3FEC496406247C842647935E3EF00", ScriptedSamChannel.Format(apdu));

            var plain = secure.Unprotect(new SamApduResponse(Hex("983A7DF82021274B40FC3919E00F7269" + "C330BD2316DAD829"), 0x9000));
            Assert.Equal("010000067577810280", ToHex(plain));
            Assert.Equal(2u, secure.Counter);
        }

        [Fact]
        public void SecureMessaging_RejectsManipulatedResponse()
        {
            var secure = new SamAv3SecureMessaging(Table4Ke, Table4Km, 0);

            // last byte of the encrypted data flipped
            var tampered = new SamApduResponse(Hex("4FE359F6A562BC2E51BA95ED48C9E9F5" + "432959D77D63B69A"), 0x9000);

            var exception = Assert.Throws<SamException>(() => secure.Unprotect(tampered));
            Assert.Contains("CMAC", exception.Message);
            Assert.Equal(0u, secure.Counter);
        }

        [Fact]
        public void SecureMessaging_RejectsReplayWithOtherCounter()
        {
            // The response of table 4 is only valid for counter 0 -> 1.
            var secure = new SamAv3SecureMessaging(Table4Ke, Table4Km, 5);

            Assert.Throws<SamException>(() => secure.Unprotect(
                new SamApduResponse(Hex("4FE359F6A562BC2E51BA95ED48C9E9F4" + "432959D77D63B69A"), 0x9000)));
        }

        // ---- AN12704 table 2: SAM_AuthenticateHost, full protection ----

        [Fact]
        public async Task AuthenticateHost_Table2_ProducesDocumentedCommandsAndSessionKeys()
        {
            var channel = new ScriptedSamChannel();
            channel.Enqueue("2509C7B09F2DA8FF6D76578B", 0x90AF);
            channel.Enqueue("E89F438446F5177E" + "03322788AE6DB98C963E12C6DF1F4019", 0x90AF);
            channel.Enqueue("F261C8E49E275A46E210899B3EFD0D58", 0x9000);

            // "Take real random" in the document: Rnd1 and RndA are 000102...
            var randoms = new Queue<byte[]>(new[] { Hex("000102030405060708090A0B"), Hex("000102030405060708090A0B0C0D0E0F") });
            var session = new SamAv3HostSession(channel, count => randoms.Dequeue());

            await session.AuthenticateHostAsync(0x05, 0x01, Hex("000102030405060708090A0B0C0D0E0F"));

            Assert.Equal(new[]
            {
                "80A400000305010200",
                "80A40000149D2231E7B99F0CFF000102030405060708090A0B00",
                "80A40000209379F61F1D6EB335803343620CE9AD045C672F4E8A66666527384A4DB251F45500"
            }, channel.Sent);
            Assert.True(session.IsAuthenticated);
            Assert.Equal("F7B5D7E05FCDA9F12D6F106CB483B66A", ToHex(session.SecureMessaging.SessionKeyEnc));
            Assert.Equal("10CDA5E6BF15A309C4DA69C85B9AACBA", ToHex(session.SecureMessaging.SessionKeyMac));
            Assert.Equal(0u, session.SecureMessaging.Counter);
        }

        // ---- AN12704 table 3: SAM_ChangeKeyEntry, full protection ----

        private static SamAv3KeyEntry Table3Entry(byte keyVersionCek)
        {
            return new SamAv3KeyEntry
            {
                KeyA = Hex("01020304050607080910111213141516"),
                VersionA = 0x00,
                KeyB = Hex("00112233445566778899AABBCCDDEEFF"),
                VersionB = 0x01,
                KeyC = Hex("ABCDEF012345678990817263545E740F"),
                VersionC = 0x02,
                DfAid = Hex("000000"),
                DfKeyNo = 0x00,
                KeyNoCek = 0x00,
                KeyVersionCek = keyVersionCek,
                RefNoKuc = 0x02,
                Set = 0x0120, // transmitted as 20 01: AES-128, host key
                ExtSet = 0x00
            };
        }

        internal const string Table3EncryptedAndMac =
            "4DC47E96DB150A861C932BC74010E5F9BE644C4089E08F9AE05CE76E5FA8EB9BECA650452E1212FEB3A3DD9A03EE8972A0D38083DEA40C69834A2EEDEFA3E407"
            + "47E9C5F61CF0D242";

        [Fact]
        public async Task ChangeKeyEntry_Table3_ProducesDocumentedCommand()
        {
            var channel = new ScriptedSamChannel();
            channel.Enqueue("AA60E01E86561A6F", 0x9000);
            var session = new SamAv3HostSession(channel);
            InstallSession(session, "092D5F2AA78F5A22B5F5A01F931A83FB", "2CA7ADBD4969DD3F22BEC6B5C39952CA");

            // Key entry 0x17, KeyNoCEK 00 / KeyVCEK 00, as in the data that the example encrypts (steps 18/19).
            await session.ChangeKeyEntryAsync(0x17, Table3Entry(0x00));

            // C-APDU 80C117FF48 + 64 byte encrypted data + CMAC, no Le
            Assert.Equal(new[] { "80C117FF48" + Table3EncryptedAndMac }, channel.Sent);
            Assert.Equal(1u, session.SecureMessaging.Counter);
        }

        [Fact]
        public void KeyEntry_Table3_CommandDataIs61BytesInTheDocumentedOrder()
        {
            var data = Table3Entry(0x00).ToCommandData();

            Assert.Equal(61, data.Length);
            Assert.Equal(
                "01020304050607080910111213141516" + "00112233445566778899AABBCCDDEEFF" + "ABCDEF012345678990817263545E740F"
                + "000000" + "00" + "00" + "00" + "02" + "2001" + "00" + "01" + "02" + "00",
                ToHex(data));
        }

        /// <summary>Puts a session into the state after authentication with known session keys.</summary>
        internal static void InstallSession(SamAv3HostSession session, string keHex, string kmHex, uint counter = 0)
        {
            var field = typeof(SamAv3HostSession).GetField("_secureMessaging", BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(session, new SamAv3SecureMessaging(Hex(keHex), Hex(kmHex), counter));
        }
    }
}
