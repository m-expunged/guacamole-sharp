namespace GuacamoleSharp.Common.Models
{
    public interface IConnectionDictionary<TKey, TValue> where TValue : class
    {
        #region Public Indexers

        TValue this[TKey key] { get; }

        #endregion Public Indexers
    }

    public class ConnectionDictionary<TKey, TValue> : Dictionary<TKey, TValue>, IConnectionDictionary<TKey, TValue> where TValue : class
    {
        #region Public Indexers

        TValue IConnectionDictionary<TKey, TValue>.this[TKey key]
        {
            get
            {
                TValue value;
                this.TryGetValue(key, out value);
                return value;
            }
        }

        #endregion Public Indexers
    }
}
