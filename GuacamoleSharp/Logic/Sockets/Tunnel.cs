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

            var r1 = await _guacd.CloseAsync();
            var r2 = await _client.CloseAsync();

            _complete.TrySetResult(r1 && r2);
        }

        public async Task OpenAsync()
        {
            await _guacd.OpenConnectionAsync(_connection);

            var ct = _cts.Token;

            _ = Task.Run(() => DoProxyToClientAsync(ct), ct);
            _ = Task.Run(() => DoClientToProxyAsync(ct), ct);
        }

        private async Task DoClientToProxyAsync(CancellationToken ct)
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
        }

        private async Task DoProxyToClientAsync(CancellationToken ct)
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
        }
    }
}