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

        public ChipModel(string uid, ChipType cardType)
        {
            ChipIdentifier = uid;
            CardType = cardType;
        }

        public string ChipIdentifier { get; set; }        

        public ChipType CardType { get; set; }
    }
}