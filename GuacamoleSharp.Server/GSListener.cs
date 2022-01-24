using GuacamoleSharp.Common.Models;
using GuacamoleSharp.Common.Settings;
using Serilog;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;

namespace GuacamoleSharp.Server
{
    internal static class GSListener
    {
        #region Private Fields

        private static readonly ManualResetEvent _connectDone = new(false);
        private static readonly ILogger _logger = Log.ForContext(typeof(GSListener));
        private static readonly ManualResetEvent _receiveDone = new(false);
        private static readonly Regex _rxQuery = new(@"GET...(.*?)HTTP", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex _rxSwk = new("Sec-WebSocket-Key: (.*)");
        private static readonly ManualResetEvent _sendDone = new(false);
        private static ulong _connectionCount = 0;
        private static GSSettings _gssettings = null!;
        private static BackgroundWorker _listenerThread = new();

        #endregion Private Fields

        #region Internal Methods

        internal static void Close(ConnectionState state)
        {
            try
            {
                _logger.Information("[Connection {Id}] Closing client connection", state.ConnectionId);

                if (state.ClientSocket != null)
                {
                    state.ClientSocket.Shutdown(SocketShutdown.Both);
                    state.ClientSocket.Close();
                }

                state.Closed = true;
            }
            catch (Exception ex)
            {
                _logger.Error("[Connection {id}] Error while closing client connection: {ex}", state.ConnectionId, ex);
            }
        }

        internal static void Send(ConnectionState state, string message)
        {
            _sendDone.Reset();

            _logger.Debug("[Connection {Id}] >>>G2W> {Message}", state.ConnectionId, message);

            byte[] data = Encoding.ASCII.GetBytes(message);
            state.ClientSocket.BeginSend(data, 0, data.Length, 0, new AsyncCallback(SendCallback), state);

            _sendDone.WaitOne();
        }

        internal static void StartListening(GSSettings gssettings)
        {
            _gssettings = gssettings;

            IPEndPoint endpoint = new(IPAddress.Any, _gssettings.WebSocket.Port);

            Socket listener = new(endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            listener.Bind(endpoint);

            _logger.Information("Socket listening on: {ipEndPoint}", endpoint);

            _listenerThread.WorkerReportsProgress = true;
            _listenerThread.WorkerSupportsCancellation = true;
            _listenerThread.DoWork += new DoWorkEventHandler(Listen_DoWork);
            _listenerThread.RunWorkerAsync(listener);
        }

        #endregion Internal Methods

        #region Private Methods

        private static void AcceptCallback(IAsyncResult ar)
        {
            Socket listener = (Socket)ar.AsyncState!;

            _connectionCount += 1;

            ConnectionState state = new();
            state.ClientSocket = listener.EndAccept(ar);
            state.ConnectionId = _connectionCount;

            state.ClientSocket.BeginReceive(state.ClientBuffer, 0, state.ClientBuffer.Length, SocketFlags.None, new AsyncCallback(ConnectCallback), state);
        }

        private static void AddDefaultConnectionSettings(this Connection connection)
        {
            if (!_gssettings.Client.ConnectionDefaultSettings.ContainsKey(connection.Type))
                return;

            foreach (var setting in _gssettings.Client.ConnectionDefaultSettings[connection.Type])
            {
                if (!connection.Settings.ContainsKey(setting.Key))
                {
                    connection.Settings.Add(setting.Key, setting.Value);
                }
            }
        }

        private static void ConnectCallback(IAsyncResult ar)
        {
            var state = (ConnectionState)ar.AsyncState!;

            if (DateTime.Now > state.LastActivity.AddMinutes(_gssettings.WebSocket.MaxInactivityAllowedInMin))
            {
                _logger.Warning("[Connection {Id}] Timeout", state.LastActivity);
                Close(state);
                return;
            }

            int receivedLength = state.ClientSocket.EndReceive(ar);

            if (receivedLength > 0)
            {
                var content = Encoding.ASCII.GetString(state.ClientBuffer);
                var queryMatches = _rxQuery.Matches(content);
                var queryString = queryMatches[0].Groups[1].Value.Trim();
                var query = HttpUtility.ParseQueryString(queryString);
                var token = query["token"];

                if (token == null)
                {
                    _logger.Warning("[Connection {Id}] Connection is missing the token query param", state.ConnectionId);
                    Close(state);
                    return;
                }

                var painText = TokenEncrypter.DecryptString(_gssettings.Token.Password, token);
                var connection = JsonSerializer.Deserialize<Connection>(painText, new JsonSerializerOptions()
                {
                    PropertyNameCaseInsensitive = true
                });

                if (connection == null)
                {
                    _logger.Warning("[Connection {Id}] Connection serialization returned null", state.ConnectionId);
                    Close(state);
                    return;
                }

                connection.Type = connection.Type.ToLowerInvariant();
                connection.AddDefaultConnectionSettings();
                connection.OverwriteWithUnencryptedConnectionSettings(query);

                state.Connection = connection;
                state.LastActivity = DateTime.Now;

                GSGuacdClient.Connect(_gssettings, connection, state);

                var swkMatches = _rxSwk.Match(content);
                var swk = swkMatches.Groups[1].Value.Trim();
                var swkSha1Base64 = Convert.ToBase64String(SHA1.Create().ComputeHash(Encoding.UTF8.GetBytes(swk + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
                var httpUpgradeResponse = "HTTP/1.1 101 Switching Protocols\r\n"
                    + "Upgrade: websocket\r\n"
                    + "Connection: Upgrade\r\n"
                    + "Sec-WebSocket-Protocol: guacamole\r\n"
                    + "Sec-WebSocket-Accept: " + swkSha1Base64 + "\r\n\r\n";

                Send(state, httpUpgradeResponse);

                state.ClientHandshakeDone.Set();

                BackgroundWorker _clientThread = new();
                _clientThread.DoWork += new DoWorkEventHandler(Receive_DoWork);
                _clientThread.RunWorkerAsync(state);

                _connectDone.Set();
            }
            else
            {
                state.ClientSocket.BeginReceive(state.ClientBuffer, 0, state.ClientBuffer.Length, SocketFlags.None, new AsyncCallback(ConnectCallback), state);
            }
        }

        private static void Listen_DoWork(object? sender, DoWorkEventArgs e)
        {
            Socket listener = (Socket)e.Argument!;
            listener.Listen(1);

            while (true)
            {
                _connectDone.Reset();

                listener.BeginAccept(new AsyncCallback(AcceptCallback), listener);

                _connectDone.WaitOne();
            }
        }

        private static void OverwriteWithUnencryptedConnectionSettings(this Connection connection, NameValueCollection query)
        {
            if (!_gssettings.Client.ConnectionAllowedUnencryptedSettings.ContainsKey(connection.Type))
                return;

            IEnumerable<string> validQueryProps = query.AllKeys
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x))
                .Where(x => query[x] != null && !string.IsNullOrWhiteSpace(query[x]))!;

            Dictionary<string, string> unencryptedConnectionSettings = validQueryProps
                .Where(x => _gssettings.Client.ConnectionAllowedUnencryptedSettings[connection.Type].Contains(x))
                .ToDictionary(x => x, x => query[x])!;

            foreach (var setting in unencryptedConnectionSettings)
            {
                if (connection.Settings.ContainsKey(setting.Key))
                {
                    connection.Settings[setting.Key] = setting.Value;
                }
                else
                {
                    connection.Settings.Add(setting.Key, setting.Value);
                }
            }
        }

        private static void Receive_DoWork(object? sender, DoWorkEventArgs e)
        {
            ConnectionState state = (ConnectionState)e.Argument!;

            state.GuacdHandshakeDone.WaitOne();

            try
            {
                while (!state.Closed)
                {
                    _receiveDone.Reset();

                    state.ClientSocket.BeginReceive(state.ClientBuffer, 0, state.ClientBuffer.Length, SocketFlags.None, new AsyncCallback(ReceiveCallback), state);

                    _receiveDone.WaitOne();
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Errow while running socket listener thread: {ex}", ex);
                GSGuacdClient.Close(state);
            }
        }

        private static void ReceiveCallback(IAsyncResult ar)
        {
            var state = (ConnectionState)ar.AsyncState!;
            var receivedLength = state.ClientSocket.EndReceive(ar);

            if (DateTime.Now > state.LastActivity.AddMinutes(_gssettings.WebSocket.MaxInactivityAllowedInMin))
            {
                _logger.Warning("[Connection {Id}] Timeout", state.LastActivity);
                GSGuacdClient.Close(state);
                return;
            }

            if (receivedLength <= 0)
                return;

            state.ClientResponseOverflowBuffer.Append(Encoding.ASCII.GetString(state.ClientBuffer[0..receivedLength]));
            string reponse = state.ClientResponseOverflowBuffer.ToString();

            if (!reponse.Contains(';'))
            {
                state.ClientSocket.BeginReceive(state.ClientBuffer, 0, state.ClientBuffer.Length, SocketFlags.None, new AsyncCallback(ReceiveCallback), state);
                return;
            }

            (string message, int delimiterIndex) = Helpers.ReadResponseUntilDelimiter(reponse);
            state.ClientResponseOverflowBuffer.Remove(0, delimiterIndex);

            GSGuacdClient.Send(state, message);

            _receiveDone.Set();
        }

        private static void SendCallback(IAsyncResult ar)
        {
            var state = (ConnectionState)ar.AsyncState!;
            state.ClientSocket.EndSend(ar);
            state.LastActivity = DateTime.Now;

            _sendDone.Set();
        }

        #endregion Private Methods
    }
}
