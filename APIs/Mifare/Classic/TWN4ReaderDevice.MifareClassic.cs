using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Elatec.NET.Helpers.ByteArrayHelper;
using Elatec.NET.Helpers.ByteArrayHelper.Extensions;

namespace Elatec.NET
{
    public partial class TWN4ReaderDevice
    {
        #region API_MIFARECLASSIC / Mifare Classic Functions

        public static readonly byte API_MIFARECLASSIC = 11;

        public static readonly byte MIFARE_CLASSIC_LOGIN = 0;
        public static readonly byte MIFARE_CLASSIC_READBLOCK = 1;
        public static readonly byte MIFARE_CLASSIC_WRITEBLOCK = 2;


        public static readonly byte MIFARE_CLASSIC_READVALUEBLOCK = 3;
        public static readonly byte MIFARE_CLASSIC_WRITEVALUEBLOCK = 4;
        public static readonly byte MIFARE_CLASSIC_INCREMENTVALUEBLOCK = 5;
        public static readonly byte MIFARE_CLASSIC_DECREMENTVALUEBLOCK = 6;
        public static readonly byte MIFARE_CLASSIC_COPYVALUEBLOCK = 7;
        /// <summary>
        /// Login to a Mifare Classic single Sector.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="keyType"></param>
        /// <param name="sectorNumber"></param>
        /// <returns></returns>
        /// <exception cref="ReaderException"></exception>
        public async Task MifareClassic_LoginAsync(string key, byte keyType, byte sectorNumber)
        {
            List<byte> bytes = new List<byte> { API_MIFARECLASSIC, MIFARE_CLASSIC_LOGIN };
            bytes.AddRange(ByteArrayConverter.GetBytesFrom(key));
            bytes.Add(keyType);
            bytes.Add(sectorNumber);

            var parser = await CallFunctionAsync(bytes.ToArray());
            var success = parser.ParseBool();

            if (!success)
            {
                throw new ReaderException("Call was not successfull, error " + Enum.GetName(typeof(ReaderError), ReaderError.AccessDenied), null);
            }
        }

        /// <summary>
        /// Read Data from Classic Chip
        /// </summary>
        /// <param name="blockNumber">DataBlock Number</param>
        /// <returns>DATA</returns>
        /// <exception cref="ReaderException"></exception>
        public async Task<byte[]> MifareClassic_ReadBlockAsync(byte blockNumber)
        {
            List<byte> bytes = new List<byte> { API_MIFARECLASSIC, MIFARE_CLASSIC_READBLOCK };
            bytes.Add(blockNumber);

            var parser = await CallFunctionAsync(bytes.ToArray());
            var success = parser.ParseBool();

            if (success)
            {
                return parser.ParseFixByteArray(16);
            }
            else
            {
                throw new ReaderException("Call was not successfull, error " + Enum.GetName(typeof(ReaderError), ReaderError.AccessDenied), null);
            }
        }

        /// <summary>
        /// Write Data to Classic Chip
        /// </summary>
        /// <param name="data">16 Bytes of Data to Write</param>
        /// <param name="blockNumber">DataBlock Number</param>
        /// <returns></returns>
        /// <exception cref="ReaderException"></exception>
        public async Task MifareClassic_WriteBlockAsync(byte[] data, byte blockNumber)
        {
            List<byte> bytes = new List<byte> { API_MIFARECLASSIC, MIFARE_CLASSIC_WRITEBLOCK, blockNumber };
            bytes.AddRange(data);

            var parser = await CallFunctionAsync(bytes.ToArray());
            var success = parser.ParseBool();

            if (!success)
            {
                throw new ReaderException("Call was not successfull, error " + Enum.GetName(typeof(ReaderError), ReaderError.AccessDenied), null);
            }
        }
        /// <summary>
        /// Read a Value Block from a MIFARE Classic transponder.
        /// </summary>
        /// <param name="blockNumber">Value block number.</param>
        /// <returns>Value stored in the block (32-bit, returned as signed int).</returns>
        /// <exception cref="ReaderException"></exception>
        public async Task<int> MifareClassic_ReadValueBlockAsync(byte blockNumber)
        {
            List<byte> bytes = new List<byte> { API_MIFARECLASSIC, MIFARE_CLASSIC_READVALUEBLOCK };
            bytes.Add(blockNumber);

            var parser = await CallFunctionAsync(bytes.ToArray());
            var success = parser.ParseBool();

            if (success)
            {
                var value = parser.ParseUInt32();
                return unchecked((int)value);
            }

            throw new ReaderException("Call was not successfull, error " +
                                      Enum.GetName(typeof(ReaderError), ReaderError.AccessDenied), null);
        }

        /// <summary>
        /// Write a Value Block to a MIFARE Classic transponder.
        /// </summary>
        /// <param name="blockNumber">Value block number.</param>
        /// <param name="value">Value to write (32-bit).</param>
        /// <exception cref="ReaderException"></exception>
        public async Task MifareClassic_WriteValueBlockAsync(byte blockNumber, int value)
        {
            List<byte> bytes = new List<byte> { API_MIFARECLASSIC, MIFARE_CLASSIC_WRITEVALUEBLOCK };
            bytes.Add(blockNumber);
            bytes.AddUInt32(unchecked((uint)value));

            var parser = await CallFunctionAsync(bytes.ToArray());
            var success = parser.ParseBool();

            if (!success)
            {
                throw new ReaderException("Call was not successfull, error " +
                                          Enum.GetName(typeof(ReaderError), ReaderError.AccessDenied), null);
            }
        }

        /// <summary>
        /// Increment a Value Block on a MIFARE Classic transponder.
        /// </summary>
        /// <param name="blockNumber">Value block number.</param>
        /// <param name="value">Increment amount (32-bit).</param>
        /// <exception cref="ReaderException"></exception>
        public async Task MifareClassic_IncrementValueBlockAsync(byte blockNumber, int value)
        {
            List<byte> bytes = new List<byte> { API_MIFARECLASSIC, MIFARE_CLASSIC_INCREMENTVALUEBLOCK };
            bytes.Add(blockNumber);
            bytes.AddUInt32(unchecked((uint)value));

            var parser = await CallFunctionAsync(bytes.ToArray());
            var success = parser.ParseBool();

            if (!success)
            {
                throw new ReaderException("Call was not successfull, error " +
                                          Enum.GetName(typeof(ReaderError), ReaderError.AccessDenied), null);
            }
        }

        /// <summary>
        /// Decrement a Value Block on a MIFARE Classic transponder.
        /// </summary>
        /// <param name="blockNumber">Value block number.</param>
        /// <param name="value">Decrement amount (32-bit).</param>
        /// <exception cref="ReaderException"></exception>
        public async Task MifareClassic_DecrementValueBlockAsync(byte blockNumber, int value)
        {
            List<byte> bytes = new List<byte> { API_MIFARECLASSIC, MIFARE_CLASSIC_DECREMENTVALUEBLOCK };
            bytes.Add(blockNumber);
            bytes.AddUInt32(unchecked((uint)value));

            var parser = await CallFunctionAsync(bytes.ToArray());
            var success = parser.ParseBool();

            if (!success)
            {
                throw new ReaderException("Call was not successfull, error " +
                                          Enum.GetName(typeof(ReaderError), ReaderError.AccessDenied), null);
            }
        }

        /// <summary>
        /// Copy a Value Block on a MIFARE Classic transponder.
        /// </summary>
        /// <param name="sourceBlockNumber">Source value block.</param>
        /// <param name="destinationBlockNumber">Destination value block.</param>
        /// <exception cref="ReaderException"></exception>
        public async Task MifareClassic_CopyValueBlockAsync(byte sourceBlockNumber, byte destinationBlockNumber)
        {
            List<byte> bytes = new List<byte> { API_MIFARECLASSIC, MIFARE_CLASSIC_COPYVALUEBLOCK };
            bytes.Add(sourceBlockNumber);
            bytes.Add(destinationBlockNumber);

            var parser = await CallFunctionAsync(bytes.ToArray());
            var success = parser.ParseBool();

            if (!success)
            {
                throw new ReaderException("Call was not successfull, error " +
                                          Enum.GetName(typeof(ReaderError), ReaderError.AccessDenied), null);
            }
        }
        #endregion
    }
}
