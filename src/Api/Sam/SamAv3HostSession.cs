using System;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Elatec.NET.Interfaces;

namespace Elatec.NET.Sam
{
    /// <summary>
    /// A host session with a MIFARE SAM AV3: AES host authentication with full protection (encrypted and MACed
    /// communication) and the commands that need it, e.g. writing a key entry.
    /// Implemented after NXP AN12704 (MIFARE SAM AV3 - Host Communication).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Typical use with a SAM in slot SAM1 of a TWN4 reader:
    /// </para>
    /// <code>
    /// await reader.ISO7816_IccPowerOnAsync(Iso7816Channel.Sam1);
    /// // a SAM that was never activated needs the SAM_LockUnlock activation first, see below
    /// using (var sam = new SamAv3HostSession(new Twn4SamApduChannel(reader, Iso7816Channel.Sam1)))
    /// {
    ///     await sam.AuthenticateHostAsync(0, 0, masterKey);
    ///     await sam.ChangeKeyEntryAsync(1, SamAv3KeyEntry.CreateAes128PiccKey(newKey, 0));
    /// }
    /// await reader.ISO7816_IccPowerOffAsync(Iso7816Channel.Sam1);
    /// </code>
    /// <para>
    /// <b>Activation is not part of this class.</b> According to NXP's evaluation board manual (UM11316) a new
    /// MIFARE SAM AV3 is delivered unactivated and must be activated with SAM_LockUnlock (activation option)
    /// before host authentication works. If SET bit 10 is set on the master key entry, SAM_LockUnlock is required
    /// after every SAM reset. That command is not described in the application notes this class is based on.
    /// </para>
    /// <para>
    /// Only AES-128 host keys, host mode "full protection" and logical channel 0 are supported. Not thread safe.
    /// After any failed command the session is discarded and <see cref="AuthenticateHostAsync"/> must be called again.
    /// </para>
    /// </remarks>
    public sealed class SamAv3HostSession : IDisposable
    {
        private const byte Cla = 0x80;
        private const byte InsAuthenticateHost = 0xA4;
        private const byte InsChangeKeyEntry = 0xC1;
        private const byte InsLockUnlock = 0x10;
        private const byte LockUnlockActivate = 0x03;
        private const byte HostModeFullProtection = 0x02;
        private const byte UpdateAllFields = 0xFF;
        private const byte MaxKeyEntryNumber = 127;
        private const int HostKeyLength = 16;

        private readonly ISamApduChannel _channel;
        private readonly Func<int, byte[]> _randomSource;
        private SamAv3SecureMessaging _secureMessaging;

        public SamAv3HostSession(ISamApduChannel channel) : this(channel, null)
        {
        }

        internal SamAv3HostSession(ISamApduChannel channel, Func<int, byte[]> randomSource)
        {
            _channel = channel ?? throw new ArgumentNullException(nameof(channel));
            _randomSource = randomSource ?? CreateRandom;
        }

        /// <summary><see langword="true"/> after a successful <see cref="AuthenticateHostAsync"/>.</summary>
        public bool IsAuthenticated
        {
            get { return _secureMessaging != null; }
        }

        /// <summary>The secure messaging state of the current session, for tests.</summary>
        internal SamAv3SecureMessaging SecureMessaging
        {
            get { return _secureMessaging; }
        }

        /// <summary>
        /// Performs the mutual three-pass host authentication with an AES-128 key and full protection.
        /// </summary>
        /// <param name="keyNo">The key entry of the SAM that holds the host key (0 is the SAM master key).</param>
        /// <param name="keyVersion">The version of the key in that entry.</param>
        /// <param name="hostKey">The 16 byte AES host key.</param>
        /// <remarks>
        /// A failed authentication still counts as a use of the key: if a key usage counter is linked to the entry,
        /// wrong keys consume its limit (AN12701, 4.5).
        /// </remarks>
        /// <exception cref="SamException">The SAM rejected the authentication or its response failed verification.</exception>
        public async Task AuthenticateHostAsync(byte keyNo, byte keyVersion, byte[] hostKey)
        {
            if (hostKey == null) throw new ArgumentNullException(nameof(hostKey));
            if (hostKey.Length != HostKeyLength) throw new ArgumentException("The host key must be 16 bytes (AES-128).", nameof(hostKey));

            Invalidate();

            var kx = (byte[])hostKey.Clone();
            try
            {
                var result = await RunThreePassAuthenticationAsync(InsAuthenticateHost, 0x00,
                    new[] { keyNo, keyVersion, HostModeFullProtection }, HostModeFullProtection, kx, true);

                // SVKe = RndA[11..15] | RndB[11..15] | RndA[4..8] xor RndB[4..8] | 0x81
                // SVKm = RndA[7..11]  | RndB[7..11]  | RndA[0..4] xor RndB[0..4] | 0x82
                var svKe = SamAv3Crypto.Concat(
                    SamAv3Crypto.Slice(result.RndA, 11, 5),
                    SamAv3Crypto.Slice(result.RndB, 11, 5),
                    SamAv3Crypto.Xor(result.RndA, 4, result.RndB, 4, 5),
                    new byte[] { 0x81 });
                var svKm = SamAv3Crypto.Concat(
                    SamAv3Crypto.Slice(result.RndA, 7, 5),
                    SamAv3Crypto.Slice(result.RndB, 7, 5),
                    SamAv3Crypto.Xor(result.RndA, 0, result.RndB, 0, 5),
                    new byte[] { 0x82 });

                _secureMessaging = new SamAv3SecureMessaging(AesHelper.EncryptBlock(kx, svKe), AesHelper.EncryptBlock(kx, svKm));
            }
            finally
            {
                Array.Clear(kx, 0, kx.Length);
            }
        }

        /// <summary>
        /// Activates a new, unactivated SAM with SAM_LockUnlock (P1 = 0x03, the "activation" option). NXP's evaluation
        /// board manual (UM11316) states that a new MIFARE SAM AV3 must be activated first, host authentication fails before.
        /// </summary>
        /// <param name="keyNo">The key entry that holds the activation key (the master key entry 0 on a new SAM).</param>
        /// <param name="keyVersion">The version of that key (0 on a new SAM).</param>
        /// <param name="key">The 16 byte AES key. NXP states that it is an all-zero key on a new SAM.</param>
        /// <remarks>
        /// <para>
        /// <b>Not verified against a SAM AV3.</b> The command is not described in the AV3 application notes. It is
        /// implemented after an open-source SAM AV2 library, where the same call (P1 = 0x03) switches a SAM from AV1 to
        /// AV2 mode, and it uses the same three-pass authentication that AN12704 specifies for SAM_AuthenticateHost.
        /// The data sheet of the SAM (DS3235) is the authoritative description.
        /// </para>
        /// <para>
        /// Activation changes the state of the SAM and should be assumed to be irreversible. Any existing session is
        /// discarded. Afterwards call <see cref="AuthenticateHostAsync"/>. If SET bit 10 is set on the master key
        /// entry, SAM_LockUnlock is required again after every SAM reset (AN12701), this method only implements the activation.
        /// </para>
        /// <para>
        /// Activation is a state change that takes effect with the last of the three commands. If the SAM answers that
        /// command with success but the proof it returns is invalid, a <see cref="SamException"/> is thrown although
        /// the SAM may have been activated: check with <see cref="AuthenticateHostAsync"/>.
        /// </para>
        /// </remarks>
        /// <exception cref="SamException">The SAM rejected the command or its response failed verification.</exception>
        public async Task ActivateSamAsync(byte keyNo, byte keyVersion, byte[] key)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (key.Length != HostKeyLength) throw new ArgumentException("The key must be 16 bytes (AES-128).", nameof(key));

            Invalidate();

            var kx = (byte[])key.Clone();
            try
            {
                // Part 1 data: [KeyNo][KeyVersion][MaxChainBlocks, 3 bytes, not used here]
                await RunThreePassAuthenticationAsync(InsLockUnlock, LockUnlockActivate,
                    new[] { keyNo, keyVersion, (byte)0x00, (byte)0x00, (byte)0x00 }, LockUnlockActivate, kx, false);
            }
            finally
            {
                Array.Clear(kx, 0, kx.Length);
            }
        }

        private sealed class ThreePassResult
        {
            public byte[] RndA;
            public byte[] RndB;
        }

        /// <summary>
        /// The mutual three-pass authentication shared by SAM_AuthenticateHost and SAM_LockUnlock: the SAM sends
        /// Rnd2, the host answers with a CMAC and Rnd1, the SAM proves the key with a CMAC and the encrypted RndB,
        /// the host returns RndA | RndB rotated, and the SAM proves RndA.
        /// </summary>
        /// <param name="ins">The instruction byte of all three commands.</param>
        /// <param name="p1">P1 of the first command (0 for the following ones).</param>
        /// <param name="part1Data">The data field of the first command.</param>
        /// <param name="modeByte">The mode byte that is part of the CMAC input (host mode, or the LockUnlock mode).</param>
        /// <param name="kx">The authentication key.</param>
        /// <param name="requireFinalProof">
        /// Whether the final response must carry the SAM's proof of RndA. If not, a response without that proof is accepted.
        /// </param>
        private async Task<ThreePassResult> RunThreePassAuthenticationAsync(byte ins, byte p1, byte[] part1Data, byte modeByte, byte[] kx, bool requireFinalProof)
        {
            byte[] kxe = null;
            try
            {
                // Part 1: the SAM answers with its 12 byte random Rnd2.
                var part1 = await _channel.TransmitAsync(new SamApdu(Cla, ins, p1, 0x00, part1Data, 0x00));
                ExpectStatus(part1, 0x90AF, "authentication (part 1)");
                if (part1.Data.Length != 12) throw new SamException("The SAM returned an unexpected random number length.", part1.StatusWord);
                var rnd2 = part1.Data;

                // Part 2: CMAC(Rnd2 | mode | padding) and our 12 byte random Rnd1.
                var rnd1 = Random(12);
                var mac = SamAv3Crypto.Cmac8(kx, SamAv3Crypto.Concat(rnd2, ModeBlock(modeByte)));
                var part2 = await _channel.TransmitAsync(new SamApdu(Cla, ins, 0x00, 0x00, SamAv3Crypto.Concat(mac, rnd1), 0x00));
                ExpectStatus(part2, 0x90AF, "authentication (part 2)");
                if (part2.Data.Length != 8 + 16) throw new SamException("The SAM returned an unexpected authentication response length.", part2.StatusWord);

                // Kxe encrypts the rest of the authentication. SV1 = Rnd1[7..11] | Rnd2[7..11] | Rnd1[0..4] xor Rnd2[0..4] | 0x91
                kxe = AesHelper.EncryptBlock(kx, SamAv3Crypto.Concat(
                    SamAv3Crypto.Slice(rnd1, 7, 5),
                    SamAv3Crypto.Slice(rnd2, 7, 5),
                    SamAv3Crypto.Xor(rnd1, 0, rnd2, 0, 5),
                    new byte[] { 0x91 }));

                // The SAM proves it knows the key: CMAC(Rnd1 | mode | padding).
                var expectedMac = SamAv3Crypto.Cmac8(kx, SamAv3Crypto.Concat(rnd1, ModeBlock(modeByte)));
                if (!SamAv3Crypto.FixedTimeEquals(expectedMac, SamAv3Crypto.Slice(part2.Data, 0, 8)))
                    throw new SamException("The SAM failed to prove knowledge of the key (CMAC mismatch).", part2.StatusWord);

                var rndB = AesHelper.DecryptCbc(kxe, new byte[AesHelper.BlockSize], SamAv3Crypto.Slice(part2.Data, 8, 16));

                // Part 3: Ek(Kxe, RndA | RndB rotated left by 2 bytes)
                var rndA = Random(16);
                var part3Data = AesHelper.EncryptCbc(kxe, new byte[AesHelper.BlockSize],
                    SamAv3Crypto.Concat(rndA, SamAv3Crypto.RotateLeft(rndB, 2)));
                var part3 = await _channel.TransmitAsync(new SamApdu(Cla, ins, 0x00, 0x00, part3Data, 0x00));
                ExpectStatus(part3, 0x9000, "authentication (part 3)");

                // The SAM proves it knows RndA: Ek(Kxe, RndA rotated left by 2 bytes)
                if (part3.Data.Length == 16)
                {
                    var rndAFromSam = AesHelper.DecryptCbc(kxe, new byte[AesHelper.BlockSize], part3.Data);
                    if (!SamAv3Crypto.FixedTimeEquals(rndAFromSam, SamAv3Crypto.RotateLeft(rndA, 2)))
                        throw new SamException(requireFinalProof
                            ? "The SAM failed to prove knowledge of the key (RndA mismatch)."
                            : "The SAM reported success, but the proof of the key it returned (RndA) is invalid. The operation may nevertheless have taken effect.",
                            part3.StatusWord);
                }
                else if (requireFinalProof)
                {
                    throw new SamException("The SAM returned an unexpected authentication response length.", part3.StatusWord);
                }

                return new ThreePassResult { RndA = rndA, RndB = rndB };
            }
            finally
            {
                if (kxe != null) Array.Clear(kxe, 0, kxe.Length);
            }
        }

        /// <summary>
        /// Writes a key entry of the SAM (SAM_ChangeKeyEntry, all fields). Requires <see cref="AuthenticateHostAsync"/>
        /// with the key entry (KeyNoCEK/KeyVCEK) that the entry to be changed demands. A new SAM has
        /// KeyNoCEK 0 on its entries, so the master key (entry 0, version 0) is needed.
        /// </summary>
        /// <param name="keyEntryNo">The key entry to write, 0 to 127.</param>
        /// <param name="entry">The new content of the key entry.</param>
        /// <param name="allowMasterKeyChange">
        /// Must be <see langword="true"/> to write entry 0, the SAM master key. If the new master key is lost
        /// or wrongly configured the SAM can no longer be managed.
        /// </param>
        /// <param name="allowIrreversibleLock">
        /// Must be <see langword="true"/> if <see cref="SamAv3KeyEntry.KeyNoCek"/> is 0xFF or any other value above 127
        /// (except 0xFE): that irreversibly locks the entry against changes with symmetric keys.
        /// </param>
        /// <exception cref="InvalidOperationException">Not authenticated, or a safeguard was not lifted.</exception>
        /// <exception cref="SamException">The SAM rejected the command or its response failed verification.</exception>
        public async Task ChangeKeyEntryAsync(byte keyEntryNo, SamAv3KeyEntry entry, bool allowMasterKeyChange = false, bool allowIrreversibleLock = false)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            if (keyEntryNo > MaxKeyEntryNumber) throw new ArgumentOutOfRangeException(nameof(keyEntryNo), "Key entry numbers range from 0 to 127.");
            if (keyEntryNo == 0 && !allowMasterKeyChange)
                throw new InvalidOperationException("Key entry 0 is the SAM master key. Pass allowMasterKeyChange: true if you really want to replace it.");
            if (entry.KeyNoCek > MaxKeyEntryNumber && entry.KeyNoCek != SamAv3KeyEntry.ChangeFree && !allowIrreversibleLock)
                throw new InvalidOperationException("KeyNoCEK " + entry.KeyNoCek.ToString("X2") + " irreversibly locks the key entry. Pass allowIrreversibleLock: true if you really want that.");

            var data = entry.ToCommandData();
            try
            {
                await ExecuteSecureAsync(InsChangeKeyEntry, keyEntryNo, UpdateAllFields, data, null);
            }
            finally
            {
                Array.Clear(data, 0, data.Length);
            }
        }

        /// <summary>
        /// Sends a command with full protection (CLA 0x80) and returns the decrypted response data.
        /// For commands not wrapped by this class; the command counter and MACs are handled here.
        /// </summary>
        /// <param name="ins">The instruction byte.</param>
        /// <param name="p1">Parameter 1.</param>
        /// <param name="p2">Parameter 2.</param>
        /// <param name="data">The plain data field, <see langword="null"/> for none.</param>
        /// <param name="le">Le, <see langword="null"/> if the command has none (the SAM still returns its CMAC).</param>
        /// <exception cref="InvalidOperationException">Not authenticated.</exception>
        /// <exception cref="SamException">The SAM returned an error or its response failed verification. The session is discarded.</exception>
        public async Task<byte[]> ExecuteSecureAsync(byte ins, byte p1, byte p2, byte[] data = null, byte? le = null)
        {
            var secureMessaging = _secureMessaging;
            if (secureMessaging == null)
                throw new InvalidOperationException("Not authenticated, call AuthenticateHostAsync first.");

            var command = secureMessaging.Protect(Cla, ins, p1, p2, data ?? new byte[0], le);
            try
            {
                var response = await _channel.TransmitAsync(command);
                return secureMessaging.Unprotect(response);
            }
            catch
            {
                Invalidate();
                throw;
            }
        }

        /// <summary>Discards the session keys.</summary>
        public void Dispose()
        {
            Invalidate();
        }

        private void Invalidate()
        {
            if (_secureMessaging != null)
            {
                _secureMessaging.Clear();
                _secureMessaging = null;
            }
        }

        // The mode byte followed by zero padding up to one AES block of the CMAC load (Rnd | mode | 000000).
        private static byte[] ModeBlock(byte mode)
        {
            return new byte[] { mode, 0x00, 0x00, 0x00 };
        }

        private static void ExpectStatus(SamApduResponse response, ushort expected, string step)
        {
            if (response.StatusWord != expected)
                throw new SamException($"The SAM returned status {response.StatusWord:X4} during {step}, expected {expected:X4}.", response.StatusWord);
        }

        private byte[] Random(int count)
        {
            var random = _randomSource(count);
            if (random == null || random.Length != count) throw new InvalidOperationException("The random source returned the wrong number of bytes.");
            return random;
        }

        private static byte[] CreateRandom(int count)
        {
            var bytes = new byte[count];
            using (var generator = RandomNumberGenerator.Create())
            {
                generator.GetBytes(bytes);
            }

            return bytes;
        }
    }
}
