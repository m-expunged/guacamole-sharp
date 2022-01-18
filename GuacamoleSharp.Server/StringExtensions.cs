namespace GuacamoleSharp.Server
{
    internal static class StringExtensions
    {
        #region Public Methods

        public static void ClearStringUntilIndex(this string value, int index)
        {
            if (index <= 0)
                return;

            value = value.Length - 1 > index
                ? value[(index + 1)..]
                : string.Empty;
        }

        public static string ReadStringUntilIndex(this string value, int index)
        {
            if (index <= 0)
                return string.Empty;

            if (value.Length - 1 <= index)
                return value;

            return value[..index];
        }

        #endregion Public Methods
    }
}
