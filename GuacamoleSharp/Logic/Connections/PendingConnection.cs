using System.Net.WebSockets;

namespace GuacamoleSharp.Logic.Connections
{
    internal sealed class PendingConnection
    {
        public PendingConnection()
        {
            Id = Guid.NewGuid();
        }

        public Dictionary<string, string> Arguments { get; set; } = null!;

        public TaskCompletionSource<bool> Complete { get; set; } = null!;

        public Guid Id { get; }

        public WebSocket Socket { get; set; } = null!;
    }
}