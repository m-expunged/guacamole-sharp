using System.Net.Sockets;
using System.Text;

namespace GuacamoleSharp.Logic.State
{
    public class SocketState
    {
        public byte[] Buffer { get; } = new byte[1024];

        public bool Closed { get; set; } = false;

        public ManualResetEvent HandshakeDone { get; set; } = new(false);

        public StringBuilder OverflowBuffer { get; } = new();

        public ManualResetEvent ReceiveDone { get; set; } = new(false);

        public ManualResetEvent SendDone { get; set; } = new(false);

        public Socket Socket { get; set; } = null!;
    }
}