using Serilog;
using System.Net.WebSockets;

namespace GuacamoleSharp.Logic.Sockets
{
    public class ClientSocket : BaseSocket
    {
        public ClientSocket(WebSocket socket, Guid id) : base(socket, id)
        {
        }

        public override async Task<bool> CloseAsync()
        {
            try
            {
                Log.Information("[{Id}] Attemping to close client socket...", _id);

                _cts.Cancel();
                await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, String.Empty, CancellationToken.None);

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
    }
}