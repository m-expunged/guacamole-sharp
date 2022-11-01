using GuacamoleSharp.Helpers;
using GuacamoleSharp.Models;
using Serilog;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace GuacamoleSharp.Logic.Sockets
{
    public class GuacdSocket
    {
        protected readonly ArraySegment<byte> _buffer;
        protected readonly Guid _id;
        protected readonly StringBuilder _overflowBuffer;
        private readonly IPEndPoint _endpoint;
        private Socket _socket = null!;

        public GuacdSocket(Guid id, IPEndPoint endpoint)
        {
            _endpoint = endpoint;
            _buffer = new ArraySegment<byte>(new byte[1024]);
            _overflowBuffer = new StringBuilder();
            _id = id;
        }

        public bool Close()
        {
            try
            {
                _socket.Shutdown(SocketShutdown.Both);
                _socket.Close();

                Log.Information("[{Id}] Guacd proxy socket closed.", _id);

                return true;
            }
            catch (ObjectDisposedException)
            {
                Log.Warning("[{Id}] Guacd proxy socket is already disposed.", _id);

                return true;
            }
            catch (Exception ex)
            {
                Log.Error("[{Id}] Error while closing guacd proxy socket: {Message}", _id, ex.Message);

                return false;
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
        }

        public async Task<string> ReceiveAsync()
        {
            var done = false;
            (string content, int index) message;

            do
            {
                var received = await _socket.ReceiveAsync(_buffer, SocketFlags.None);

                if (received > 0)
                {
                    _overflowBuffer.Append(Encoding.UTF8.GetString(_buffer[0..received]));
                }

                var reponse = _overflowBuffer.ToString();
                message = ProtocolHelper.ReadProtocolUntilLastDelimiter(reponse);

                if (message.content != string.Empty)
                {
                    done = true;
                }
            }
            while (!done);

            _overflowBuffer.Remove(0, message.index);

            return message.content;
        }

        public async Task SendAsync(string message)
        {
            Log.Debug("[{Id}] >>>C2G> {Message}", _id, message);

            var data = Encoding.UTF8.GetBytes(message);
            await _socket.SendAsync(data, SocketFlags.None);
        }
    }
}