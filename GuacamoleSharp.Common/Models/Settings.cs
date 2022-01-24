namespace GuacamoleSharp.Common.Models
{
    public class Settings : Dictionary<string, string>
    {
        #region Public Indexers

        public new string? this[string key]
        {
            get
            {
                this.TryGetValue(key, out string? value);
                return value;
            }

            set
            {
                base[key] = value!;
            }
        }

        #endregion Public Indexers

        #region Public Constructors

        public Settings() : base()
        {
        }

        #endregion Public Constructors
    }
}
