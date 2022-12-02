using Microsoft.Win32;

using System;
using System.IO.Ports;
using System.Globalization;
using System.Threading;

using Elatec.NET.Model;

using Log4CSharp;

using ByteArrayHelper.Extensions;
using System.Linq;
using System.Security.Cryptography;
using System.Collections;
using ByteArrayHelper;
/*
* Elatec.NET is a C# library to easily Talk to Elatec's TWN4 Devices
* 
* Boolean "Results": Success = true, Failed = false
* 
* 
* 
*/

namespace Elatec.NET
{
    public class TWN4ReaderDevice : IDisposable
    {
        private const bool RESULT_SUCCESS = true;
        private const bool RESULT_FAILED = false;
        private string LogFacilityName = "RFiDGear";

        private int portNumber;

        private bool _disposed;
        private static readonly object syncRoot = new object();
        private static TWN4ReaderDevice instance;

        #region ELATEC COMMANDS
        private const string GET_LASTERR = "000A";

        private const string BEEP_CMD = "0407";
        private const string LEDINIT_CMD = "0410";
        private const string LEDON_CMD = "0411";
        private const string LEDOFF_CMD = "0412";

        private const string MIFARELOGIN = "0B00";
        private const string MIFAREREADBLOCK = "0B01";
        private const string MIFAREWRITEBLOCK = "0B02";

        private const string ISO14443_GET_ATS = "1200";

        private const string MIFARE_DESFIRE_GETAPPIDS = "0F00";
        private const string MIFARE_DESFIRE_CREATEAPP = "0F01";
        private const string MIFARE_DESFIRE_SELECTAPP = "0F03";
        private const string MIFARE_DESFIRE_AUTH = "0F04";
        private const string MIFARE_DESFIRE_GETKEYSETTINGS = "0F05";
        private const string MIFARE_DESFIRE_GETFILEIDS = "0F06";
        private const string MIFARE_DESFIRE_GETFILESETTINGS = "0F07";
        private const string MIFARE_DESFIRE_GETFREEMEMORY = "0F0E";

        private const string MIFARE_DESFIRE_COMMIT_TRNS = "0F1400";
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

        public TWN4ReaderDevice()
        {
        }

        public TWN4ReaderDevice(int port)
        {
            portNumber = port;
        }

        #region Common

        public void Beep()
        {
            Result = DoTXRX(new byte[] { 0x04, 0x07, 0x64, 0x60, 0x09, 0x54, 0x01, 0xF4, 0x01 }); 
        }

        public void Beep(ushort iterations, ushort length, ushort freq, byte vol)
        {
            for (uint i = 0; i < iterations; i++)
            {
                Result = DoTXRX(
                    ByteConverter.GetBytesFrom(BEEP_CMD +
                    ByteConverter.GetStringFrom(vol) +
                    ByteConverter.GetStringFrom(BitConverter.GetBytes(freq)) +
                    ByteConverter.GetStringFrom(BitConverter.GetBytes(length)) +
                    ByteConverter.GetStringFrom(BitConverter.GetBytes(length)))
                    );
            }
        }

        public void GreenLED(bool On)
        {
            Result = DoTXRX(
                    ByteConverter.GetBytesFrom(LEDINIT_CMD +
                    ByteConverter.GetStringFrom(0x0F))
                    );
            if(On)
            {
                Result = DoTXRX(
                    ByteConverter.GetBytesFrom(LEDON_CMD +
                    ByteConverter.GetStringFrom(0x02))
                    );
            }
            else
            {
                Result = DoTXRX(
                    ByteConverter.GetBytesFrom(LEDOFF_CMD +
                    ByteConverter.GetStringFrom(0x02))
                    );
            }
        }

        public void RedLED(bool On)
        {
            Result = DoTXRX(
                    ByteConverter.GetBytesFrom(LEDINIT_CMD +
                    ByteConverter.GetStringFrom(0x0F))
                    );
            if (On)
            {
                Result = DoTXRX(
                    ByteConverter.GetBytesFrom(LEDON_CMD +
                    ByteConverter.GetStringFrom(0x01))
                    );
            }
            else
            {
                Result = DoTXRX(
                    ByteConverter.GetBytesFrom(LEDOFF_CMD +
                    ByteConverter.GetStringFrom(0x01))
                    );
            }
        }

        public ChipModel GetSingleChip()
        {
            try
            {
                genericChipModel = new ChipModel();

                SAK = 0x00;
                ATS = new byte[1] {0x00};

                Result = DoTXRX(new byte[] { 0x05, 0x02, 0x00, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF }); //SetChipTypes (HF onyl) DoTXRX(new byte[] { 0x05, 0x02, 0x00, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF });
                Result = DoTXRX(new byte[] { 0x05, 0x00, 0x20}); //GetChip  //

                if (Result?.Length >= 3)
                {
                    genericChipModel.CardType = (ChipType)Result[2];
                }
                else
                {
                    Result = DoTXRX(new byte[] { 0x12, 0x08, 0xFF }); //GetChip UID if GetChip failed (SmartMX)
                }

                genericChipModel.ChipIdentifier = ByteConverter.GetStringFrom(Result, 5);

                switch (genericChipModel.CardType)
                {
                    case ChipType.NOTAG:
                    case ChipType.MIFARE: //Start Mifare Identification Process

                        Result = DoTXRX(new byte[] { 0x12, 0x05 }); //GetSAK

                        if (Result?.Length == 3)
                        {
                            SAK = Result[2];
                            
                            // Start MIFARE identification
                            if ((SAK & 0x02) == 0x02)
                            {
                                genericChipModel.CardType = ChipType.Unspecified;
                            } // RFU bit set (RFU = Reserved for Future Use)

                            else
                            {
                                if ((SAK & 0x08) == 0x08)
                                {
                                    if ((SAK & 0x10) == 0x10)
                                    {
                                        if ((SAK & 0x01) == 0x01)
                                        {
                                            genericChipModel.CardType = ChipType.Mifare2K;
                                        } // // SAK b1 = 1 ? >> Mifare Classic 2K
                                        else
                                        {
                                            if ((SAK & 0x20) == 0x20)
                                            {
                                                genericChipModel.CardType = ChipType.SmartMX_Mifare_4K;
                                            } // SAK b6 = 1 ?  >> SmartMX Classic 4K
                                            else
                                            {
                                                //Get ATS - Switch to L4 ?
                                                ATS = DoTXRX(ByteConverter.GetBytesFrom(ISO14443_GET_ATS + "40"));

                                                if (ATS.Length > 4)
                                                {
                                                    if (ByteConverter.SearchBytePattern(ATS, new byte[] { 0xC1, 0x05, 0x2F, 0x2F, 0x00, 0x35, 0xC7 }) != 0) //MF PlusS 4K in SL1
                                                    {
                                                        genericChipModel.CardType = ChipType.MifarePlus_SL1_4K;
                                                    }

                                                    else if (ByteConverter.SearchBytePattern(ATS, new byte[] { 0xC1, 0x05, 0x2F, 0x2F, 0x01, 0xBC, 0xD6 }) != 0) //MF PlusX 4K in SL1
                                                    {
                                                        genericChipModel.CardType = ChipType.MifarePlus_SL1_4K;
                                                    }

                                                } // Mifare Plus S / Plus X 4K

                                                else
                                                {
                                                    genericChipModel.CardType = ChipType.Mifare4K;
                                                } //Error on ATS = Mifare Classic 4K
                                                break;
                                            }
                                        }
                                    } // SAK b5 = 1 ?
                                    else
                                    {
                                        if ((SAK & 0x01) == 0x01)
                                        {
                                            genericChipModel.CardType = ChipType.MifareMini;
                                        } // // SAK b1 = 1 ? >> Mifare Mini
                                        else
                                        {
                                            if ((SAK & 0x20) == 0x20)
                                            {
                                                genericChipModel.CardType = ChipType.SmartMX_Mifare_1K;
                                            } // // SAK b6 = 1 ? >> SmartMX Classic 1K
                                            else
                                            {
                                                ATS = DoTXRX(ByteConverter.GetBytesFrom(ISO14443_GET_ATS + "40"));

                                                if (ATS.Length > 4)
                                                {
                                                    if (ByteConverter.SearchBytePattern(ATS, new byte[] { 0xC1, 0x05, 0x2F, 0x2F, 0x00, 0x35, 0xC7 }) != 0) //MF PlusS 4K in SL1
                                                    {
                                                        genericChipModel.CardType = ChipType.MifarePlus_SL1_2K;
                                                    }

                                                    else if (ByteConverter.SearchBytePattern(ATS, new byte[] { 0xC1, 0x05, 0x2F, 0x2F, 0x01, 0xBC, 0xD6 }) != 0) //MF PlusX 4K in SL1
                                                    {
                                                        genericChipModel.CardType = ChipType.MifarePlus_SL1_2K;
                                                    }

                                                    else if (ByteConverter.SearchBytePattern(ATS, new byte[] { 0xC1, 0x05, 0x21, 0x30, 0x00, 0xF6, 0xD1 }) != 0) //MF PlusSE 1K
                                                    {
                                                        genericChipModel.CardType = ChipType.MifarePlus_SL0_1K;
                                                    }

                                                } // Mifare Plus S / Plus X 4K

                                                else
                                                {
                                                    genericChipModel.CardType = ChipType.Mifare1K;
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
                                            genericChipModel.CardType = ChipType.MifarePlus_SL2_4K;
                                        } // Mifare Plus 4K in SL2
                                        else
                                        {
                                            genericChipModel.CardType = ChipType.MifarePlus_SL2_2K;
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
                                                ATS = DoTXRX(ByteConverter.GetBytesFrom(ISO14443_GET_ATS + "40"));

                                                var getVersion = DoTXRX(new byte[] { 0x12, 0x03, 0x01, 0x60, 0x20 }); //issue GetVersion

                                                if (getVersion?.Length > 4 && getVersion?[3] == 0xAF)
                                                {
                                                    // Mifare Plus EV1/2 || DesFire || NTAG
                                                    if (getVersion?.Length > 1 && (getVersion?[5] == 0x01)) // DESFIRE
                                                    {
                                                        switch (getVersion?[7] & 0x0F) // Desfire(Sub)Type by lower Nibble of Major Version
                                                        {
                                                            case 0: //DESFIRE EV0
                                                                genericChipModel.CardType = ChipType.DESFire;

                                                                switch (getVersion?[9])
                                                                {
                                                                    case 0x10:
                                                                        genericChipModel.CardType = ChipType.DESFire_256; // DESFIRE 256B
                                                                        break;
                                                                    case 0x16:
                                                                        genericChipModel.CardType = ChipType.DESFire_2K; // DESFIRE 2K
                                                                        break;
                                                                    case 0x18:
                                                                        genericChipModel.CardType = ChipType.DESFire_4K; // 4K
                                                                        break;
                                                                    default:
                                                                        break;
                                                                } // Size ?
                                                                break;

                                                            case 1: // DESFIRE EV1
                                                                genericChipModel.CardType = ChipType.DESFireEV1;

                                                                switch (getVersion?[9])
                                                                {
                                                                    case 0x10:
                                                                        genericChipModel.CardType = ChipType.DESFireEV1_256; //DESFIRE 256B
                                                                        break;
                                                                    case 0x16:
                                                                        genericChipModel.CardType = ChipType.DESFireEV1_2K; // DESFIRE 2K
                                                                        break;
                                                                    case 0x18:
                                                                        genericChipModel.CardType = ChipType.DESFireEV1_4K; // 4K
                                                                        break;
                                                                    case 0x1A:
                                                                        genericChipModel.CardType = ChipType.DESFireEV1_8K; // 8K
                                                                        break;
                                                                    default:
                                                                        break;
                                                                } // Size ?
                                                                break;

                                                            case 2: // EV2
                                                                genericChipModel.CardType = ChipType.DESFireEV2;

                                                                switch (getVersion?[9])
                                                                {
                                                                    case 0x16:
                                                                        genericChipModel.CardType = ChipType.DESFireEV2_2K; // DESFIRE 2K
                                                                        break;
                                                                    case 0x18:
                                                                        genericChipModel.CardType = ChipType.DESFireEV2_4K; // 4K
                                                                        break;
                                                                    case 0x1A:
                                                                        genericChipModel.CardType = ChipType.DESFireEV2_8K; // 8K
                                                                        break;
                                                                    case 0x1C:
                                                                        genericChipModel.CardType = ChipType.DESFireEV2_16K; // 16K
                                                                        break;
                                                                    case 0x1E:
                                                                        genericChipModel.CardType = ChipType.DESFireEV2_32K; // 32K
                                                                        break;
                                                                    default:
                                                                        break;
                                                                } // SIZE ?
                                                                break;

                                                            case 3: // EV3
                                                                genericChipModel.CardType = ChipType.DESFireEV3;

                                                                switch (getVersion?[9])
                                                                {
                                                                    case 0x16:
                                                                        genericChipModel.CardType = ChipType.DESFireEV3_2K; // DESFIRE 2K
                                                                        break;
                                                                    case 0x18:
                                                                        genericChipModel.CardType = ChipType.DESFireEV3_4K; // 4K
                                                                        break;
                                                                    case 0x1A:
                                                                        genericChipModel.CardType = ChipType.DESFireEV3_8K; // 8K
                                                                        break;
                                                                    case 0x1C:
                                                                        genericChipModel.CardType = ChipType.DESFireEV3_16K; // 16K
                                                                        break;
                                                                    case 0x1E:
                                                                        genericChipModel.CardType = ChipType.DESFireEV3_32K; // 32K
                                                                        break;
                                                                    default:
                                                                        break;
                                                                } // SIZE ?
                                                                break;

                                                            default:
                                                                genericChipModel.CardType = ChipType.Unspecified;

                                                                break;
                                                        }
                                                    }
                                                    else if (getVersion?.Length > 1 && getVersion?[5] == 0x81) // Emulated e.g. SmartMX
                                                    {
                                                        switch (getVersion?[7] & 0x0F) // Desfire(Sub)Type by lower Nibble of Major Version
                                                        {
                                                            case 0: //DESFIRE EV0
                                                                genericChipModel.CardType = ChipType.SmartMX_DESFire_Generic;

                                                                switch (getVersion?[9])
                                                                {
                                                                    case 0x10:
                                                                        genericChipModel.CardType = ChipType.SmartMX_DESFire_Generic; // DESFIRE 256B
                                                                        break;
                                                                    case 0x16:
                                                                        genericChipModel.CardType = ChipType.SmartMX_DESFire_2K; // DESFIRE 2K
                                                                        break;
                                                                    case 0x18:
                                                                        genericChipModel.CardType = ChipType.SmartMX_DESFire_4K; // 4K
                                                                        break;
                                                                    default:
                                                                        break;
                                                                } // Size ?
                                                                break;

                                                            case 1: // DESFIRE EV1
                                                                genericChipModel.CardType = ChipType.SmartMX_DESFire_Generic;

                                                                switch (getVersion?[9])
                                                                {
                                                                    case 0x10:
                                                                        genericChipModel.CardType = ChipType.SmartMX_DESFire_Generic; //DESFIRE 256B
                                                                        break;
                                                                    case 0x16:
                                                                        genericChipModel.CardType = ChipType.SmartMX_DESFire_2K; // DESFIRE 2K
                                                                        break;
                                                                    case 0x18:
                                                                        genericChipModel.CardType = ChipType.SmartMX_DESFire_4K; // 4K
                                                                        break;
                                                                    case 0x1A:
                                                                        genericChipModel.CardType = ChipType.SmartMX_DESFire_8K; // 8K
                                                                        break;
                                                                    default:
                                                                        break;
                                                                } // Size ?
                                                                break;

                                                            case 2: // EV2
                                                                genericChipModel.CardType = ChipType.SmartMX_DESFire_Generic;

                                                                switch (getVersion?[9])
                                                                {
                                                                    case 0x16:
                                                                        genericChipModel.CardType = ChipType.SmartMX_DESFire_2K; // DESFIRE 2K
                                                                        break;
                                                                    case 0x18:
                                                                        genericChipModel.CardType = ChipType.SmartMX_DESFire_4K; // 4K
                                                                        break;
                                                                    case 0x1A:
                                                                        genericChipModel.CardType = ChipType.SmartMX_DESFire_8K; // 8K
                                                                        break;
                                                                    case 0x1C:
                                                                        genericChipModel.CardType = ChipType.SmartMX_DESFire_16K; // 16K
                                                                        break;
                                                                    case 0x1E:
                                                                        genericChipModel.CardType = ChipType.SmartMX_DESFire_32K; // 32K
                                                                        break;
                                                                    default:
                                                                        break;
                                                                } // SIZE ?
                                                                break;

                                                            case 3: // EV3
                                                                genericChipModel.CardType = ChipType.SmartMX_DESFire_Generic;

                                                                switch (getVersion?[9])
                                                                {
                                                                    case 0x16:
                                                                        genericChipModel.CardType = ChipType.SmartMX_DESFire_2K; // DESFIRE 2K
                                                                        break;
                                                                    case 0x18:
                                                                        genericChipModel.CardType = ChipType.SmartMX_DESFire_4K; // 4K
                                                                        break;
                                                                    case 0x1A:
                                                                        genericChipModel.CardType = ChipType.SmartMX_DESFire_8K; // 8K
                                                                        break;
                                                                    case 0x1C:
                                                                        genericChipModel.CardType = ChipType.SmartMX_DESFire_16K; // 16K
                                                                        break;
                                                                    case 0x1E:
                                                                        genericChipModel.CardType = ChipType.SmartMX_DESFire_32K; // 32K
                                                                        break;
                                                                    default:
                                                                        break;
                                                                } // SIZE ?
                                                                break;

                                                            default:
                                                                genericChipModel.CardType = ChipType.Unspecified;

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
                                                            genericChipModel.CardType = ChipType.MifarePlus_SL3_4K;
                                                        }

                                                        else if (ByteConverter.SearchBytePattern(ATS, new byte[] { 0xC1, 0x05, 0x2F, 0x2F, 0x01, 0xBC, 0xD6 }) != 0) //MF PlusX 4K in SL1
                                                        {
                                                            genericChipModel.CardType = ChipType.MifarePlus_SL3_4K;
                                                        }
                                                        else
                                                        {
                                                            genericChipModel.CardType = ChipType.SmartMX_Mifare_4K;
                                                        }
                                                    }
                                                    else
                                                    {
                                                        genericChipModel.CardType = ChipType.SmartMX_Mifare_4K;
                                                    }
                                                } // Mifare Plus
                                            } // SAK b6 = 1 ?
                                            else
                                            {
                                                genericChipModel.CardType = ChipType.MifareUltralight;
                                            } // Ultralight || NTAG
                                        }
                                    } // SAK b5 = 1 ?
                                } // SAK b5 = 1 ?
                            }


                        }
                        break;

                    default:

                        break;
                }
                
            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(e, LogFacilityName);
            }

            if (genericChipModel.CardType != ChipType.NOTAG)
            {
                GreenLED(true);
                RedLED(false);
            }
            else
            {
                RedLED(true);
            }

            return genericChipModel;
        }
        private ChipModel genericChipModel;

        #endregion

        #region Reader Communication

        public bool Connect()
        {
            return (DoTXRX(null)[0] == 0x01); 
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

        private byte[] DoTXRX(byte[] CMD)
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
            
                    // Open TWN4 com port
                    twnPort.Open();

                    IsConnected = twnPort.IsOpen;

                    if (CMD != null)
                    {
                        // Discard com port inbuffer
                        twnPort.DiscardInBuffer();
                        // Generate simple protocol string and send command
                        twnPort.WriteLine(GetPRSfromByteArray(CMD));
                        // Read simple protocoll string and convert to byte array
                        return GetByteArrayfromPRS(twnPort.ReadLine());
                    }

                    else
                    {
                        return new byte[] {0x00};
                    }

                }
            }

            catch
            {
                this.Dispose();
                return null;
            }

        }// End of DoTXRX

        #region Tools for connect TWN4

        /// <summary>
        /// Get Registry Value From Key
        /// </summary>
        /// <param name="SubKey"></param>
        /// <param name="ValueName"></param>
        /// <returns></returns>
        private string RegHKLMQuerySZ(string SubKey, string ValueName)
        {
            string Data;

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

            while (true)
            {
                string Path = "SYSTEM\\CurrentControlSet\\Services\\" + Driver + "\\Enum";
                string Data = RegHKLMQuerySZ(Path, PortIndex.ToString());
                PortIndex++;
                if (Data == "")
                    return "";
                string substr = Data.Substring(0, DevicePath.Length).ToUpper();
                if (substr == DevicePath)
                    return Data;
            }
        }// End of FindUSBDevice

        /// <summary>
        /// GetComPort from Devices
        /// </summary>
        /// <param name="Device"></param>
        /// <returns></returns>
        private int GetCOMPortNr(string Device)
        {
            string Path = "SYSTEM\\CurrentControlSet\\Enum\\" + Device + "\\Device Parameters";
            string Data = RegHKLMQuerySZ(Path, "PortName");
            if (Data == "")
                return 0;
            if (Data.Length < 4)
                return 0;
            int PortNr = Convert.ToUInt16(Data.Substring(3));
            if (PortNr < 1 || PortNr > 256)
                return 0;
            return PortNr;
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
                LogWriter.CreateLogEntry(e, LogFacilityName);
            }

            return PortName;
        }// End of GetTWNPortName
        #endregion

        #endregion

        #region Public Properties
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
        #endregion

        #region ClassicCommands

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key">The Key. Format: "FFFFFFFFFFFF"</param>
        /// <param name="keyType">The KeyType. Keytype: KEY_A = 0, KEY_B = 1</param>
        /// <param name="sectorNumber"></param>
        /// <returns>Success = true, false otherwise</returns>
        public bool MifareClassicLogin(string key, byte keyType, byte sectorNumber)
        {
            try
            {
                Result = DoTXRX(new byte[] { 0x05, 0x00, 0x20 }); //GetChip
                if (Result.Length > 2 && Result[1] == 0x01 ? true : false)
                {
                    var cmd = ByteConverter.GetBytesFrom(MIFARELOGIN + key + keyType.ToString("X2") + sectorNumber.ToString("X2"));
                    Result = DoTXRX(cmd);
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
        /// Read Data from Classic Chip
        /// </summary>
        /// <param name="blockNumber">DataBlock Number</param>
        /// <returns>DATA</returns>
        public byte[] MifareClassicReadBlock(byte blockNumber)
        {
            Result = DoTXRX(ByteConverter.GetBytesFrom(MIFAREREADBLOCK + blockNumber.ToString("X2")));
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
            if(data.Length == 16)
            {
                Result = DoTXRX(ByteConverter.GetBytesFrom(MIFAREWRITEBLOCK + blockNumber.ToString("X2") + ByteConverter.GetStringFrom(data)));

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

        /// <summary>
        /// Select a desfire Application
        /// </summary>
        /// <param name="appID">The Application ID to select</param>
        /// <returns>true if Application could be selected, false otherwise</returns>
        public bool DesfireSelectApplication(uint appID)
        {
            try
            {
                Result = DoTXRX(new byte[] { 0x05, 0x00, 0x20 }); //GetChip
                Result = DoTXRX(ByteConverter.GetBytesFrom(MIFARE_DESFIRE_SELECTAPP + "00" + ByteConverter.GetStringFrom(BitConverter.GetBytes(appID))));

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
                LogWriter.CreateLogEntry(e, LogFacilityName);
                return false;
            }

        }

        /// <summary>
        /// Get the free Memory of a desfire. 
        /// </summary>
        /// <returns>a uint32 of the available memory if supported, null if freemem could not be read out</returns>
        public UInt32? GetDesfireFreeMemory()
        {
            try
            {
                Result = DoTXRX(ByteConverter.GetBytesFrom(MIFARE_DESFIRE_GETFREEMEMORY + "00"));

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
                LogWriter.CreateLogEntry(e, LogFacilityName);
                return null;
            }
        }

        /// <summary>
        /// Authenticate to a previously selected desfire application
        /// </summary>
        /// <param name="key">string: a 16 bytes key e.g. 00000000000000000000000000000000</param>
        /// <param name="keyNo">byte: the keyNo to use</param>
        /// <param name="keyType">byte: 0 = 3DES, 1 = 3K3DES, 2 = AES</param>
        /// <param name="authMode">byte: 1 = EV1 Mode, 0 = EV0 Mode</param>
        /// <returns>true if Authentication was successful, false otherwise</returns>
        public bool DesfireAuthenticate(string key, byte keyNo, byte keyType, byte authMode)
        {
            try
            {
                Result = DoTXRX(ByteConverter.GetBytesFrom(MIFARE_DESFIRE_AUTH + "00" //CryptoEnv
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
                LogWriter.CreateLogEntry(e, LogFacilityName);
                return false;
            }

        }

        /// <summary>
        /// Creates a new Application
        /// </summary>
        /// <param name="_keySettingsTarget">byte: KS_CHANGE_KEY_WITH_MK = 0, KS_ALLOW_CHANGE_MK = 1, KS_FREE_LISTING_WITHOUT_MK = 2, KS_FREE_CREATE_DELETE_WITHOUT_MK = 4, KS_CONFIGURATION_CHANGEABLE = 8, KS_DEFAULT = 11, KS_CHANGE_KEY_WITH_TARGETED_KEYNO = 224, KS_CHANGE_KEY_FROZEN = 240</param>
        /// <param name="_keyTypeTargetApplication">byte: 0 = 3DES, 1 = 3K3DES, 2 = AES</param>
        /// <param name="_maxNbKeys">int max. number of keys</param>
        /// <param name="_appID">int application id</param>
        /// <returns>true if the Operation was successful, false otherwise</returns>
        public bool DesfireCreateApplication(DESFireKeySettings _keySettingsTarget, DESFireKeyType _keyTypeTargetApplication, int _maxNbKeys, int _appID)
        {
            try
            {
                Result = DoTXRX(ByteConverter.GetBytesFrom(MIFARE_DESFIRE_CREATEAPP + "00" //CryptoEnv
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
                LogWriter.CreateLogEntry(e, LogFacilityName);
                return false;
            }
        }

        /// <summary>
        /// Retrieve the Available File IDs after selecing App and Authenticating
        /// </summary>
        /// <returns>byte[] array of available file ids. null on error</returns>
        public byte[] GetDesfireFileIDs()
        {
            try
            {
                Result = DoTXRX(ByteConverter.GetBytesFrom(MIFARE_DESFIRE_GETFILEIDS + "00" + "FF"));

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
                LogWriter.CreateLogEntry(e, LogFacilityName);
                return null;
            }
        }

        /// <summary>
        /// Retrieve the Available Application IDs after selecing PICC (App 0), Authentication is needed - depending on the security config
        /// </summary>
        /// <returns>a uint32[] of the available appids with 4bytes each, null if no apps are available or on error</returns>
        public UInt32[] GetDesfireAppIDs()
        {
            try
            {
                Result = DoTXRX(ByteConverter.GetBytesFrom(MIFARE_DESFIRE_GETAPPIDS + "00" + "1C"));

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
                LogWriter.CreateLogEntry(e, LogFacilityName);
                return null;
            }
        }

        /// <summary>
        /// Get the KeySettings (Properties: KeySettings, NumberOfKeys, KeyType) of the selected Application. Authentication is needed - depending on the security config
        /// </summary>
        /// <returns>true if the Operation was successful, false otherwise</returns>
        public bool GetDesFireKeySettings()
        {
            try
            {
                Result = DoTXRX(ByteConverter.GetBytesFrom(MIFARE_DESFIRE_GETKEYSETTINGS + "00"));

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
                LogWriter.CreateLogEntry(e, LogFacilityName);
                return false;
            }
        }

        /// <summary>
        /// Get the filesettings of a fileid
        /// </summary>
        /// <param name="fileNo">id of the desired file</param>
        /// <returns>byte[] array of the file settings. null on error. content: FileType = fileSettings[2]; comSett = fileSettings[3]; accessRights[0] = fileSettings[4]; accessRights[1] = fileSettings[5];</returns>
        public byte[] GetDesFireFileSettings(byte fileNo)
        {
            try
            {
                Result = DoTXRX(ByteConverter.GetBytesFrom(MIFARE_DESFIRE_GETFILESETTINGS + "00" + ByteConverter.GetStringFrom(fileNo)));

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
                LogWriter.CreateLogEntry(e, LogFacilityName);
                return null;
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
            GreenLED(false);
            Dispose(true);
        }

    }


}
