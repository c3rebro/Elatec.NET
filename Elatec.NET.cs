using Microsoft.Win32;

using System;
using System.IO.Ports;
using System.Globalization;
using System.Threading;

using Elatec.NET.Model;

using Log4CSharp;

using ByteArrayHelper.Extensions;
/*
 * Elatec.NET is a C# library to easily Talk to Elatec's TWN4 Devices
 * 
 * 
 * 
 * 
 * 
 */

namespace Elatec.NET
{
    public class TWN4ReaderDevice : IDisposable
    {
        private const string BEEP_CMD = "0407";
        private const string LEDINIT_CMD = "0410";
        private const string LEDON_CMD = "0411";
        private const string LEDOFF_CMD = "0412";

        private string LogFacilityName = "RFiDGear";

        private int portNumber;

        private bool _disposed;

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

        private static readonly object syncRoot = new object();
        private static TWN4ReaderDevice instance;

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
            for (int i = 0; i < iterations; i++)
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
                    ByteConverter.GetStringFrom(0x01))
                    );
            Result = DoTXRX(
                    ByteConverter.GetBytesFrom(LEDON_CMD +
                    ByteConverter.GetStringFrom(0x01))
                    );
        }

        public ChipModel GetSingleChip()
        {
            try
            {
                genericChipModel = new ChipModel();

                Result = DoTXRX(new byte[] { 0x05, 0x02, 0x00, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF }); //SetChipTypes (HF onyl)
                Result = DoTXRX(new byte[] { 0x05, 0x00, 0x20}); //GetChip
                
                genericChipModel.ChipIdentifier = ByteConverter.GetStringFrom(Result, 5);

                if (Result?.Length >= 3)
                {
                    genericChipModel.CardType = (ChipType)Result[2];
                }

                switch (genericChipModel.CardType)
                {
                    case ChipType.MIFARE: //Start Mifare Identification Process

                        Result = DoTXRX(new byte[] { 0x12, 0x05 }); //GetSAK

                        if (Result?.Length == 3)
                        {
                            // Start MIFARE identification
                            if ((Result[2] & 0x02) == 0x02)
                            {
                                genericChipModel.CardType = ChipType.Unspecified;
                            } // RFU bit set (RFU = Reserved for Future Use)

                            else
                            {
                                if ((Result[2] & 0x08) == 0x08)
                                {
                                    if ((Result[2] & 0x10) == 0x10)
                                    {
                                        if ((Result[2] & 0x01) == 0x01)
                                        {
                                            genericChipModel.CardType = ChipType.Mifare2K;
                                        } // // SAK b1 = 1 ? >> Mifare Classic 2K
                                        else
                                        {
                                            if ((Result[2] & 0x20) == 0x20)
                                            {
                                                genericChipModel.CardType = ChipType.SmartMX_Mifare_4K;
                                            } // SAK b6 = 1 ?  >> SmartMX Classic 4K
                                            else
                                            {
                                                var ATS = DoTXRX(new byte[] { 0x12, 0x07, 0x04, 0xE0, 0x10, 0xB8, 0xE7, 0xFF, 0xFF, 0x00 }); // Get ATS

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
                                        if ((Result[2] & 0x01) == 0x01)
                                        {
                                            genericChipModel.CardType = ChipType.MifareMini;
                                        } // // SAK b1 = 1 ? >> Mifare Mini
                                        else
                                        {
                                            if ((Result[2] & 0x20) == 0x20)
                                            {
                                                genericChipModel.CardType = ChipType.SmartMX_Mifare_1K;
                                            } // // SAK b6 = 1 ? >> SmartMX Classic 1K
                                            else
                                            {
                                                var ATS = DoTXRX(new byte[] { 0x12, 0x07, 0x04, 0xE0, 0x10, 0xB8, 0xE7, 0xFF, 0xFF, 0x00 }); // Get ATS

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
                                    if ((Result[2] & 0x10) == 0x10)
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
                                        if ((Result[2] & 0x01) == 0x01) // SAK b1 = 1 ?
                                        {

                                        } // Chip is "TagNPlay"
                                        else
                                        {
                                            if ((Result[2] & 0x20) == 0x20)
                                            {
                                                //ISO 14443-4
                                                var ATS = DoTXRX(new byte[] { 0x12,0x07,  0x04,  0xE0,0x10,0xB8,0xE7,  0xFF,  0xFF,0x00 }); // Get ATS
                                                Result = DoTXRX(new byte[] { 0x05, 0x00, 0x20 }); //GetChip
                                                var SAK = DoTXRX(new byte[] { 0x12, 0x05 }); //GetSAK
                                                var getVersion = DoTXRX(new byte[] { 0x12, 0x03, 0x01, 0x60, 0x20 }); //issue GetVersion

                                                if (ATS.Length == 0x0b && getVersion?.Length > 4 && getVersion?[3] == 0xAF)
                                                {
                                                    // Mifare Plus EV1/2 || DesFire || NTAG
                                                    if (getVersion?.Length > 1 && (getVersion?[5] == 0x01 || getVersion?[5] == 0x81)) // DESFIRE
                                                    {
                                                        switch (getVersion?[7] & 0x0F) // Desfire(Sub)Type by lower Nibble of Major Version
                                                        {
                                                            case 0:
                                                                genericChipModel.CardType = ChipType.DESFire;
                                                                break;
                                                            case 1:
                                                                genericChipModel.CardType = ChipType.DESFireEV1;
                                                                break;
                                                            case 2:
                                                                genericChipModel.CardType = ChipType.DESFireEV2;
                                                                break;
                                                            case 3:
                                                                genericChipModel.CardType = ChipType.DESFireEV3;
                                                                break;
                                                            default:
                                                                break;
                                                        }

                                                        switch (getVersion?[9]) // Size
                                                        {
                                                            case 0x10:
                                                                // DESFIRE 256B
                                                                break;
                                                            case 0x16:
                                                                // DESFIRE 2K
                                                                break;
                                                            case 0x18:
                                                                // 4K
                                                                break;
                                                            case 0x1A:
                                                                // 8K
                                                                break;
                                                            case 0x1C:
                                                                // 16K
                                                                break;
                                                            case 0x1E:
                                                                // 32K
                                                                break;
                                                            default:
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

            return genericChipModel;
        }
        private ChipModel genericChipModel;

        #endregion

        #region Reader Communication

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
            using(SerialPort twnPort = new SerialPort())
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

                // Discard com port inbuffer
                twnPort.DiscardInBuffer();
                // Generate simple protocol string and send command
                twnPort.WriteLine(GetPRSfromByteArray(CMD));
                // Read simple protocoll string and convert to byte array
                return GetByteArrayfromPRS(twnPort.ReadLine());
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
        public byte[] Result { get; set; }
        #endregion

        #region DesFireCommands

        public int GetDesFireVersion()
        {
            return 1;
        }

        #endregion

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    //DisconnectTWN4();
                    //instance = null;

                    // Dispose any managed objects
                    // ...
                }

                Thread.Sleep(200);
                _disposed = true;
            }
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            Dispose(true);
        }

    }


}
