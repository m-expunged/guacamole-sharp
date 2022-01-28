using GuacamoleSharp.Common.Models;

namespace GuacamoleSharp.Server
{
    internal class ConnectionState
    {
        #region Public Properties

        public SocketState Client { get; } = new();

        public Connection Connection { get; set; } = null!;

        public ulong ConnectionId { get; set; }

        public object DisposeLock { get; } = new();

        public SocketState Guacd { get; } = new();

        public DateTime LastActivity { get; set; } = DateTime.Now;

        #endregion Public Properties
    }
}
