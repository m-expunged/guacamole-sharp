using GuacamoleSharp.Helpers;
using GuacamoleSharp.Logic.Sockets;
using GuacamoleSharp.Models;
using GuacamoleSharp.Options;
using Microsoft.Extensions.Options;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text.Json;

namespace GuacamoleSharp.Logic.Connections
{
    internal sealed class ConnectionProcessorService : BackgroundService
    {
        private static readonly ManualResetEvent _idle;
        private static readonly ConcurrentQueue<PendingConnection> _pendingConnections;
        private static readonly SemaphoreSlim _processing;
        private static readonly CancellationTokenSource _shutdownTokenSource;
        private readonly ClientOptions _clientOptions;
        private readonly GuacamoleSharpOptions _guacamoleSharpOptions;
        private readonly GuacdOptions _guacdOptions;

        static ConnectionProcessorService()
        {
            _pendingConnections = new ConcurrentQueue<PendingConnection>();
            _idle = new ManualResetEvent(false);
            _processing = new SemaphoreSlim(1, 1);
            _shutdownTokenSource = new CancellationTokenSource();
        }

        public ConnectionProcessorService(IOptions<ClientOptions> clientOptions, IOptions<GuacamoleSharpOptions> guacamoleSharpOptions, IOptions<GuacdOptions> guacdOptions)
        {
            _clientOptions = clientOptions.Value;
            _guacamoleSharpOptions = guacamoleSharpOptions.Value;
            _guacdOptions = guacdOptions.Value;
        }

        public static async Task AddAsync(WebSocket socket, Dictionary<string, string> arguments, TaskCompletionSource complete)
        {
            // wait until previous sockets have finished processing
            await _processing.WaitAsync(_shutdownTokenSource.Token);

            try
            {
                _pendingConnections.Enqueue(new PendingConnection
                {
                    Socket = socket,
                    Arguments = arguments,
                    Complete = complete
                });

                // signal that sockets need processing
                _idle.Set();
            }
            finally
            {
                // sockets added, allow processing or adding of more sockets
                _processing.Release();
            }
        }

        public override async Task StopAsync(CancellationToken stoppingToken)
        {            
            // signal shutdown
            _shutdownTokenSource.Cancel();
            // wake up processing loop from idle state to allow shutdown
            _idle.Set();

            await Task.Delay(5000, CancellationToken.None);

            await base.StopAsync(stoppingToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Yield();

            while (!stoppingToken.IsCancellationRequested)
            {
                // wait until sockets need processing to prevent cpu spam
                _idle.WaitOne();
                // wait until sockets have been added for processing
                await _processing.WaitAsync(_shutdownTokenSource.Token);

                try
                {
                    while (_pendingConnections.TryDequeue(out var pendingConnection))
                    {
                        Connection connection;
                        IPEndPoint endpoint;

                        try
                        {
                            connection = GetConnectionConfiguration(pendingConnection);
                            endpoint = GetProxyEndPoint();
                        }
                        catch (Exception ex)
                        {
                            Log.Error("[{Id}] {Message}.", pendingConnection.Id, ex.Message);
                            Log.Information("[{Id}] Closing connection...", pendingConnection.Id);
                            await pendingConnection.Socket.CloseAsync(WebSocketCloseStatus.InternalServerError, string.Empty, CancellationToken.None);
                            pendingConnection.Complete.TrySetResult();
                            continue;
                        }

                        var client = new ClientSocket(pendingConnection.Id, pendingConnection.Socket, _shutdownTokenSource.Token);
                        var guacd = new GuacdSocket(pendingConnection.Id, endpoint, _shutdownTokenSource.Token);
                        var tunnel = new Tunnel(pendingConnection.Id, connection, client, guacd, pendingConnection.Complete, _shutdownTokenSource.Token); ;
                        await tunnel.OpenAsync();                   
                    }

                    // processing finished, enter idle state
                    _idle.Reset();
                }
                finally
                {
                    // processing finished, allow new sockets to be added
                    _processing.Release();
                }
            }

            await Task.CompletedTask;
        }

        private Connection GetConnectionConfiguration(PendingConnection pendingConnection)
        {
            try
            {
                Log.Information("[{Id}] Building connection configuration...", pendingConnection.Id);

                var token = pendingConnection.Arguments["token"];
                var plainText = TokenEncryptionHelper.DecryptString(_guacamoleSharpOptions.Password, token);
                var connection = JsonSerializer.Deserialize<Connection>(plainText, new JsonSerializerOptions() { PropertyNameCaseInsensitive = true }) ?? throw new Exception($"Connection token failed to serialize");
                connection.Type = connection.Type.ToLowerInvariant();

                foreach (var arg in _clientOptions.DefaultArguments[connection.Type])
                {
                    if (!connection.Arguments.ContainsKey(arg.Key))
                    {
                        connection.Arguments.Add(arg.Key, arg.Value);
                    }
                }

                var paramKeys = pendingConnection.Arguments.Keys
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Intersect(_clientOptions.UnencryptedArguments[connection.Type])
                    .ToList();

                foreach (var key in paramKeys)
                {
                    if (string.IsNullOrWhiteSpace(pendingConnection.Arguments[key])) continue;

                    connection.Arguments[key] = pendingConnection.Arguments[key];
                }

                Log.Debug("[{Id}] Connection configuration: {@Connection}", pendingConnection.Id, connection);

                return connection;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to configure connection: {ex.Message}");
            }
        }

        private IPEndPoint GetProxyEndPoint()
        {
            try
            {
                if (!IPAddress.TryParse(_guacdOptions.Hostname, out IPAddress? ip) || ip == null)
                {
                    ip = Dns.GetHostAddresses(_guacdOptions.Hostname).First(x => x.AddressFamily == AddressFamily.InterNetwork);
                }

                return new IPEndPoint(ip, _guacdOptions.Port);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to configure endpoint: {ex.Message}");
            }
        }
    }
}
