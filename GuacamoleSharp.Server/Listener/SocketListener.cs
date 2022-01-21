using GuacamoleSharp.Common.Models;
using GuacamoleSharp.Server.Client;
using Serilog;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;

namespace GuacamoleSharp.Server.Listener
{
    internal class SocketListener
    {
        #region Private Fields

        private static readonly ILogger _logger = Log.ForContext(typeof(SocketListener));
        private static byte[] _buffer = new byte[1024];
        private static Dictionary<int, Socket> _clients = new();
        private static Socket _listener = null!;
        private static GuacamoleOptions _options = null!;
        private static Regex _rx = new Regex(@"GET...(.*?)HTTP", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        #endregion Private Fields

        #region Public Methods

        internal static void StartListening(GuacamoleOptions options, int port, string? hostname = null)
        {
            _options = options;

            IPEndPoint endpoint = new IPEndPoint(IPAddress.Any, port);

            _listener = new Socket(endpoint.Address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            _listener.Bind(endpoint);
            _listener.Listen(1);

            _logger.Information("Socket listening on: {ipEndPoint}", endpoint);

            _listener.BeginAccept(new AsyncCallback(AcceptCallback), null);
        }

        #endregion Public Methods

        #region Private Methods

        public static void Send(Socket handler, string content)
        {
        }

        private static void AcceptCallback(IAsyncResult result)
        {
            Socket client = _listener.EndAccept(result);
            _clients.Add(_clients.Count + 1, client);

            _listener.BeginAccept(new AsyncCallback(AcceptCallback), null);
        }

        private static void AddDefaultConnectionOptions(ConnectionOptions connectionOptions)
        {
            if (_options.Client.ConnectionDefault.ContainsKey(connectionOptions.Type))
            {
                foreach (var setting in _options.Client.ConnectionDefault[connectionOptions.Type])
                {
                    if (!connectionOptions.Settings.ContainsKey(setting.Key))
                    {
                        connectionOptions.Settings.Add(setting.Key, setting.Value);
                    }
                }
            }
        }

        private static void AddUnencryptedConnectionOptions(ConnectionOptions connectionOptions, System.Collections.Specialized.NameValueCollection query)
        {
            if (_options.Client.AllowedUnencrypted.ContainsKey(connectionOptions.Type))
            {
                Dictionary<string, string> unencryptedSettings = query.AllKeys
                    .Where(x => x != null && query[x] != null && _options.Client.AllowedUnencrypted[connectionOptions.Type].Contains(x))
                    .ToDictionary(x => x!, x => query[x]!);

                foreach (var setting in unencryptedSettings)
                {
                    if (connectionOptions.Settings.ContainsKey(setting.Key))
                    {
                        connectionOptions.Settings[setting.Key] = setting.Value;
                    }
                    else
                    {
                        connectionOptions.Settings.Add(setting.Key, setting.Value);
                    }
                }
            }
        }

        private static void ConnectCallback(IAsyncResult result)
        {
            string content = string.Empty;

            SocketListenerState state = (SocketListenerState)result.AsyncState!;
            Socket handler = state.WorkSocket;

            int bytesRead = handler.EndReceive(result);

            state.Data.Append(Encoding.UTF8.GetString(state.Buffer, 0, bytesRead));

            content = state.Data.ToString();

            MatchCollection matches = _rx.Matches(content);
            var queryString = matches[0].Groups[1].Value.Trim();
            var query = HttpUtility.ParseQueryString(queryString);
            var token = query["token"];

            if (token == null)
            {
                _logger.Warning("Missing connection token in query string");
                return;
            }

            string painText = TokenEncrypter.DecryptString(_options.Key, token);
            ConnectionOptions connectionOptions = JsonSerializer.Deserialize<ConnectionOptions>(painText)
                ?? throw new ArgumentNullException(nameof(connectionOptions), "Could not parse connection options from token");

            AddDefaultConnectionOptions(connectionOptions);

            AddUnencryptedConnectionOptions(connectionOptions, query);

            SocketClient.Connect(_options, connectionOptions, handler, out Socket client);

            handler.BeginReceive(state.Buffer, 0, state.BufferSize, 0, new AsyncCallback(ReadCallback), state);
        }

        private static void ReadCallback(IAsyncResult result)
        {
        }

        #endregion Private Methods
    }
}
