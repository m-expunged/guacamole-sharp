using GuacamoleSharp.Models;

namespace GuacamoleSharp.Helpers
{
    public class ProtocolHelper
    {
        public static string?[] BuildHandshakeReply(Connection connection, string handshake)
        {
            string[] args = handshake.Split(',');

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                string argKey = arg[(arg.IndexOf('.') + 1)..];
                args[i] = connection.Arguments[argKey]!;
            }

            return args;
        }

        public static string BuildProtocol(params string?[] args)
        {
            string[] result = new string[args.Length];

            for (int i = 0; i < args.Length; i++)
            {
                var arg = args[i] ?? string.Empty;
                result[i] = $"{arg.Length}.{arg}";
            }

            return string.Join(',', result) + ";";
        }

        public static (string content, int index) ReadProtocolUntilLastDelimiter(string content)
        {
            int index = content.LastIndexOf(';');

            if (index == -1)
                return (string.Empty, index);

            if (content.Length - 1 == index)
                return (content, content.Length);

            index += 1;

            return (content[..index], index);
        }
    }
}