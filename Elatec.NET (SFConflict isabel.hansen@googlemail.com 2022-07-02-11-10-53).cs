﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.IO.Ports;
using Microsoft.Win32;
using System.Globalization;
using System.Threading;

using Elatec.NET.DataAccessLayer;

namespace Elatec.NET
{
    public class TWN4ReaderDevice
    {
        private byte[] Message;
        private SerialPort TWNPort;
        private bool Run = false;

        public TWN4ReaderDevice() : this(new SerialPort())
        {

        }

        public TWN4ReaderDevice(System.IO.Ports.SerialPort port)
        {
            this.TWNPort = port;
        }


        #region Common

        public ERROR ReadChipPublic()
        {
            try
            {
                /*if (readerUnit.ConnectToReader())
                {
                    if (readerUnit.WaitInsertion(Constants.MAX_WAIT_INSERTION))
                    {
                        if (readerUnit.Connect())
                        {
                            ReaderUnitName = readerUnit.ConnectedName;
                            //string readerSerialNumber = readerUnit.GetReaderSerialNumber(); //-> ERROR with OmniKey (and some others?) Reader when card isnt removed before recalling!

                            card = readerUnit.GetSingleChip();

                            if (!string.IsNullOrWhiteSpace(card.ChipIdentifier))
                            {
                                try
                                {
                                    CardInfo = new CARD_INFO((CARD_TYPE)Enum.Parse(typeof(CARD_TYPE), card.Type), card.ChipIdentifier);
                                    //readerUnit.Disconnect();
                                    return ERROR.NoError;
                                }
                                catch (Exception e)
                                {
                                    LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""));

                                    return ERROR.IOError;
                                }
                            }
                            else
                                return ERROR.DeviceNotReadyError;
                        }
                    }
                }*/
            }
            catch (Exception e)
            {
                /*if (readerProvider != null)
                    readerProvider.ReleaseInstance();
*/
                LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""));

                return ERROR.NoError;
            }

            return ERROR.IOError;
        }

        #endregion
        private void threadSearch()
        {
            bool IsInit = false;
            // Polling device loop
            while (Run)
            {
                try
                {
                    // Is there a TWN4?
                    string PortName = GetTWNPortName(0);
                    // Is TWNPort open?
                    if (!IsInit)
                    {
                        if (PortName == "")
                            // There is no TWN4 connected. Do a silent execption.
                            throw new ApplicationException("");

                        // Ensure, that COM port becomes available. Sleep a while.
                        System.Threading.Thread.Sleep(100);
                        // A TWN4 was connected. Initialize it.
                        ConnectTWN4(PortName);
                        // Do log ErrorMessage
                        LogWriter.CreateLogEntry("TWN4 connected");
                        IsInit = true;
                    }
                    // TWN4 is initialized
                    if (PortName == "")
                        throw new ApplicationException("TWN4 disconnected");

                    // TWN4 is ready for communication. Try to do some kind of NFC communication.                    
                    // DoNFCCommunication();
                }
                catch (Exception ex)
                {
                    // An error occured. Show it.
                    if (ex.Message != "")
                        LogWriter.CreateLogEntry("Error: " + ex.Message);
                    // Try to close TWNPort. Do not generate further exceptions
                    try
                    {
                        if (IsInit)
                            LogWriter.CreateLogEntry("Disconnecting from TWN4");
                        IsInit = false;
                        TWNPort.Close();
                    }
                    catch { }
                }
            }
            try
            {
                // Try to close TWNPort
                TWNPort.Close();
            }
            catch { }
        }// End of threadSearch

        #region Reader Communication

        #region Tools for Simple Protocol
        private byte[] GetByteArrayfromPRS(string PRSString)
        {
            // Is string length = 0?
            if (PRSString.Length < 1)
                // Yes, return null
                return null;
            // Initialize byte array, half string length
            byte[] buffer = new byte[PRSString.Length / 2];
            // Get byte array from PRS string
            for (int i = 0; i < (buffer.Length); i++)
                // Convert PRS Chars to byte array buffer
                buffer[i] = byte.Parse(PRSString.Substring((i * 2), 2), NumberStyles.HexNumber);
            // Return byte array
            return buffer;
        }// End of PRStoByteArray
        private string GetPRSfromByteArray(byte[] PRSStream)
        {
            // Is length of PRS stream = 0
            if (PRSStream.Length < 1)
                // Yes, return null
                return null;
            // Iinitialize PRS buffer
            string buffer = null;
            // Convert byte to PRS string
            for (int i = 0; i < PRSStream.Length; i++)
                // convert byte to characters
                buffer = buffer + PRSStream[i].ToString("X2");
            // return buffer
            return buffer;
        }// End of GetPRSfromByteArray
        #endregion

        private void ConnectTWN4(string PortName)
        {
            // Initialize serial port
            TWNPort.PortName = PortName;
            TWNPort.BaudRate = 9600;
            TWNPort.DataBits = 8;
            TWNPort.StopBits = System.IO.Ports.StopBits.One;
            TWNPort.Parity = System.IO.Ports.Parity.None;
            // NFC functions are known to take less than 2 second to execute.
            TWNPort.ReadTimeout = 2000;
            TWNPort.WriteTimeout = 2000;
            TWNPort.NewLine = "\r";
            // Open TWN4 com port
            TWNPort.Open();
        }// End of ConnectTWN4
        private byte[] DoTXRX(byte[] CMD)
        {
            // Discard com port inbuffer
            TWNPort.DiscardInBuffer();
            // Generate simple protocol string and send command
            TWNPort.WriteLine(GetPRSfromByteArray(CMD));
            // Read simple protocoll string and convert to byte array
            return GetByteArrayfromPRS(TWNPort.ReadLine());
        }// End of DoTXRX

        #region Tools for connect TWN4

        private string GetTWNPortName(int PortNr)
        {
            string PortName;
            if (PortNr == 0)
            {
                PortName = "";
            }
            else
            {
                    return "";
            }
            return PortName;
        }// End of GetTWNPortName
        #endregion

        #endregion

        #region Tool for byte arrays
        private byte[] AddByteArray(byte[] Source, byte[] Add)
        {
            // Is Source = null
            if (Source == null)
            {
                // Yes, copy Add in Source
                Source = Add;
                // Return source
                return Source;
            }
            // Initialize buffer array, with the length of Source and Add
            byte[] buffer = new byte[Source.Length + Add.Length];
            // Copy Source in buffer
            for (int i = 0; i < Source.Length; i++)
                // Copy source bytes to buffer
                buffer[i] = Source[i];
            // Add the secound array to buffer
            for (int i = Source.Length; i < buffer.Length; i++)
                // Copy Add bytes after the Source bytes in buffer
                buffer[i] = Add[i - Source.Length];
            // Return the combined array buffer
            return buffer;
        }// End of AddByteArray
        private byte[] AddByte2Array(byte[] Source, byte Add)
        {
            if (Source == null)
            {
                return new byte[] { Add };
            }
            // Initialize buffer with the length of Source + 1
            byte[] buffer = new byte[Source.Length + 1];
            // Copy Source in buffer
            for (int i = 0; i < Source.Length; i++)
                // Copy Source bytes in buffer array
                buffer[i] = Source[i];
            // Add byte behind the Source
            buffer[Source.Length] = Add;
            // Return the buffer
            return buffer;
        }// End of AddByte2Array
        private byte[] GetSegmentFromByteArray(byte[] Source, int index, int count)
        {
            // Initialize buffer with the segment size
            byte[] buffer = new byte[count];
            // Copy bytes from index until count
            for (int i = index; i < (index + count); i++)
                // Copy in segment buffer
                buffer[i - index] = Source[i];
            // Return segment buffer
            return buffer;
        }// End of GetSegmentFromByteArray
        private bool CompareArraysSegments(byte[] Array1, int index1, byte[] Array2, int index2, int count)
        {
            // Plausibility check, is index + count longer than arran
            if (((index1 + count) > Array1.Length) || ((index2 + count) > Array2.Length))
                // Yes, return false
                return false;
            // Compare segments of count
            for (int i = 0; i < count; i++)
                // Is byte in Array1 == byte in Array2?
                if (Array1[i + index1] != Array2[i + index2])
                    // No, return flase
                    return false;
            // Return true
            return true;
        }// End of CompareArraysSegment
        #endregion
    }


}