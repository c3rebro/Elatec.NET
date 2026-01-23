using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using Elatec.NET.Interfaces;
using Elatec.NET.Helpers.ByteArrayHelper.Extensions;
using System.IO;
using Elatec.NET.Cards;
using Elatec.NET.Cards.Mifare;
using Elatec.NET.System;

/*
* Elatec.NET is a C# library to easily Talk to Elatec's TWN4 Devices
* 
* Some TWN4 Specific "Special" information:
* 
* Getting the ATS on different Readers works differently.
* 
*/

namespace Elatec.NET
{
    /// <summary>
    ///     This class offers communications methods with a TWN4 Reader device, e.g. TWN4 MultiTech 2.
    ///     The methods are on different abstraction levels:<br/>
    ///     <list type="number">
    ///     <item>Low level TWN4 Simple Protocol APIs<br/>
    ///     - <see cref="CallFunctionAsync(byte[])"/> which takes a raw byte[] as input and returns a parser. Errors are thrown as TwnException.<br />
    ///     - See <see cref="CallFunctionParserAsync(byte[])"/> and <see cref="CallFunctionRawAsync(byte[])"/> for variants without error handling and parser.</item>
    ///     <item>TWN4 Simple Protocol APIs<br/>
    ///     - e.g. <see cref="GpioSetBitsAsync(Gpios)"/></item>
    ///     <item>High level<br/>
    ///     - e.g. <see cref="GetSingleChipAsync(bool)"/> providing detailed information</item>
    ///     </list>
    /// </summary>
    public partial class TWN4ReaderDevice : IDisposable
    {
        private bool _disposed;

        private static readonly object syncRoot = new object();
        private static List<TWN4ReaderDevice> instance;
        // Serialize access to the underlying transport to prevent interleaved async commands.
        private readonly SemaphoreSlim _transportLock = new SemaphoreSlim(1, 1);
        private readonly Func<string, IReaderTransport> _transportFactory;
        private IReaderTransport _transport;

        public static List<TWN4ReaderDevice> Instance
        {
            get
            {
                lock (TWN4ReaderDevice.syncRoot)
                {
                    instance = DeviceManager.GetAvailableReaders();

                    if (instance == null)
                    {
                        return new List<TWN4ReaderDevice>();
                    }

                    return instance;
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public TWN4ReaderDevice(string portName, Func<string, IReaderTransport> transportFactory = null)
        {
            PortName = portName;
            _transportFactory = transportFactory ?? new Func<string, IReaderTransport>(name => new SerialPortTransport(name));
        }

        /// <summary>
        /// 
        /// </summary>
        public static readonly int TIMEOUT = 20;

        private void EnsureTransport()
        {
            if (_transport == null)
            {
                _transport = _transportFactory(PortName) ?? throw new InvalidOperationException("Transport factory returned null.");
                _transport.ErrorReceived += TXRXErr;
            }
        }

        #region High Level APIs

        /// <summary>
        /// Play a melody on the device. 
        /// </summary>
        /// <param name="tempo">Specify the BPM.</param>
        /// <param name="song">Specify the song as List of Tones <see cref="Tone"/>.<</param>
        /// <returns></returns>
        public async Task PlayMelody(int tempo, List<Tone> song)
        {
            foreach (Tone tone in song)
            {
                ushort onTime, offTime;
                ushort duration = (ushort)(1000 * 1 / tempo * 60 / (16 / tone.Value));

                if (tone.Pitch > 0)
                {
                    onTime = duration;
                    offTime = 0;
                }
                else // Play a PAUSE
                {
                    onTime = 0;
                    offTime = duration;
                }

                if (tone.IsStaccato)
                {
                    onTime = (ushort)(duration / 2);
                    offTime = (ushort)(duration / 2);
                }

                await BeepAsync(tone.Volume, (ushort)tone.Pitch, onTime, offTime);
            }
        }

        /// <summary>
        /// Activate the reader buzzer for a specified duration.
        /// </summary>
        /// <param name="volume">Volume in percent (0-100).</param>
        /// <param name="frequency">Tone frequency in hertz.</param>
        /// <param name="onTime">Duration in milliseconds the buzzer should be on.</param>
        /// <param name="offTime">Duration in milliseconds the buzzer should remain off afterwards.</param>
        /// <remarks>
        ///     The firmware exposes the beeper via the periphery API. This helper packages the parameters and delegates to
        ///     the low-level <see cref="CallFunctionAsync(byte[])"/> call.
        /// </remarks>
        public async Task BeepAsync(byte volume, ushort frequency, ushort onTime, ushort offTime)
        {
            var payload = new List<byte> { TWN4ReaderDevice.API_PERIPH, 12, volume };
            payload.AddUInt16(frequency);
            payload.AddUInt16(onTime);
            payload.AddUInt16(offTime);
            await CallFunctionAsync(payload.ToArray());
        }

        /// <summary>
        /// Each tone contains a volume, a Pitch <see cref="NotePitch"/> and a velue. Contains the optional IsStaccato Property which Produces a Tone with a Pulsewidth of 50%.  
        /// Value is calculated as 1 / tempo * 60 (length of a cadence) * 1 / (ToneValue / 16). Defaults to Volume = 60, and Value = 16
        /// Where Value is the Tone Length with a Base 16. So 16 is a whole note, 8 is a half note, 4 is a quarter note, 2 a quaver and 1 a semiquaver.
        /// The dotted Notes are then 12 for dotted half, 6 for dotted quarter and 3 for a dotted quaver
        /// </summary>
        public class Tone
        {
            public Tone()
            {
                Volume = 60;
                Value = 16;
            }
            public int Value    {       get; set;   }
            public byte Volume  {       get; set;   }
            public NotePitch Pitch  {   get; set;   }
            public bool IsStaccato  {   get; set;   }
        }

        #endregion

        #region Common

        /// <summary>
        /// Get a single chip which is currently in the reading range of the device.
        /// If multiple chips are present, only the first one is returned.
        /// Use <see cref="ISO14443A_SearchMultiTagAsync"/> if you need to work with multiple chips.
        /// </summary>
        /// <returns>Depending on the ChipType a BaseChip or specialized class is returned, e.g. MifareChip.</returns>
        public async Task<BaseChip> GetSingleChipAsync()
        {
            var tag = await SearchTagAsync();
            if (tag != null)
            {
                var chip = await GetTypedChipInstance(tag.ChipType, tag.IDBytes);
                return chip;
            }

            return null;
        }

        private async Task<BaseChip> GetTypedChipInstance(ChipType chipType, byte[] uid)
        {
            BaseChip chip;
            switch (chipType)
            {
                case ChipType.MIFARE:
                    var mifareChip = new MifareChip(chipType, uid);
                    await DetectMifareSubType(mifareChip);
                    chip = mifareChip;
                    break;
                case ChipType.LEGIC:
                    chip = new BaseChip(chipType, uid);
                    break;
                default:
                    chip = new BaseChip(chipType, uid);
                    break;
            }
            
            return chip;
        }

        /// <summary>
        /// Identify the MIFARE subtype using the ATQA/SAK/ATS negotiation described in NXP AN10833.
        /// </summary>
        /// <param name="currentChip">Chip metadata that will be enriched with the discovered subtype.</param>
        /// <remarks>
        ///     On Legic-capable TWN4 readers the tag selection portion of this flow must be skipped, because the Legic
        ///     co-processor already completed RATS/ATS during <see cref="SearchTagAsync"/>. Attempting to manually select the
        ///     tag on those readers results in protocol errors, but the ATS/SAK values returned here still reflect the cached
        ///     Legic-managed exchange.
        /// </remarks>
        private async Task DetectMifareSubType(MifareChip currentChip)
        {
            //Start Mifare Identification Process as of NXP AN10833

            /*
             * IMPORTANT: Selecting a Tag is unsupported when using a TWN4 Multitec HF LF 2 Legic capable Reader.
             * The elatec support said, this is not documented. Use SearchTag instead.
             * 
             * Original Version:
            // If multiple tags were detected, select one.
            bool success = await ISO14443A_SelectTagAsync(currentChip.UID);
            if (!success)
            {
                var errorNumber = await GetLastErrorAsync();
                // returned error numbers are not properly documented, like 0x10000001. But the code execution usually continues successfully with GetAtqa.
                return;
            }
            */

            var atqa = await ISO14443A_GetAtqaAsync();
            if (atqa.HasValue)
            {
                currentChip.ATQA = atqa.Value;
            }
            
            var sakResult = await ISO14443A_GetSakAsync();
            if (sakResult.HasValue)
            {
                currentChip.SAK = sakResult.Value;
            }

            var atsResult = await ISO14443A_GetAtsAsync();
            if (atsResult != null)
            {
                currentChip.ATS = atsResult;
            }

            // Start MIFARE identification
            if ((currentChip.SAK & 0x02) == 0x02)
            {
                currentChip.SubType = MifareChipSubType.Unspecified;
            } // RFU bit set (RFU = Reserved for Future Use)

            else
            {
                if ((currentChip.SAK & 0x08) == 0x08)
                {
                    if ((currentChip.SAK & 0x10) == 0x10)
                    {
                        if ((currentChip.SAK & 0x01) == 0x01)
                        {
                            currentChip.SubType = MifareChipSubType.Mifare2K;
                        } // // SAK b1 = 1 ? >> Mifare Classic 2K
                        else
                        {
                            if ((currentChip.SAK & 0x20) == 0x20)
                            {
                                currentChip.SubType = MifareChipSubType.SmartMX_Mifare_4K;
                            } // SAK b6 = 1 ?  >> SmartMX Classic 4K
                            else
                            {
                                //Get ATS - Switch to L4 ?

                                if (currentChip.ATS != null)
                                {
                                    if (ByteArrayConverter.SearchBytePattern(currentChip.ATS, new byte[] { 0xC1, 0x05, 0x2F, 0x2F, 0x00, 0x35, 0xC7 }) != 0) //MF PlusS 4K in SL1
                                    {
                                        currentChip.SubType = MifareChipSubType.MifarePlus_SL1_4K;
                                    }

                                    else if (ByteArrayConverter.SearchBytePattern(currentChip.ATS, new byte[] { 0xC1, 0x05, 0x2F, 0x2F, 0x01, 0xBC, 0xD6 }) != 0) //MF PlusX 4K in SL1
                                    {
                                        currentChip.SubType = MifareChipSubType.MifarePlus_SL1_4K;
                                    }

                                } // Mifare Plus S / Plus X 4K

                                else
                                {
                                    currentChip.SubType = MifareChipSubType.Mifare4K;
                                } //Error on ATS = Mifare Classic 4K
                            }
                        }
                    } // SAK b5 = 1 ?
                    else
                    {
                        if ((currentChip.SAK & 0x01) == 0x01)
                        {
                            currentChip.SubType = MifareChipSubType.MifareMini;
                        } // // SAK b1 = 1 ? >> Mifare Mini
                        else
                        {
                            if ((currentChip.SAK & 0x20) == 0x20)
                            {
                                currentChip.SubType = MifareChipSubType.SmartMX_Mifare_1K;
                            } // // SAK b6 = 1 ? >> SmartMX Classic 1K
                            else
                            {
                                if (currentChip.ATS != null)
                                {
                                    if (ByteArrayConverter.SearchBytePattern(currentChip.ATS, new byte[] { 0xC1, 0x05, 0x2F, 0x2F, 0x00, 0x35, 0xC7 }) != 0) //MF PlusS 4K in SL1
                                    {
                                        currentChip.SubType = MifareChipSubType.MifarePlus_SL1_2K;
                                    }

                                    else if (ByteArrayConverter.SearchBytePattern(currentChip.ATS, new byte[] { 0xC1, 0x05, 0x2F, 0x2F, 0x01, 0xBC, 0xD6 }) != 0) //MF PlusX 4K in SL1
                                    {
                                        currentChip.SubType = MifareChipSubType.MifarePlus_SL1_2K;
                                    }

                                    else if (ByteArrayConverter.SearchBytePattern(currentChip.ATS, new byte[] { 0xC1, 0x05, 0x21, 0x30, 0x00, 0xF6, 0xD1 }) != 0) //MF PlusSE 1K
                                    {
                                        currentChip.SubType = MifareChipSubType.MifarePlus_SL0_1K;
                                    }

                                    else
                                    {
                                        currentChip.SubType = MifareChipSubType.MifarePlus_SL1_1K;
                                    }
                                } // Mifare Plus S / Plus X 4K

                                else
                                {
                                    currentChip.SubType = MifareChipSubType.Mifare1K;
                                } //Error on ATS = Mifare Classic 1K
                            } // Mifare Plus; Historical Bytes ?
                        }
                    }
                } // SAK b4 = 1 ?
                else
                {
                    if ((currentChip.SAK & 0x10) == 0x10)
                    {
                        if ((currentChip.SAK & 0x01) == 0x01) // 
                        {
                            currentChip.SubType = MifareChipSubType.MifarePlus_SL2_4K;
                        } // Mifare Plus 4K in SL2
                        else
                        {
                            currentChip.SubType = MifareChipSubType.MifarePlus_SL2_2K;
                        } // Mifare Plus 2K in SL2
                    }
                    else
                    {
                        if ((currentChip.SAK & 0x01) == 0x01) // SAK b1 = 1 ?
                        {

                        } // Chip is "TagNPlay"
                        else
                        {
                            if ((currentChip.SAK & 0x20) == 0x20)
                            {
                                //Get Version in L4 ?
                                byte[] version = null;
                                try
                                {
                                    //for some reason MifareDesfire_GetVersionAsync does not work
                                    version = await ISO14443_4_TdxAsync(new byte[] { 0x60 });
                                    currentChip.VersionL4 = version;
                                }
                                //TODO: Check reader behaviour
                                catch { }

                                if (version != null && version?[0] == 0xAF)
                                {
                                    // Mifare Plus EV1/2 || DesFire || NTAG
                                    if (version?[2] == 0x01) // DESFIRE
                                    {
                                        switch (version?[4] & 0x0F) // Desfire(Sub)Type by lower Nibble of Major Version
                                        {
                                            case 0: //DESFIRE EV0
                                                switch (version?[6])
                                                {
                                                    case 0x10:
                                                        currentChip.SubType = MifareChipSubType.DESFireEV0_256; // DESFIRE 256B
                                                        break;
                                                    case 0x16:
                                                        currentChip.SubType = MifareChipSubType.DESFireEV0_1K; // DESFIRE 2K
                                                        break;
                                                    case 0x18:
                                                        currentChip.SubType = MifareChipSubType.DESFireEV0_4K; // 4K
                                                        break;
                                                    default:
                                                        currentChip.SubType = MifareChipSubType.Unspecified;
                                                        break;
                                                } // Size ?
                                                break;

                                            case 1: // DESFIRE EV1
                                                switch (version?[6])
                                                {
                                                    case 0x10:
                                                        currentChip.SubType = MifareChipSubType.DESFireEV1_256; //DESFIRE 256B
                                                        break;
                                                    case 0x16:
                                                        currentChip.SubType = MifareChipSubType.DESFireEV1_2K; // DESFIRE 2K
                                                        break;
                                                    case 0x18:
                                                        currentChip.SubType = MifareChipSubType.DESFireEV1_4K; // 4K
                                                        break;
                                                    case 0x1A:
                                                        currentChip.SubType = MifareChipSubType.DESFireEV1_8K; // 8K
                                                        break;
                                                    default:
                                                        currentChip.SubType = MifareChipSubType.Unspecified;
                                                        break;
                                                } // Size ?
                                                break;

                                            case 2: // EV2
                                                switch (version?[6])
                                                {
                                                    case 0x16:
                                                        currentChip.SubType = MifareChipSubType.DESFireEV2_2K; // DESFIRE 2K
                                                        break;
                                                    case 0x18:
                                                        currentChip.SubType = MifareChipSubType.DESFireEV2_4K; // 4K
                                                        break;
                                                    case 0x1A:
                                                        currentChip.SubType = MifareChipSubType.DESFireEV2_8K; // 8K
                                                        break;
                                                    case 0x1C:
                                                        currentChip.SubType = MifareChipSubType.DESFireEV2_16K; // 16K
                                                        break;
                                                    case 0x1E:
                                                        currentChip.SubType = MifareChipSubType.DESFireEV2_32K; // 32K
                                                        break;
                                                    default:
                                                        currentChip.SubType = MifareChipSubType.Unspecified;
                                                        break;
                                                } // SIZE ?
                                                break;

                                            case 3: // EV3
                                                switch (version?[6])
                                                {
                                                    case 0x16:
                                                        currentChip.SubType = MifareChipSubType.DESFireEV3_2K; // DESFIRE 2K
                                                        break;
                                                    case 0x18:
                                                        currentChip.SubType = MifareChipSubType.DESFireEV3_4K; // 4K
                                                        break;
                                                    case 0x1A:
                                                        currentChip.SubType = MifareChipSubType.DESFireEV3_8K; // 8K
                                                        break;
                                                    case 0x1C:
                                                        currentChip.SubType = MifareChipSubType.DESFireEV3_16K; // 16K
                                                        break;
                                                    case 0x1E:
                                                        currentChip.SubType = MifareChipSubType.DESFireEV3_32K; // 32K
                                                        break;
                                                    default:
                                                        currentChip.SubType = MifareChipSubType.Unspecified;
                                                        break;
                                                } // SIZE ?
                                                break;

                                            default:
                                                currentChip.SubType = MifareChipSubType.Unspecified;

                                                break;
                                        }
                                    }
                                    if (version?[2] == 0x81) // Emulated e.g. SmartMX
                                    {
                                        switch (version?[4] & 0x0F) // Desfire(Sub)Type by lower Nibble of Major Version
                                        {
                                            case 0: //DESFIRE EV0
                                                switch (version?[6])
                                                {
                                                    case 0x10:
                                                        currentChip.SubType = MifareChipSubType.SmartMX_DESFireEV0_256; // DESFIRE 256B
                                                        break;
                                                    case 0x16:
                                                        currentChip.SubType = MifareChipSubType.SmartMX_DESFireEV0_2K; // DESFIRE 2K
                                                        break;
                                                    case 0x18:
                                                        currentChip.SubType = MifareChipSubType.SmartMX_DESFireEV0_4K; // 4K
                                                        break;
                                                    default:
                                                        currentChip.SubType = MifareChipSubType.Unspecified;
                                                        break;
                                                } // Size ?
                                                break;

                                            case 1: //DESFIRE EV1
                                                switch (version?[6])
                                                {
                                                    case 0x10:
                                                        currentChip.SubType = MifareChipSubType.SmartMX_DESFireEV1_256; //DESFIRE 256B
                                                        break;
                                                    case 0x16:
                                                        currentChip.SubType = MifareChipSubType.SmartMX_DESFireEV1_2K; // DESFIRE 2K
                                                        break;
                                                    case 0x18:
                                                        currentChip.SubType = MifareChipSubType.SmartMX_DESFireEV1_4K; // 4K
                                                        break;
                                                    case 0x1A:
                                                        currentChip.SubType = MifareChipSubType.SmartMX_DESFireEV1_8K; // 8K
                                                        break;
                                                    default:
                                                        break;
                                                } // Size ?
                                                break;

                                            case 2: // EV2
                                                switch (version?[6])
                                                {
                                                    case 0x16:
                                                        currentChip.SubType = MifareChipSubType.SmartMX_DESFireEV2_2K; // DESFIRE 2K
                                                        break;
                                                    case 0x18:
                                                        currentChip.SubType = MifareChipSubType.SmartMX_DESFireEV2_4K; // 4K
                                                        break;
                                                    case 0x1A:
                                                        currentChip.SubType = MifareChipSubType.SmartMX_DESFireEV2_8K; // 8K
                                                        break;
                                                    case 0x1C:
                                                        currentChip.SubType = MifareChipSubType.SmartMX_DESFireEV2_16K; // 16K
                                                        break;
                                                    case 0x1E:
                                                        currentChip.SubType = MifareChipSubType.SmartMX_DESFireEV2_32K; // 32K
                                                        break;
                                                    default:
                                                        break;
                                                } // SIZE ?
                                                break;

                                            case 3: // EV3
                                                switch (version?[6])
                                                {
                                                    case 0x16:
                                                        currentChip.SubType = MifareChipSubType.SmartMX_DESFireEV3_2K; // DESFIRE 2K
                                                        break;
                                                    case 0x18:
                                                        currentChip.SubType = MifareChipSubType.SmartMX_DESFireEV3_4K; // 4K
                                                        break;
                                                    case 0x1A:
                                                        currentChip.SubType = MifareChipSubType.SmartMX_DESFireEV3_8K; // 8K
                                                        break;
                                                    case 0x1C:
                                                        currentChip.SubType = MifareChipSubType.SmartMX_DESFireEV3_16K; // 16K
                                                        break;
                                                    case 0x1E:
                                                        currentChip.SubType = MifareChipSubType.SmartMX_DESFireEV3_32K; // 32K
                                                        break;
                                                    default:
                                                        break;
                                                } // SIZE ?
                                                break;

                                            default:
                                                currentChip.SubType = MifareChipSubType.Unspecified;

                                                break;
                                        }
                                    }
                                } // Get Version L4 Failed

                                else
                                {
                                    if (currentChip.ATS != null)
                                    {
                                        if (ByteArrayConverter.SearchBytePattern(currentChip.ATS, new byte[] { 0xC1, 0x05, 0x2F, 0x2F, 0x00, 0x35, 0xC7 }) != 0) //MF PlusS 4K in SL1
                                        {
                                            currentChip.SubType = MifareChipSubType.MifarePlus_SL3_4K;
                                        }

                                        else if (ByteArrayConverter.SearchBytePattern(currentChip.ATS, new byte[] { 0xC1, 0x05, 0x2F, 0x2F, 0x01, 0xBC, 0xD6 }) != 0) //MF PlusX 4K in SL1
                                        {
                                            currentChip.SubType = MifareChipSubType.MifarePlus_SL3_4K;
                                        }
                                        else
                                        {
                                            currentChip.SubType = MifareChipSubType.Unspecified;
                                        }
                                    }
                                    else
                                    {
                                        currentChip.SubType = MifareChipSubType.SmartMX_Mifare_4K;
                                    }
                                } // Mifare Plus
                            } // SAK b6 = 1 ?
                            else
                            {
                                currentChip.SubType = MifareChipSubType.MifareUltralight;
                            } // Ultralight || NTAG
                        }
                    } // SAK b5 = 1 ?
                } // SAK b5 = 1 ?
            }



        }

        #endregion

        #region Reader Communication

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public async Task<bool> ConnectAsync()
        {
            try
            {
                EnsureTransport();

                // NFC functions are known to take less than 2 second to execute.
                _transport.ReadTimeout = 2000;
                _transport.WriteTimeout = 2000;

                // Open TWN4 com port
                await _transport.ConnectAsync().ConfigureAwait(false);
            }
            catch (Exception e)
            {
                //Port Busy? Try Again
                if (e is UnauthorizedAccessException)
                {
                    if (_transport != null && _transport.IsOpen)
                    {
                        await _transport.DisconnectAsync().ConfigureAwait(false);
                    }

                    throw new ReaderException("Call was not successfull, error " + Enum.GetName(typeof(ReaderError), ReaderError.NotOpen), null);
                }

                if (e is IOException)
                {
                    instance = DeviceManager.GetAvailableReaders();
                }

                throw new ReaderException("Call was not successfull, error " + Enum.GetName(typeof(ReaderError), ReaderError.NotInit), null);
            }

            var version = await GetVersionStringAsync();
            return version.StartsWith("TWN4");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public async Task<bool> DisconnectAsync()
        {
            try
            {
                if (_transport == null)
                {
                    return true;
                }

                if (_transport.IsOpen)
                {
                    _transport.DiscardInBuffer();
                    _transport.DiscardOutBuffer();
                    await _transport.DisconnectAsync().ConfigureAwait(false);
                    return true;
                }

                return true;
            }
            catch (Exception e)
            {
                //Port Busy? Try Again
                if (e is UnauthorizedAccessException)
                {
                    throw new ReaderException("Call was not successfull, error " + Enum.GetName(typeof(ReaderError), ReaderError.NotOpen), null);
                }

                if (e is IOException)
                {
                    throw new ReaderException("Call was not successfull, error " + Enum.GetName(typeof(ReaderError), ReaderError.NotInit), null);
                }

                throw;
            }
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

        private static string GetPRSfromByteArray(byte[] PRSStream)
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
            return buffer;
        }// End of GetPRSfromByteArray

        #endregion

        /// <summary>
        /// Call a function of the TWN device in raw format. Sends the bytes provided by CMD and returns the response.
        /// The caller is responsible for providing the correct input sequence, parsing the result and handling the errorCode.
        /// 
        /// Example: 
        /// <code>
        ///     byte[] result = await CallFunctionRawAsync(new byte[] { API_SYS, 3 });
        ///     byte errorCode = result[0];
        /// </code>
        /// </summary>
        /// <param name="CMD">Command to send to the device.</param>
        /// <returns>The response of the device.</returns>
        public async Task<byte[]> CallFunctionRawAsync(byte[] CMD)
        {
            return await DoTXRXAsync(CMD);
        }

        /// <summary>
        /// Call a function of the TWN device in raw format. Sends the bytes provided by CMD and returns a ResponseParser wrapping the response.
        /// The caller is responsible for providing the correct input sequence and handling errors.
        /// 
        /// Example: 
        /// <code>
        ///     var parser = await CallFunctionParserAsync(new byte[] { API_SYS, 3 });
        ///     var errorCode = parser.ParseResponseError();
        ///     if (errorCode != ResponseError.None)
        ///         uint ticks = parser.ParseLong();
        /// </code>
        /// </summary>
        /// <param name="CMD">Command to send to the device.</param>
        /// <returns>A <see cref="ResponseParser"/> wrapping the response of the device.</returns>
        public async Task<ResponseParser> CallFunctionParserAsync(byte[] CMD)
        {
            var result = await CallFunctionRawAsync(CMD);
            var parser = new ResponseParser(result.ToList());
            return parser;
        }

        /// <summary>
        /// Call a function of the TWN device in raw format. Sends the bytes provided by CMD and returns a ResponseParser wrapping the response.
        /// The caller is responsible for providing the correct input sequence.
        /// If the device returns an error, it is thrown as a TwnException. The caller must NOT treat the first parser byte as an error code, as it has already been parsed.
        /// 
        /// Example: 
        /// <code>
        ///   try {
        ///     var parser = await CallFunctionAsync(new byte[] { API_SYS, 3 });
        ///     uint ticks = parser.ParseLong();
        ///   } catch (TwnException e) {...}
        /// </code>
        /// </summary>
        /// <param name="CMD">Command to send to the device.</param>
        /// <returns>A <see cref="ResponseParser"/> wrapping the response of the device.</returns>
        /// <exception cref="TwnException">Is thrown if a communication error occurs.</exception>
        public async Task<ResponseParser> CallFunctionAsync(byte[] CMD)
        {
            var parser = await CallFunctionParserAsync(CMD);
            var errorCode = parser.ParseResponseError();
            if (errorCode != ResponseError.None)
            {
                throw new TwnException("Reader error: " + errorCode, errorCode);
            }
            return parser;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="CMD"></param>
        /// <returns></returns>
        private async Task<byte[]> DoTXRXAsync(byte[] CMD)
        {
            await _transportLock.WaitAsync().ConfigureAwait(false);
            try
            {
                EnsureTransport();

                if (!_transport.IsOpen)
                {
                    await _transport.ConnectAsync().ConfigureAwait(false);
                }

                // Discard com port inbuffer
                _transport.DiscardInBuffer();

                // Generate simple protocol string and send command
                await _transport.WriteLineAsync(GetPRSfromByteArray(CMD)).ConfigureAwait(false);

                // Read simple protocoll string and convert to byte array
                var rawResponse = await _transport.ReadLineAsync().ConfigureAwait(false);
                return GetByteArrayfromPRS(rawResponse);

            }

            catch
            {
                throw new ReaderException("Call was not successfull, error " + Enum.GetName(typeof(ReaderError), ReaderError.NotOpen), null);
            }
            finally
            {
                _transportLock.Release();
            }

        }// End of DoTXRXAsync

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TXRXErr(object sender, Exception e)
        {
            throw new ReaderException("Call was not successfull, error " + e.ToString(), null);
        }

        #endregion

        #region Public Properties

        /// <summary>
        /// Return the number of Connected Readers
        /// </summary>
        public int AvailableReadersCount => DeviceManager.GetAvailableReaders().Count;

        public bool IsConnected
        {
            get
            {
                return _transport != null && _transport.IsOpen;
            }
        }

        public string PortName
        {
            get; internal set;
        }

        /// <summary>
        /// Indicates whether the connected reader is a Legic-capable TWN4 variant based on the firmware version string.
        /// </summary>
        /// <remarks>
        ///     Legic devices offload parts of the ISO14443 flow to a secondary chip (e.g., automatic RATS/ATS), so callers
        ///     should consult this flag before attempting low-level selection or transparent exchanges.
        /// </remarks>
        public bool IsTWN4LegicReader
        {
            get; internal set;
        }

        #endregion

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Dispose any managed objects
                    if (_transport != null)
                    {
                        if (_transport.IsOpen)
                        {
                            _transport.DisconnectAsync().GetAwaiter().GetResult();
                        }

                        _transport.Dispose();
                        _transport = null;
                    }
                }

                Thread.Sleep(20);
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
