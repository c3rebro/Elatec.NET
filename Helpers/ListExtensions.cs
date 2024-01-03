using System.Collections.Generic;

namespace Elatec.NET
{
    public static class ListExtensions
    {
        /// <summary>
        /// Add two bytes to the list with the least-significant-byte first.
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="word"></param>
        public static void AddWord(this List<byte> bytes, int word)
        {
            bytes.Add((byte)((uint)word & 0xFFu));
            bytes.Add((byte)((uint)(word >> 8) & 0xFFu));
        }

        /// <summary>
        /// Add four bytes to the list with the least-significant-byte first.
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="value"></param>
        public static void AddLong(this List<byte> bytes, uint value)
        {
            bytes.Add((byte)(value & 0xFFu));
            bytes.Add((byte)((value >> 8) & 0xFFu));
            bytes.Add((byte)((value >> 16) & 0xFFu));
            bytes.Add((byte)((value >> 24) & 0xFFu));
        }
    }
}
