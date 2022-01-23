using GuacamoleSharp.Common.Models;
using System.Net.Sockets;
using System.Text;

namespace GuacamoleSharp.Server
{
    internal class ConnectionState
    {
        #region Public Properties

        public byte[] ClientBuffer { get; } = new byte[1024];
        public StringBuilder ClientResponseOverflowBuffer { get; } = new();
        public Socket ClientSocket { get; internal set; } = null!;
        public Connection Connection { get; internal set; } = null!;
        public ulong ConnectionId { get; internal set; }
        public byte[] GuacdBuffer { get; } = new byte[256];
        public StringBuilder GuacdResponseOverflowBuffer { get; } = new();
        public Socket GuacdSocket { get; internal set; } = null!;
        public DateTime LastActivity { get; internal set; } = DateTime.Now;

        #endregion Public Properties
    }
}
