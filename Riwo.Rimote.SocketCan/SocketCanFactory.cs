using System;
using System.Net.Sockets;
using Riwo.Rimote.SocketCan.Internal;
using BindingFlags = System.Reflection.BindingFlags;

namespace Riwo.Rimote.SocketCan
{
    public sealed class SocketCanFactory
    {
        public Socket CreateSocket(string adapterName)
        {
            if (Environment.OSVersion.Platform != PlatformID.Unix)
                throw new PlatformNotSupportedException("Only supported on Unix");

            var socketType = typeof(Socket);

            // Workaround for .Net core 2.0+ (tested on: NETCORE 2.2)
            var safeSocketCloseType = socketType.Assembly.GetType("System.Net.Sockets.SafeCloseSocket");
            if (safeSocketCloseType != null)
                return CreateSocketNetCore2_x(socketType, safeSocketCloseType, adapterName);

            // Workaround for .Net core 3.0+
            var safeSocketHandleType = socketType.Assembly.GetType("SafeSocketHandle");
            if (safeSocketHandleType != null)
                return CreateSocketNetCore3_x(socketType, safeSocketHandleType, adapterName);

            throw new PlatformNotSupportedException("Invalid NETCore version workarounds could not be applied");
        }

        private Socket CreateSocketNetCore2_x(Type socketType, Type safeSocketType, string name)
        {
            var socketConstructor = socketType.GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, new[] {safeSocketType}, null);
            var socketMethod = safeSocketType.GetMethod("CreateSocket", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(IntPtr) }, null);

            if(socketConstructor == null || socketMethod == null)
                throw new PlatformNotSupportedException("Failed to create socket on platform missing required constructors");

            // Todo: check cleanup.. when calls below fail
            var nativeSocket = NativeMethods.CreateCanSocket();
            var safeSocketHandle = socketMethod.Invoke(null, new object[] {nativeSocket});
            var socket = (Socket) socketConstructor.Invoke(new[] {safeSocketHandle} );

            
            InitializeSocketCanForAdaper(nativeSocket, name);
            
            return socket;
        }

        private Socket CreateSocketNetCore3_x(Type socketType, Type safeSocketType, string name)
        {
            throw new PlatformNotSupportedException("NETCore version >= 3 is currently not supported");
        }

        private void InitializeSocketCanForAdaper(IntPtr socketHandlePtr, string name)
        {
            NativeMethods.EnableCanFd(socketHandlePtr, true);
            NativeMethods.Bind(socketHandlePtr, name);
        }

    }
}
