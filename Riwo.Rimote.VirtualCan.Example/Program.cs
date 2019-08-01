using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Riwo.Rimote.VirtualCan.Linux;

namespace Riwo.Rimote.VirtualCan.Example
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // To run this example make sure your can bus is up and running (i use the network interface can0 (FlexCan) on an imx6 processor for testing but x86 should definitely work)
            // You also could use VCAN software emulation see: https://elinux.org/Bringing_CAN_interface_up#Virtual_Interfaces
            // For testing i recommend using the linux can-utils (candump, cansend, etc.) they should be in modern distro's otherwise: https://github.com/linux-can/can-utils 

            // First configure the bitrate (I used 250K for testing): "ip link set can0 type can bitrate 250000"
            // Then set the canbus up: "ip link set can0 up"
            // Last but not least verify it's up and running by: "ip link show can0" this gives me the following result on my test system:
            // 3: can0: <NOARP,UP,LOWER_UP,ECHO> mtu 16 qdisc pfifo_fast state UNKNOWN mode DEFAULT group default qlen 10 link / ca

            await SimpleLoopBackAsync("can0", CancellationToken.None).ConfigureAwait(false);
        }

        static async Task SimpleLoopBackAsync(string adapter, CancellationToken cancellationToken)
        {
            var factory = new SocketCanFactory();
            var incomingBuffer = new byte[CanFrame.FrameLength];

            using var socket = factory.CreateSocket(adapter);
            while (true)
            {
                await socket.ReceiveAsync(incomingBuffer, SocketFlags.None, CancellationToken.None).ConfigureAwait(false);
                var incomingFrame = new CanFrame(incomingBuffer);

                try
                {
                    await socket.SendAsync(new CanFrame {IsExtendedFrame = true, Id = incomingFrame.Id - 1, DataLength = 4}.FrameBytes, SocketFlags.None).ConfigureAwait(false);
                }
                catch (SocketException se) when (se.SocketErrorCode == SocketError.NoBufferSpaceAvailable)
                {
                    // Default the buffer space is only 10 message you can extend this on Linux or wait a while before retransmitting.
                    // RUN: "ip link set can0 txqueuelen 1000" to increase size to 1000 messages where can0 is the name of the SocketCAN interface in Linux

                    Console.WriteLine("No buffer space available waiting a few ms");
                    await Task.Delay(TimeSpan.FromMilliseconds(10), cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }
}
