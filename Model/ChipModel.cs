using Elatec.NET;
using System.Collections.Generic;

namespace Elatec.NET.Model
{
    /// <summary>
    /// Description of chipUid.
    /// </summary>
    public class ChipModel
    {
        public ChipModel()
        {
        }

        public ChipModel(string uid, CARD_TYPE cardType)
        {
            ChipIdentifier = uid;
            CardType = cardType;
        }

        public uint FreeMemory { get; set; }

        public string ChipIdentifier { get; set; }        

        public CARD_TYPE CardType { get; set; }
    }
}