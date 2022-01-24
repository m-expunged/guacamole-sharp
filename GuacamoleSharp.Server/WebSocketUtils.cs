using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace GuacamoleSharp.Server
{
    internal static class WebSocketUtils
    {
        #region Private Fields

        private static readonly Regex _rxSwk = new("Sec-WebSocket-Key: (.*)");

        #endregion Private Fields

        #region Internal Methods

        internal static string BuildHttpUpgradeResponse(string content)
        {
            var swkMatches = _rxSwk.Match(content);
            var swk = swkMatches.Groups[1].Value.Trim();
            var swkSha1Base64 = Convert.ToBase64String(SHA1.Create().ComputeHash(Encoding.UTF8.GetBytes(swk + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
            var httpUpgradeResponse = "HTTP/1.1 101 Switching Protocols\r\n"
                + "Upgrade: websocket\r\n"
                + "Connection: Upgrade\r\n"
                + "Sec-WebSocket-Protocol: guacamole\r\n"
                + "Sec-WebSocket-Accept: " + swkSha1Base64 + "\r\n\r\n";

            return httpUpgradeResponse;
        }

        internal static string ReadFromFrame(byte[] payload)
        {
            int dataLength = payload[1] & 127;
            int maskIndex = 2;
            if (dataLength == 126)
            {
                maskIndex = 4;
            }
            else if (dataLength == 127)
            {
                maskIndex = 10;
            }

            var masks = payload[maskIndex..(maskIndex + 4)];
            int firstDataByteIndex = maskIndex + 4;
            byte[] decoded = new byte[payload.Length - firstDataByteIndex];

            for (int i = firstDataByteIndex, j = 0; i < payload.Length; i++, j++)
            {
                decoded[j] = (byte)(payload[i] ^ masks.ElementAt(j % 4));
            }

            return Encoding.UTF8.GetString(decoded);
        }

        internal static byte[] WriteToFrame(string message)
        {
            int frameCount;
            byte[] payload = Encoding.UTF8.GetBytes(message);
            byte[] frame = new byte[10];
            frame[0] = (byte)129;

            if (payload.Length <= 125)
            {
                frame[1] = (byte)payload.Length;
                frameCount = 2;
            }
            else if (payload.Length >= 126 && payload.Length <= 65535)
            {
                frame[1] = (byte)126;
                frame[2] = (byte)((payload.Length >> 8) & (byte)255);
                frame[3] = (byte)(payload.Length & (byte)255);
                frameCount = 4;
            }
            else
            {
                frame[1] = (byte)127;
                frame[2] = (byte)((payload.Length >> 56) & (byte)255);
                frame[3] = (byte)((payload.Length >> 48) & (byte)255);
                frame[4] = (byte)((payload.Length >> 40) & (byte)255);
                frame[5] = (byte)((payload.Length >> 32) & (byte)255);
                frame[6] = (byte)((payload.Length >> 24) & (byte)255);
                frame[7] = (byte)((payload.Length >> 16) & (byte)255);
                frame[8] = (byte)((payload.Length >> 8) & (byte)255);
                frame[9] = (byte)(payload.Length & (byte)255);
                frameCount = 10;
            }

            byte[] result = new byte[frameCount + payload.Length];

            int j = 0;
            for (int i = 0; i < frameCount; i++)
            {
                result[j] = frame[i];
                j++;
            }
            for (int i = 0; i < payload.Length; i++)
            {
                result[j] = payload[i];
                j++;
            }

            return result;
        }

        #endregion Internal Methods
    }
}
