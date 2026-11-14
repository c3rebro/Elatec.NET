using System;
using System.Security.Cryptography;

namespace Elatec.NET
{
    /// <summary>
    /// Minimal AES building blocks used by the SAM host protocol (no padding, caller handles block alignment).
    /// </summary>
    internal static class AesHelper
    {
        public const int BlockSize = 16;

        public static byte[] EncryptCbc(byte[] key, byte[] iv, byte[] data)
        {
            if (data.Length % BlockSize != 0)
            {
                throw new ArgumentException("Data must be a multiple of the AES block size.", nameof(data));
            }

            using (var aes = Aes.Create())
            {
                // AES-CBC is what NXP AN12704 prescribes for the encrypted SAM communication, and CBC-MAC is the core of AES-CMAC (RFC 4493).
                // Every message is authenticated with that CMAC, so the mode does not have to provide integrity itself.
                // nosemgrep
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.None;
                aes.Key = key;
                aes.IV = iv;
                using (var encryptor = aes.CreateEncryptor())
                {
                    return encryptor.TransformFinalBlock(data, 0, data.Length);
                }
            }
        }

        public static byte[] DecryptCbc(byte[] key, byte[] iv, byte[] data)
        {
            if (data.Length % BlockSize != 0)
            {
                throw new ArgumentException("Data must be a multiple of the AES block size.", nameof(data));
            }

            using (var aes = Aes.Create())
            {
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.None;
                aes.Key = key;
                aes.IV = iv;
                using (var decryptor = aes.CreateDecryptor())
                {
                    return decryptor.TransformFinalBlock(data, 0, data.Length);
                }
            }
        }

        /// <summary>
        /// Encrypts a single block (CBC with a zero IV, which is identical to ECB for one block).
        /// </summary>
        public static byte[] EncryptBlock(byte[] key, byte[] block)
        {
            if (block.Length != BlockSize)
            {
                throw new ArgumentException("Exactly one AES block is required.", nameof(block));
            }

            return EncryptCbc(key, new byte[BlockSize], block);
        }
    }

    /// <summary>
    /// AES-CMAC according to RFC 4493 (the .NET framework does not provide it).
    /// </summary>
    internal static class AesCmac
    {
        public static byte[] Compute(byte[] key, byte[] message)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            var l = AesHelper.EncryptBlock(key, new byte[AesHelper.BlockSize]);
            var k1 = Double(l);
            var k2 = Double(k1);

            var blockCount = (message.Length + AesHelper.BlockSize - 1) / AesHelper.BlockSize;
            if (blockCount == 0)
            {
                blockCount = 1;
            }

            var lastBlockComplete = message.Length != 0 && message.Length % AesHelper.BlockSize == 0;

            // Message with the last block already masked with K1 (complete) or padded and masked with K2 (incomplete).
            var buffer = new byte[blockCount * AesHelper.BlockSize];
            Array.Copy(message, buffer, message.Length);
            var lastOffset = (blockCount - 1) * AesHelper.BlockSize;
            if (!lastBlockComplete)
            {
                buffer[message.Length] = 0x80;
            }

            var subKey = lastBlockComplete ? k1 : k2;
            for (var i = 0; i < AesHelper.BlockSize; i++)
            {
                buffer[lastOffset + i] ^= subKey[i];
            }

            // CBC-MAC over the prepared message: the MAC is the last cipher block.
            var cipher = AesHelper.EncryptCbc(key, new byte[AesHelper.BlockSize], buffer);
            var mac = new byte[AesHelper.BlockSize];
            Array.Copy(cipher, cipher.Length - AesHelper.BlockSize, mac, 0, AesHelper.BlockSize);
            return mac;
        }

        // Multiplication by x in GF(2^128) with the reduction polynomial of RFC 4493 (Rb = 0x87).
        private static byte[] Double(byte[] input)
        {
            var output = new byte[AesHelper.BlockSize];
            byte carry = 0;
            for (var i = AesHelper.BlockSize - 1; i >= 0; i--)
            {
                output[i] = (byte)((input[i] << 1) | carry);
                carry = (byte)(input[i] >> 7);
            }

            if ((input[0] & 0x80) != 0)
            {
                output[AesHelper.BlockSize - 1] ^= 0x87;
            }

            return output;
        }
    }
}
