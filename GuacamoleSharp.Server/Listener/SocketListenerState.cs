using System.Net.Sockets;
using System.Text;

namespace GuacamoleSharp.Server.Listener
{
    internal class SocketListenerState
    {
        #region Private Fields

        private const int _bufferSize = 1024;

        #endregion Private Fields

        #region Public Properties

        public byte[] Buffer { get; } = new byte[_bufferSize];

        public int BufferSize { get; } = _bufferSize;

        public StringBuilder Data { get; set; } = new();

        public Socket WorkSocket { get; set; } = null!;

        #endregion Public Properties
    }
}
