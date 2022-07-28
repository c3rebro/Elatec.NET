using Elatec.NET;
using System.Collections.Generic;

namespace Elatec.NET.Model
{
    /// <summary>
    /// Description of chipUid.
    /// </summary>
    public class MifareKey
    {
        public MifareKey()
        {
        }

        public uint FreeMemory { get; set; }

        public string ChipIdentifier { get; set; }        

        public string Value { get; set; }

        public CARD_TYPE CardType { get; set; }
    }
}