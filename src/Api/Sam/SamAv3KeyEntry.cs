using System;

namespace Elatec.NET.Sam
{
    /// <summary>
    /// The content of a symmetric key entry of a MIFARE SAM AV3 (sKST entry), as written by SAM_ChangeKeyEntry.
    /// Only AES-128 key entries are supported.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An entry holds up to three keys (positions A, B and C), each with its own version. A key is addressed by
    /// entry number plus version; if several positions carry the same version, the first one is used.
    /// If <see cref="KeyB"/> or <see cref="KeyC"/> is <see langword="null"/>, the key and version of position A are
    /// written to that position as well. This avoids leaving an unrelated key (e.g. all zeros) behind under a
    /// version number someone could still select.
    /// </para>
    /// <para>
    /// <see cref="Set"/> and <see cref="ExtSet"/> are the raw configuration words described in the MIFARE SAM AV3
    /// documentation (AN12701, tables 2 and 3). Use <see cref="CreateAes128HostKey"/> or
    /// <see cref="CreateAes128PiccKey"/> for the common cases.
    /// </para>
    /// </remarks>
    public sealed class SamAv3KeyEntry
    {
        /// <summary>SET: key type bits 3 to 5 = 100b (AES-128).</summary>
        public const ushort SetKeyTypeAes128 = 0x0020;

        /// <summary>SET: mask of the key type bits 3 to 5.</summary>
        public const ushort SetKeyTypeMask = 0x0038;

        /// <summary>SET bit 8: the key can be used for SAM_AuthenticateHost (host key).</summary>
        public const ushort SetHostAuthKey = 0x0100;

        /// <summary>ExtSET key class 000b: host key, can only be used for host authentication.</summary>
        public const byte ExtSetKeyClassHost = 0x00;

        /// <summary>ExtSET key class 001b: PICC key.</summary>
        public const byte ExtSetKeyClassPicc = 0x01;

        /// <summary>RefNoKUC value for "no key usage counter linked".</summary>
        public const byte NoKeyUsageCounter = 0xFF;

        /// <summary>KeyNoCEK value for "this entry can be changed without authentication".</summary>
        public const byte ChangeFree = 0xFE;

        /// <summary>KeyNoCEK value (or any value above 127) that irreversibly locks the entry against changes by symmetric keys.</summary>
        public const byte ChangeLocked = 0xFF;

        /// <summary>The 16 byte key in position A (required).</summary>
        public byte[] KeyA { get; set; }

        /// <summary>Version of the key in position A.</summary>
        public byte VersionA { get; set; }

        /// <summary>The 16 byte key in position B, <see langword="null"/> to repeat position A.</summary>
        public byte[] KeyB { get; set; }

        /// <summary>Version of the key in position B, ignored if <see cref="KeyB"/> is <see langword="null"/>.</summary>
        public byte VersionB { get; set; }

        /// <summary>The 16 byte key in position C, <see langword="null"/> to repeat position A.</summary>
        public byte[] KeyC { get; set; }

        /// <summary>Version of the key in position C, ignored if <see cref="KeyC"/> is <see langword="null"/>.</summary>
        public byte VersionC { get; set; }

        /// <summary>DF_AID: the linked DESFire application ID, 3 bytes in the order they are transmitted. Zeros if unused.</summary>
        public byte[] DfAid { get; set; } = new byte[3];

        /// <summary>DF_KeyNo: the linked DESFire key number. Zero if unused.</summary>
        public byte DfKeyNo { get; set; }

        /// <summary>
        /// KeyNoCEK: the key entry whose key must be used for host authentication to change this entry later.
        /// Defaults to 0 (the SAM master key entry). <see cref="ChangeFree"/> (0xFE) allows changes without
        /// authentication, values above 127 otherwise irreversibly lock the entry.
        /// </summary>
        public byte KeyNoCek { get; set; }

        /// <summary>KeyVCEK: the version of the key in <see cref="KeyNoCek"/> that must be used to change this entry. Defaults to 0.</summary>
        public byte KeyVersionCek { get; set; }

        /// <summary>RefNoKUC: the key usage counter (0 to 15) linked to this entry, <see cref="NoKeyUsageCounter"/> if none.</summary>
        public byte RefNoKuc { get; set; } = NoKeyUsageCounter;

        /// <summary>SET: the configuration settings of the key entry (16 bit).</summary>
        public ushort Set { get; set; }

        /// <summary>ExtSET: the extended configuration settings of the key entry (key class and restrictions).</summary>
        public byte ExtSet { get; set; }

        /// <summary>
        /// Creates an AES-128 key entry that can be used for SAM_AuthenticateHost (SET = 0x0120, ExtSET = 0x00),
        /// the configuration of the key entry in the NXP SAM_ChangeKeyEntry example.
        /// </summary>
        public static SamAv3KeyEntry CreateAes128HostKey(byte[] key, byte version = 0)
        {
            return new SamAv3KeyEntry
            {
                KeyA = key,
                VersionA = version,
                Set = SetKeyTypeAes128 | SetHostAuthKey,
                ExtSet = ExtSetKeyClassHost
            };
        }

        /// <summary>
        /// Creates an AES-128 key entry for authenticating PICCs such as MIFARE DESFire (SET = 0x0020, ExtSET = 0x01).
        /// </summary>
        /// <remarks>
        /// SET and ExtSET are derived from the bit tables of the NXP documentation, not from a published example.
        /// Which combinations a SAM accepts is specified in NXP's Key Management and Personalization note,
        /// check it before relying on this.
        /// </remarks>
        public static SamAv3KeyEntry CreateAes128PiccKey(byte[] key, byte version = 0)
        {
            return new SamAv3KeyEntry
            {
                KeyA = key,
                VersionA = version,
                Set = SetKeyTypeAes128,
                ExtSet = ExtSetKeyClassPicc
            };
        }

        /// <summary><see langword="true"/> if <see cref="Set"/> marks the entry as host key (bit 8).</summary>
        public bool IsHostKey
        {
            get { return (Set & SetHostAuthKey) != 0; }
        }

        /// <summary>
        /// Builds the 61 byte data field of SAM_ChangeKeyEntry:
        /// PosA, PosB, PosC, DF_AID, DF_KeyNo, KeyNoCEK, KeyVCEK, RefNoKUC, SET (LSB first), Va, Vb, Vc, ExtSET.
        /// </summary>
        internal byte[] ToCommandData()
        {
            if (KeyA == null) throw new ArgumentException("Key A is required.", nameof(KeyA));
            RequireKeyLength(KeyA, "Key A");
            if (KeyB != null) RequireKeyLength(KeyB, "Key B");
            if (KeyC != null) RequireKeyLength(KeyC, "Key C");
            if (DfAid == null || DfAid.Length != 3) throw new ArgumentException("DF_AID must be 3 bytes.", nameof(DfAid));
            if (RefNoKuc > 15 && RefNoKuc != NoKeyUsageCounter)
                throw new ArgumentException("RefNoKUC must be 0 to 15, or 0xFF if no key usage counter is used.", nameof(RefNoKuc));
            if ((Set & SetKeyTypeMask) != SetKeyTypeAes128)
                throw new NotSupportedException("Only AES-128 key entries are supported (SET key type bits 3 to 5 must be 100b).");

            var keyB = KeyB ?? KeyA;
            var versionB = KeyB != null ? VersionB : VersionA;
            var keyC = KeyC ?? KeyA;
            var versionC = KeyC != null ? VersionC : VersionA;

            var data = new byte[61];
            var offset = 0;
            Append(data, ref offset, KeyA);
            Append(data, ref offset, keyB);
            Append(data, ref offset, keyC);
            Append(data, ref offset, DfAid);
            data[offset++] = DfKeyNo;
            data[offset++] = KeyNoCek;
            data[offset++] = KeyVersionCek;
            data[offset++] = RefNoKuc;
            data[offset++] = (byte)(Set & 0xFF);
            data[offset++] = (byte)(Set >> 8);
            data[offset++] = VersionA;
            data[offset++] = versionB;
            data[offset++] = versionC;
            data[offset++] = ExtSet;
            return data;
        }

        private static void RequireKeyLength(byte[] key, string name)
        {
            if (key.Length != 16) throw new ArgumentException(name + " must be 16 bytes (AES-128).");
        }

        private static void Append(byte[] target, ref int offset, byte[] source)
        {
            Array.Copy(source, 0, target, offset, source.Length);
            offset += source.Length;
        }
    }
}
