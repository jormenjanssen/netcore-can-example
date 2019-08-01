using System.Net.Sockets;

namespace Riwo.Rimote.VirtualCan
{
    public interface ICanInterface
    {
        Socket ControllerAreaNetworkSocket { get; }
    }
}
