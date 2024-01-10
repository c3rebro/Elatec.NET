using System;
using System.Collections.Generic;
using System.Text;

namespace Elatec.NET
{
    public class ResponseParser
    {
        private List<byte> Bytes;
        public int ParseIdx;

        public ResponseParser(List<byte> bytes)
        {
            Bytes = bytes;
            ParseIdx = 0;
        }

        public void BeginParse()
        {
            ParseIdx = 0;
        }

        public byte ParseByte()
        {
            if (ParseIdx >= Bytes.Count)
            {
                throw new ApplicationException("Response too short");
            }
            return Bytes[ParseIdx++];
        }

        public ushort ParseUInt16()
        {
            ushort num = 0;
            if (ParseIdx >= Bytes.Count - 1)
            {
                throw new ApplicationException("Response too short");
            }
            num = Bytes[ParseIdx++];
            return (ushort)(num | (ushort)(Bytes[ParseIdx++] << 8));
        }

        public uint ParseUInt32()
        {
            uint num = 0u;
            if (ParseIdx >= Bytes.Count - 3)
            {
                throw new ApplicationException("Response too short");
            }
            num = Bytes[ParseIdx++];
            num |= (uint)(Bytes[ParseIdx++] << 8);
            num |= (uint)(Bytes[ParseIdx++] << 16);
            num |= (uint)(Bytes[ParseIdx++] << 24);
            return num;
        }

        public bool ParseBool()
        {
            return ParseByte() != 0;
        }

        public ResponseError ParseResponseError()
        {
            return (ResponseError)ParseByte();
        }

        public byte[] ParseVarByteArray()
        {
            byte num = ParseByte();
            var result = new byte[num];
            Bytes.CopyTo(ParseIdx, result, 0, num);
            ParseIdx += num;
            return result;
        }

        public byte[] ParseFixByteArray(byte num)
        {
            if (ParseIdx >= Bytes.Count - num + 1)
            {
                throw new ApplicationException("Response too short");
            }
            var result = new byte[num];
            Bytes.CopyTo(ParseIdx, result, 0, num);
            ParseIdx += num;
            return result;
        }

        public string ParseAsciiString()
        {
            byte[] stringBytes = ParseVarByteArray();
            return Encoding.ASCII.GetString(stringBytes);
        }
    }
}
