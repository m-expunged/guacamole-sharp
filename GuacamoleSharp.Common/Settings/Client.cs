namespace GuacamoleSharp.Common.Settings
{
    public class Client
    {
        #region Public Properties

        public Dictionary<string, Dictionary<string, string>> DefaultConnectionSettings { get; set; } = new();

        public Dictionary<string, List<string>> UnencryptedConnectionSettings { get; set; } = new();

        #endregion Public Properties
    }
}
