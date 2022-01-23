using GuacamoleSharp.Common.Models;
using GuacamoleSharp.Common.Settings;
using Serilog;
using System.Collections.Specialized;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;

namespace GuacamoleSharp.Server
{
    internal static class GSListener
    {
        #region Private Fields

        private static readonly ILogger _logger = Log.ForContext(typeof(GSListener));
        private static readonly Regex _rx = new(@"GET...(.*?)HTTP", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly ManualResetEvent _sendDone = new(false);
        private static ulong _connectionCount = 0;
        private static GSSettings _gssettings = null!;
        private static Socket _listener = null!;

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
            }
            catch (Exception ex)
            {
                _logger.Error("[Connection {id}] Error while closing client connection: {ex}", state.ConnectionId, ex);
            }
        }

        internal static void SendWithReply(ConnectionState state, string message)
        {
            Send(state, message);

            state.ClientSocket.BeginReceive(state.ClientBuffer, 0, state.ClientBuffer.Length, SocketFlags.None, new AsyncCallback(ReceiveCallback), state);
        }

        internal static void StartListening(GSSettings gssettings)
        {
            var thread = new Thread(() =>
            {
                _gssettings = gssettings;

                IPEndPoint endpoint = new(IPAddress.Any, _gssettings.WebSocket.Port);

                _listener = new Socket(endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                _listener.Bind(endpoint);
                _listener.Listen(1);

                _logger.Information("Socket listening on: {ipEndPoint}", endpoint);

                _listener.BeginAccept(new AsyncCallback(AcceptCallback), null);
            });

            thread.IsBackground = true;
            thread.Start();
        }

        #endregion Internal Methods

        #region Private Methods

        private static void AcceptCallback(IAsyncResult ar)
        {
            _connectionCount += 1;

            ConnectionState state = new();
            state.ClientSocket = _listener.EndAccept(ar);
            state.ConnectionId = _connectionCount;

            state.ClientSocket.BeginReceive(state.ClientBuffer, 0, state.ClientBuffer.Length, SocketFlags.None, new AsyncCallback(ConnectCallback), state);

            _listener.BeginAccept(new AsyncCallback(AcceptCallback), null);
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
                var matches = _rx.Matches(content);
                var queryString = matches[0].Groups[1].Value.Trim();
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

                connection.AddDefaultConnectionSettings();
                connection.OverwriteWithUnencryptedConnectionSettings(query);

                state.Connection = connection;
                state.LastActivity = DateTime.Now;

                GSGuacdClient.Connect(_gssettings, connection, state);
            }
            else
            {
                state.ClientSocket.BeginReceive(state.ClientBuffer, 0, state.ClientBuffer.Length, SocketFlags.None, new AsyncCallback(ConnectCallback), state);
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
                .ToDictionary(x => x.ToLowerInvariant(), x => query[x]!.ToLowerInvariant())!;

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
            {
                state.ClientSocket.BeginReceive(state.ClientBuffer, 0, state.ClientBuffer.Length, SocketFlags.None, new AsyncCallback(ReceiveCallback), state);
                return;
            }

            state.ClientResponseOverflowBuffer.Append(Encoding.ASCII.GetString(state.ClientBuffer[0..receivedLength]));
            string reponse = state.ClientResponseOverflowBuffer.ToString();

            if (!reponse.Contains(';'))
            {
                state.ClientSocket.BeginReceive(state.ClientBuffer, 0, state.ClientBuffer.Length, SocketFlags.None, new AsyncCallback(ReceiveCallback), state);
                return;
            }

            (string message, int delimiterIndex) = Helpers.ReadResponseUntilDelimiter(reponse);
            state.ClientResponseOverflowBuffer.Remove(0, delimiterIndex);

            GSGuacdClient.SendWithReply(state, message);
        }

        private static void Send(ConnectionState state, string message)
        {
            _sendDone.Reset();

            _logger.Debug("[Connection {Id}] >>>G2W> {Message}", state.ConnectionId, message);

            byte[] data = Encoding.ASCII.GetBytes(message);
            state.ClientSocket.BeginSend(data, 0, data.Length, 0, new AsyncCallback(SendCallback), state);

            _sendDone.WaitOne();
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
