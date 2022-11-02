namespace GuacamoleSharp.Common.Settings
{
    public class WebSocket
    {
        #region Public Properties

        public int MaxInactivityAllowedInMin { get; set; } = 10;
        public int Port { get; set; } = 8080;

        #endregion Public Properties
    }
}
