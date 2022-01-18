using GuacamoleSharp.Common.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WebSocketSharp.Server;

namespace GuacamoleSharp.Server
{
    public class GuacamoleServer
    {
        #region Private Fields

        private readonly TokenEncrypter _encrypter;
        private readonly ILogger<GuacamoleServer> _logger;
        private readonly GuacamoleOptions _options;

        #endregion Private Fields

        #region Public Constructors

        public GuacamoleServer(ILogger<GuacamoleServer> logger, IOptions<GuacamoleOptions> options, TokenEncrypter encrypter)
        {
            _logger = logger;
            _options = options.Value ?? throw new ArgumentException("Guacamole options could not be parsed form appsettings.", nameof(options));
            _encrypter = encrypter;
        }

        #endregion Public Constructors

        #region Public Methods

        public void Start()
        {
            string url = $"ws://localhost:{_options.WebSocket.Port}";

            var wssr = new WebSocketServer(url);
            wssr.AddWebSocketService<ClientBehavior>("/", () => new ClientBehavior(_logger, _options, _encrypter));
            wssr.Start();

            _logger.LogInformation("WebSocket server listening on: {url}", url);
        }

        #endregion Public Methods
    }
}
