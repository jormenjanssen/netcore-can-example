using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

namespace Riwo.Rimote.VirtualCan
{
    public class CanFrame : ICloneable
    {
        private class OffsetArray<T> : IList<T>
        {
            private readonly T[] _buffer;
            private readonly int _offset;

            public OffsetArray(T[] buffer, int offset)
            {
                _buffer = buffer;
                _offset = offset;
            }

            public IEnumerator<T> GetEnumerator()
            {
                for (var i = 0; i < Count; ++i)
                    yield return this[i];
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                for (var i = 0; i < Count; ++i)
                    yield return this[i];
            }

            public void Add(T item)
            {
                throw new NotSupportedException();
            }

            public void Clear()
            {
                throw new NotSupportedException();
            }

            public bool Contains(T item)
            {
                return IndexOf(item) != -1;
            }

            public void CopyTo(T[] array, int arrayIndex)
            {
                for (var index = 0; index < Count; ++index)
                    array[arrayIndex + index] = this[index];
            }

            public bool Remove(T item)
            {
                throw new NotSupportedException();
            }

            public int Count => _buffer.Length - _offset;
            public bool IsReadOnly => false;

            public int IndexOf(T item)
            {
                for (var index = 0; index < Count; ++index)
                {
                    if (this[index].Equals(item))
                        return index;
                }
                return -1;
            }

            public void Insert(int index, T item)
            {
                throw new NotSupportedException();
            }

            public void RemoveAt(int index)
            {
                throw new NotSupportedException();
            }

            public T this[int index]
            {
                get => _buffer[_offset + index];
                set => _buffer[_offset + index] = value;
            }
        }

        private byte[] _frame;
        private OffsetArray<byte> _frameArray;

        public const int FrameLength = 16;

        public CanFrame() : this(new byte[FrameLength])
        {

        }

        public CanFrame(byte[] frame)
        {
            if (frame == null)
                throw new ArgumentNullException(nameof(frame));
            if (frame.Length != FrameLength)
                throw new ArgumentException("CAN frame should be 16 bytes", nameof(frame));

            _frame = frame;
        }

        public void Reinitialize(byte[] frame)
        {
            if (frame == null)
                throw new ArgumentNullException(nameof(frame));
            if (frame.Length != FrameLength)
                throw new ArgumentException("CAN frame should be 16 bytes", nameof(frame));

            _frame = frame;
            _frameArray = null;
        }

        private bool GetBit(int index, int bit)
        {
            var mask = (byte)(1 << bit);
            return (_frame[index] & mask) != 0;
        }

        private void SetBit(int index, int bit, bool value)
        {
            var mask = (byte)(1 << bit);
            _frame[index] = (byte)((_frame[index] & ~mask) | (value ? mask : 0));
        }

        public bool IsExtendedFrame
        {
            get => GetBit(3, 7);
            set => SetBit(3, 7, value);
        }

        public bool IsRemoteTransmissionRequest
        {
            get => GetBit(3, 6);
            set
            {
                SetBit(3, 6, value);
                if (value)
                    DataLength = 0;
            }
        }

        public bool IsErrorMessage
        {
            get => GetBit(3, 5);
            set => SetBit(3, 5, value);
        }

        public int Id
        {
            get => (_frame[0] << 0) | (_frame[1] << 8) | (_frame[2] << 16) | ((_frame[3] & 0x1F) << 24);
            set
            {
                _frame[3] = (byte)((_frame[0] & 0xE0) | ((value & 0x1F000000) >> 24));
                _frame[2] = (byte)((value & 0x00FF0000) >> 16);
                _frame[1] = (byte)((value & 0x0000FF00) >> 8);
                _frame[0] = (byte)(value & 0x000000FF);
            }
        }

        public int DataLength
        {
            get => _frame[4];
            set
            {
                if (value < 0 || value > 8)
                    throw new ArgumentOutOfRangeException(nameof(value), "CAN frame should be between 0-8 bytes");
                _frame[4] = (byte)value;
                for (var index = 8 + value; index < 16; ++index)
                    _frame[index] = 0;
            }
        }

        public IList<byte> Data
        {
            get
            {
                if (_frameArray == null)
                    _frameArray = new OffsetArray<byte>(_frame, 8);
                return _frameArray;
            }
        }

        public byte[] FrameBytes => _frame;

        public byte GetByte(int index)
        {
            return Data[index];
        }

        public ushort GetUInt16(int index)
        {
            return (ushort)((Data[index] << 8) | Data[index + 1]);
        }

        public uint GetUInt32(int index)
        {
            return ((uint)Data[index] << 24) | ((uint)Data[index + 1] << 16) | ((uint)Data[index + 2] << 8) | Data[index + 3];
        }

        private static readonly byte[] _emptyBytes = new byte[0];

        public static IList<byte> CreatePayload(params object[] items)
        {
            // Return empty array for NULL
            if (items == null)
                return _emptyBytes;

            var byteList = new List<byte>(6);
            foreach (var item in items)
            {
                // Skip null items
                if (item == null)
                    continue;

                if (item is bool b)
                {
                    byteList.Add(b ? (byte)1 : (byte)0);
                }
                else if (item is byte b1)
                {
                    byteList.Add(b1);
                }
                else if (item is ushort item1)
                {
                    byteList.Add((byte)(item1 >> 8));
                    byteList.Add((byte)item1);
                }
                else if (item is uint u)
                {
                    byteList.Add((byte)(u >> 24));
                    byteList.Add((byte)(u >> 16));
                    byteList.Add((byte)(u >> 8));
                    byteList.Add((byte)(u & 0xFF));
                }
                else if (item is Enum @enum)
                {
                    byteList.Add(Convert.ToByte(@enum));
                }
                else
                {
                    throw new NotSupportedException($"Unable to get payload from type '{item.GetType()}'");
                }
            }
            return byteList;
        }

        public object Clone()
        {
            var buffer = new byte[FrameLength];
            Array.Copy(_frame, 0, buffer, 0, FrameLength);

            Debug.Assert(CloneIsEquals(_frame, buffer));

            return new CanFrame(buffer);
        }

        private bool CloneIsEquals(byte[] a, byte[] b)
        {
            if (a.Length != b.Length)
                return false;

            for (var i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                    return false;
            }

            return true;
        }

    }
}
