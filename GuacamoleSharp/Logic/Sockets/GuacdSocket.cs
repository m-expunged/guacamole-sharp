using GuacamoleSharp.Helpers;
using GuacamoleSharp.Models;
using Serilog;
using System.Net;
using System.Net.WebSockets;

namespace GuacamoleSharp.Logic.Sockets
{
    public class GuacdSocket : BaseSocket
    {
        private readonly IPEndPoint _endpoint;

        public GuacdSocket(ClientWebSocket socket, Guid id, IPEndPoint endpoint) : base(socket, id)
        {
            _endpoint = endpoint;
        }

        public override async Task<bool> CloseAsync()
        {
            try
            {
                Log.Information("[{Id}] Attemping to close guacd proxy socket...", _id);

                _cts.Cancel();
                await _socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, String.Empty, CancellationToken.None);

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
                Log.Error("[{Id}] Error while closing guacd proxy socket: {ex}", _id, ex.Message);

                return false;
            }
        }

        public async Task OpenConnectionAsync(Connection connection)
        {
            await ((ClientWebSocket)_socket).ConnectAsync(new Uri($"ws://{_endpoint.Address}:{_endpoint.Port}"), CancellationToken.None);

            await SendAsync(ProtocolHelper.BuildProtocol("select", connection.Type));

            var request = await ReceiveAsync();

            if (request == null)
            {
                throw new Exception("Guacd proxy failed to send handshake request.");
            }

            Log.Debug("[Connection {Id}] Attempting to resolve handshake: {Request}", _id, request);

            await SendAsync(ProtocolHelper.BuildProtocol("size", connection.Arguments["width"], connection.Arguments["height"], connection.Arguments["dpi"]));
            await SendAsync(ProtocolHelper.BuildProtocol("audio", "audio/L16", connection.Arguments["audio"]));
            await SendAsync(ProtocolHelper.BuildProtocol("video", connection.Arguments["video"]));
            await SendAsync(ProtocolHelper.BuildProtocol("image", "image/png", "image/jpeg", "image/webp", connection.Arguments["image"]));

            var reply = ProtocolHelper.BuildHandshakeReply(connection, request);

            await SendAsync(ProtocolHelper.BuildProtocol(reply));
        }
    }
}