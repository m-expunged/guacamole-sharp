using GuacamoleSharp.Models;
using Serilog;

namespace GuacamoleSharp.Logic.Sockets
{
    internal sealed class Tunnel
    {
        private readonly ClientSocket _client;
        private readonly TaskCompletionSource<bool> _complete;
        private readonly Connection _connection;
        private readonly CancellationTokenSource _cts;
        private readonly GuacdSocket _guacd;
        private readonly Guid _id;

        public Tunnel(Guid id, Connection connection, ClientSocket client, GuacdSocket guacd, TaskCompletionSource<bool> complete)
        {
            _id = id;
            _connection = connection;
            _client = client;
            _guacd = guacd;
            _complete = complete;
            _cts = new CancellationTokenSource();
        }

        public async Task CloseAsync()
        {
            _cts.Cancel();

            var r1 = _guacd.Close();
            var r2 = await _client.CloseAsync();

            _complete.TrySetResult(r1 && r2);
        }

        public async Task OpenAsync()
        {
            var ready = await _guacd.OpenConnectionAsync(_connection);
            await _client.SendAsync(ready);

            var ct = _cts.Token;

            _ = Task.Run(async () =>
            {
                while (!ct.IsCancellationRequested)
                {
                    var message = await _client.ReceiveAsync();

                    if (message.Contains("10.disconnect;"))
                    {
                        await CloseAsync();
                        return;
                    }

                    await _guacd.SendAsync(message);
                }
            }).ContinueWith(t => HandleError(t), CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);


            _ = Task.Run(async () =>
            {
                while (!ct.IsCancellationRequested)
                {
                    var message = await _guacd.ReceiveAsync();

                    if (message.Contains("10.disconnect;"))
                    {
                        await CloseAsync();
                        return;
                    }

                    await _client.SendAsync(message);
                }
            }).ContinueWith(t => HandleError(t), CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
        }

        private async Task HandleError(Task t)
        {
            Log.Error("[{Id}] Socket faulted unexpectedly: {Message}", _id, t.Exception?.Message);
            await CloseAsync();
        }
    }
}
