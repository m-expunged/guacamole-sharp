namespace GuacamoleSharp.Common.Models
{
    public class Client
    {
        #region Public Properties

        public Dictionary<string, List<string>> AllowedUnencrypted { get; set; } = new();

        public Dictionary<string, Dictionary<string, string>> ConnectionDefault { get; set; } = new();

        #endregion Public Properties
    }
}
