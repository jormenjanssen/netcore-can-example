using System;

namespace Riwo.Rimote.VirtualCan
{
    public class CanFrame
    {
        public const int FrameLength = 16;
        public const int FrameStart = 8;

        public byte[] FrameBytes { get; }

        public int Id
        {
            get => (FrameBytes[0] << 0) | (FrameBytes[1] << 8) | (FrameBytes[2] << 16) | ((FrameBytes[3] & 0x1F) << 24);
            set
            {
                FrameBytes[3] = (byte)((FrameBytes[0] & 0xE0) | ((value & 0x1F000000) >> 24));
                FrameBytes[2] = (byte)((value & 0x00FF0000) >> 16);
                FrameBytes[1] = (byte)((value & 0x0000FF00) >> 8);
                FrameBytes[0] = (byte)(value & 0x000000FF);
            }
        }

        public Span<byte> Data => new Span<byte>(FrameBytes, FrameStart, FrameLength - FrameStart);
        public int DataLength
        {
            get => FrameBytes[4];
            set
            {
                if (value < 0 || value > 8)
                    throw new ArgumentOutOfRangeException(nameof(value), "CAN frame should be between 0-8 bytes");
                FrameBytes[4] = (byte)value;
                for (var index = 8 + value; index < 16; ++index)
                    FrameBytes[index] = 0;
            }
        }

        public bool IsErrorMessage
        {
            get => GetBit(3, 5);
            set => SetBit(3, 5, value);
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

        public CanFrame(byte[] frame)
        {
            if (frame == null)
                throw new ArgumentNullException(nameof(frame));
            if (frame.Length != FrameLength)
                throw new ArgumentException("CAN frame should be 16 bytes", nameof(frame));

            FrameBytes = frame;
        }

        public CanFrame() : this(new byte[FrameLength])
        {

        }

        private bool GetBit(int index, int bit)
        {
            var mask = (byte)(1 << bit);
            return (FrameBytes[index] & mask) != 0;
        }

        private void SetBit(int index, int bit, bool value)
        {
            var mask = (byte)(1 << bit);
            FrameBytes[index] = (byte)((FrameBytes[index] & ~mask) | (value ? mask : 0));
        }

        public bool IsExtendedFrame
        {
            get => Id > 0x7FF;
            set => SetBit(3, 7, value);
        }
    }
}
