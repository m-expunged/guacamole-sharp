using GuacamoleSharp.Common.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GuacamoleSharp.Server
{
    public class GSServer
    {
        #region Private Fields

        private readonly GSSettings _gssettings;
        private readonly ILogger<GSServer> _logger;

        #endregion Private Fields

        #region Public Constructors

        public GSServer(ILogger<GSServer> logger, IOptions<GSSettings> gssettings)
        {
            _logger = logger;
            _gssettings = gssettings.Value ?? throw new ArgumentException("Guacamole settings could not be parsed from appsettings", nameof(gssettings));
        }

        #endregion Public Constructors

        #region Public Methods

        public void Restart()
        {
            GSListener.Restart();
        }

        public void Start()
        {
            _logger.LogInformation("Starting guacamole-sharp websocket server...");

            GSListener.StartListening(_gssettings);
        }

        #endregion Public Methods
    }
}
