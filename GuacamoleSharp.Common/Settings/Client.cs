namespace GuacamoleSharp.Common.Settings
{
    public class Client
    {
        #region Public Properties

        public Dictionary<string, List<string>> ConnectionAllowedUnencryptedSettings { get; set; } = new();

        public Dictionary<string, Dictionary<string, string>> ConnectionDefaultSettings { get; set; } = new();

        #endregion Public Properties
    }
}
