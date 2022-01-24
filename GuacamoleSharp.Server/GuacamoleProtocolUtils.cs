using GuacamoleSharp.Common.Models;
using System.Collections.Specialized;

namespace GuacamoleSharp.Server
{
    internal static class GuacamoleProtocolUtils
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

        internal static string BuildGuacamoleProtocol(params string?[] args)
        {
            List<string> parts = new();
            for (int i = 0; i < args.Length; i++)
            {
                var arg = args[i] ?? string.Empty;
                parts.Add($"{arg.Length}.{arg}");
            }

            return string.Join(',', parts) + ";";
        }

        internal static string?[] BuildHandshakeReply(Settings settings, string handshake)
        {
            var handshakeAttributes = handshake.Split(',');

            List<string?> replyAttributes = new();

            foreach (var attr in handshakeAttributes)
            {
                int attrDelimiter = attr.IndexOf('.') + 1;
                string settingKey = attr[attrDelimiter..];
                replyAttributes.Add(settings[settingKey]);
            }

            return replyAttributes.ToArray();
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

        internal static (string content, int index) ReadResponseUntilDelimiter(string content)
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
