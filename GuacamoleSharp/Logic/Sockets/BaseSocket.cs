using GuacamoleSharp.Helpers;
using System.Net.WebSockets;
using System.Text;

namespace GuacamoleSharp.Logic.Sockets
{
    public abstract class BaseSocket
    {
        protected readonly ArraySegment<byte> _buffer;
        protected readonly CancellationTokenSource _cts;
        protected readonly Guid _id;
        protected readonly StringBuilder _overflowBuffer;
        protected readonly WebSocket _socket;

        public BaseSocket(WebSocket socket, Guid id)
        {
            _socket = socket;
            _buffer = new ArraySegment<byte>(new byte[1024]);
            _overflowBuffer = new StringBuilder();
            _id = id;
            _cts = new CancellationTokenSource();
        }

        public abstract Task<bool> CloseAsync();

        public async Task<string> ReceiveAsync()
        {
            WebSocketReceiveResult result;

            do
            {
                result = await _socket.ReceiveAsync(_buffer, _cts.Token);
                if (result.Count > 0)
                {
                    _overflowBuffer.Append(Encoding.UTF8.GetString(_buffer[0..result.Count]));
                }
            }
            while (!result.EndOfMessage);

            var content = _overflowBuffer.ToString();
            var message = ProtocolHelper.ReadProtocolUntilLastDelimiter(content);

            if (message.index > 0)
            {
                _overflowBuffer.Remove(0, message.index);
            }

            return message.content;
        }

        public async Task SendAsync(string message)
        {
            var data = Encoding.UTF8.GetBytes(message);
            await _socket.SendAsync(new ArraySegment<byte>(data, 0, data.Length), WebSocketMessageType.Binary, true, _cts.Token);
        }
    }
}