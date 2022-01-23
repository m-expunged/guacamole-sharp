namespace GuacamoleSharp.Common.Models
{
    public class Settings : Dictionary<string, string>
    {
        #region Public Constructors

        public Settings() : base(StringComparer.OrdinalIgnoreCase)
        {
        }

        #endregion Public Constructors

        #region Public Indexers

        public new string? this[string key]
        {
            get
            {
                this.TryGetValue(key, out string? value);
                return value == null ? value : value.ToLowerInvariant();
            }

            set
            {
                this[key] = value;
            }
        }

        #endregion Public Indexers
    }
}
