#pragma warning disable S2360 // Optional parameters are intended: callers skip some of them by name (e.g. le: 0, maxRxByteCount: 128), which overloads cannot express.
using System;
using System.Threading.Tasks;
using Elatec.NET.Interfaces;

namespace Elatec.NET.Sam
{
    /// <summary>
    /// Exchanges APDUs with a SAM in a slot of a TWN4 reader, using the reader's ISO7816 API.
    /// </summary>
    /// <remarks>
    /// The SAM must already be activated (<see cref="TWN4ReaderDevice.ISO7816_IccPowerOnAsync"/>), and, if the
    /// card requires it, the communication settings applied
    /// (<see cref="TWN4ReaderDevice.ISO7816_SetCommSettingsAsync"/>). This class does neither.
    /// </remarks>
    public sealed class Twn4SamApduChannel : ISamApduChannel
    {
        private readonly TWN4ReaderDevice _reader;
        private readonly Iso7816Channel _channel;
        private readonly ushort _maxRxByteCount;

        /// <param name="reader">The reader the SAM is inserted in.</param>
        /// <param name="channel">The slot of the SAM.</param>
        /// <param name="maxRxByteCount">Size of the reader's receive buffer for the response data field.</param>
        public Twn4SamApduChannel(TWN4ReaderDevice reader, Iso7816Channel channel, ushort maxRxByteCount = 256)
        {
            _reader = reader ?? throw new ArgumentNullException(nameof(reader));
            _channel = channel;
            _maxRxByteCount = maxRxByteCount;
        }

        public async Task<SamApduResponse> TransmitAsync(SamApdu command)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            var response = await _reader.ISO7816_ExchangeAPDUAsync(_channel, command.Cla, command.Ins, command.P1, command.P2,
                command.Data, command.Le, false, _maxRxByteCount);

            if (response == null)
            {
                throw new SamException("The reader could not exchange the APDU with the SAM. Is the SAM inserted and activated?");
            }

            return new SamApduResponse(response.Data, response.StatusWord);
        }
    }
}
