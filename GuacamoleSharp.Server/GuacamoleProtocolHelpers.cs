using GuacamoleSharp.Common.Models;
using System.Collections.Specialized;

namespace GuacamoleSharp.Server
{
    internal static class GuacamoleProtocolHelpers
    {
        #region Internal Methods

        internal static void AddDefaultConnectionSettings(Connection connection, Dictionary<string, Dictionary<string, string>> connectionDefaultSettings)
        {
            if (!connectionDefaultSettings.ContainsKey(connection.Type))
                return;

            foreach (var setting in connectionDefaultSettings[connection.Type])
            {
                if (!connection.Settings.ContainsKey(setting.Key))
                {
                    connection.Settings.Add(setting.Key, setting.Value);
                }
            }
        }

        internal static string?[] BuildHandshakeReply(Settings settings, string handshake)
        {
            string[] args = handshake.Split(',');

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                string argKey = arg[(arg.IndexOf('.') + 1)..];
                args[i] = settings[argKey]!;
            }

            return args;
        }

        internal static string BuildProtocol(params string?[] args)
        {
            string[] result = new string[args.Length];

            for (int i = 0; i < args.Length; i++)
            {
                var arg = args[i] ?? string.Empty;
                result[i] = $"{arg.Length}.{arg}";
            }

            return string.Join(',', result) + ";";
        }

        internal static void OverwriteConnectionWithUnencryptedConnectionSettings(Connection connection, NameValueCollection query, Dictionary<string, List<string>> connectionAllowedUnencryptedSettings)
        {
            if (!connectionAllowedUnencryptedSettings.ContainsKey(connection.Type))
                return;

            IEnumerable<string> validQueryProps = query.AllKeys
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x))
                .Where(x => query[x] != null && !string.IsNullOrWhiteSpace(query[x]))!;

            Dictionary<string, string> unencryptedConnectionSettings = validQueryProps
                .Where(x => connectionAllowedUnencryptedSettings[connection.Type].Contains(x))
                .ToDictionary(x => x, x => query[x])!;

            foreach (var setting in unencryptedConnectionSettings)
            {
                if (connection.Settings.ContainsKey(setting.Key))
                {
                    connection.Settings[setting.Key] = setting.Value;
                }
                else
                {
                    connection.Settings.Add(setting.Key, setting.Value);
                }
            }
        }

        internal static (string content, int index) ReadProtocolUntilLastDelimiter(string content)
        {
            int index = content.LastIndexOf(';');

            if (index == -1)
                return (string.Empty, index);

            if (content.Length - 1 == index)
                return (content, content.Length);

            index += 1;

            return (content[..index], index);
        }

        #endregion Internal Methods
    }
}
