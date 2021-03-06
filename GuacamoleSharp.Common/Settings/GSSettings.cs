namespace GuacamoleSharp.Common.Settings
{
    public class GSSettings
    {
        #region Public Properties

        public Client Client { get; set; } = new();

        public Guacd Guacd { get; set; } = new();

        public string Password { get; set; } = null!;

        public WebSocket WebSocket { get; set; } = new();

        #endregion Public Properties
    }
}
