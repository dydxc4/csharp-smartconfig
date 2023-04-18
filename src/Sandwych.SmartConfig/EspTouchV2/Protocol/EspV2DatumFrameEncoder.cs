using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using System.Text;
using Sandwych.SmartConfig.Util;

#if DEBUG
using System.Diagnostics;
#endif

namespace Sandwych.SmartConfig.EspTouchV2.Protocol
{
    public class EspV2DatumFrameEncoder
    {
        private static readonly byte[] EmptyArray = Array.Empty<byte>();

        private readonly List<ushort> _frameList = new List<ushort>();
        private readonly byte[] _ssid;
        private readonly byte[] _password;
        private readonly byte[] _aesKey;
        private readonly byte[] _reservedData;
        private readonly byte[] _head;
        private readonly int _portMark;

        private bool _isPasswordEncoded;
        private bool _isReservedDataEncoded;
        private bool _isSSIDEncoded;

        private int PasswordPaddingFactor { get; set; }
        private int ReservedDataPaddingFactor { get; set; }
        private int SSIDPaddingFactor { get; set; }

        private bool IsPasswordEncoded
        {
            get => _isPasswordEncoded;
            set
            {
                _isPasswordEncoded = value;
                PasswordPaddingFactor = value ? 5 : 6;
            }
        }

        private bool IsReservedDataEncoded
        {
            get => _isReservedDataEncoded;
            set
            {
                _isReservedDataEncoded = value;
                ReservedDataPaddingFactor = value ? 5 : 6;
            }
        }

        private bool IsSSIDEncoded
        {
            get => _isSSIDEncoded;
            set
            {
                _isSSIDEncoded = value;
                SSIDPaddingFactor = value ? 5 : 6;
            }
        }

        private bool RequireEncrypt 
            => _aesKey.Any() && (_password.Any() || _reservedData.Any());

        private SmartConfigContext Context { get; }

        public EspV2DatumFrameEncoder(SmartConfigContext context, SmartConfigArguments args)
        {
            Context = context;

            _portMark = Context.GetOption<int>(StandardOptionNames.SelectedListeningPortIndex);
            _ssid = args.Ssid != null ? Encoding.UTF8.GetBytes(args.Ssid) : EmptyArray;
            _password = args.Password != null ? Encoding.UTF8.GetBytes(args.Password) : EmptyArray;
            _aesKey = args.AesKey ?? EmptyArray;
            _reservedData = args.ReservedData ?? EmptyArray;

            Validate();

            IsSSIDEncoded = IsEncoded(_ssid);
            IsPasswordEncoded = IsEncoded(_password);
            IsReservedDataEncoded = IsEncoded(_reservedData);

            _head = new byte[6];

            _head[0] = (byte)(_ssid.Length | (IsSSIDEncoded ? 0x80 : 0));
            _head[1] = (byte)(_password.Length | (IsPasswordEncoded ? 0x80 : 0));
            _head[2] = (byte)(_reservedData.Length | (IsReservedDataEncoded ? 0x80 : 0));
            _head[3] = Crc8.ComputeOnceOnly(args.Bssid.GetAddressBytes());

            int flag = (1) |                                    // bit 0: ipv4 or ipv6
                       (RequireEncrypt ? 0b010 : 0) |           // bit 1-2: crypt
                       ((_portMark & 0b11) << 3) |              // bit 3-4: app port
                       ((EspV2Constants.Version & 0b11) << 6);  // bit 6-7: version

            _head[4] = (byte)flag;
            _head[5] = Crc8.ComputeOnceOnly(_head, 0, 5);

            bool IsEncoded(byte[] data) => data.Any(b => b < 0);
        }

        public IEnumerable<ushort> Encode()
        {
            using var stream = new MemoryStream();
            var random = new Random();

            stream.Write(_head);
            
            byte[] CreatePadding(int factor, int length)
            {
                length = factor - length % factor;
                if (length < factor)
                {
                    byte[] buffer = new byte[length];
                    random.NextBytes(buffer);
                    return buffer;
                }

                return Array.Empty<byte>();
            }

            int passwordLength = 0;
            int passwordPaddingLength = 0;

            int reservedLength = 0;
            int reservedPaddingLength = 0;

            if (RequireEncrypt)
            {
                byte[] plainData = new byte[_password.Length + _reservedData.Length];
                Array.Copy(_password, 0, plainData, 0, _password.Length);
                Array.Copy(_reservedData, 0, plainData, _password.Length, _reservedData.Length);

                IsPasswordEncoded = true;
                var encryptedData = EspV2Aes.Encrypt(plainData, _aesKey);
                var padding = CreatePadding(PasswordPaddingFactor, encryptedData.Length);

                passwordLength = encryptedData.Length;
                passwordPaddingLength = padding.Length;

                stream.Write(encryptedData);
                stream.Write(padding);
            }
            else
            {
                stream.Write(_password);
                passwordLength = _password.Length;
                if (IsPasswordEncoded || IsReservedDataEncoded)
                {
                    var padding = CreatePadding(PasswordPaddingFactor, passwordLength);
                    stream.Write(padding);
                    passwordPaddingLength = padding.Length;
                }
                
                stream.Write(_reservedData);
                reservedLength = _reservedData.Length;
                if (IsPasswordEncoded || IsReservedDataEncoded)
                {
                    var padding = CreatePadding(ReservedDataPaddingFactor, reservedLength);
                    stream.Write(padding);
                    reservedPaddingLength = padding.Length;
                }
            }

            stream.Write(_ssid);
            stream.Write(CreatePadding(SSIDPaddingFactor, _ssid.Length));

            int reservedDataBeginPos = _head.Length + passwordLength + passwordPaddingLength;
            int ssidBeginPos = reservedDataBeginPos + reservedLength + reservedPaddingLength;
#if DEBUG
            Debug.WriteLine($"Buffer created, size={stream.Length}");
            Debug.WriteLine($"Paddings: pass={passwordPaddingLength}, data={reservedLength}");
            Debug.WriteLine($"Encoded: pass={IsPasswordEncoded}, data={IsReservedDataEncoded}, Ssid={IsSSIDEncoded}");
            Debug.WriteLine($"Padding factors: pass={PasswordPaddingFactor}, data={ReservedDataPaddingFactor}, Ssid={SSIDPaddingFactor}");
            Debug.WriteLine($"Begin pos: data={reservedDataBeginPos}, Ssid={ssidBeginPos}");
#endif
            int count = 0;
            int offset = 0;
            stream.Seek(0, SeekOrigin.Begin);
            _frameList.Clear();

            while (offset < stream.Length)
            {
                int expectLength;
                bool tailIsCrc;

                if (count == 0)
                {
                    // First packet
                    tailIsCrc = false;
                    expectLength = 6;
                }
                else
                {
                    if (offset < reservedDataBeginPos)
                    {
                        // Password data
                        tailIsCrc = !IsPasswordEncoded;
                        expectLength = PasswordPaddingFactor;
                    }
                    else if (offset < ssidBeginPos)
                    {
                        // Reserved data
                        tailIsCrc = !IsReservedDataEncoded;
                        expectLength = ReservedDataPaddingFactor;
                    }
                    else
                    {
                        // Ssid data
                        tailIsCrc = !IsSSIDEncoded;
                        expectLength = SSIDPaddingFactor;
                    }
                }

                byte[] buf = new byte[6];
                int read = stream.Read(buf, 0, expectLength);
                if (read == 0)
                {
                    break;
                }

                offset += read;
                byte checksum = Crc8.ComputeOnceOnly(buf, 0, read);

                if (expectLength < buf.Length)
                {
                    buf[buf.Length - 1] = checksum;
                }

                CreateBlockFor6Bytes(buf, count - 1, checksum, tailIsCrc);
                count++;
            }

            UpdateBlocksForSequencesLength(count);

            return _frameList;
        }

        private void Validate()
        {
            int ssidLenMax = Context.GetOption<int>(EspV2OptionNames.SSIDLengthMax);
            int passLenMin = Context.GetOption<int>(EspV2OptionNames.PasswordLengthMin);
            int passLenMax = Context.GetOption<int>(EspV2OptionNames.PasswordLengthMax);
            int dataLenMax = Context.GetOption<int>(EspV2OptionNames.ReservedDataLengthMax);
            int aeskeyLen = Context.GetOption<int>(EspV2OptionNames.AESKeyLength);

            if (_ssid.Length > ssidLenMax)
            {
                throw new ArgumentOutOfRangeException("Ssid", 
                    $"Ssid length is greater than {ssidLenMax} characters.");
            }

            if (_password.Length < passLenMin || _password.Length > passLenMax)
            {
                throw new ArgumentOutOfRangeException("Password",
                    $"Password length must be between {passLenMin} or {passLenMax} characters.");
            }

            if (_aesKey.Length != aeskeyLen)
            {
                throw new ArgumentOutOfRangeException("AesKey",
                    $"Encryption key length must be {aeskeyLen} characters.");
            }

            if (_reservedData.Length > dataLenMax)
            {
                throw new ArgumentOutOfRangeException("ReservedData",
                    $"Reserved data length must be less than {dataLenMax} characters.");
            }
        }
    
        private void CreateBlockFor6Bytes(byte[] buffer, int sequence, int crc, bool tailIsCrc)
        {
#if DEBUG
            Debug.WriteLine("buf={0}, seq={1}, crc={2:x2}, tailIsCrc={3}",
                            BitConverter.ToString(buffer),
                            sequence,
                            crc,
                            tailIsCrc);
#endif
            if (sequence == EspV2Constants.SequenceFirst)
            {
                var syncBlock = GetSyncFrames();

                _frameList.Add(syncBlock);
                _frameList.Add(0);
                _frameList.Add(syncBlock);
                _frameList.Add(0);
            }
            else
            {
                var sequenceBlock = GetSequenceFrames(sequence);

                _frameList.Add(sequenceBlock);
                _frameList.Add(sequenceBlock);
                _frameList.Add(sequenceBlock);
            }

            for (int bit = 0; bit < (tailIsCrc ? 7 : 8); bit++)
            {
                int data = (buffer[5] >> bit & 1) |
                           ((buffer[4] >> bit & 1) << 1) |
                           ((buffer[3] >> bit & 1) << 2) |
                           ((buffer[2] >> bit & 1) << 3) |
                           ((buffer[1] >> bit & 1) << 4) |
                           ((buffer[0] >> bit & 1) << 5);

                _frameList.Add(GetDataFrames(data, bit));
            }

            if (tailIsCrc)
            {
                _frameList.Add(GetDataFrames(crc, 7));
            }
        }

        private void UpdateBlocksForSequencesLength(int size)
        {
            var block = GetSequenceSizeFrames(size);
            _frameList[1] = block;
            _frameList[3] = block;
        }

        private ushort GetSyncFrames() => 1048;

        private ushort GetSequenceSizeFrames(int size) 
            => (ushort)(1072 + size - 1);

        private ushort GetSequenceFrames(int sequence) 
            => (ushort)(128 + sequence);

        private ushort GetDataFrames(int data, int index) 
            => (ushort)((index << 7) | (1 << 6) | data);
    }
}