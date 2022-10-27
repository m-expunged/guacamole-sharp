using GuacamoleSharp.Helpers;
using GuacamoleSharp.Models;

namespace GuacamoleSharp.Logic.State
{
    public class ConnectionState
    {
        public SocketState Client { get; } = new();

        public Connection Connection { get; set; } = null!;

        public Guid ConnectionId { get; set; } = Guid.NewGuid();

        public object DisposeLock { get; } = new();

        public SocketState Guacd { get; } = new();

        public DateTimeOffset LastActivity { get; set; } = DateTimeOffset.Now;

        public bool Timeout => DateTimeOffset.Now > LastActivity.AddMinutes(OptionsHelper.Socket.MaxInactivityAllowedInMin);
    }
}