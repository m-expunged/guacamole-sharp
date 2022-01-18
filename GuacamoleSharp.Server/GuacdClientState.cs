using System.Net.Sockets;
using System.Text;

namespace GuacamoleSharp.Server
{
    internal class GuacdClientState
    {
        #region Private Fields

        private const int _bufferSize = 256;

        #endregion Private Fields

        #region Public Properties

        public byte[] Buffer { get; } = new byte[_bufferSize];

        public int BufferSize { get; } = _bufferSize;

        public StringBuilder Data { get; } = new StringBuilder();

        public Socket Socket { get; set; } = null!;

        #endregion Public Properties
    }
}
