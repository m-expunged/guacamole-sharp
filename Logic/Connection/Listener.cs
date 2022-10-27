using GuacamoleSharp.Helpers;
using GuacamoleSharp.Logic.State;
using GuacamoleSharp.Models;
using Serilog;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;

namespace GuacamoleSharp.Logic
{
    public static class Listener
    {
        private static readonly ManualResetEvent _connectDone = new(false);
        private static readonly Regex _rxRequest = new(@"(GET|OPTIONS)..(.*?)HTTP", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static void Start()
        {
            Task.Run(() =>
            {
                var endpoint = new IPEndPoint(IPAddress.Any, OptionsHelper.Socket.Port);
                var socket = new Socket(endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                socket.Bind(endpoint);
                socket.Listen(1);

                while (true)
                {
                    _connectDone.Reset();

                    socket.BeginAccept(new AsyncCallback(AcceptCallback), socket);

                    _connectDone.WaitOne();
                }
            });
        }

        private static void AcceptCallback(IAsyncResult ar)
        {
            var socket = (Socket)ar.AsyncState!;

            try
            {
                var state = new ConnectionState();
                state.Client.Socket = socket.EndAccept(ar);
                state.Client.Socket.BeginReceive(state.Client.Buffer, 0, state.Client.Buffer.Length, 0, new AsyncCallback(ConnectCallback), state);
            }
            catch (ObjectDisposedException)
            {
                Log.Warning("Accept callback attempted to perform operation on disposed listener.");
            }
        }

        private static void ConnectCallback(IAsyncResult ar)
        {
            var state = (ConnectionState)ar.AsyncState!;

            try
            {
                if (state.Timeout) throw new Exception($"[Connection {state.ConnectionId}] Timeout.");

                int receivedLength = state.Client.Socket.EndReceive(ar);

                if (receivedLength > 0)
                {
                    var request = Encoding.UTF8.GetString(state.Client.Buffer);
                    var queryMatches = _rxRequest.Matches(request);
                    var queryString = queryMatches[0].Groups[2].Value.Trim();
                    var query = HttpUtility.ParseQueryString(queryString);
                    var token = query["token"] ?? throw new Exception($"[Connection {state.ConnectionId}] Connection request is missing required token query param.");
                    var plainText = TokenEncrypter.DecryptString(OptionsHelper.Socket.Password, token);
                    var connection = JsonSerializer.Deserialize<Connection>(plainText, new JsonSerializerOptions() { PropertyNameCaseInsensitive = true }) ?? throw new Exception($"[Connection {state.ConnectionId}] Connection token failed to serialize.");

                    connection.Type = connection.Type.ToLowerInvariant();

                    foreach (var arg in OptionsHelper.Client.DefaultArguments[connection.Type])
                    {
                        if (!connection.Arguments.ContainsKey(arg.Key))
                        {
                            connection.Arguments.Add(arg.Key, arg.Value);
                        }
                    }

                    var paramKeys = query.AllKeys
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Intersect(OptionsHelper.Client.UnencryptedArguments[connection.Type])
                        .ToList();

                    foreach (var key in paramKeys)
                    {
                        if (string.IsNullOrWhiteSpace(query[key])) continue;

                        connection.Arguments[key!] = query[key];
                    }

                    state.Connection = connection;
                    state.LastActivity = DateTimeOffset.Now;

                    Guacd.Start(state);

                    string response = WebSocketHelpers.BuildHttpUpgradeResponseFromRequest(request);
                    Client.Send(state, response, false);
                    state.Client.HandshakeDone.Set();

                    Client.Start(state);

                    _connectDone.Set();
                }
                else
                {
                    state.Client.Socket.BeginReceive(state.Client.Buffer, 0, state.Client.Buffer.Length, SocketFlags.None, new AsyncCallback(ConnectCallback), state);
                }
            }
            catch (Exception ex)
            {
                Log.Error("[Connection {Id}] {Message}", state.ConnectionId, ex.Message);

                Client.Close(state);
                _connectDone.Set();
                return;
            }
        }
    }
}