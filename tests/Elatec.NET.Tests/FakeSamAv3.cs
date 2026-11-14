using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Elatec.NET.Interfaces;
using Elatec.NET.Sam;

namespace Elatec.NET.Tests
{
    /// <summary>
    /// A software model of the SAM side of the host protocol (activation, host authentication with full protection and
    /// secure commands), to test the host code beyond the fixed examples. The host authentication and secure messaging
    /// follow NXP AN12704. The activation follows an open-source SAM AV2 library and the statements of NXP UM11316
    /// (a new SAM is unactivated and refuses host authentication). It is not a model of a real SAM.
    /// </summary>
    internal sealed class FakeSamAv3 : ISamApduChannel
    {
        private const byte InsAuthenticateHost = 0xA4;
        private const byte InsLockUnlock = 0x10;

        private readonly Dictionary<string, byte[]> _hostKeys = new Dictionary<string, byte[]>();

        private byte _stageIns;
        private int _stage;
        private byte _modeByte;
        private byte[] _kx;
        private byte[] _kxe;
        private byte[] _rnd2;
        private byte[] _rndB;
        private byte[] _ke;
        private byte[] _km;
        private uint _counter;
        private bool _authenticated;

        /// <param name="requireActivation">A new SAM: host authentication is refused until the SAM was activated.</param>
        public FakeSamAv3(bool requireActivation = false)
        {
            IsActivated = !requireActivation;
        }

        public bool IsActivated { get; private set; }

        /// <summary>Whether the final response of the activation carries the SAM's proof of RndA.</summary>
        public bool ActivationReturnsProof { get; set; } = true;

        /// <summary>Damage the proof in the final response of the activation.</summary>
        public bool CorruptActivationProof { get; set; }

        /// <summary>Every key entry written with SAM_ChangeKeyEntry, by entry number (the decrypted 61 byte data).</summary>
        public Dictionary<byte, byte[]> WrittenEntries { get; } = new Dictionary<byte, byte[]>();

        /// <summary>Plain data of every secure command received: INS, P1, P2 and the decrypted data field.</summary>
        public List<Tuple<byte, byte, byte, byte[]>> Commands { get; } = new List<Tuple<byte, byte, byte, byte[]>>();

        public List<string> Sent { get; } = new List<string>();

        /// <summary>Answer SAM_ChangeKeyEntry with this plain status word instead of executing it.</summary>
        public ushort? ChangeKeyEntryStatus { get; set; }

        /// <summary>Damage the CMAC of the next secure response.</summary>
        public bool CorruptNextResponseMac { get; set; }

        public uint Counter
        {
            get { return _counter; }
        }

        public void AddHostKey(byte keyNo, byte version, byte[] key)
        {
            _hostKeys[keyNo + ":" + version] = key;
        }

        public Task<SamApduResponse> TransmitAsync(SamApdu command)
        {
            Sent.Add(SamAv3KnownAnswerTests.ScriptedSamChannel.Format(command));

            switch (command.Ins)
            {
                case InsAuthenticateHost:
                case InsLockUnlock:
                    return Task.FromResult(HandleThreePass(command));
                case 0xC1:
                case 0x7F:
                    return Task.FromResult(HandleSecureCommand(command));
                default:
                    return Task.FromResult(new SamApduResponse(null, 0x6D00));
            }
        }

        private SamApduResponse HandleThreePass(SamApdu command)
        {
            switch (_stage)
            {
                case 0:
                {
                    _authenticated = false;
                    byte[] keyData;
                    if (command.Ins == InsAuthenticateHost)
                    {
                        if (!IsActivated)
                            {
                                return new SamApduResponse(null, 0x6985);
                            }

                            if (command.P1 != 0x00 || command.Data.Length != 3 || command.Data[2] != 0x02)
                            {
                                return new SamApduResponse(null, 0x6A86);
                            }

                            _modeByte = 0x02;
                    }
                    else
                    {
                        // SAM_LockUnlock, activation: [KeyNo][KeyVersion][MaxChainBlocks, 3 bytes]
                        if (command.P1 != 0x03 || command.Data.Length != 5)
                            {
                                return new SamApduResponse(null, 0x6A86);
                            }

                            _modeByte = 0x03;
                    }

                    if (!_hostKeys.TryGetValue(command.Data[0] + ":" + command.Data[1], out keyData))
                    {
                        return new SamApduResponse(null, 0x6A88);
                    }

                    _kx = keyData;
                    _stageIns = command.Ins;
                    _rnd2 = RandomBytes(12);
                    _stage = 1;
                    return new SamApduResponse(_rnd2, 0x90AF);
                }
                case 1:
                {
                    if (command.Ins != _stageIns || command.Data.Length != 20)
                        {
                            return Fail();
                        }

                        var mac = SamAv3Crypto.Slice(command.Data, 0, 8);
                    var rnd1 = SamAv3Crypto.Slice(command.Data, 8, 12);
                    var modeBlock = new byte[] { _modeByte, 0x00, 0x00, 0x00 };
                    if (!SamAv3Crypto.FixedTimeEquals(mac, SamAv3Crypto.Cmac8(_kx, SamAv3Crypto.Concat(_rnd2, modeBlock))))
                        {
                            return Fail();
                        }

                        _kxe = AesHelper.EncryptBlock(_kx, SamAv3Crypto.Concat(
                        SamAv3Crypto.Slice(rnd1, 7, 5), SamAv3Crypto.Slice(_rnd2, 7, 5), SamAv3Crypto.Xor(rnd1, 0, _rnd2, 0, 5), new byte[] { 0x91 }));
                    _rndB = RandomBytes(16);
                    _stage = 2;
                    var response = SamAv3Crypto.Concat(
                        SamAv3Crypto.Cmac8(_kx, SamAv3Crypto.Concat(rnd1, modeBlock)),
                        AesHelper.EncryptCbc(_kxe, new byte[16], _rndB));
                    return new SamApduResponse(response, 0x90AF);
                }
                case 2:
                {
                    if (command.Ins != _stageIns || command.Data.Length != 32)
                        {
                            return Fail();
                        }

                        var plain = AesHelper.DecryptCbc(_kxe, new byte[16], command.Data);
                    var rndA = SamAv3Crypto.Slice(plain, 0, 16);
                    if (!SamAv3Crypto.FixedTimeEquals(SamAv3Crypto.Slice(plain, 16, 16), SamAv3Crypto.RotateLeft(_rndB, 2)))
                        {
                            return Fail();
                        }

                        _stage = 0;
                    if (_stageIns == InsAuthenticateHost)
                    {
                        _ke = AesHelper.EncryptBlock(_kx, SamAv3Crypto.Concat(
                            SamAv3Crypto.Slice(rndA, 11, 5), SamAv3Crypto.Slice(_rndB, 11, 5), SamAv3Crypto.Xor(rndA, 4, _rndB, 4, 5), new byte[] { 0x81 }));
                        _km = AesHelper.EncryptBlock(_kx, SamAv3Crypto.Concat(
                            SamAv3Crypto.Slice(rndA, 7, 5), SamAv3Crypto.Slice(_rndB, 7, 5), SamAv3Crypto.Xor(rndA, 0, _rndB, 0, 5), new byte[] { 0x82 }));
                        _counter = 0;
                        _authenticated = true;
                    }
                    else
                    {
                        IsActivated = true;
                    }

                    if (_stageIns == InsLockUnlock && !ActivationReturnsProof)
                    {
                        return new SamApduResponse(null, 0x9000);
                    }

                    var proof = AesHelper.EncryptCbc(_kxe, new byte[16], SamAv3Crypto.RotateLeft(rndA, 2));
                    if (_stageIns == InsLockUnlock && CorruptActivationProof)
                    {
                        proof[0] ^= 0x01;
                    }

                    return new SamApduResponse(proof, 0x9000);
                }
                default:
                    return Fail();
            }
        }

        private SamApduResponse Fail()
        {
            _stage = 0;
            _authenticated = false;
            return new SamApduResponse(null, 0x6982);
        }

        private SamApduResponse HandleSecureCommand(SamApdu command)
        {
            if (!_authenticated)
            {
                return new SamApduResponse(null, 0x6985);
            }

            if (command.Ins == 0xC1 && ChangeKeyEntryStatus.HasValue)
            {
                return new SamApduResponse(null, ChangeKeyEntryStatus.Value);
            }

            if (command.Data.Length < 8)
            {
                return new SamApduResponse(null, 0x6700);
            }

            var encrypted = SamAv3Crypto.Slice(command.Data, 0, command.Data.Length - 8);
            var mac = SamAv3Crypto.Slice(command.Data, command.Data.Length - 8, 8);
            var counter = SamAv3Crypto.BigEndian(_counter);
            var macLoad = SamAv3Crypto.Concat(
                new[] { command.Cla, command.Ins }, counter, new[] { command.P1, command.P2, (byte)command.Data.Length }, encrypted,
                command.Le.HasValue ? new[] { command.Le.Value } : new byte[0]);
            if (!SamAv3Crypto.FixedTimeEquals(mac, SamAv3Crypto.Cmac8(_km, macLoad)))
            {
                return new SamApduResponse(null, 0x1E00);
            }

            byte[] plain = new byte[0];
            if (encrypted.Length > 0)
            {
                var iv = AesHelper.EncryptBlock(_ke, SamAv3Crypto.Concat(SamAv3Crypto.Repeat(0x01, 4), counter, counter, counter));
                plain = SamAv3Crypto.UnpadMethod2(AesHelper.DecryptCbc(_ke, iv, encrypted));
            }

            Commands.Add(Tuple.Create(command.Ins, command.P1, command.P2, plain));
            if (command.Ins == 0xC1)
            {
                WrittenEntries[command.P1] = plain;
            }

            // Response: ChangeKeyEntry returns only the CMAC, the test command 0x7F echoes the plain data back encrypted.
            var next = SamAv3Crypto.BigEndian(_counter + 1);
            var responseData = new byte[0];
            if (command.Ins == 0x7F && plain.Length > 0)
            {
                var iv = AesHelper.EncryptBlock(_ke, SamAv3Crypto.Concat(SamAv3Crypto.Repeat(0x02, 4), next, next, next));
                responseData = AesHelper.EncryptCbc(_ke, iv, SamAv3Crypto.PadMethod2(plain));
            }

            var responseMac = SamAv3Crypto.Cmac8(_km, SamAv3Crypto.Concat(new byte[] { 0x90, 0x00 }, next, responseData));
            if (CorruptNextResponseMac)
            {
                responseMac[0] ^= 0x01;
                CorruptNextResponseMac = false;
            }

            _counter++;
            return new SamApduResponse(SamAv3Crypto.Concat(responseData, responseMac), 0x9000);
        }

        private static byte[] RandomBytes(int count)
        {
            var bytes = new byte[count];
            using (var generator = RandomNumberGenerator.Create())
            {
                generator.GetBytes(bytes);
            }

            return bytes;
        }
    }
}
