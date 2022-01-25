using System.Collections.Specialized;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace GuacamoleSharp.Server
{
    internal struct WebSocketFrameDimensions
    {
        #region Public Properties

        public int DataLength { get; set; }

        public int FrameLength { get; set; }

        public int MaskIndex { get; set; }

        #endregion Public Properties
    }

    internal static class WebSocketHelpers
    {
        #region Private Fields

        private const string responseTemplate = "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Protocol: guacamole\r\nSec-WebSocket-Accept: {0}\r\n\r\n";
        private const string rfcguid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

        private static readonly Regex _rxQuery = new(@"GET...(.*?)HTTP", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex _rxSwk = new("Sec-WebSocket-Key: (.*)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        #endregion Private Fields

        #region Internal Methods

        internal static string BuildHttpUpgradeResponseFromRequest(string content)
        {
            var swkMatches = _rxSwk.Match(content);
            var swk = swkMatches.Groups[1].Value.Trim();
            var swkSha1Base64 = Convert.ToBase64String(SHA1.Create().ComputeHash(Encoding.UTF8.GetBytes(swk + rfcguid)));
            var response = string.Format(responseTemplate, swkSha1Base64);

            return response;
        }

        internal static NameValueCollection ParseQueryStringFromRequest(string content)
        {
            var queryMatches = _rxQuery.Matches(content);
            var queryString = queryMatches[0].Groups[1].Value.Trim();
            var query = HttpUtility.ParseQueryString(queryString);

            return query;
        }

        internal static string ReadFromFrames(byte[] payload, int receivedLength)
        {
            List<WebSocketFrameDimensions> frames = SplitFrames(payload, receivedLength, new());

            string message = string.Empty;
            int payloadStartIndex = 0;

            foreach (var frame in frames)
            {
                if (frame.DataLength == 0)
                {
                    payloadStartIndex += frame.FrameLength;
                    continue;
                }

                var framePayload = payload[payloadStartIndex..(payloadStartIndex + frame.FrameLength)];
                var masks = framePayload[frame.MaskIndex..(frame.MaskIndex + 4)];
                int firstDataByteIndex = frame.MaskIndex + 4;
                byte[] decoded = new byte[framePayload.Length - firstDataByteIndex];

                for (int i = firstDataByteIndex, j = 0; i < framePayload.Length; i++, j++)
                {
                    decoded[j] = (byte)(framePayload[i] ^ masks.ElementAt(j % 4));
                }

                payloadStartIndex += frame.FrameLength;
                message += Encoding.UTF8.GetString(decoded);
            }

            return message;
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

        #region Private Methods

        private static List<WebSocketFrameDimensions> SplitFrames(byte[] payload, int receivedLength, List<WebSocketFrameDimensions> frames)
        {
            int frameStartIndex = frames.Any() ? frames.Sum(x => x.FrameLength) : 0;
            int dataLength = payload[frameStartIndex..][1] & 127;
            int maskIndex;
            int frameLength;

            if (dataLength <= 125)
            {
                maskIndex = 2;
                frameLength = dataLength + 6;
            }
            else if (dataLength == 126)
            {
                maskIndex = 4;
                frameLength = dataLength + 8;
            }
            else if (dataLength == 127)
            {
                maskIndex = 10;
                frameLength = dataLength + 14;
            }
            else
            {
                throw new ArgumentException("Unable to parse websocket frame length");
            }

            frames.Add(new WebSocketFrameDimensions { FrameLength = frameLength, DataLength = dataLength, MaskIndex = maskIndex });

            if (receivedLength > frames.Sum(x => x.FrameLength))
            {
                SplitFrames(payload, receivedLength, frames);
            }

            return frames;
        }

        #endregion Private Methods
    }
}
