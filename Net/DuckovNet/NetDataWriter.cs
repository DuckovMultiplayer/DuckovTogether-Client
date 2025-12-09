using System;
using System.Text;

namespace DuckovNet
{
    public class NetDataWriter
    {
        private byte[] _data;
        private int _position;
        private const int InitialSize = 256;

        public byte[] Data => _data;
        public int Length => _position;

        public NetDataWriter() : this(InitialSize) { }

        public NetDataWriter(int initialSize)
        {
            _data = new byte[initialSize];
            _position = 0;
        }

        public void Reset()
        {
            _position = 0;
        }

        public byte[] CopyData()
        {
            var result = new byte[_position];
            Buffer.BlockCopy(_data, 0, result, 0, _position);
            return result;
        }

        private void EnsureCapacity(int additionalSize)
        {
            var requiredSize = _position + additionalSize;
            if (requiredSize <= _data.Length) return;
            
            var newSize = Math.Max(_data.Length * 2, requiredSize);
            var newData = new byte[newSize];
            Buffer.BlockCopy(_data, 0, newData, 0, _position);
            _data = newData;
        }

        public void Put(byte value)
        {
            EnsureCapacity(1);
            _data[_position++] = value;
        }

        public void Put(sbyte value) => Put((byte)value);

        public void Put(bool value)
        {
            EnsureCapacity(1);
            _data[_position++] = value ? (byte)1 : (byte)0;
        }

        public void Put(short value)
        {
            EnsureCapacity(2);
            _data[_position++] = (byte)value;
            _data[_position++] = (byte)(value >> 8);
        }

        public void Put(ushort value)
        {
            EnsureCapacity(2);
            _data[_position++] = (byte)value;
            _data[_position++] = (byte)(value >> 8);
        }

        public void Put(int value)
        {
            EnsureCapacity(4);
            _data[_position++] = (byte)value;
            _data[_position++] = (byte)(value >> 8);
            _data[_position++] = (byte)(value >> 16);
            _data[_position++] = (byte)(value >> 24);
        }

        public void Put(uint value)
        {
            EnsureCapacity(4);
            _data[_position++] = (byte)value;
            _data[_position++] = (byte)(value >> 8);
            _data[_position++] = (byte)(value >> 16);
            _data[_position++] = (byte)(value >> 24);
        }

        public void Put(long value)
        {
            EnsureCapacity(8);
            _data[_position++] = (byte)value;
            _data[_position++] = (byte)(value >> 8);
            _data[_position++] = (byte)(value >> 16);
            _data[_position++] = (byte)(value >> 24);
            _data[_position++] = (byte)(value >> 32);
            _data[_position++] = (byte)(value >> 40);
            _data[_position++] = (byte)(value >> 48);
            _data[_position++] = (byte)(value >> 56);
        }

        public void Put(ulong value)
        {
            EnsureCapacity(8);
            _data[_position++] = (byte)value;
            _data[_position++] = (byte)(value >> 8);
            _data[_position++] = (byte)(value >> 16);
            _data[_position++] = (byte)(value >> 24);
            _data[_position++] = (byte)(value >> 32);
            _data[_position++] = (byte)(value >> 40);
            _data[_position++] = (byte)(value >> 48);
            _data[_position++] = (byte)(value >> 56);
        }

        public void Put(float value)
        {
            EnsureCapacity(4);
            var bytes = BitConverter.GetBytes(value);
            Buffer.BlockCopy(bytes, 0, _data, _position, 4);
            _position += 4;
        }

        public void Put(double value)
        {
            EnsureCapacity(8);
            var bytes = BitConverter.GetBytes(value);
            Buffer.BlockCopy(bytes, 0, _data, _position, 8);
            _position += 8;
        }

        public void Put(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                Put(0);
                return;
            }
            
            var bytes = Encoding.UTF8.GetBytes(value);
            Put(bytes.Length);
            Put(bytes);
        }

        public void Put(byte[] value)
        {
            if (value == null || value.Length == 0) return;
            EnsureCapacity(value.Length);
            Buffer.BlockCopy(value, 0, _data, _position, value.Length);
            _position += value.Length;
        }

        public void Put(byte[] value, int offset, int length)
        {
            if (value == null || length == 0) return;
            EnsureCapacity(length);
            Buffer.BlockCopy(value, offset, _data, _position, length);
            _position += length;
        }

        public void PutBytesWithLength(byte[] value)
        {
            if (value == null)
            {
                Put(0);
                return;
            }
            Put(value.Length);
            Put(value);
        }
    }
}
