using GuacamoleSharp.Models;

namespace GuacamoleSharp.Logic.Sockets
{
    public class Tunnel
    {
        private readonly ClientSocket _client;
        private readonly TaskCompletionSource<bool> _complete;
        private readonly Connection _connection;
        private readonly CancellationTokenSource _cts;
        private readonly GuacdSocket _guacd;

        public Tunnel(Connection connection, ClientSocket client, GuacdSocket guacd, TaskCompletionSource<bool> complete)
        {
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
            await _guacd.OpenConnectionAsync(_connection);

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
            }, ct);

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
            }, ct);
        }
    }
}