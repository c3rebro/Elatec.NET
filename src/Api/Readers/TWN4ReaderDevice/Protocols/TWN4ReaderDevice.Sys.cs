using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Elatec.NET.Helpers.ByteArrayHelper.Extensions;

namespace Elatec.NET
{
    public partial class TWN4ReaderDevice
    {
        #region API_SYS / System Functions

        public static readonly byte API_SYS = 0;

        // Not supported: SYSFUNC(API_SYS, 0, bool SysCall(TEnvSysCall* Env))

        /// <summary>
        /// This function is performing a reset of the firmware, which also includes a restart of the currently running App.
        /// </summary>
        /// <returns></returns>
        public async Task ResetAsync()
        {
            await CallFunctionAsync(new byte[] { API_SYS, 1 });
        }

        /// <summary>
        /// This function is performing a manual call of the boot loader. As a consequence the execution of the App is stopped.
        /// </summary>
        /// <returns></returns>
        public async Task StartBootloaderAsync()
        {
            await CallFunctionAsync(new byte[] { API_SYS, 2 });
        }

        /// <summary>
        /// Retrieve number of system ticks, specified in multiple of 1 milliseconds, since startup of the firmware.
        /// </summary>
        /// <returns></returns>
        public async Task<uint> GetSysTicksAsync()
        {
            var parser = await CallFunctionAsync(new byte[] { API_SYS, 3 });
            uint ticks = parser.ParseUInt32();
            return ticks;
        }

        /// <summary>
        /// Retrieve version information.
        /// </summary>
        /// <returns></returns>
        public async Task<string> GetVersionStringAsync()
        {
            var parser = await CallFunctionAsync(new byte[] { API_SYS, 4, /* maxLen */ byte.MaxValue });
            string version = parser.ParseAsciiString();
            var subVersion = version.Split('/');
            IsTWN4LegicReader = subVersion.Length >= 3 && subVersion[2].Contains("B");
            return version;
        }

        /// <summary>
        ///     Retrieve type of USB communication. This could by keyboard emulation or CDC emulation or some other
        ///     value for future or custom implementations.
        /// </summary>
        /// <returns></returns>
        public async Task<UsbType> GetUsbTypeAsync()
        {
            var parser = await CallFunctionAsync(new byte[] { API_SYS, 5 });
            var type = (UsbType)parser.ParseByte();
            return type;
        }

        /// <summary>
        /// Retrieve type of underlying TWN4 hardware.
        /// </summary>
        /// <returns></returns>
        public async Task<DeviceType> GetDeviceTypeAsync()
        {
            var parser = await CallFunctionAsync(new byte[] { API_SYS, 6 });
            var type = (DeviceType)parser.ParseByte();
            return type;
        }

        /// <summary>
        ///     The device enters the sleep state for a specified time. During sleep state, the device reduces the current
        ///     consumption to a value, which depends on the mode of sleep.
        /// </summary>
        /// <param name="ticks">Time, specified in milliseconds, the device should enter the sleep state.</param>
        /// <param name="flags">See TWN4 API Reference.</param>
        /// <returns>See TWN4 API Reference.</returns>
        public async Task<byte> SleepAsync(uint ticks, uint flags)
        {
            List<byte> bytes = new List<byte> { API_SYS, 7 };
            bytes.AddUInt32(ticks);
            bytes.AddUInt32(flags);
            var parser = await CallFunctionAsync(bytes.ToArray());
            var result = parser.ParseByte();
            return result;
        }

        /// <summary>
        /// This function returns a UID, which is unique to the specific TWN4 device.
        /// </summary>
        /// <returns></returns>
        public async Task<byte[]> GetDeviceUidAsync()
        {
            var parser = await CallFunctionAsync(new byte[] { API_SYS, 8 });
            byte[] result = parser.ParseFixByteArray(12);
            return result;
        }

        /// <summary>
        ///     This function allows to set parameters, which influence the behaviour of the TWN4 firmware. See also
        ///     chapter System Parameters of TWN4 API Reference for a description of the TLV list and all available parameters.
        /// </summary>
        /// <param name="TLV">The raw bytes of the TLV list. Do not include TLV_END, as it is appended automatically!</param>
        /// <returns>The function returns true, if the parameters were set to the new value. Otherwise
        ///     the function returns false.</returns>
        /// <remarks>SYSFUNC(API_SYS, 9, bool SetParameters(const byte* TLV,int ByteCount))</remarks>
        public async Task<bool> SetParametersAsync(byte[] TLV)
        {
            List<byte> bytes = new List<byte> { API_SYS, 9 };
            bytes.Add((byte)(TLV.Length + 1));
            bytes.AddRange(TLV);
            bytes.Add(0); // TLV_END
            var parser = await CallFunctionAsync(bytes.ToArray());
            var result = parser.ParseBool();
            return result;
        }

        /// <summary>
        /// This function is used to retrieve internal system errors of the reader. Do not deduce protocol or communication errors from this function call.
        /// </summary>
        /// <returns></returns>
        /// <remarks>SYSFUNC(API_SYS,10, unsigned int GetLastError(void))</remarks>
        public async Task<ReaderError> GetLastErrorAsync()
        {
            var parser = await CallFunctionAsync(new byte[] { API_SYS, 10 });
            var result = (ReaderError)parser.ParseUInt32();
            return result;
        }

        // Not supported: SYSFUNC(API_SYS,11, int Diagnostic(int Mode,const void* In,int InLen,void* Out,int* OutLen,int MaxOutLen))

        /// <summary>
        /// Get the product serial number of the TWN device.
        /// </summary>
        /// <returns></returns>
        public async Task<string> GetProdSerNoAsync()
        {
            var parser = await CallFunctionAsync(new byte[] { API_SYS, 13, /* maxBytes */ byte.MaxValue });
            string result = parser.ParseAsciiString();
            return result;
        }

        // Not supported: SYSFUNC(API_SYS,14, bool SetInterruptHandler(TInterruptHandler InterruptHandler, int IntNo))

        /// <summary>
        /// Retrieve version information.
        /// </summary>
        /// <returns></returns>
        /// <remarks>SYSFUNC(API_SYS,15, void GetVersionInfo(TVersionInfo* VersionInfo)).<br/>
        ///     This internal method is not documented in TWN4 API reference.
        /// </remarks>
        public async Task<VersionInfo> GetVersionInfoAsync()
        {
            var parser = await CallFunctionAsync(new byte[] { API_SYS, 15 });
            var info = new VersionInfo();
            info.Compatibility = parser.ParseUInt16();
            info.BootBranch = parser.ParseUInt16();
            var minor = parser.ParseByte();
            var major = parser.ParseByte();
            info.BootVersion = new Version(major, minor);
            info.FirmwareKeyType = parser.ParseUInt16();
            info.BranchNum = parser.ParseByte();
            info.BranchChar = (char)parser.ParseByte();
            minor = parser.ParseByte();
            major = parser.ParseByte();
            info.FirmwareVersion = new Version(major, minor);
            info.AppChars = parser.ParseFixByteArray(4);
            minor = parser.ParseByte();
            major = parser.ParseByte();
            info.AppVersion = new Version(major, minor);

            return info;
        }

        public class VersionInfo
        {
            public int Compatibility { get; set; }
            public int BootBranch { get; set; }
            public Version BootVersion { get; set; }
            public int FirmwareKeyType { get; set; }
            public byte BranchNum { get; set; }
            /// <summary>
            /// 'K' = Keyboard, 'C' = CDC
            /// </summary>
            public char BranchChar { get; set; }
            public Version FirmwareVersion { get; set; }
            /// <summary>
            /// e.g. "STD", "STDC", "PRS" = Simple Protocol
            /// </summary>
            public byte[] AppChars { get; set; }
            public Version AppVersion { get; set; }
        }

        // Not supported: SYSFUNC(API_SYS,16, bool ReadInfoValue(int Index, int FilterType, int* Type, int* Length, byte* Value, int MaxLength))
        // Not supported: SYSFUNC(API_SYS,17, bool WriteInfoValue(int Type, int Length,const byte* Value))
        // Not supported: SYSFUNC(API_SYS,18, bool GetCustomKeyID(byte* CustomKeyID, int* Length, int MaxLength))
        // Not supported: SYSFUNC(API_SYS,19, bool GetParameters(const byte* Types,int TypeCount,byte* TLVBytes,int* TLVByteCount,int TLVMaxByteCount))

        #endregion


    }
}