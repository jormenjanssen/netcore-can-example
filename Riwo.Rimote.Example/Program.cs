using Riwo.Rimote.SocketCan;
using Riwo.Rimote.VirtualCan;
using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Riwo.Rimote.Example
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Starting application");
            await RunCanLoopAsync(new CancellationToken()).ConfigureAwait(false);
            Console.WriteLine("Ended application gracefully");
        }

        static async Task RunCanLoopAsync(CancellationToken cancellationToken)
        {
            var factory = new SocketCanFactory();

            var incomingBuffer = new byte[16];
            var incomingFrame = new CanFrame();

            using (var socket = factory.CreateSocket("can0"))
            {
                while (true)
                {
                    await socket.ReceiveAsync(incomingBuffer, SocketFlags.None, CancellationToken.None).ConfigureAwait(false);
                    incomingFrame.Reinitialize(incomingBuffer);

                    try
                    {
                        await socket.SendAsync(new CanFrame {IsExtendedFrame = true, Id = incomingFrame.Id - 1, DataLength = 4}.FrameBytes, SocketFlags.None).ConfigureAwait(false);
                    }
                    catch (SocketException se) when (se.SocketErrorCode == SocketError.NoBufferSpaceAvailable)
                    {
                        Console.WriteLine("No buffer space available waiting a few ms");
                        await Task.Delay(TimeSpan.FromMilliseconds(10), cancellationToken).ConfigureAwait(false);
                    }
                }
            }
        }
    }
}
