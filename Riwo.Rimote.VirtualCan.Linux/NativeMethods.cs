using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Riwo.Rimote.VirtualCan.Linux
{
    internal static class NativeMethods
    {
        private const int Siocgifindex = 0x8933;

        private enum CanSocketOptionLevel
        {
            SOL_CAN_RAW = 101
        }

        private enum CanRawSocketOptionValue
        {
            CAN_RAW_FILTER = 1,
            CAN_RAW_ERR_FILTER = 2,
            CAN_RAW_LOOPBACK = 3,
            CAN_RAW_RECV_OWN_MSGS = 4,
            CAN_RAW_FD_FRAMES = 5,
        }

        [DllImport("libc", EntryPoint = "socket", SetLastError = true)]
        private static extern int socket(int addressFamily, int socketType, int protocolType);

        [DllImport("libc", EntryPoint = "bind")]
        private static extern int Bind(IntPtr socketHandle, byte[] addr, int addrBytes);

        [DllImport("libc", EntryPoint = "setsockopt")]
        private static extern int SetSockOpt(IntPtr socketHandle, int level, int optname, byte[] optval, int optlen);

        [DllImport("libc", EntryPoint = "ioctl")]
        private static extern int Ioctl(int fd, uint cmd, byte[] data);

        public static void Bind(IntPtr socketHandle, string name)
        {
            var addr = new byte[16];
            Array.Copy(BitConverter.GetBytes(29), 0, addr, 0, 4);
            Array.Copy(BitConverter.GetBytes(GetCanInterface(socketHandle, name)), 0, addr, 4, 4);
            var result = Bind(socketHandle, addr, addr.Length);
            if (result != 0)
                throw new Exception($"Bind failed (result: {result})");
        }

        public static void EnableCanFd(IntPtr socketHandle, bool enable)
        {
            // The adapter name should fit in the first 16 bytes
            var optval = BitConverter.GetBytes(enable ? 1 : 0);
            var result = SetSockOpt(socketHandle, (int)CanSocketOptionLevel.SOL_CAN_RAW, (int)CanRawSocketOptionValue.CAN_RAW_FD_FRAMES, optval, optval.Length);
            if (result != 0)
                throw new Exception($"Bind failed (result: {result})");
        }

        public static IntPtr CreateCanSocket() => CreateSocket(29, 3, 1);

        private static IntPtr CreateSocket(int addressFamily, int socketType, int protocolType)
        {
            var socketPtr = socket(addressFamily, socketType, protocolType);
            if (socketPtr < 0)
                throw new InvalidOperationException($"Failed to create socket error code {socketPtr} native error: {Marshal.GetLastWin32Error()}");

            return new IntPtr(socketPtr);
        }

        private static int GetCanInterface(IntPtr socketHandle, string adapterName)
        {
            var deviceNameBytes = Encoding.ASCII.GetBytes(adapterName);
            if (deviceNameBytes.Length > 15)
                throw new ArgumentException($"{nameof(adapterName)} to long should be max 15 chars, canAdapter", adapterName);

            // The adapter name should fit in the first 16 bytes
            var ioIn = new byte[32];
            Array.Copy(deviceNameBytes, ioIn, deviceNameBytes.Length);

            var result = Ioctl(socketHandle.ToInt32(), Siocgifindex, ioIn);
            if (result != 32)
                throw new InvalidOperationException($"IOControl failed (result: {result})");

            // The interface is at position 16-19
            return BitConverter.ToInt32(ioIn, 16);
        }
    }
}
