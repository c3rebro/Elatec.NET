using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.IO.Ports;
using Microsoft.Win32;
using System.Globalization;
using System.Threading;

using Elatec.NET;
using Elatec.NET.Model;

using Log4CSharp;
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
        private string LogFacilityName = "RFiDGear";

        private readonly byte[] Message;
        private readonly SerialPort TWNPort;
        private readonly bool Run = false;
        private readonly int portNumber;

        private bool _disposed = false;

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
                        return null;
                    }
                }
            }
        }

        private static readonly object syncRoot = new object();
        private static TWN4ReaderDevice instance;

        public TWN4ReaderDevice() : this(new SerialPort())
        {
            Run = true;
        }

        public TWN4ReaderDevice(System.IO.Ports.SerialPort port)
        {
            TWNPort = port;
            Run = true;
        }

        public TWN4ReaderDevice(int port)
        {
            TWNPort = new SerialPort(GetTWNPortName(port));
            Run = true;
        }

        public bool Connect()
        {
            ConnectTWN4(TWNPort.PortName);

            return true;
        }

        public bool Disconnect()
        {
            DisconnectTWN4();

            return true;
        }

        #region Common

        public void Beep()
        {
            var Result = DoTXRX(new byte[] { 0x04, 0x07, 0x64, 0x60, 0x09, 0x54, 0x01, 0xF4, 0x01 });
        }

        public void GreenLED(bool On)
        {
            var Result = DoTXRX(new byte[] { 0x04, 0x10, 0x00, 0x07 });
        }

        public ChipModel GetSingleChip()
        {
            try
            {
                var Result = DoTXRX(new byte[] { 0x04, 0x07, 0x64, 0x60, 0x09, 0x54, 0x01, 0xF4, 0x01 });
                Result = DoTXRX(new byte[] { 0x05, 0x02, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF });
                Result = DoTXRX(new byte[] { 0x05, 0x00, 0x10 });
                Result = DoTXRX(new byte[] { 0x0F, 0x12, 0x00 });
            }
            catch (Exception e)
            {
                LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""), LogFacilityName);
            }

            return genericChipModel;
        }
        private ChipModel genericChipModel;

        public Result ReadChipPublic()
        {
            try
            {
                var Result = DoTXRX(new byte[] { 0x04, 0x07, 0x64, 0x60, 0x09, 0x54, 0x01, 0xF4, 0x01 });
                Result = DoTXRX(new byte[] { 0x05, 0x02, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF });
                Result = DoTXRX(new byte[] { 0x05, 0x00, 0x10 });
                Result = DoTXRX(new byte[] { 0x0F, 0x12, 0x00 });
            }
            catch (Exception e)
            {
                /*if (readerProvider != null)
                    readerProvider.release();
*/
                LogWriter.CreateLogEntry(string.Format("{0}: {1}; {2}", DateTime.Now, e.Message, e.InnerException != null ? e.InnerException.Message : ""), LogFacilityName);

                return Result.NoError;
            }

            return Result.IOError;
        }

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

        private void DisconnectTWN4()
        {
            // Open TWN4 com port
            TWNPort.Close();
        }// End of DisconnectTWN4

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
                return string.Format("COM{0}", PortNr);
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
            {
                // Copy source bytes to buffer
                buffer[i] = Source[i];
            }
            // Add the secound array to buffer
            for (int i = Source.Length; i < buffer.Length; i++)
            {
                // Copy Add bytes after the Source bytes in buffer
                buffer[i] = Add[i - Source.Length];
            }
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
            {
                // Copy Source bytes in buffer array
                buffer[i] = Source[i];
            }
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
            {
                // Copy in segment buffer
                buffer[i - index] = Source[i];
            }
            // Return segment buffer
            return buffer;
        }// End of GetSegmentFromByteArray
        private bool CompareArraysSegments(byte[] Array1, int index1, byte[] Array2, int index2, int count)
        {
            // Plausibility check, is index + count longer than arran
            if (((index1 + count) > Array1.Length) || ((index2 + count) > Array2.Length))
            {
                // Yes, return false
                return false;
            }
            // Compare segments of count
            for (int i = 0; i < count; i++)
            {
                // Is byte in Array1 == byte in Array2?
                if (Array1[i + index1] != Array2[i + index2])
                {
                    // No, return flase
                    return false;
                }
            }
            // Return true
            return true;
        }// End of CompareArraysSegment
        #endregion

        #region Public Properties

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
                    this.DisconnectTWN4();
                    instance = null;
                    // Dispose any managed objects
                    // ...
                }

                if (this != null)
                {
                    //readerUnit.Disconnect();
                    //readerUnit.DisconnectFromReader();
                }

                // Now disposed of any unmanaged objects
                // ...

                Thread.Sleep(200);
                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

    }


}
