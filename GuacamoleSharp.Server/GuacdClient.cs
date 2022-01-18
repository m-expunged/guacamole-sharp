using GuacamoleSharp.Common.Models;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace GuacamoleSharp.Server
{
    internal class GuacdClient
    {
        #region Private Fields

        private readonly ConnectionOptions _connectionOptions;
        private readonly ILogger<GuacamoleServer> _logger;
        private readonly GuacamoleOptions _options;
        private readonly Action<string> _serverSendCallback;
        private readonly IConnectionDictionary<string, string> _settings;

        private ManualResetEvent _connectDone = new ManualResetEvent(false);
        private bool _handshakeDone = false;
        private bool _keepOpen = true;
        private ManualResetEvent _receiveDone = new ManualResetEvent(false);
        private string _response = string.Empty;
        private ManualResetEvent _sendDone = new ManualResetEvent(false);

        #endregion Private Fields

        #region Public Constructors

        public GuacdClient(ILogger<GuacamoleServer> logger, GuacamoleOptions options, ConnectionOptions connectionOptions, Action<string> serverSendCallback)
        {
            _logger = logger;
            _options = options;
            _serverSendCallback = serverSendCallback;
            _connectionOptions = connectionOptions;
            _settings = connectionOptions.Settings;
        }

        public void Connect()
        {
            try
            {
                IPAddress ip = IPAddress.Parse(_options.Guacd.Host);
                IPEndPoint remoteEndpoint = new IPEndPoint(ip, _options.Guacd.Port);

                Socket client = new Socket(ip.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

                client.BeginConnect(remoteEndpoint, new AsyncCallback(ConnectCallback), client);
                _connectDone.WaitOne();

                _logger.LogInformation("Guacd connection has opened.");
                _logger.LogInformation("Selecting connection type: {type}", _connectionOptions.Type);

                Send(client, BuildGuacamoleProtocolCode("select", _connectionOptions.Type.ToLowerInvariant()));

                Receive(client);

                Send(client, BuildGuacamoleProtocolCode("size", _settings["width"], _settings["height"], _settings["dpi"]));
                Send(client, BuildGuacamoleProtocolCode("audio", "audio/L16", _settings["audio"]));
                Send(client, BuildGuacamoleProtocolCode("video", _settings["video"]));
                Send(client, BuildGuacamoleProtocolCode("image", "image/png", "image/jpeg", "image/webp", _settings["image"]));

                int delimiterIndex = _response.IndexOf(';');
                string handshake = _response.ReadStringUntilIndex(delimiterIndex);
                _response.ClearStringUntilIndex(delimiterIndex);

                _logger.LogDebug("Server sent handshake: {handshake}", handshake);

                var connectionCode = BuildHandshakeReplyAttributes(handshake);
                Send(client, BuildGuacamoleProtocolCode(connectionCode));

                _handshakeDone = true;

                while (_keepOpen)
                {
                    Receive(client);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error while starting gaucd connection: {ex}", ex);
            }
        }

        #endregion Public Constructors

        #region Private Methods

        private string BuildGuacamoleProtocolCode(params string?[] args)
        {
            List<string> parts = new();
            for (int i = 0; i < args.Length; i++)
            {
                var arg = args[i] ?? string.Empty;
                parts.Add($"{arg.Length}.{arg}");
            }

            return string.Join(',', parts) + ";";
        }

        private string?[] BuildHandshakeReplyAttributes(string handshake)
        {
            var handshakeAttributes = handshake.Split(',');

            List<string?> replyAttributes = new();

            foreach (var attr in handshakeAttributes)
            {
                int attrDelimiter = attr.IndexOf('.') + 1;
                string settingKey = attr[attrDelimiter..];
                replyAttributes.Add(_settings[settingKey]);
            }

            return replyAttributes.ToArray();
        }

        private void ConnectCallback(IAsyncResult result)
        {
            try
            {
                Socket client = (Socket)result.AsyncState!;
                client.EndConnect(result);

                _logger.LogInformation("Socket connected to {RemoteEndPoint}", client.RemoteEndPoint!.ToString());

                _connectDone.Set();
            }
            catch (Exception ex)
            {
                _logger.LogError("Error during connection callback: {Exception}", ex);
            }
        }

        private void Receive(Socket client)
        {
            _receiveDone.Reset();

            GuacdClientState state = new GuacdClientState();
            state.Socket = client;

            client.BeginReceive(state.Buffer, 0, state.BufferSize, 0, new AsyncCallback(ReceiveCallback), state);

            _receiveDone.WaitOne();
        }

        private void ReceiveCallback(IAsyncResult result)
        {
            try
            {
                GuacdClientState state = (GuacdClientState)result.AsyncState!;
                Socket client = state.Socket;

                int responseSize = client.EndReceive(result);

                state.Data.Append(Encoding.UTF8.GetString(state.Buffer, 0, responseSize));

                if (!_handshakeDone)
                {
                    // handshake response is incomplete, wait for more
                    if (!state.Data.ToString().Contains(';'))
                    {
                        client.BeginReceive(state.Buffer, 0, state.BufferSize, 0, new AsyncCallback(ReceiveCallback), state);
                    }
                    else
                    {
                        _response = state.Data.ToString();
                        state.Data.Remove(0, _response.IndexOf(';') + 1);
                        _receiveDone.Set();
                    }
                }
                else
                {
                    string responseBuffer = state.Data.ToString();
                    int delimiterIndex = responseBuffer.LastIndexOf(';');
                    _response = responseBuffer.ReadStringUntilIndex(delimiterIndex + 1);

                    if (_response.Length > 0)
                    {
                        state.Data.Remove(0, delimiterIndex + 1);
                        _serverSendCallback(_response);
                    }

                    _receiveDone.Set();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error during response callback: {ex}", ex);
            }
        }

        private void Send(Socket client, string message)
        {
            _sendDone.Reset();

            _logger.LogDebug("<<<W2G< {message}", message);

            byte[] data = Encoding.UTF8.GetBytes(message);

            client.BeginSend(data, 0, data.Length, 0, new AsyncCallback(SendCallback), client);

            _sendDone.WaitOne();
        }

        private void SendCallback(IAsyncResult result)
        {
            try
            {
                Socket client = (Socket)result.AsyncState!;
                client.EndSend(result);

                _sendDone.Set();
            }
            catch (Exception ex)
            {
                _logger.LogError("Error while sending message: {ex}", ex);
            }
        }

        #endregion Private Methods
    }
}
