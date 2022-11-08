using GuacamoleSharp.Logic.Connections;
using GuacamoleSharp.Models;
using Serilog;
using System.Net.Sockets;

namespace GuacamoleSharp.Logic.Sockets
{
    internal sealed class Tunnel
    {
        private readonly ClientSocket _client;
        private readonly TaskCompletionSource _complete;
        private readonly Connection _connection;
        private readonly CancellationToken _shutdownToken;
        private readonly GuacdSocket _guacd;
        private readonly Guid _id;
        private readonly object _shutdownLock;
        private bool _shutdownTriggered;

        public Tunnel(Guid id, Connection connection, ClientSocket client, GuacdSocket guacd, TaskCompletionSource complete, CancellationToken shutdownToken)
        {
            _id = id;
            _connection = connection;
            _client = client;
            _guacd = guacd;
            _complete = complete;
            _shutdownToken = shutdownToken;
            _shutdownLock = new object();
            _shutdownTriggered = false;
        }

        public async Task CloseAsync()
        {
            _guacd.Close();
            await _client.CloseAsync();

            _complete.TrySetResult();
        }

        public async Task OpenAsync()
        {
            try
            {
                await _guacd.OpenConnectionAsync(_connection);
            }
            catch (Exception ex)
            {
                Log.Error("[{Id}] Error during handshake phase: {Message}.", _id, ex.Message);
                Log.Information("[{Id}] Closing connection...", _id);

                await CloseAsync();
                return;
            }

            _ = Task.Run(async () =>
            {
                while (!_shutdownToken.IsCancellationRequested)
                {          
                    var message = await _client.ReceiveAsync();

                    if (message.Contains("10.disconnect;"))
                    {
                        break;
                    }

                    await _guacd.SendAsync(message);
                }
            }).ContinueWith(t => HandleShutdown(t), CancellationToken.None, TaskContinuationOptions.RunContinuationsAsynchronously, TaskScheduler.Default);


            _ = Task.Run(async () =>
            {
                while (!_shutdownToken.IsCancellationRequested)
                {
                    var message = await _guacd.ReceiveAsync();

                    if (message.Contains("10.disconnect;"))
                    {
                        break;
                    }

                    await _client.SendAsync(message);
                }
            }).ContinueWith(t => HandleShutdown(t), CancellationToken.None, TaskContinuationOptions.RunContinuationsAsynchronously, TaskScheduler.Default);
        }

        private async Task HandleShutdown(Task t)
        {
            lock (_shutdownLock)
            {
                if (_shutdownTriggered)
                {
                    return;
                }
                else
                {
                    _shutdownTriggered = true;
                }
            }

            if (t.Exception != null)
            {
                if (t.Exception.InnerException is SocketException ex && ex.SocketErrorCode == SocketError.OperationAborted)
                {
                    Log.Warning("[{Id}] Socket operation has been aborted.", _id);
                }
                else
                {
                    Log.Error("[{Id}] Sockets faulted unexpectedly: {Message}", _id, t.Exception.Message);
                }
            }

            Log.Information("[{Id}] Closing connection...", _id);

            await CloseAsync();
        }
    }
}
