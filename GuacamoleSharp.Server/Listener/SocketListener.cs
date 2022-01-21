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
        private static ManualResetEvent _allDone = new(false);
        private static GuacamoleOptions _options = null!;
        private static Regex _rx = new Regex(@"GET...(.*?)HTTP", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        #endregion Private Fields

        #region Public Methods

        internal static void StartListening(GuacamoleOptions options, int port, string? hostname = null)
        {
            _options = options;

            var thread = new Thread(() =>
            {
                IPHostEntry ipHostEntry = Dns.GetHostEntry(Dns.GetHostName());
                IPAddress? ipAddress;

                if (hostname == null)
                {
                    ipAddress = ipHostEntry.AddressList.FirstOrDefault(x => x.AddressFamily == AddressFamily.InterNetwork);
                }
                else
                {
                    ipAddress = IPAddress.Parse(hostname);
                }

                if (ipAddress == null)
                    throw new ArgumentNullException(nameof(ipAddress), $"No IP Adress of type {AddressFamily.InterNetwork} was found");

                IPEndPoint endpoint = new IPEndPoint(ipAddress, port);
                Socket listener = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

                _logger.Information("Socket listening on: {ipEndPoint}", endpoint);

                try
                {
                    listener.Bind(endpoint);
                    listener.Listen(100);

                    while (true)
                    {
                        _allDone.Reset();

                        listener.BeginAccept(new AsyncCallback(AcceptCallback), listener);

                        _allDone.WaitOne();
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error("Error while creating connection: {ex}", ex);
                }
            });

            thread.IsBackground = true;

            thread.Start();
        }

        #endregion Public Methods

        #region Private Methods

        public static void Send(Socket handler, string content)
        {
        }

        private static void AcceptCallback(IAsyncResult result)
        {
            _allDone.Set();

            Socket listener = (Socket)result.AsyncState!;
            Socket handler = listener.EndAccept(result);

            SocketListenerState state = new SocketListenerState();

            state.WorkSocket = handler;
            handler.BeginReceive(state.Buffer, 0, state.BufferSize, 0, new AsyncCallback(ConnectCallback), state);
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
