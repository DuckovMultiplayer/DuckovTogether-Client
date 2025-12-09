using System;
using System.Text;

namespace DuckovNet
{
    public class NetDataReader
    {
        private byte[] _data;
        private int _position;
        private int _dataSize;

        public byte[] RawData => _data;
        public int Position => _position;
        public int AvailableBytes => _dataSize - _position;
        public bool EndOfData => _position >= _dataSize;

        public NetDataReader() { }

        public NetDataReader(byte[] data) : this(data, 0, data.Length) { }

        public NetDataReader(byte[] data, int offset, int length)
        {
            SetSource(data, offset, length);
        }

        public void SetSource(byte[] data)
        {
            SetSource(data, 0, data.Length);
        }

        public void SetSource(byte[] data, int offset, int length)
        {
            _data = data;
            _position = offset;
            _dataSize = offset + length;
        }

        public void Clear()
        {
            _data = null;
            _position = 0;
            _dataSize = 0;
        }

        public byte GetByte()
        {
            if (_position >= _dataSize) throw new IndexOutOfRangeException();
            return _data[_position++];
        }

        public sbyte GetSByte() => (sbyte)GetByte();

        public bool GetBool() => GetByte() != 0;

        public short GetShort()
        {
            if (_position + 2 > _dataSize) throw new IndexOutOfRangeException();
            var result = (short)(_data[_position] | (_data[_position + 1] << 8));
            _position += 2;
            return result;
        }

        public ushort GetUShort()
        {
            if (_position + 2 > _dataSize) throw new IndexOutOfRangeException();
            var result = (ushort)(_data[_position] | (_data[_position + 1] << 8));
            _position += 2;
            return result;
        }

        public int GetInt()
        {
            if (_position + 4 > _dataSize) throw new IndexOutOfRangeException();
            var result = _data[_position] | 
                        (_data[_position + 1] << 8) | 
                        (_data[_position + 2] << 16) | 
                        (_data[_position + 3] << 24);
            _position += 4;
            return result;
        }

        public uint GetUInt()
        {
            if (_position + 4 > _dataSize) throw new IndexOutOfRangeException();
            var result = (uint)(_data[_position] | 
                        (_data[_position + 1] << 8) | 
                        (_data[_position + 2] << 16) | 
                        (_data[_position + 3] << 24));
            _position += 4;
            return result;
        }

        public long GetLong()
        {
            if (_position + 8 > _dataSize) throw new IndexOutOfRangeException();
            var lo = (uint)(_data[_position] | 
                           (_data[_position + 1] << 8) | 
                           (_data[_position + 2] << 16) | 
                           (_data[_position + 3] << 24));
            var hi = (uint)(_data[_position + 4] | 
                           (_data[_position + 5] << 8) | 
                           (_data[_position + 6] << 16) | 
                           (_data[_position + 7] << 24));
            _position += 8;
            return (long)((ulong)hi << 32 | lo);
        }

        public ulong GetULong()
        {
            if (_position + 8 > _dataSize) throw new IndexOutOfRangeException();
            var lo = (uint)(_data[_position] | 
                           (_data[_position + 1] << 8) | 
                           (_data[_position + 2] << 16) | 
                           (_data[_position + 3] << 24));
            var hi = (uint)(_data[_position + 4] | 
                           (_data[_position + 5] << 8) | 
                           (_data[_position + 6] << 16) | 
                           (_data[_position + 7] << 24));
            _position += 8;
            return (ulong)hi << 32 | lo;
        }

        public float GetFloat()
        {
            if (_position + 4 > _dataSize) throw new IndexOutOfRangeException();
            var result = BitConverter.ToSingle(_data, _position);
            _position += 4;
            return result;
        }

        public double GetDouble()
        {
            if (_position + 8 > _dataSize) throw new IndexOutOfRangeException();
            var result = BitConverter.ToDouble(_data, _position);
            _position += 8;
            return result;
        }

        public string GetString()
        {
            var length = GetInt();
            if (length <= 0) return string.Empty;
            if (_position + length > _dataSize) throw new IndexOutOfRangeException();
            var result = Encoding.UTF8.GetString(_data, _position, length);
            _position += length;
            return result;
        }

        public byte[] GetBytes(int length)
        {
            if (_position + length > _dataSize) throw new IndexOutOfRangeException();
            var result = new byte[length];
            Buffer.BlockCopy(_data, _position, result, 0, length);
            _position += length;
            return result;
        }
        
        public void GetBytes(int length, byte[] buffer)
        {
            if (_position + length > _dataSize) throw new IndexOutOfRangeException();
            Buffer.BlockCopy(_data, _position, buffer, 0, length);
            _position += length;
        }
        
        public void GetBytes(byte[] buffer, int length)
        {
            GetBytes(length, buffer);
        }

        public byte[] GetBytesWithLength()
        {
            var length = GetInt();
            if (length <= 0) return Array.Empty<byte>();
            return GetBytes(length);
        }

        public bool TryGetByte(out byte value)
        {
            if (_position >= _dataSize) { value = 0; return false; }
            value = GetByte();
            return true;
        }

        public bool TryGetBool(out bool value)
        {
            if (_position >= _dataSize) { value = false; return false; }
            value = GetBool();
            return true;
        }

        public bool TryGetInt(out int value)
        {
            if (_position + 4 > _dataSize) { value = 0; return false; }
            value = GetInt();
            return true;
        }

        public bool TryGetString(out string value)
        {
            if (_position + 4 > _dataSize) { value = null; return false; }
            var length = PeekInt();
            if (_position + 4 + length > _dataSize) { value = null; return false; }
            value = GetString();
            return true;
        }

        public int PeekInt()
        {
            if (_position + 4 > _dataSize) return 0;
            return _data[_position] | 
                   (_data[_position + 1] << 8) | 
                   (_data[_position + 2] << 16) | 
                   (_data[_position + 3] << 24);
        }

        public byte PeekByte()
        {
            if (_position >= _dataSize) return 0;
            return _data[_position];
        }

        public void SkipBytes(int count)
        {
            _position += count;
            if (_position > _dataSize) _position = _dataSize;
        }

        public void SetPosition(int position)
        {
            _position = position;
            if (_position > _dataSize) _position = _dataSize;
            if (_position < 0) _position = 0;
        }

        public byte[] GetRemainingBytes()
        {
            var remaining = _dataSize - _position;
            if (remaining <= 0) return Array.Empty<byte>();
            return GetBytes(remaining);
        }
    }
}
