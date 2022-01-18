using GuacamoleSharp.Common.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using WebSocketSharp.Server;

namespace GuacamoleSharp.Server
{
    internal class ClientBehavior : WebSocketBehavior
    {
        #region Private Fields

        private readonly TokenEncrypter _encrypter;
        private readonly ILogger<GuacamoleServer> _logger;
        private readonly GuacamoleOptions _options;

        #endregion Private Fields

        #region Public Constructors

        public ClientBehavior(ILogger<GuacamoleServer> logger, GuacamoleOptions options, TokenEncrypter encrypter)
        {
            _logger = logger;
            _options = options;
            _encrypter = encrypter;
        }

        #endregion Public Constructors

        #region Protected Methods

        protected override void OnOpen()
        {
            var token = Context.QueryString["token"];

            if (token == null)
            {
                _logger.LogWarning("No connection token was specifed for websocket connection.");
                Context.WebSocket.Close();
            }

            string plainText = _encrypter.DecryptString(token!);

            ConnectionOptions connectionOptions = JsonSerializer.Deserialize<ConnectionOptions>(plainText)
                ?? throw new ArgumentNullException("Could not parse connection options from token", nameof(connectionOptions));

            AddDefaultConnectionOptions(connectionOptions);

            AddUnencryptedConnectionOptions(connectionOptions);

            var guacdClientThread = new Thread(() =>
            {
                var guacdClient = new GuacdClient(_logger, _options, connectionOptions, SendCallback);
                guacdClient.Connect();
            });

            guacdClientThread.IsBackground = true;
            guacdClientThread.Start();
        }

        #endregion Protected Methods

        #region Private Methods

        private void AddDefaultConnectionOptions(ConnectionOptions connectionOptions)
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

        private void AddUnencryptedConnectionOptions(ConnectionOptions connectionOptions)
        {
            if (_options.Client.AllowedUnencrypted.ContainsKey(connectionOptions.Type))
            {
                Dictionary<string, string> unencryptedSettings = Context.QueryString.AllKeys
                    .Where(x => x != null && Context.QueryString[x] != null && _options.Client.AllowedUnencrypted[connectionOptions.Type].Contains(x))
                    .ToDictionary(x => x!, x => Context.QueryString[x]!);

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

        private void SendCallback(string message)
        {
            _logger.LogDebug(">>>G2W> {message}", message);

            Send(message);
        }

        #endregion Private Methods
    }
}
