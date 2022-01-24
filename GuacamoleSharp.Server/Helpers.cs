using GuacamoleSharp.Common.Models;

namespace GuacamoleSharp.Server
{
    internal static class Helpers
    {
        #region Internal Methods

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
