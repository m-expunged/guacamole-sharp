using GuacamoleSharp.Common.Models;
using Serilog;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace GuacamoleSharp.Server.Client
{
    internal static class SocketClient
    {
        #region Private Fields

        private static ManualResetEvent _connectDone = new ManualResetEvent(false);
        private static ILogger _logger = Log.ForContext(typeof(SocketClient));
        private static ManualResetEvent _receiveDone = new ManualResetEvent(false);
        private static string _response = string.Empty;
        private static ManualResetEvent _sendDone = new ManualResetEvent(false);

        #endregion Private Fields

        #region Public Methods

        public static void Connect(GuacamoleOptions options, ConnectionOptions connectionOptions, Socket server, out Socket client)
        {
            IPAddress ipAddress = IPAddress.Parse(options.Guacd.Host);
            IPEndPoint endpoint = new IPEndPoint(ipAddress, options.Guacd.Port);
            client = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            try
            {
                client.BeginConnect(endpoint, new AsyncCallback(ConnectCallback), client);
                _connectDone.WaitOne();

                _logger.Information("Guacd connection has opened");
                _logger.Information("Selecting connection type: {type}", connectionOptions.Type);

                Send(client, BuildGuacamoleProtocol(
                    "select",
                    connectionOptions.Type.ToLowerInvariant()));

                ReceiveHandshake(client);

                Send(client, BuildGuacamoleProtocol(
                    "size",
                    connectionOptions.Settings["width"],
                    connectionOptions.Settings["height"],
                    connectionOptions.Settings["dpi"]));

                Send(client, BuildGuacamoleProtocol(
                    "audio",
                    "audio/L16",
                    connectionOptions.Settings["audio"]));

                Send(client, BuildGuacamoleProtocol(
                    "video",
                    connectionOptions.Settings["video"]));

                Send(client, BuildGuacamoleProtocol(
                    "image",
                    "image/png",
                    "image/jpeg",
                    "image/webp",
                    connectionOptions.Settings["image"]));
                int delimiterIndex = _response.IndexOf(';');

                string handshake = _response.ReadStringUntilIndex(delimiterIndex);
                _response.ClearStringUntilIndex(delimiterIndex);

                _logger.Debug("Server sent handshake: {handshake}", handshake);

                var handshakeReply = BuildHandshakeReply(connectionOptions.Settings, handshake);
                Send(client, BuildGuacamoleProtocol(handshakeReply));

                while (true)
                {
                    Receive(client, server);
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Error while connecting to guacd: {ex}", ex);
            }
        }

        #endregion Public Methods

        #region Private Methods

        public static void Send(Socket client, string message)
        {
            _sendDone.Reset();

            _logger.Debug("<<<W2G< {message}", message);

            byte[] data = Encoding.UTF8.GetBytes(message);

            client.BeginSend(data, 0, data.Length, 0, new AsyncCallback(SendCallback), null);

            _sendDone.WaitOne();
        }

        private static string BuildGuacamoleProtocol(params string?[] args)
        {
            List<string> parts = new();
            for (int i = 0; i < args.Length; i++)
            {
                var arg = args[i] ?? string.Empty;
                parts.Add($"{arg.Length}.{arg}");
            }

            return string.Join(',', parts) + ";";
        }

        private static string?[] BuildHandshakeReply(ConnectionDictionary<string, string> settings, string handshake)
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

        private static void ConnectCallback(IAsyncResult result)
        {
            try
            {
                Socket client = (Socket)result.AsyncState!;
                client.EndConnect(result);

                _logger.Information("Socket connected to {RemoteEndPoint}", client.RemoteEndPoint!.ToString());

                _connectDone.Set();
            }
            catch (Exception ex)
            {
                _logger.Error("Error during connection callback: {Exception}", ex);
            }
        }

        private static void Receive(Socket client, Socket server)
        {
            _receiveDone.Reset();

            SocketClientState state = new SocketClientState();
            state.WorkSocket = client;
            state.ServerSocket = server;

            client.BeginReceive(state.Buffer, 0, state.BufferSize, 0, new AsyncCallback(ReceiveCallback), state);

            _receiveDone.WaitOne();
        }

        private static void ReceiveCallback(IAsyncResult result)
        {
            SocketClientState state = (SocketClientState)result.AsyncState!;
            Socket client = state.WorkSocket;

            int bytesRead = client.EndReceive(result);
            state.Data.Append(Encoding.UTF8.GetString(state.Buffer, 0, bytesRead));

            string responseBuffer = state.Data.ToString();
            int delimiterIndex = responseBuffer.LastIndexOf(';');
            _response = responseBuffer.ReadStringUntilIndex(delimiterIndex + 1);

            if (_response.Length > 0)
            {
                state.Data.Remove(0, delimiterIndex + 1);
                byte[] byteData = Encoding.UTF8.GetBytes(_response);
                state.ServerSocket.BeginSend(byteData, 0, byteData.Length, 0, new AsyncCallback(SendCallback), state.ServerSocket);
            }

            _receiveDone.Set();
        }

        private static void ReceiveHandshake(Socket client)
        {
            _receiveDone.Reset();

            SocketClientState state = new SocketClientState();
            state.WorkSocket = client;

            client.BeginReceive(state.Buffer, 0, state.BufferSize, 0, new AsyncCallback(ReceiveHandshakeCallback), state);

            _receiveDone.WaitOne();
        }

        private static void ReceiveHandshakeCallback(IAsyncResult result)
        {
            SocketClientState state = (SocketClientState)result.AsyncState!;
            Socket client = state.WorkSocket;

            int bytesRead = client.EndReceive(result);
            state.Data.Append(Encoding.UTF8.GetString(state.Buffer, 0, bytesRead));

            // guacamole protocol delimiter not found, listen for more
            if (!state.Data.ToString().Contains(';'))
            {
                client.BeginReceive(state.Buffer, 0, state.BufferSize, 0, new AsyncCallback(ReceiveHandshakeCallback), state);
            }
            else
            {
                _response = (state.Data.ToString());
                state.Data.Remove(0, _response.IndexOf(';') + 1);
                _receiveDone.Set();
            }
        }

        private static void SendCallback(IAsyncResult ar)
        {
            try
            {
                Socket client = (Socket)ar.AsyncState!;

                client.EndSend(ar);

                _sendDone.Set();
            }
            catch (Exception ex)
            {
                _logger.Error("Error during send callback: {ex}", ex);
            }
        }

        #endregion Private Methods
    }
}
