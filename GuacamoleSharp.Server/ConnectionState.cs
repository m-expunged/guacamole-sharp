using GuacamoleSharp.Common.Models;
using System.Net.Sockets;
using System.Text;

namespace GuacamoleSharp.Server
{
    internal class ConnectionState
    {
        #region Public Properties

        public byte[] ClientBuffer { get; } = new byte[1024];

        public ManualResetEvent ClientHandshakeDone { get; set; } = new(false);

        public ManualResetEvent ClientReceiveDone { get; set; } = new(false);

        public StringBuilder ClientResponseOverflowBuffer { get; } = new();

        public ManualResetEvent ClientSendDone { get; set; } = new(false);

        public Socket ClientSocket { get; internal set; } = null!;

        public bool Closed { get; set; } = false;

        public Connection Connection { get; internal set; } = null!;

        public ulong ConnectionId { get; internal set; }

        public byte[] GuacdBuffer { get; } = new byte[1024];

        public ManualResetEvent GuacdHandshakeDone { get; } = new(false);

        public ManualResetEvent GuacdReceiveDone { get; set; } = new(false);

        public StringBuilder GuacdResponseOverflowBuffer { get; } = new();

        public ManualResetEvent GuacdSendDone { get; set; } = new(false);

        public Socket GuacdSocket { get; internal set; } = null!;

        public DateTime LastActivity { get; internal set; } = DateTime.Now;

        #endregion Public Properties
    }
}
