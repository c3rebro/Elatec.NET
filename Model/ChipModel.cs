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
            UID = uid;
            CardType = cardType;
        }

        public ChipModel(string uid, ChipType cardType, string sak, string rats)
        {
            UID = uid;
            CardType = cardType;
            SAK = sak;
            RATS = rats;
        }

        public ChipModel(string uid, ChipType cardType, string sak, string rats, string versionL4)
        {
            UID = uid;
            CardType = cardType;
            SAK = sak;
            RATS = rats;
            VersionL4 = versionL4;
        }

        public string UID
        {
            get; set;
        }
        public string SAK
        {
            get; set;
        }
        public string RATS
        {
            get; set;
        }
        public string VersionL4
        {
            get; set;
        }
        public ChipModel Slave
        {
            get; set;
        }
        public ChipType CardType
        {
            get; set;
        }

    }
}