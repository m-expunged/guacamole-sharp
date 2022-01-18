namespace GuacamoleSharp.Common.Models
{
    public class GuacamoleOptions
    {
        #region Public Properties

        public Client Client { get; set; } = new();

        public Guacd Guacd { get; set; } = new();

        public string Key { get; set; } = null!;

        public WebSocket WebSocket { get; set; } = new();

        #endregion Public Properties
    }
}
