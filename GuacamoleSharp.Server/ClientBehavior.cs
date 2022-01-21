//using GuacamoleSharp.Common.Models;
//using GuacamoleSharp.Server.Client;
//using Microsoft.Extensions.Logging;
//using System.Text.Json;
//using WebSocketSharp;
//using WebSocketSharp.Server;

//namespace GuacamoleSharp.Server
//{
//    internal class ClientBehavior : WebSocketBehavior
//    {
//        #region Private Fields

//        private readonly TokenEncrypter _encrypter;
//        private readonly ILogger<GuacamoleServer> _logger;
//        private readonly GuacamoleOptions _options;

//        private bool _closed = false;
//        private GuacdClient _guacdClient = null!;

//        #endregion Private Fields

//        #region Public Constructors

//        public ClientBehavior(ILogger<GuacamoleServer> logger, GuacamoleOptions options, TokenEncrypter encrypter)
//        {
//            _logger = logger;
//            _options = options;
//            _encrypter = encrypter;
//        }

//        #endregion Public Constructors

//        #region Protected Methods

//        protected override void OnClose(CloseEventArgs e)
//        {
//            if (!_guacdClient.Closed)
//                _guacdClient.Close();

//            _closed = true;
//        }

//        protected override void OnError(WebSocketSharp.ErrorEventArgs e)
//        {
//            _logger.LogError("Client error: {message}", e.Message);

//            if (!_guacdClient.Closed)
//                _guacdClient.Close();

//            _closed = true;
//        }

//        protected override void OnMessage(MessageEventArgs e)
//        {
//            if (!_guacdClient.Closed)
//                _guacdClient.Send(e.Data);
//        }

//        protected override void OnOpen()
//        {
//            var token = Context.QueryString["token"];

//            if (token == null)
//            {
//                _logger.LogWarning("No connection token was specifed for websocket connection");
//                Context.WebSocket.Close();
//                return;
//            }

//            string plainText;

//            try
//            {
//                plainText = _encrypter.DecryptString(token!);
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError("Error occured while parsing token: {ex}", ex);
//                return;
//            }

//            ConnectionOptions connectionOptions = JsonSerializer.Deserialize<ConnectionOptions>(plainText)
//                ?? throw new ArgumentNullException("Could not parse connection options from token", nameof(connectionOptions));

//            AddDefaultConnectionOptions(connectionOptions);

//            AddUnencryptedConnectionOptions(connectionOptions);

//            _logger.LogDebug("Attempting connection with settings: {@settings}", connectionOptions.Settings);

//            _guacdClient = new GuacdClient(_logger, _options, connectionOptions, this);

//            _guacdClient.Connect();
//        }

//        #endregion Protected Methods

//        #region Public Methods

//        public void SendMessage(string message)
//        {
//            if (!_guacdClient.Closed && !_closed)
//            {
//                _logger.LogDebug(">>>G2W> {message}", message);

//                Send(message);
//            }
//        }

//        #endregion Public Methods

//        #region Private Methods

//        private void AddDefaultConnectionOptions(ConnectionOptions connectionOptions)
//        {
//            if (_options.Client.ConnectionDefault.ContainsKey(connectionOptions.Type))
//            {
//                foreach (var setting in _options.Client.ConnectionDefault[connectionOptions.Type])
//                {
//                    if (!connectionOptions.Settings.ContainsKey(setting.Key))
//                    {
//                        connectionOptions.Settings.Add(setting.Key, setting.Value);
//                    }
//                }
//            }
//        }

//        private void AddUnencryptedConnectionOptions(ConnectionOptions connectionOptions)
//        {
//            if (_options.Client.AllowedUnencrypted.ContainsKey(connectionOptions.Type))
//            {
//                Dictionary<string, string> unencryptedSettings = Context.QueryString.AllKeys
//                    .Where(x => x != null && Context.QueryString[x] != null && _options.Client.AllowedUnencrypted[connectionOptions.Type].Contains(x))
//                    .ToDictionary(x => x!, x => Context.QueryString[x]!);

//                foreach (var setting in unencryptedSettings)
//                {
//                    if (connectionOptions.Settings.ContainsKey(setting.Key))
//                    {
//                        connectionOptions.Settings[setting.Key] = setting.Value;
//                    }
//                    else
//                    {
//                        connectionOptions.Settings.Add(setting.Key, setting.Value);
//                    }
//                }
//            }
//        }

//        #endregion Private Methods
//    }
//}
