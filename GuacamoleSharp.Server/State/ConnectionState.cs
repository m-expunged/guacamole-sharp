using GuacamoleSharp.Common.Models;

namespace GuacamoleSharp.Server
{
    internal class ConnectionState
    {
        #region Public Properties

        public SocketState Client { get; } = new();

        public Connection Connection { get; internal set; } = null!;

        public ulong ConnectionId { get; internal set; }

        public object DisposeLock { get; } = new();

        public SocketState Guacd { get; } = new();

        public DateTime LastActivity { get; internal set; } = DateTime.Now;

        #endregion Public Properties
    }
}
