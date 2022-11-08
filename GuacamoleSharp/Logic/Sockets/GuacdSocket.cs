using GuacamoleSharp.Helpers;
using GuacamoleSharp.Models;
using Microsoft.AspNetCore.Identity;
using Serilog;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace GuacamoleSharp.Logic.Sockets
{
    internal sealed class GuacdSocket
    {
        private readonly ArraySegment<byte> _buffer;
        private readonly Guid _id;
        private readonly StringBuilder _overflowBuffer;
        private readonly IPEndPoint _endpoint;
        private readonly CancellationToken _shutdownToken;
        private Socket _socket = null!;

        public GuacdSocket(Guid id, IPEndPoint endpoint, CancellationToken shutdownToken)
        {
            _id = id;
            _endpoint = endpoint;
            _shutdownToken = shutdownToken;
            _buffer = new ArraySegment<byte>(new byte[1024]);
            _overflowBuffer = new StringBuilder();
        }

        public void Close()
        {
            try
            {
                try
                {
                    _socket.Shutdown(SocketShutdown.Both);
                }
                finally
                {
                    _socket.Close();
                }

                Log.Information("[{Id}] Guacd proxy socket closed.", _id);
            }
            catch (ObjectDisposedException)
            {
                Log.Warning("[{Id}] Guacd proxy socket is already disposed.", _id);
            }
            catch (Exception ex)
            {
                Log.Error("[{Id}] Error while closing guacd proxy socket: {Message}", _id, ex.Message);
            }
        }

        public async Task OpenConnectionAsync(Connection connection)
        {
            Log.Information("[{Id}] Attempting connection to guacd proxy at: {Hostname}:{Port}", _id, _endpoint.Address, _endpoint.Port);

            _socket = new Socket(_endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            await _socket.ConnectAsync(_endpoint);
            await SendAsync(ProtocolHelper.BuildProtocol("select", connection.Type));

            var request = await ReceiveAsync();

            Log.Debug("[{Id}] Attempting to resolve handshake: {Request}", _id, request);

            await SendAsync(ProtocolHelper.BuildProtocol("size", connection.Arguments["width"], connection.Arguments["height"], connection.Arguments["dpi"]));
            await SendAsync(ProtocolHelper.BuildProtocol("audio", "audio/L16", connection.Arguments["audio"]));
            await SendAsync(ProtocolHelper.BuildProtocol("video", connection.Arguments["video"]));
            await SendAsync(ProtocolHelper.BuildProtocol("image", "image/png", "image/jpeg", "image/webp", connection.Arguments["image"]));

            var reply = ProtocolHelper.BuildHandshakeReply(connection, request);

            await SendAsync(ProtocolHelper.BuildProtocol(reply));

            Log.Information("[{Id}] Handshake success. Connection ready.", _id);
        }

        public async Task<string> ReceiveAsync()
        {
            var done = false;

            do
            {
                var received = await _socket.ReceiveAsync(_buffer, SocketFlags.None, _shutdownToken);

                if (received > 0)
                {
                    var message = Encoding.UTF8.GetString(_buffer[0..received]);

                    _overflowBuffer.Append(message);

                    if (message.Contains(';'))
                    {
                        done = true;
                    }
                }          
            }
            while (!done);

            var reponse = _overflowBuffer.ToString();
            var (content, index) = ProtocolHelper.ReadProtocolUntilLastDelimiter(reponse);

            _overflowBuffer.Remove(0, index);

            return content;
        }

        public async Task SendAsync(string message)
        {
            Log.Debug("[{Id}] >>>C2G> {Message}", _id, message);

            var data = Encoding.UTF8.GetBytes(message);
            await _socket.SendAsync(data, SocketFlags.None, _shutdownToken);
        }
    }
}