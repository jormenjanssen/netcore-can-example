using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Riwo.Rimote.VirtualCan.Linux;

namespace Riwo.Rimote.VirtualCan.Example
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // This is an example application for .Net Core 3+ to support SocketCAN with CAN 2.0B.
            // For CAN-FD (Flexible data rate) some small changes are required which are not included in this example.

            // To run this example make sure your CANBus is up and running (for testing i used network interface can0 (FlexCan interface off) an NXP Imx6 processor for testing but X86/X64/ARM64 should definitely work)
            // You also could use VCAN software emulation see: https://elinux.org/Bringing_CAN_interface_up#Virtual_Interfaces
            // For testing i recommend using the linux can-utils (candump, cansend, etc.), they should be available in modern distro's otherwise: https://github.com/linux-can/can-utils 
            // I tested this application with .Net Core 3 Preview 7 on a Poky (Thud) based Yocto image on an Toradex Colibri IMX6 DL and Colibri Imx6ULL SOM

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
            {
                Console.WriteLine($"Created adapter: {adapter}");

                while (true)
                {
                    await socket.ReceiveAsync(incomingBuffer, SocketFlags.None, CancellationToken.None).ConfigureAwait(false);

                    var incomingFrame = new CanFrame(incomingBuffer);
                    Console.WriteLine($"Received {nameof(CanFrame)} Id: {incomingFrame.Id} IsRtr: {incomingFrame.IsRemoteTransmissionRequest} Length: {incomingFrame.DataLength} IsError: {incomingFrame.IsErrorMessage} Data: {ByteArrayToString(incomingFrame.Data)}");

                    try
                    {
                        var sendFrame = new CanFrame {IsExtendedFrame = true, Id = incomingFrame.Id - 1, DataLength = 4};

                        sendFrame.Data[0] = 1;
                        sendFrame.Data[1] = 2;
                        sendFrame.Data[2] = 3;
                        sendFrame.Data[3] = 4;

                        await socket.SendAsync(sendFrame.FrameBytes, SocketFlags.None).ConfigureAwait(false);
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

        private static string ByteArrayToString(Span<byte> ba)
        {
            var hex = new StringBuilder(ba.Length * 2);
            foreach (var b in ba)
                hex.AppendFormat("{0:x2}", b);
            return hex.ToString();
        }
    }
}
