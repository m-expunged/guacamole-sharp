﻿using GuacamoleSharp.Helpers;
using Serilog;
using System.Net.WebSockets;
using System.Text;

namespace GuacamoleSharp.Logic.Sockets
{
    internal sealed class ClientSocket
    {
        #region Private Fields

        private readonly ArraySegment<byte> _buffer;
        private readonly CancellationTokenSource _cts;
        private readonly Guid _id;
        private readonly StringBuilder _overflowBuffer;
        private readonly WebSocket _socket;

        #endregion Private Fields

        #region Public Constructors

        public ClientSocket(Guid id, WebSocket socket)
        {
            _socket = socket;
            _cts = new CancellationTokenSource();
            _buffer = new ArraySegment<byte>(new byte[1024]);
            _overflowBuffer = new StringBuilder();
            _id = id;
        }

        #endregion Public Constructors

        #region Public Methods

        public async Task<bool> CloseAsync()
        {
            try
            {
                _cts.Cancel();

                if (_socket.State == WebSocketState.Open)
                {
                    await _socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, String.Empty, CancellationToken.None);
                }

                Log.Information("[{Id}] Client socket closed.", _id);

                return true;
            }
            catch (ObjectDisposedException)
            {
                Log.Warning("[{Id}] Client socket is already disposed.", _id);

                return true;
            }
            catch (Exception ex)
            {
                Log.Error("[{Id}] Error while closing client socket: {ex}", _id, ex);

                return false;
            }
        }

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
            Log.Debug("[{Id}] >>>G2C> {Message}", _id, message);

            var data = Encoding.UTF8.GetBytes(message);
            await _socket.SendAsync(new ArraySegment<byte>(data, 0, data.Length), WebSocketMessageType.Text, true, _cts.Token);
        }

        #endregion Public Methods
    }
}
