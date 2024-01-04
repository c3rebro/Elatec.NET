using Microsoft.Win32;

using Elatec.Net.Helpers.ByteArrayHelper.Extensions;

using Elatec.Net.Helpers.Log4CSharp;

using System;
using System.IO.Ports;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

using Elatec.NET.Model;

using System.Diagnostics;

/*
* Elatec.NET is a C# library to easily Talk to Elatec's TWN4 Devices
* 
* Boolean "Results": Success = true, Failed = false
* 
* Some TWN4 Specific "Special" information:
* 
* Getting the ATS on different Readers works differently.
* 
*/

namespace Elatec.NET
{
    public class TWN4ReaderDevice : IDisposable
    {
        private const bool RESULT_SUCCESS = true;
        private const bool RESULT_FAILED = false;

        private protected int portNumber;

        private bool _disposed;
        private static readonly object syncRoot = new object();
        private static TWN4ReaderDevice instance;

        #region ELATEC COMMANDS
        private const string GET_LASTERR = "000A";

        private const string BEEP_CMD = "0407";
        private const string LEDINIT_CMD = "0410";
        private const string LEDON_CMD = "0411";
        private const string LEDOFF_CMD = "0412";

        private const string MIFARE_CLASSIC_LOGIN = "0B00";
        private const string MIFARE_CLASSIC_READBLOCK = "0B01";
        private const string MIFARE_CLASSIC_WRITEBLOCK = "0B02";

        private const string MIFARE_DESFIRE_GETAPPIDS = "0F00";
        private const string MIFARE_DESFIRE_CREATEAPP = "0F01";
        private const string MIFARE_DESFIRE_DELETEAPP = "0F02";
        private const string MIFARE_DESFIRE_SELECTAPP = "0F03";
        private const string MIFARE_DESFIRE_AUTH = "0F04";
        private const string MIFARE_DESFIRE_GETKEYSETTINGS = "0F05";
        private const string MIFARE_DESFIRE_GETFILEIDS = "0F06";
        private const string MIFARE_DESFIRE_GETFILESETTINGS = "0F07";
        private const string MIFARE_DESFIRE_READDATA = "0F08";
        private const string MIFARE_DESFIRE_GETFREEMEMORY = "0F0E";
        private const string MIFARE_DESFIRE_FORMATTAG = "0F0F";
        private const string MIFARE_DESFIRE_CREATE_STDDATAFILE = "0F10";
        private const string MIFARE_DESFIRE_DELETEFILE = "0F13";

        private const string MIFARE_DESFIRE_CHANGEKEYSETTINGS = "0F18";
        private const string MIFARE_DESFIRE_CHANGEKEY = "0F19";
   
        private const string ISO14443_GET_ATS = "1200";
        private const string ISO14443_4_TXD = "1203";
        private const string ISO14443_3_TXD = "1207";

        private const string ISO_CMD_RATS = "E050BCA5FFFF00";

        #endregion

        public static TWN4ReaderDevice Instance
        {
            get
            {
                lock (TWN4ReaderDevice.syncRoot)
                {
                    if (instance == null)
                    {
                        instance = new TWN4ReaderDevice();
                        return instance;

                    }
                    else
                    {
                        return instance;
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public TWN4ReaderDevice()
        {
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="port"></param>
        public TWN4ReaderDevice(int port)
        {
            portNumber = port;
        }

        /// <summary>
        /// 
        /// </summary>
        public bool PortAccessDenied { get; private set; }

        #region Common

        /// <summary>
        /// 
        /// </summary>
        public void Beep()
        {
            BeepAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="iterations"></param>
        /// <param name="length"></param>
        /// <param name="freq"></param>
        /// <param name="vol"></param>
        public void Beep(ushort iterations, ushort length, ushort freq, byte vol)
        {
            BeepAsync(iterations, length, freq, vol).GetAwaiter().GetResult();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public async Task BeepAsync()
        {
            Result = await DoTXRXAsync(new byte[] { 0x04, 0x07, 0x64, 0x60, 0x09, 0x54, 0x01, 0xF4, 0x01 }); 
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="iterations"></param>
        /// <param name="length"></param>
        /// <param name="freq"></param>
        /// <param name="vol"></param>
        /// <returns></returns>
        public async Task BeepAsync(ushort iterations, ushort length, ushort freq, byte vol)
        {
            for (uint i = 0; i < iterations; i++)
            {
                Result = await DoTXRXAsync(
                    ByteConverter.GetBytesFrom(BEEP_CMD +
                    ByteConverter.GetStringFrom(vol) +
                    ByteConverter.GetStringFrom(BitConverter.GetBytes(freq)) +
                    ByteConverter.GetStringFrom(BitConverter.GetBytes(length)) +
                    ByteConverter.GetStringFrom(BitConverter.GetBytes(length)))
                    );
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="On"></param>
        public void GreenLED(bool On)
        {
            GreenLEDAsync(On).GetAwaiter().GetResult();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="On"></param>
        /// <returns></returns>
        public async Task GreenLEDAsync(bool On)
        {
            Result = await DoTXRXAsync(
                    ByteConverter.GetBytesFrom(LEDINIT_CMD +
                    ByteConverter.GetStringFrom(0x0F))
                    );
            if(On)
            {
                Result = await DoTXRXAsync(
                    ByteConverter.GetBytesFrom(LEDON_CMD +
                    ByteConverter.GetStringFrom(0x02))
                    );
            }
            else
            {
                Result = await DoTXRXAsync(
                    ByteConverter.GetBytesFrom(LEDOFF_CMD +
                    ByteConverter.GetStringFrom(0x02))
                    );
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="On"></param>
        public void RedLED(bool On)
        {
            RedLEDAsync(On).GetAwaiter().GetResult();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="On"></param>
        /// <returns></returns>
        public async Task RedLEDAsync(bool On)
        {
            Result = await DoTXRXAsync(
                    ByteConverter.GetBytesFrom(LEDINIT_CMD +
                    ByteConverter.GetStringFrom(0x0F))
                    );
            if (On)
            {
                Result = await DoTXRXAsync(
                    ByteConverter.GetBytesFrom(LEDON_CMD +
                    ByteConverter.GetStringFrom(0x01))
                    );
            }
            else
            {
                Result = await DoTXRXAsync(
                    ByteConverter.GetBytesFrom(LEDOFF_CMD +
                    ByteConverter.GetStringFrom(0x01))
                    );
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="hf"></param>
        /// <returns></returns>
        public ChipModel GetSingleChip(bool hf)
        {
            return GetSingleChipAsync(hf, false).Result;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="hf"></param>
        /// <returns></returns>
        public async Task<ChipModel> GetSingleChipAsync(bool hf)
        {
            return await GetSingleChipAsync(hf, false);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="hf"></param>
        /// <param name="legicOnly"></param>
        /// <returns></returns>
        public ChipModel GetSingleChip(bool hf, bool legicOnly)
        {
            return GetSingleChipAsync(hf, legicOnly).Result;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="hf"></param>
        /// <param name="legicOnly"></param>
        /// <returns></returns>
        public async Task<ChipModel> GetSingleChipAsync(bool hf, bool legicOnly)
        {
            try
            {
                var currentChip = new ChipModel();

                SAK = 0x00;
                ATS = new byte[1] {0x00};

                if (hf)
                {
                    Result = await DoTXRXAsync(new byte[] { 0x05, 0x02, 0x00, 0x00, 0x00, 0x00, 0xF7, 0xFF, 0xFF, 0xFF }); //SetChipTypes (HF Only)
                }
                else
                {
                    Result = await DoTXRXAsync(new byte[] { 0x05, 0x02, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00 }); //Set Chip Types (LF Only)
                }

                if (legicOnly)
                {
                    Result = await DoTXRXAsync(new byte[] { 0x05, 0x02, 0x00, 0x00, 0x00, 0x00, 0x08, 0x00, 0x00, 0x00 }); //SetChipTypes (Legic Only)
                }

                Result = await DoTXRXAsync(new byte[] { 0x05, 0x00, 0x20}); //GetChip

                if (Result?.Length >= 3)
                {
                    currentChip.CardType = (ChipType)Result[2];
                }
                else if(hf)
                {
                    Result = await DoTXRXAsync(new byte[] { 0x12, 0x08, 0xFF }); //GetChip UID if GetChip failed (SmartMX elatec workaround) 
                }
                else if (legicOnly)
                {
                    Result = await DoTXRXAsync(new byte[] { 0x12, 0x08, 0xFF }); //GetChip UID if GetChip failed (SmartMX elatec workaround) 
                }

                currentChip.UID = ByteConverter.GetStringFrom(Result, 5);  //BitConverter.ToString(Result, 5, Result.Length-5);

                switch (currentChip.CardType)
                {
                    case ChipType.NOTAG:
                    case ChipType.MIFARE: //Start Mifare Identification Process

                        Result = await DoTXRXAsync(new byte[] { 0x12, 0x05 }); //GetSAK

                        if (Result?.Length == 3 && Result[2] != 0x00)
                        {
                            SAK = Result[2];
                            
                            // Start MIFARE identification
                            if ((SAK & 0x02) == 0x02)
                            {
                                currentChip.CardType = ChipType.Unspecified;
                            } // RFU bit set (RFU = Reserved for Future Use)

                            else
                            {
                                if ((SAK & 0x08) == 0x08)
                                {
                                    if ((SAK & 0x10) == 0x10)
                                    {
                                        if ((SAK & 0x01) == 0x01)
                                        {
                                            currentChip.CardType = ChipType.Mifare2K;
                                        } // // SAK b1 = 1 ? >> Mifare Classic 2K
                                        else
                                        {
                                            if ((SAK & 0x20) == 0x20)
                                            {
                                                currentChip.CardType = ChipType.SmartMX_Mifare_4K;
                                            } // SAK b6 = 1 ?  >> SmartMX Classic 4K
                                            else
                                            {
                                                //Get ATS - Switch to L4 ?
                                                var response = await DoTXRXAsync(
                                                    ByteConverter.GetBytesFrom(
                                                        ISO14443_3_TXD +
                                                        "04" +
                                                        ISO_CMD_RATS
                                                    ));

                                                if (response != null && response.Length <= 4)
                                                {
                                                    response = await DoTXRXAsync(ByteConverter.GetBytesFrom(ISO14443_GET_ATS + "20"));
                                                    ATS = new byte[response.Length - 2];
                                                }
                                                else if (response != null && response.Length >= 5)
                                                {
                                                    ATS = new byte[response.Length - 2];
                                                }
                                                else
                                                {
                                                    ATS = new byte[1] { 0x00 };
                                                }

                                                Buffer.BlockCopy(response, 2, ATS, 0, response.Length - 2);

                                                if (ATS.Length > 4)
                                                {
                                                    if (ByteConverter.SearchBytePattern(ATS, new byte[] { 0xC1, 0x05, 0x2F, 0x2F, 0x00, 0x35, 0xC7 }) != 0) //MF PlusS 4K in SL1
                                                    {
                                                        currentChip.CardType = ChipType.MifarePlus_SL1_4K;
                                                    }

                                                    else if (ByteConverter.SearchBytePattern(ATS, new byte[] { 0xC1, 0x05, 0x2F, 0x2F, 0x01, 0xBC, 0xD6 }) != 0) //MF PlusX 4K in SL1
                                                    {
                                                        currentChip.CardType = ChipType.MifarePlus_SL1_4K;
                                                    }

                                                } // Mifare Plus S / Plus X 4K

                                                else
                                                {
                                                    currentChip.CardType = ChipType.Mifare4K;
                                                } //Error on ATS = Mifare Classic 4K
                                                break;
                                            }
                                        }
                                    } // SAK b5 = 1 ?
                                    else
                                    {
                                        if ((SAK & 0x01) == 0x01)
                                        {
                                            currentChip.CardType = ChipType.MifareMini;
                                        } // // SAK b1 = 1 ? >> Mifare Mini
                                        else
                                        {
                                            if ((SAK & 0x20) == 0x20)
                                            {
                                                currentChip.CardType = ChipType.SmartMX_Mifare_1K;
                                            } // // SAK b6 = 1 ? >> SmartMX Classic 1K
                                            else
                                            {
                                                //Get ATS - Switch to L4 ?
                                                var response = await DoTXRXAsync(
                                                    ByteConverter.GetBytesFrom(
                                                        ISO14443_3_TXD +
                                                        "04" +
                                                        ISO_CMD_RATS
                                                    ));

                                                if (response != null && response.Length <= 4)
                                                {
                                                    response = await DoTXRXAsync(ByteConverter.GetBytesFrom(ISO14443_GET_ATS + "20"));
                                                    ATS = new byte[response.Length - 2];
                                                }
                                                else if (response != null && response.Length >= 5)
                                                {
                                                    ATS = new byte[response.Length - 2];
                                                }
                                                else
                                                {
                                                    ATS = new byte[1] { 0x00 };
                                                }
                                                Buffer.BlockCopy(response, 2, ATS, 0, response.Length - 2);

                                                if (ATS.Length > 4)
                                                {
                                                    if (ByteConverter.SearchBytePattern(ATS, new byte[] { 0xC1, 0x05, 0x2F, 0x2F, 0x00, 0x35, 0xC7 }) != 0) //MF PlusS 4K in SL1
                                                    {
                                                        currentChip.CardType = ChipType.MifarePlus_SL1_2K;
                                                    }

                                                    else if (ByteConverter.SearchBytePattern(ATS, new byte[] { 0xC1, 0x05, 0x2F, 0x2F, 0x01, 0xBC, 0xD6 }) != 0) //MF PlusX 4K in SL1
                                                    {
                                                        currentChip.CardType = ChipType.MifarePlus_SL1_2K;
                                                    }

                                                    else if (ByteConverter.SearchBytePattern(ATS, new byte[] { 0xC1, 0x05, 0x21, 0x30, 0x00, 0xF6, 0xD1 }) != 0) //MF PlusSE 1K
                                                    {
                                                        currentChip.CardType = ChipType.MifarePlus_SL0_1K;
                                                    }

                                                    else
                                                    {
                                                        currentChip.CardType = ChipType.MifarePlus_SL1_1K;
                                                    }
                                                } // Mifare Plus S / Plus X 4K

                                                else
                                                {
                                                    currentChip.CardType = ChipType.Mifare1K;
                                                } //Error on ATS = Mifare Classic 1K
                                            } // Mifare Plus; Historical Bytes ?
                                        }
                                    }
                                } // SAK b4 = 1 ?
                                else
                                {
                                    if ((SAK & 0x10) == 0x10)
                                    {
                                        if ((Result[2] & 0x01) == 0x01)
                                        {
                                            currentChip.CardType = ChipType.MifarePlus_SL2_4K;
                                        } // Mifare Plus 4K in SL2
                                        else
                                        {
                                            currentChip.CardType = ChipType.MifarePlus_SL2_2K;
                                        } // Mifare Plus 2K in SL2
                                    }
                                    else
                                    {
                                        if ((SAK & 0x01) == 0x01) // SAK b1 = 1 ?
                                        {

                                        } // Chip is "TagNPlay"
                                        else
                                        {
                                            if ((SAK & 0x20) == 0x20)
                                            {
                                                //Get ATS - Switch to L4 ?
                                                var response = await DoTXRXAsync(
                                                    ByteConverter.GetBytesFrom(
                                                        ISO14443_3_TXD +
                                                        "04" +
                                                        ISO_CMD_RATS
                                                    ));

                                                if (response != null && response.Length <= 4)
                                                {
                                                    response = await DoTXRXAsync(ByteConverter.GetBytesFrom(ISO14443_GET_ATS + "20"));
                                                    ATS = new byte[response.Length - 2];
                                                }
                                                else if (response != null && response.Length >= 5)
                                                {
                                                    ATS = new byte[response.Length - 2];
                                                }
                                                else
                                                {
                                                    ATS = new byte[1] { 0x00 };
                                                }

                                                Buffer.BlockCopy(response, 2, ATS, 0, response.Length - 2);
                                                Result = await DoTXRXAsync(new byte[] { 0x05, 0x00, 0x20 }); //GetChip
                                                var getVersion = await DoTXRXAsync(new byte[] { 0x12, 0x03, 0x01, 0x60, 0x20 }); //issue GetVersion

                                                if (getVersion?.Length > 4 && getVersion?[3] == 0xAF)
                                                {
                                                    L4VERSION = new byte[getVersion.Length - 2];
                                                    Buffer.BlockCopy(getVersion, 2, L4VERSION, 0, getVersion.Length - 2);

                                                    // Mifare Plus EV1/2 || DesFire || NTAG
                                                    if (getVersion?.Length > 1 && (getVersion?[5] == 0x01)) // DESFIRE
                                                    {
                                                        switch (getVersion?[7] & 0x0F) // Desfire(Sub)Type by lower Nibble of Major Version
                                                        {
                                                            case 0: //DESFIRE EV0
                                                                currentChip.CardType = ChipType.DESFire;

                                                                switch (getVersion?[9])
                                                                {
                                                                    case 0x10:
                                                                        currentChip.CardType = ChipType.DESFire_256; // DESFIRE 256B
                                                                        break;
                                                                    case 0x16:
                                                                        currentChip.CardType = ChipType.DESFire_2K; // DESFIRE 2K
                                                                        break;
                                                                    case 0x18:
                                                                        currentChip.CardType = ChipType.DESFire_4K; // 4K
                                                                        break;
                                                                    default:
                                                                        break;
                                                                } // Size ?
                                                                break;

                                                            case 1: // DESFIRE EV1
                                                                currentChip.CardType = ChipType.DESFireEV1;

                                                                switch (getVersion?[9])
                                                                {
                                                                    case 0x10:
                                                                        currentChip.CardType = ChipType.DESFireEV1_256; //DESFIRE 256B
                                                                        break;
                                                                    case 0x16:
                                                                        currentChip.CardType = ChipType.DESFireEV1_2K; // DESFIRE 2K
                                                                        break;
                                                                    case 0x18:
                                                                        currentChip.CardType = ChipType.DESFireEV1_4K; // 4K
                                                                        break;
                                                                    case 0x1A:
                                                                        currentChip.CardType = ChipType.DESFireEV1_8K; // 8K
                                                                        break;
                                                                    default:
                                                                        break;
                                                                } // Size ?
                                                                break;

                                                            case 2: // EV2
                                                                currentChip.CardType = ChipType.DESFireEV2;

                                                                switch (getVersion?[9])
                                                                {
                                                                    case 0x16:
                                                                        currentChip.CardType = ChipType.DESFireEV2_2K; // DESFIRE 2K
                                                                        break;
                                                                    case 0x18:
                                                                        currentChip.CardType = ChipType.DESFireEV2_4K; // 4K
                                                                        break;
                                                                    case 0x1A:
                                                                        currentChip.CardType = ChipType.DESFireEV2_8K; // 8K
                                                                        break;
                                                                    case 0x1C:
                                                                        currentChip.CardType = ChipType.DESFireEV2_16K; // 16K
                                                                        break;
                                                                    case 0x1E:
                                                                        currentChip.CardType = ChipType.DESFireEV2_32K; // 32K
                                                                        break;
                                                                    default:
                                                                        break;
                                                                } // SIZE ?
                                                                break;

                                                            case 3: // EV3
                                                                currentChip.CardType = ChipType.DESFireEV3;

                                                                switch (getVersion?[9])
                                                                {
                                                                    case 0x16:
                                                                        currentChip.CardType = ChipType.DESFireEV3_2K; // DESFIRE 2K
                                                                        break;
                                                                    case 0x18:
                                                                        currentChip.CardType = ChipType.DESFireEV3_4K; // 4K
                                                                        break;
                                                                    case 0x1A:
                                                                        currentChip.CardType = ChipType.DESFireEV3_8K; // 8K
                                                                        break;
                                                                    case 0x1C:
                                                                        currentChip.CardType = ChipType.DESFireEV3_16K; // 16K
                                                                        break;
                                                                    case 0x1E:
                                                                        currentChip.CardType = ChipType.DESFireEV3_32K; // 32K
                                                                        break;
                                                                    default:
                                                                        break;
                                                                } // SIZE ?
                                                                break;

                                                            default:
                                                                currentChip.CardType = ChipType.Unspecified;

                                                                break;
                                                        }
                                                    }
                                                    else if (getVersion?.Length > 1 && getVersion?[5] == 0x81) // Emulated e.g. SmartMX
                                                    {
                                                        switch (getVersion?[7] & 0x0F) // Desfire(Sub)Type by lower Nibble of Major Version
                                                        {
                                                            case 0: //DESFIRE EV0
                                                                currentChip.CardType = ChipType.SmartMX_DESFire_Generic;

                                                                switch (getVersion?[9])
                                                                {
                                                                    case 0x10:
                                                                        currentChip.CardType = ChipType.SmartMX_DESFire_Generic; // DESFIRE 256B
                                                                        break;
                                                                    case 0x16:
                                                                        currentChip.CardType = ChipType.SmartMX_DESFire_2K; // DESFIRE 2K
                                                                        break;
                                                                    case 0x18:
                                                                        currentChip.CardType = ChipType.SmartMX_DESFire_4K; // 4K
                                                                        break;
                                                                    default:
                                                                        break;
                                                                } // Size ?
                                                                break;

                                                            case 1: // DESFIRE EV1
                                                                currentChip.CardType = ChipType.SmartMX_DESFire_Generic;

                                                                switch (getVersion?[9])
                                                                {
                                                                    case 0x10:
                                                                        currentChip.CardType = ChipType.SmartMX_DESFire_Generic; //DESFIRE 256B
                                                                        break;
                                                                    case 0x16:
                                                                        currentChip.CardType = ChipType.SmartMX_DESFire_2K; // DESFIRE 2K
                                                                        break;
                                                                    case 0x18:
                                                                        currentChip.CardType = ChipType.SmartMX_DESFire_4K; // 4K
                                                                        break;
                                                                    case 0x1A:
                                                                        currentChip.CardType = ChipType.SmartMX_DESFire_8K; // 8K
                                                                        break;
                                                                    default:
                                                                        break;
                                                                } // Size ?
                                                                break;

                                                            case 2: // EV2
                                                                currentChip.CardType = ChipType.SmartMX_DESFire_Generic;

                                                                switch (getVersion?[9])
                                                                {
                                                                    case 0x16:
                                                                        currentChip.CardType = ChipType.SmartMX_DESFire_2K; // DESFIRE 2K
                                                                        break;
                                                                    case 0x18:
                                                                        currentChip.CardType = ChipType.SmartMX_DESFire_4K; // 4K
                                                                        break;
                                                                    case 0x1A:
                                                                        currentChip.CardType = ChipType.SmartMX_DESFire_8K; // 8K
                                                                        break;
                                                                    case 0x1C:
                                                                        currentChip.CardType = ChipType.SmartMX_DESFire_16K; // 16K
                                                                        break;
                                                                    case 0x1E:
                                                                        currentChip.CardType = ChipType.SmartMX_DESFire_32K; // 32K
                                                                        break;
                                                                    default:
                                                                        break;
                                                                } // SIZE ?
                                                                break;

                                                            case 3: // EV3
                                                                currentChip.CardType = ChipType.SmartMX_DESFire_Generic;

                                                                switch (getVersion?[9])
                                                                {
                                                                    case 0x16:
                                                                        currentChip.CardType = ChipType.SmartMX_DESFire_2K; // DESFIRE 2K
                                                                        break;
                                                                    case 0x18:
                                                                        currentChip.CardType = ChipType.SmartMX_DESFire_4K; // 4K
                                                                        break;
                                                                    case 0x1A:
                                                                        currentChip.CardType = ChipType.SmartMX_DESFire_8K; // 8K
                                                                        break;
                                                                    case 0x1C:
                                                                        currentChip.CardType = ChipType.SmartMX_DESFire_16K; // 16K
                                                                        break;
                                                                    case 0x1E:
                                                                        currentChip.CardType = ChipType.SmartMX_DESFire_32K; // 32K
                                                                        break;
                                                                    default:
                                                                        break;
                                                                } // SIZE ?
                                                                break;

                                                            default:
                                                                currentChip.CardType = ChipType.Unspecified;

                                                                break;
                                                        }
                                                    }
                                                } // Get Version L4 Failed

                                                else
                                                {
                                                    if (ATS.Length > 4)
                                                    {
                                                        if (ByteConverter.SearchBytePattern(ATS, new byte[] { 0xC1, 0x05, 0x2F, 0x2F, 0x00, 0x35, 0xC7 }) != 0) //MF PlusS 4K in SL1
                                                        {
                                                            currentChip.CardType = ChipType.MifarePlus_SL3_4K;
                                                        }

                                                        else if (ByteConverter.SearchBytePattern(ATS, new byte[] { 0xC1, 0x05, 0x2F, 0x2F, 0x01, 0xBC, 0xD6 }) != 0) //MF PlusX 4K in SL1
                                                        {
                                                            currentChip.CardType = ChipType.MifarePlus_SL3_4K;
                                                        }
                                                        else
                                                        {
                                                            currentChip.CardType = ChipType.Unspecified;
                                                        }
                                                    }
                                                    else
                                                    {
                                                        currentChip.CardType = ChipType.SmartMX_Mifare_4K;
                                                    }
                                                } // Mifare Plus
                                            } // SAK b6 = 1 ?
                                            else
                                            {
                                                currentChip.CardType = ChipType.MifareUltralight;
                                            } // Ultralight || NTAG
                                        }
                                    } // SAK b5 = 1 ?
                                } // SAK b5 = 1 ?
                            }


                        }
                        break;

                    default:

                        break;
                } // Get Tag

                genericChipModel = 
                    new ChipModel(
                        currentChip.UID, 
                        currentChip.CardType, 
                        ByteConverter.GetStringFrom(SAK), 
                        ByteConverter.GetStringFrom(ATS), 
                        ByteConverter.GetStringFrom(L4VERSION)
                    );
            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(e);
            }

            if (genericChipModel?.CardType != ChipType.NOTAG)
            {

            }
            else
            {

            }

            return genericChipModel;
        }
        private ChipModel genericChipModel;

        #endregion

        #region Reader Communication

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public bool Connect()
        {
            return ConnectAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public async Task<bool> ConnectAsync()
        {
            return (await DoTXRXAsync(new byte[] { 0 }))?[0] == 0x01; 
        }

        #region Tools for Simple Protocol

        private byte[] GetByteArrayfromPRS(string PRSString)
        {
            // Is string length = 0?
            if (PRSString.Length < 1)
            {
                // Yes, return null
                return null;
            }
            // Initialize byte array, half string length
            byte[] buffer = new byte[PRSString.Length / 2];
            // Get byte array from PRS string
            for (int i = 0; i < (buffer.Length); i++)
            {
                // Convert PRS Chars to byte array buffer
                buffer[i] = byte.Parse(PRSString.Substring((i * 2), 2), NumberStyles.HexNumber);
            }
            // Return byte array
            return buffer;
        }// End of PRStoByteArray
        private string GetPRSfromByteArray(byte[] PRSStream)
        {
            // Is length of PRS stream = 0
            if (PRSStream.Length < 1)
            {
                // Yes, return null
                return null;
            }
            // Iinitialize PRS buffer
            string buffer = null;
            // Convert byte to PRS string
            for (int i = 0; i < PRSStream.Length; i++)
            {
                // convert byte to characters
                buffer += PRSStream[i].ToString("X2");
            }
            // return buffer
            return buffer;
        }// End of GetPRSfromByteArray
        #endregion

        /// <summary>
        /// 
        /// </summary>
        /// <param name="CMD"></param>
        /// <returns></returns>
        private byte[] DoTXRX(byte[] CMD)
        {
            return DoTXRXAsync(CMD).GetAwaiter().GetResult();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="CMD"></param>
        /// <returns></returns>
        private async Task<byte[]> DoTXRXAsync(byte[] CMD)
        {
            try
            {
                using (SerialPort twnPort = new SerialPort())
                {
                    // Initialize serial port
                    twnPort.PortName = GetTWNPortName(portNumber);
                    twnPort.BaudRate = 9600;
                    twnPort.DataBits = 8;
                    twnPort.StopBits = System.IO.Ports.StopBits.One;
                    twnPort.Parity = System.IO.Ports.Parity.None;
                    // NFC functions are known to take less than 2 second to execute.
                    twnPort.ReadTimeout = 2000;
                    twnPort.WriteTimeout = 2000;
                    twnPort.NewLine = "\r";
                    twnPort.ErrorReceived += TXRXErr;

                    Func<Task> toDo = async () =>
                    {
                        try
                        {
                            // Open TWN4 com port
                            twnPort.Open();
                            PortAccessDenied = false;
                            return;
                        }
                        catch (Exception e)
                        {
                            //Port Busy? Try Again
                            if (e is UnauthorizedAccessException)
                            {
                                for (int i = 0; i <= 2; i++)
                                {
                                    await Task.Delay(1000).ConfigureAwait(false);
                                    // Open TWN4 com port
                                    try
                                    {
                                        twnPort.Open();
                                        PortAccessDenied = false;
                                        break;
                                    }
                                    // Force Open
                                    catch (Exception e2)
                                    {
                                        PortAccessDenied = true;
                                        LogWriter.CreateLogEntry(e2);
                                    };
                                }  
                            }
                        }
                    };
                    await toDo().ConfigureAwait(false);

                    IsConnected = twnPort.IsOpen;

                    if (CMD?[0] != 0 && IsConnected)
                    {
                        // Discard com port inbuffer
                        twnPort.DiscardInBuffer();
                        // Generate simple protocol string and send command
                        twnPort.WriteLine(GetPRSfromByteArray(CMD));
                        // Read simple protocoll string and convert to byte array
                        var ret = GetByteArrayfromPRS(twnPort.ReadLine());

                        twnPort.Close();

                        return ret;
                    }

                    else
                    {
                        return new byte[] { 0x01 };
                    }
                }
            }

            catch
            {
                this.Dispose();
                return new byte[] { 0x00 }; ;
            }

        }// End of DoTXRXAsync

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TXRXErr(object sender, EventArgs e)
        {
            Debug.WriteLine(e.ToString());
            return;
        }

        #region Tools for connect TWN4

        /// <summary>
        /// Get Registry Value From Key
        /// </summary>
        /// <param name="SubKey"></param>
        /// <param name="ValueName"></param>
        /// <returns></returns>
        private string RegHKLMQuerySZ(string SubKey, string ValueName)
        {
            string Data = "";

            try
            {
                RegistryKey Key = Registry.LocalMachine.OpenSubKey(SubKey);
                if (Key == null)
                    return "";
                if (Key.GetValue(ValueName) != null)
                    Data = Key.GetValue(ValueName).ToString();
                else
                    return "";
                if (Data == "")
                    return "";
                if (Key.GetValueKind(ValueName) != RegistryValueKind.String)
                    Data = "";
                Key.Close();
            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(e);
            }

            return Data;
        }// End of RegHKLMQuerySZ

        /// <summary>
        /// Get Device From Devices in Registry
        /// </summary>
        /// <param name="Driver"></param>
        /// <param name="DevicePath"></param>
        /// <returns></returns>
        private string FindUSBDevice(string Driver, string DevicePath)
        {
            int PortIndex = 0;

            try
            {
                while (true)
                {
                    string Path = "SYSTEM\\CurrentControlSet\\Services\\" + Driver + "\\Enum";
                    string Data = RegHKLMQuerySZ(Path, PortIndex.ToString());
                    string secondData = RegHKLMQuerySZ(Path, (PortIndex + 1).ToString());

                    if (Data == "")
                    {
                        return "";
                    }
                    else if ((Data.Substring(0, DevicePath.Length).ToUpper() == DevicePath) && secondData == "")
                    {
                        MoreThanOneReader = false;
                        return Data;
                    }
                    else if ((Data.Substring(0, DevicePath.Length).ToUpper() == DevicePath) && secondData != "")
                    {
                        MoreThanOneReader = true;
                        return Data;
                    }    
                    else
                    {
                        return "";
                    }
                }
            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(e);
            }
            return "";
        }// End of FindUSBDevice

        /// <summary>
        /// GetComPort from Devices
        /// </summary>
        /// <param name="Device"></param>
        /// <returns></returns>
        private int GetCOMPortNr(string Device)
        {
            string Path, Data;

            try
            {
                Path = "SYSTEM\\CurrentControlSet\\Enum\\" + Device + "\\Device Parameters";
                Data = RegHKLMQuerySZ(Path, "PortName");

                if (Data == "" || Data.Length < 4)
                {
                    return 0;
                }

                int PortNr = Convert.ToUInt16(Data.Substring(3));

                if (PortNr < 1 || PortNr > 256)
                {
                    return 0;
                }

                return PortNr;
            }
            catch(Exception e)
            {
                LogWriter.CreateLogEntry(e);
            }

            return 0;
        }// End of GetCOMPortNr

        private string GetTWNPortName(int PortNr)
        {
            string PortName = "";

            try
            {
                string path = FindUSBDevice("usbser", "USB\\VID_09D8&PID_0420\\");

                if (PortNr == 0)
                {
                    int portNumber = GetCOMPortNr(path);
                    if (portNumber != 0)
                    {
                        PortNr = portNumber;
                        PortName = string.Format("COM{0}", PortNr);
                    }
                    else
                    {
                        PortName = "";
                    }
                }
                else
                {
                    return string.Format("COM{0}", PortNr);
                }
                return PortName;
            }
            catch(Exception e)
            {
                LogWriter.CreateLogEntry(e);
            }

            return PortName;
        }// End of GetTWNPortName
        #endregion

        #endregion

        #region Public Properties
        public bool MoreThanOneReader
        {
            get; set;
        }

        public byte[] Result
        {
            get; set;
        }

        public byte KeySettings
        {
            get; set;
        }

        public byte KeyType
        {
            get; set;
        }

        public byte NumberOfKeys
        {
            get; set;
        }

        public bool IsConnected
        {
            get; private set;
        }

        public byte[] ATS
        {
            get; set;
        }

        public byte SAK
        {
            get; private set;
        }

        public byte[] L4VERSION
        {
            get; private set;
        }

        public byte[] ChipIdentifier
        {
            get; private set;
        }
        #endregion

        #region ClassicCommands

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <param name="keyType"></param>
        /// <param name="sectorNumber"></param>
        /// <returns></returns>
        public bool MifareClassicLogin(string key, byte keyType, byte sectorNumber)
        {
            return MifareClassicLoginAsync(key, keyType, sectorNumber).GetAwaiter().GetResult();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key">The Key. Format: "FFFFFFFFFFFF"</param>
        /// <param name="keyType">The KeyType. Keytype: KEY_A = 0, KEY_B = 1</param>
        /// <param name="sectorNumber"></param>
        /// <returns>Success = true, false otherwise</returns>
        public async Task<bool> MifareClassicLoginAsync(string key, byte keyType, byte sectorNumber)
        {
            try
            {
                Result = await DoTXRXAsync(new byte[] { 0x05, 0x00, 0x20 }); //GetChip
                if (Result.Length > 2 && Result[1] == 0x01 ? true : false)
                {
                    var cmd = ByteConverter.GetBytesFrom(MIFARE_CLASSIC_LOGIN + key + keyType.ToString("X2") + sectorNumber.ToString("X2"));
                    Result = await DoTXRXAsync(cmd);
                }
                else
                {
                    return RESULT_FAILED;
                }
            }
            catch
            {
                throw new ArgumentException();
            }

            return Result[1] == 0x01 ? RESULT_SUCCESS : RESULT_FAILED;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="blockNumber"></param>
        /// <returns></returns>
        public byte[] MifareClassicReadBlock(byte blockNumber)
        {
            return MifareClassicReadBlockAsync(blockNumber).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Read Data from Classic Chip
        /// </summary>
        /// <param name="blockNumber">DataBlock Number</param>
        /// <returns>DATA</returns>
        public async Task<byte[]> MifareClassicReadBlockAsync(byte blockNumber)
        {
            Result = await DoTXRXAsync(ByteConverter.GetBytesFrom(MIFARE_CLASSIC_READBLOCK + blockNumber.ToString("X2")));
            if(Result.Length > 2)
            {
                return ByteConverter.Trim(Result, 2, Result.Length - 2);
            }
            else
            {
                return new byte[] { 0x00 };
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="data"></param>
        /// <param name="blockNumber"></param>
        /// <returns></returns>
        public bool MifareClassicWriteBlock(byte[] data, byte blockNumber)
        {
            return MifareClassicWriteBlockAsync(data, blockNumber).GetAwaiter().GetResult();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="data"></param>
        /// <param name="blockNumber"></param>
        /// <returns></returns>
        public async Task<bool> MifareClassicWriteBlockAsync(byte[] data, byte blockNumber)
        {
            if(data.Length == 16)
            {
                Result = await DoTXRXAsync(ByteConverter.GetBytesFrom(MIFARE_CLASSIC_WRITEBLOCK + blockNumber.ToString("X2") + ByteConverter.GetStringFrom(data)));

                if (Result.Length == 2)
                {
                    return Result[1] == 0x01 ? RESULT_SUCCESS : RESULT_FAILED;
                }
                else
                {
                    return RESULT_FAILED;
                }
            }

            else
            {
                return RESULT_FAILED;
            }
        }

        #endregion

        #region DesFireCommands

        public UInt32[] GetDesfireAppIDs()
        {
            return GetDesfireAppIDsAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Retrieve the Available Application IDs after selecing PICC (App 0), Authentication is needed - depending on the security config
        /// </summary>
        /// <returns>a uint32[] of the available appids with 4bytes each, null if no apps are available or on error</returns>
        public async Task<UInt32[]> GetDesfireAppIDsAsync()
        {
            try
            {
                Result = await DoTXRXAsync(ByteConverter.GetBytesFrom(MIFARE_DESFIRE_GETAPPIDS + "00" + "1C"));

                UInt32[] appids = new UInt32[1];

                if (Result.Length > 2)
                {
                    appids = new UInt32[Result[2]];

                    for (int i = 0; i < Result[2]; i++)
                    {
                        appids[i] = 0x00000000;

                        for (int j = 6 + (i * 4); j > 2 + (i * 4); j--)
                        {
                            appids[i] = appids[i] << 8;
                            appids[i] |= (byte)(Result[j]);
                        }
                    }
                    return appids;
                }
                else
                {
                    return null;
                }
            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(e);
                return null;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="appID"></param>
        /// <returns></returns>
        public bool DesfireSelectApplication(uint appID)
        {
            return DesfireSelectApplicationAsync(appID).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Select a desfire Application
        /// </summary>
        /// <param name="appID">The Application ID to select</param>
        /// <returns>true if Application could be selected, false otherwise</returns>
        public async Task<bool> DesfireSelectApplicationAsync(uint appID)
        {
            try
            {
                Result = await DoTXRXAsync(new byte[] { 0x05, 0x00, 0x20 }); //GetChip
                Result = await DoTXRXAsync(ByteConverter.GetBytesFrom(MIFARE_DESFIRE_SELECTAPP + "00" + ByteConverter.GetStringFrom(BitConverter.GetBytes(appID))));

                if (Result?.Length == 2)
                {
                    return Result[1] == 0x01 ? true : false;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(e);
                return false;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public UInt32? GetDesfireFreeMemory()
        {
            return GetDesfireFreeMemoryAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Get the free Memory of a desfire. 
        /// </summary>
        /// <returns>a uint32 of the available memory if supported, null if freemem could not be read out</returns>
        public async Task<UInt32?> GetDesfireFreeMemoryAsync()
        {
            try
            {
                Result = await DoTXRXAsync(ByteConverter.GetBytesFrom(MIFARE_DESFIRE_GETFREEMEMORY + "00"));

                if (Result?.Length > 2)
                {
                    UInt32 freemem = 0x00000000;

                    for (uint i = 3; i >= 2; i--)
                    {
                        freemem = (freemem << 8);
                        freemem |= (byte)(Result[i]);
                    }
                    return freemem;
                }

                else
                {
                    return null;
                }
            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(e);
                return null;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <param name="keyNo"></param>
        /// <param name="keyType"></param>
        /// <param name="authMode"></param>
        /// <returns></returns>
        public bool DesfireAuthenticate(string key, byte keyNo, byte keyType, byte authMode)
        {
            return DesfireAuthenticateAsync(key, keyNo, keyType, authMode).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Authenticate to a previously selected desfire application
        /// </summary>
        /// <param name="key">string: a 16 bytes key e.g. 00000000000000000000000000000000</param>
        /// <param name="keyNo">byte: the keyNo to use</param>
        /// <param name="keyType">byte: 0 = 3DES, 1 = 3K3DES, 2 = AES</param>
        /// <param name="authMode">byte: 1 = EV1 Mode, 0 = EV0 Mode</param>
        /// <returns>true if Authentication was successful, false otherwise</returns>
        public async Task<bool> DesfireAuthenticateAsync(string key, byte keyNo, byte keyType, byte authMode)
        {
            try
            {
                Result = await DoTXRXAsync(ByteConverter.GetBytesFrom(MIFARE_DESFIRE_AUTH + "00" //CryptoEnv
                                                               + keyNo.ToString("X2")
                                                               + "10" // keyLength ?
                                                               + key
                                                               + keyType.ToString("X2")
                                                               + authMode.ToString("X2"))); // EV1-ISO Mode = 1, compatible = 0

                if (Result?.Length == 2)
                {
                    return Result[1] == 0x01 ? true : false;
                }

                else
                {
                    return false;
                }
            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(e);
                return false;
            }

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="_keySettingsTarget"></param>
        /// <param name="_keyTypeTargetApplication"></param>
        /// <param name="_maxNbKeys"></param>
        /// <param name="_appID"></param>
        /// <returns></returns>
        public bool DesfireCreateApplication(DESFireKeySettings _keySettingsTarget, DESFireKeyType _keyTypeTargetApplication, int _maxNbKeys, int _appID)
        {
            return DesfireCreateApplicationAsync(_keySettingsTarget, _keyTypeTargetApplication, _maxNbKeys, _appID).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Creates a new Application
        /// </summary>
        /// <param name="_keySettingsTarget">byte: KS_CHANGE_KEY_WITH_MK = 0, KS_ALLOW_CHANGE_MK = 1, KS_FREE_LISTING_WITHOUT_MK = 2, KS_FREE_CREATE_DELETE_WITHOUT_MK = 4, KS_CONFIGURATION_CHANGEABLE = 8, KS_DEFAULT = 11, KS_CHANGE_KEY_WITH_TARGETED_KEYNO = 224, KS_CHANGE_KEY_FROZEN = 240</param>
        /// <param name="_keyTypeTargetApplication">byte: 0 = 3DES, 1 = 3K3DES, 2 = AES</param>
        /// <param name="_maxNbKeys">int max. number of keys</param>
        /// <param name="_appID">int application id</param>
        /// <returns>true if the Operation was successful, false otherwise</returns>
        public async Task<bool> DesfireCreateApplicationAsync(DESFireKeySettings _keySettingsTarget, DESFireKeyType _keyTypeTargetApplication, int _maxNbKeys, int _appID)
        {
            try
            {
                Result = await DoTXRXAsync(ByteConverter.GetBytesFrom(MIFARE_DESFIRE_CREATEAPP + "00" //CryptoEnv
                                                                   + ByteConverter.GetStringFrom(ByteConverter.Reverse(ByteConverter.GetBytesFrom(_appID.ToString("X8"))))
                                                                   + ((byte)_keySettingsTarget).ToString("X2")
                                                                   + _maxNbKeys.ToString("D2")
                                                                   + "000000"
                                                                   + ((int)_keyTypeTargetApplication).ToString("D2")
                                                                   + "000000"));

                if (Result?.Length == 2)
                {
                    return Result[1] == 0x01 ? true : false;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(e);
                return false;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="appID"></param>
        /// <returns></returns>
        public bool DesfireDeleteApplication(uint appID)
        {
            return DesfireDeleteApplicationAsync(appID).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Select a desfire Application
        /// </summary>
        /// <param name="appID">The Application ID to delete</param>
        /// <returns>true if Application could be deleted, false otherwise</returns>
        public async Task<bool> DesfireDeleteApplicationAsync(uint appID)
        {
            try
            {
                Result = await DoTXRXAsync(ByteConverter.GetBytesFrom(MIFARE_DESFIRE_DELETEAPP + "00" + ByteConverter.GetStringFrom(BitConverter.GetBytes(appID))));

                if (Result?.Length == 2)
                {
                    return Result[1] == 0x01 ? true : false;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(e);
                return false;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public byte[] GetDesfireFileIDs()
        {
            return GetDesfireFileIDsAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Retrieve the Available File IDs after selecing App and Authenticating
        /// </summary>
        /// <returns>byte[] array of available file ids. null on error</returns>
        public async Task<byte[]> GetDesfireFileIDsAsync()
        {
            try
            {
                Result = await DoTXRXAsync(ByteConverter.GetBytesFrom(MIFARE_DESFIRE_GETFILEIDS + "00" + "FF"));

                var fids = new byte[1];

                if (Result?.Length > 2)
                {
                    fids = new byte[Result[2]];
                    for (var i = 3; i < Result.Length; i++)
                    {
                        fids[i - 3] = (byte)Result[i];
                    }
                    return fids;
                }
                else
                {
                    return null;
                }
            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(e);
                return null;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public bool GetDesFireKeySettings()
        {
            return GetDesFireKeySettingsAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Get the KeySettings (Properties: KeySettings, NumberOfKeys, KeyType) of the selected Application. Authentication is needed - depending on the security config
        /// </summary>
        /// <returns>true if the Operation was successful, false otherwise</returns>
        public async Task<bool> GetDesFireKeySettingsAsync()
        {
            try
            {
                Result = await DoTXRXAsync(ByteConverter.GetBytesFrom(MIFARE_DESFIRE_GETKEYSETTINGS + "00"));

                if (Result?.Length >= 3 && (Result[1] == 0x01 ? true : false))
                {
                    KeySettings = Result[2];
                    NumberOfKeys = Result[3];
                    KeyType = Result[7];

                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(e);
                return false;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileNo"></param>
        /// <returns></returns>
        public byte[] GetDesFireFileSettings(byte fileNo)
        {
            return GetDesFireFileSettingsAsync(fileNo).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Get the filesettings of a fileid
        /// </summary>
        /// <param name="fileNo">id of the desired file</param>
        /// <returns>byte[] array of the file settings. null on error. content: FileType = fileSettings[2]; comSett = fileSettings[3]; accessRights[0] = fileSettings[4]; accessRights[1] = fileSettings[5];</returns>
        public async Task<byte[]> GetDesFireFileSettingsAsync(byte fileNo)
        {
            try
            {
                Result = await DoTXRXAsync(ByteConverter.GetBytesFrom(MIFARE_DESFIRE_GETFILESETTINGS + "00" + ByteConverter.GetStringFrom(fileNo)));

                if (Result?.Length >= 3 && (Result[1] == 0x01 ? true : false))
                {
                    return Result;
                }
                else
                {
                    return null;
                }
            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(e);
                return null;
            }

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileNo"></param>
        /// <param name="fileType"></param>
        /// <param name="comSet"></param>
        /// <param name="accessRights"></param>
        /// <param name="fileSize"></param>
        /// <returns></returns>
        public bool DesfireCreateFile(byte fileNo, byte fileType, byte comSet, UInt16 accessRights, UInt32 fileSize)
        {
            return DesfireCreateFileAsync(fileNo, fileType, comSet, accessRights, fileSize).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Read out Data on a Desfire
        /// </summary>
        /// <param name="fileNo">byte: filenumber: 0x00 - 0x14</param>
        /// <param name="length">int: filesize to read</param>
        /// <param name="comSet">byte: 0 = Plain, 1 = CMAC, 2 = Encrypted</param>
        /// <returns>byte[] of data, null on error</returns>
        public async Task<bool> DesfireCreateFileAsync(byte fileNo, byte fileType, byte comSet, UInt16 accessRights, UInt32 fileSize)
        {
            try
            {
                Result = await DoTXRXAsync(ByteConverter.GetBytesFrom(MIFARE_DESFIRE_CREATE_STDDATAFILE + "00" //CryptoEnv
                                                                   + fileNo.ToString("X2")
                                                                   + fileType.ToString("X2")
                                                                   + comSet.ToString("X2")
                                                                   + ByteConverter.GetStringFrom(ByteConverter.GetBytesFrom(accessRights.ToString("X4")))
                                                                   + ByteConverter.GetStringFrom(ByteConverter.Reverse(ByteConverter.GetBytesFrom(fileSize.ToString("X8"))))
                                                                   + "000000000000000000000000"));



                return Result?.Length >= 3 && (Result[1] == 0x01 ? true : false);

            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(e);
                return false;
            }

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileNo"></param>
        /// <param name="length"></param>
        /// <param name="comSet"></param>
        /// <returns></returns>
        public byte[] DesfireReadData(byte fileNo, int length, byte comSet)
        {
            return DesfireReadDataAsync(fileNo, length, comSet).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Read out Data on a Desfire
        /// </summary>
        /// <param name="fileNo">byte: filenumber: 0x00 - 0x14</param>
        /// <param name="length">int: filesize to read</param>
        /// <param name="comSet">byte: 0 = Plain, 1 = CMAC, 2 = Encrypted</param>
        /// <returns>byte[] of data, null on error</returns>
        public async Task<byte[]> DesfireReadDataAsync(byte fileNo, int length, byte comSet)
        {
            try
            {
                var data = new byte[length];
                var iterations = (length / 0xFF) == 0 ? 1 : (length / 0xFF);
                var dataLengthToRead = length;

                for (var i = 0; i < iterations; i++)
                {
                    Result = await DoTXRXAsync(ByteConverter.GetBytesFrom(MIFARE_DESFIRE_READDATA + "00" //CryptoEnv
                                                                   + fileNo.ToString("X2")
                                                                   + ByteConverter.GetStringFrom(ByteConverter.Reverse(ByteConverter.GetBytesFrom((i*0xFF).ToString("X4"))))
                                                                   + (dataLengthToRead >= 0xFF ? 0xFF : length).ToString("X2")
                                                                   + (comSet).ToString("X2")));
                    
                    Array.Copy(ByteConverter.Trim(Result, 3, Result[2]),0, data,(i*0xFF), (dataLengthToRead >= 0xFF ? 0xFF : length));
                }
                

                if (Result?.Length >= 3 && (Result[1] == 0x01 ? true : false))
                {
                    return data;
                }
                else
                {
                    return null;
                }
            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(e);
                return null;
            }

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="keySettings"></param>
        /// <param name="numberOfKeys"></param>
        /// <param name="keyType"></param>
        /// <returns></returns>
        public bool DesfireChangeKeySettings(byte keySettings, UInt32 numberOfKeys, UInt32 keyType)
        {
            return DesfireChangeKeySettingsAsync(keySettings, numberOfKeys, keyType).GetAwaiter().GetResult();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="keySettings"></param>
        /// <param name="numberOfKeys"></param>
        /// <param name="keyType"></param>
        /// <returns></returns>
        public async Task<bool> DesfireChangeKeySettingsAsync(byte keySettings, UInt32 numberOfKeys, UInt32 keyType)
        {
            try
            {
                Result = await DoTXRXAsync(ByteConverter.GetBytesFrom(MIFARE_DESFIRE_CHANGEKEYSETTINGS + "00" 
                    + keySettings.ToString("X2") 
                    + numberOfKeys.ToString("X8") 
                    + keyType.ToString("X8") ));

                return Result?.Length == 2 && (Result[1] == 0x01 ? true : false);
            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(e);
                return false;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="oldKey"></param>
        /// <param name="newKey"></param>
        /// <param name="keyVersion"></param>
        /// <param name="accessRights"></param>
        /// <param name="keyNo"></param>
        /// <param name="numberOfKeys"></param>
        /// <param name="keyType"></param>
        /// <returns></returns>
        public bool DesfireChangeKey(string oldKey, string newKey, byte keyVersion, byte accessRights, byte keyNo, UInt32 numberOfKeys, UInt32 keyType)
        {
            return DesfireChangeKeyAsync(oldKey, newKey, keyVersion, accessRights, keyNo, numberOfKeys, keyType).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Changes a Key
        /// </summary>
        /// <param name="oldKey"></param>
        /// <param name="newKey"></param>
        /// <param name="keyVersion"></param>
        /// <param name="accessRights"></param>
        /// <param name="keyNo"></param>
        /// <param name="numberOfKeys"></param>
        /// <param name="keyType">The Type of the new Key</param>
        /// <returns></returns>
        public async Task<bool> DesfireChangeKeyAsync(string oldKey, string newKey, byte keyVersion, byte accessRights, byte keyNo, UInt32 numberOfKeys, UInt32 keyType)
        {
            try
            {
                Result = await DoTXRXAsync(ByteConverter.GetBytesFrom(MIFARE_DESFIRE_CHANGEKEY + "00" 
                    + keyNo.ToString("X2") 
                    + "10" + oldKey 
                    + "10" + newKey 
                    + keyVersion.ToString("X2") 
                    + accessRights.ToString("X2")
                    + ByteConverter.GetStringFrom(ByteConverter.Reverse(ByteConverter.GetBytesFrom(numberOfKeys.ToString("X8"))))
                    + ByteConverter.GetStringFrom(ByteConverter.Reverse(ByteConverter.GetBytesFrom(keyType.ToString("X8"))))));

                return Result?.Length == 2 && (Result[1] == 0x01 ? true : false);
            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(e);
                return false;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileNo"></param>
        /// <returns></returns>
        public bool DesfireDeleteFile(byte fileNo)
        {
            return DesfireDeleteFileAsync(fileNo).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Delete a File
        /// </summary>
        /// <param name="fileNo">byte: Filenumber to delete</param>
        /// <returns>true if the Operation was successful, false otherwise</returns>
        public async Task<bool> DesfireDeleteFileAsync(byte fileNo)
        {
            try
            {
                Result = await DoTXRXAsync(ByteConverter.GetBytesFrom(MIFARE_DESFIRE_DELETEFILE + "00" + fileNo.ToString("X2")));

                if (Result?.Length == 2)
                {
                    return Result[1] == 0x01 ? true : false;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(e);
                return false;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public bool DesfireFormatTag()
        {
            return DesfireFormatTagAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Format a Chip
        /// </summary>
        /// <returns>true if the Operation was successful, false otherwise</returns>
        public async Task<bool> DesfireFormatTagAsync()
        {
            try
            {
                Result = await DoTXRXAsync(ByteConverter.GetBytesFrom(MIFARE_DESFIRE_FORMATTAG + "00" ));

                if (Result?.Length == 2)
                {
                    return Result[1] == 0x01 ? true : false;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(e);
                return false;
            }
        }
        #endregion

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    IsConnected = false;
                    
                    // Dispose any managed objects
                    // ...
                }

                Thread.Sleep(20);
                _disposed = true;
            }
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            //GreenLED(false);
            Dispose(true);
        }

    }


}
