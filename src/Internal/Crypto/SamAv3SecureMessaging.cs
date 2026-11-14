using System;
using System.Collections.Generic;
using Elatec.NET.Sam;

namespace Elatec.NET
{
    /// <summary>
    /// Helpers shared by the SAM AV3 host protocol code.
    /// </summary>
    internal static class SamAv3Crypto
    {
        /// <summary>
        /// The 8 byte "MIFARE Plus specific" CMAC: the odd-indexed bytes (1, 3, ..., 15) of the 16 byte AES-CMAC.
        /// </summary>
        public static byte[] Cmac8(byte[] key, byte[] message)
        {
            var full = AesCmac.Compute(key, message);
            var truncated = new byte[8];
            for (var i = 0; i < truncated.Length; i++)
            {
                truncated[i] = full[2 * i + 1];
            }

            return truncated;
        }

        public static byte[] Concat(params byte[][] parts)
        {
            var result = new List<byte>();
            foreach (var part in parts)
            {
                result.AddRange(part);
            }

            return result.ToArray();
        }

        public static byte[] Slice(byte[] source, int index, int count)
        {
            var result = new byte[count];
            Array.Copy(source, index, result, 0, count);
            return result;
        }

        /// <summary>XOR of source1[index1 ..] and source2[index2 ..] over <paramref name="count"/> bytes.</summary>
        public static byte[] Xor(byte[] source1, int index1, byte[] source2, int index2, int count)
        {
            var result = new byte[count];
            for (var i = 0; i < count; i++)
            {
                result[i] = (byte)(source1[index1 + i] ^ source2[index2 + i]);
            }

            return result;
        }

        /// <summary>Rotates left by <paramref name="count"/> bytes: the first bytes move to the end.</summary>
        public static byte[] RotateLeft(byte[] source, int count)
        {
            return Concat(Slice(source, count, source.Length - count), Slice(source, 0, count));
        }

        public static byte[] BigEndian(uint value)
        {
            return new[] { (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value };
        }

        public static byte[] Repeat(byte value, int count)
        {
            var result = new byte[count];
            for (var i = 0; i < count; i++)
            {
                result[i] = value;
            }

            return result;
        }

        /// <summary>Compares two byte arrays in a time that does not depend on where they differ.</summary>
        public static bool FixedTimeEquals(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;

            var difference = 0;
            for (var i = 0; i < a.Length; i++)
            {
                difference |= a[i] ^ b[i];
            }

            return difference == 0;
        }

        /// <summary>ISO/IEC 9797-1 padding method 2: 0x80 followed by zeros up to a multiple of the block size.</summary>
        public static byte[] PadMethod2(byte[] data)
        {
            var paddedLength = (data.Length / AesHelper.BlockSize + 1) * AesHelper.BlockSize;
            var padded = new byte[paddedLength];
            Array.Copy(data, padded, data.Length);
            padded[data.Length] = 0x80;
            return padded;
        }

        public static byte[] UnpadMethod2(byte[] padded)
        {
            var index = padded.Length - 1;
            while (index >= 0 && padded[index] == 0x00)
            {
                index--;
            }

            if (index < 0 || padded[index] != 0x80)
            {
                throw new SamException("The decrypted SAM response has invalid padding.");
            }

            return Slice(padded, 0, index);
        }
    }

    /// <summary>
    /// Secure messaging with full protection (encryption and MAC) between host and SAM AV3 after a host authentication,
    /// as described in NXP AN12704 section 2.2.
    /// </summary>
    /// <remarks>
    /// Command: the data field is padded (method 2), encrypted with Ke (CBC, IV = AES(Ke, 01 01 01 01 | counter x 3)) and
    /// followed by the 8 byte CMAC over CLA INS counter P1 P2 Lc' encrypted data [Le], Lc' = encrypted length + 8.
    /// Response: encrypted data | CMAC, the CMAC covers SW1 SW2 counter+1 encrypted data, the IV for the decryption is
    /// AES(Ke, 02 02 02 02 | (counter+1) x 3). The 4 byte counter is big endian and increases by one per command.
    /// </remarks>
    internal sealed class SamAv3SecureMessaging
    {
        private const int MacLength = 8;

        private readonly byte[] _ke;
        private readonly byte[] _km;

        public SamAv3SecureMessaging(byte[] sessionKeyEnc, byte[] sessionKeyMac, uint counter = 0)
        {
            _ke = (byte[])sessionKeyEnc.Clone();
            _km = (byte[])sessionKeyMac.Clone();
            Counter = counter;
        }

        /// <summary>The command counter that will be used for the next command.</summary>
        public uint Counter { get; private set; }

        /// <summary>A copy of the encryption session key, for tests.</summary>
        internal byte[] SessionKeyEnc
        {
            get { return (byte[])_ke.Clone(); }
        }

        /// <summary>A copy of the MAC session key, for tests.</summary>
        internal byte[] SessionKeyMac
        {
            get { return (byte[])_km.Clone(); }
        }

        public SamApdu Protect(byte cla, byte ins, byte p1, byte p2, byte[] data, byte? le)
        {
            if (Counter == uint.MaxValue)
                throw new SamException("The command counter of the SAM session is exhausted, authenticate again.");

            var counter = SamAv3Crypto.BigEndian(Counter);

            var encrypted = new byte[0];
            if (data.Length > 0)
            {
                var iv = AesHelper.EncryptBlock(_ke, SamAv3Crypto.Concat(SamAv3Crypto.Repeat(0x01, 4), counter, counter, counter));
                encrypted = AesHelper.EncryptCbc(_ke, iv, SamAv3Crypto.PadMethod2(data));
            }

            var lc = encrypted.Length + MacLength;
            if (lc > byte.MaxValue)
                throw new ArgumentException("The data field is too long for a short APDU once encrypted.", nameof(data));

            var macLoad = SamAv3Crypto.Concat(
                new[] { cla, ins },
                counter,
                new[] { p1, p2, (byte)lc },
                encrypted,
                le.HasValue ? new[] { le.Value } : new byte[0]);

            var mac = SamAv3Crypto.Cmac8(_km, macLoad);
            return new SamApdu(cla, ins, p1, p2, SamAv3Crypto.Concat(encrypted, mac), le);
        }

        /// <summary>
        /// Verifies the response, decrypts its data field and advances the command counter.
        /// </summary>
        /// <returns>The decrypted response data, empty if the SAM returned none.</returns>
        public byte[] Unprotect(SamApduResponse response)
        {
            if (response.StatusWord != 0x9000)
                throw new SamException($"The SAM returned status {response.StatusWord:X4}.", response.StatusWord);

            if (response.Data.Length < MacLength)
                throw new SamException("The SAM response is too short to contain a CMAC.", response.StatusWord);

            var encrypted = SamAv3Crypto.Slice(response.Data, 0, response.Data.Length - MacLength);
            var receivedMac = SamAv3Crypto.Slice(response.Data, response.Data.Length - MacLength, MacLength);

            var responseCounter = SamAv3Crypto.BigEndian(Counter + 1);
            var macLoad = SamAv3Crypto.Concat(new[] { response.Sw1, response.Sw2 }, responseCounter, encrypted);
            if (!SamAv3Crypto.FixedTimeEquals(SamAv3Crypto.Cmac8(_km, macLoad), receivedMac))
                throw new SamException("The CMAC of the SAM response is invalid.", response.StatusWord);

            Counter++;

            if (encrypted.Length == 0)
                return new byte[0];

            if (encrypted.Length % AesHelper.BlockSize != 0)
                throw new SamException("The encrypted SAM response is not a multiple of the AES block size.", response.StatusWord);

            var iv = AesHelper.EncryptBlock(_ke, SamAv3Crypto.Concat(SamAv3Crypto.Repeat(0x02, 4), responseCounter, responseCounter, responseCounter));
            return SamAv3Crypto.UnpadMethod2(AesHelper.DecryptCbc(_ke, iv, encrypted));
        }

        /// <summary>Overwrites the session keys.</summary>
        public void Clear()
        {
            Array.Clear(_ke, 0, _ke.Length);
            Array.Clear(_km, 0, _km.Length);
        }
    }
}
