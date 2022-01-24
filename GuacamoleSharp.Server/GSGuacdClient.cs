using GuacamoleSharp.Common.Models;
using GuacamoleSharp.Common.Settings;
using Serilog;
using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace GuacamoleSharp.Server
{
    internal class GSGuacdClient
    {
        #region Private Fields

        private static readonly ILogger _logger = Log.ForContext(typeof(GSGuacdClient));
        private static readonly ManualResetEvent _receiveDone = new(false);
        private static readonly ManualResetEvent _sendDone = new(false);

        private static GSSettings _gssettings = null!;

        #endregion Private Fields

        #region Internal Methods

        internal static void Close(ConnectionState state)
        {
            try
            {
                _logger.Information("[Connection {Id}] Closing guacd connection", state.ConnectionId);

                if (state.GuacdSocket != null)
                {
                    state.GuacdSocket.Shutdown(SocketShutdown.Both);
                    state.GuacdSocket.Close();
                }
            }
            catch (Exception ex)
            {
                _logger.Error("[Connection {id}] Error while closing guacd connection: {ex}", state.ConnectionId, ex);
            }
            finally
            {
                GSListener.Close(state);
            }
        }

        internal static void Connect(GSSettings gssettings, Connection connection, ConnectionState state)
        {
            if (_gssettings == null)
                _gssettings = gssettings;

            IPEndPoint endpoint = new(IPAddress.Parse(gssettings.Guacd.Host), gssettings.Guacd.Port);

            _logger.Information("[Connection {Id}] Attemping connection to: {Endpoint}", state.ConnectionId, endpoint.ToString());
            _logger.Information("[Connection {Id}] Connection settings: {@connection}", state.ConnectionId, connection);

            state.GuacdSocket = new Socket(endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            state.GuacdSocket.BeginConnect(endpoint, new AsyncCallback(ConnectCallback), state);
        }

        internal static void Send(ConnectionState state, string message)
        {
            _sendDone.Reset();

            _logger.Debug("[Connection {Id}] <<<W2G< {Message}", state.ConnectionId, message);

            byte[] data = Encoding.UTF8.GetBytes(message);
            state.GuacdSocket.BeginSend(data, 0, data.Length, 0, new AsyncCallback(SendCallback), state);

            _sendDone.WaitOne();
        }

        #endregion Internal Methods

        #region Private Methods

        private static void ConnectCallback(IAsyncResult ar)
        {
            var state = (ConnectionState)ar.AsyncState!;
            state.GuacdSocket.EndConnect(ar);

            _logger.Information("[Connection {Id}] Socket connected to {Endpoint}", state.ConnectionId, state.GuacdSocket.RemoteEndPoint?.ToString());
            _logger.Information("Selecting connection type: {type}", state.Connection.Type);

            Send(state, GuacamoleProtocolUtils.BuildGuacamoleProtocol("select", state.Connection.Type.ToLowerInvariant()));

            state.GuacdSocket.BeginReceive(state.GuacdBuffer, 0, state.GuacdBuffer.Length, SocketFlags.None, new AsyncCallback(HandshakeCallback), state);
        }

        private static void HandshakeCallback(IAsyncResult ar)
        {
            var state = (ConnectionState)ar.AsyncState!;
            var receivedLength = state.GuacdSocket.EndReceive(ar);

            if (DateTime.Now > state.LastActivity.AddMinutes(_gssettings.WebSocket.MaxInactivityAllowedInMin))
            {
                _logger.Warning("[Connection {Id}] Timeout", state.LastActivity);
                Close(state);
                return;
            }

            if (receivedLength <= 0)
            {
                state.GuacdSocket.BeginReceive(state.GuacdBuffer, 0, state.GuacdBuffer.Length, SocketFlags.None, new AsyncCallback(HandshakeCallback), state);
                return;
            }

            state.GuacdResponseOverflowBuffer.Append(Encoding.UTF8.GetString(state.GuacdBuffer[0..receivedLength]));
            string reponse = state.GuacdResponseOverflowBuffer.ToString();

            if (!reponse.Contains(';'))
            {
                state.GuacdSocket.BeginReceive(state.GuacdBuffer, 0, state.GuacdBuffer.Length, SocketFlags.None, new AsyncCallback(HandshakeCallback), state);
                return;
            }

            (string handshake, int delimiterIndex) = GuacamoleProtocolUtils.ReadResponseUntilDelimiter(reponse);
            state.GuacdResponseOverflowBuffer.Remove(0, delimiterIndex);
            var handshakeReply = GuacamoleProtocolUtils.BuildHandshakeReply(state.Connection.Settings, handshake);

            Send(state, GuacamoleProtocolUtils.BuildGuacamoleProtocol("size", state.Connection.Settings["width"], state.Connection.Settings["height"], state.Connection.Settings["dpi"]));
            Send(state, GuacamoleProtocolUtils.BuildGuacamoleProtocol("audio", "audio/L16", state.Connection.Settings["audio"]));
            Send(state, GuacamoleProtocolUtils.BuildGuacamoleProtocol("video", state.Connection.Settings["video"]));
            Send(state, GuacamoleProtocolUtils.BuildGuacamoleProtocol("image", "image/png", "image/jpeg", "image/webp", state.Connection.Settings["image"]));

            _logger.Debug("Server sent handshake: {handshake}", handshake);

            Send(state, GuacamoleProtocolUtils.BuildGuacamoleProtocol(handshakeReply));

            state.GuacdHandshakeDone.Set();

            BackgroundWorker guacdThread = new();
            guacdThread.DoWork += new DoWorkEventHandler(Receive_DoWork);
            guacdThread.RunWorkerAsync(state);
        }

        private static void Receive_DoWork(object? sender, DoWorkEventArgs e)
        {
            ConnectionState state = (ConnectionState)e.Argument!;

            state.ClientHandshakeDone.WaitOne();

            try
            {
                while (!state.Closed)
                {
                    _receiveDone.Reset();

                    state.GuacdSocket.BeginReceive(state.GuacdBuffer, 0, state.GuacdBuffer.Length, SocketFlags.None, new AsyncCallback(ReceiveCallback), state);

                    _receiveDone.WaitOne();
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Error while running guacd socket client thread: {ex}", ex);
                Close(state);
            }
        }

        private static void ReceiveCallback(IAsyncResult ar)
        {
            var state = (ConnectionState)ar.AsyncState!;
            int receivedLength = state.GuacdSocket.EndReceive(ar);

            if (DateTime.Now > state.LastActivity.AddMinutes(_gssettings.WebSocket.MaxInactivityAllowedInMin))
            {
                _logger.Warning("[Connection {Id}] Timeout", state.LastActivity);
                Close(state);
                return;
            }

            if (receivedLength <= 0)
                return;

            state.GuacdResponseOverflowBuffer.Append(Encoding.UTF8.GetString(state.GuacdBuffer[0..receivedLength]));
            string reponse = state.GuacdResponseOverflowBuffer.ToString();

            if (!reponse.Contains(';'))
            {
                state.GuacdSocket.BeginReceive(state.GuacdBuffer, 0, state.GuacdBuffer.Length, SocketFlags.None, new AsyncCallback(ReceiveCallback), state);
                return;
            }

            (string message, int delimiterIndex) = GuacamoleProtocolUtils.ReadResponseUntilDelimiter(reponse);
            state.GuacdResponseOverflowBuffer.Remove(0, delimiterIndex);

            GSListener.Send(state, message);

            _receiveDone.Set();
        }

        private static void SendCallback(IAsyncResult ar)
        {
            var state = (ConnectionState)ar.AsyncState!;
            state.LastActivity = DateTime.Now;
            state.GuacdSocket.EndSend(ar);

            _sendDone.Set();
        }

        #endregion Private Methods
    }
}
