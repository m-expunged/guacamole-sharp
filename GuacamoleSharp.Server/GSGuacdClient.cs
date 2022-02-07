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

        private static GSSettings _gssettings = null!;

        #endregion Private Fields

        #region Internal Methods

        internal static void Close(ConnectionState state)
        {
            try
            {
                lock (state.DisposeLock)
                {
                    if (state.Guacd.Socket != null && !state.Guacd.Closed)
                    {
                        _logger.Information("[Connection {Id}] Closing guacd connection", state.ConnectionId);

                        state.Guacd.Socket.Shutdown(SocketShutdown.Both);
                        state.Guacd.Socket.Close();
                    }

                    state.Guacd.Closed = true;
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

            _logger.Information("[Connection {Id}] Attempting connection to guacd proxy at: {Hostname}:{Port}", state.ConnectionId, gssettings.Guacd.Hostname, gssettings.Guacd.Port);
            _logger.Debug("[Connection {Id}] Connection settings: {@connection}", state.ConnectionId, connection);

            if (IPAddress.TryParse(gssettings.Guacd.Hostname, out IPAddress? address))
            {
                IPEndPoint endpoint = new(address, gssettings.Guacd.Port);
                state.Guacd.Socket = new Socket(endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                state.Guacd.Socket.BeginConnect(endpoint, new AsyncCallback(ConnectCallback), state);
            }
            else
            {
                try
                {
                    address = Dns.GetHostAddresses(gssettings.Guacd.Hostname).FirstOrDefault(x => x.AddressFamily == AddressFamily.InterNetwork)
                        ?? throw new ArgumentException(nameof(address));
                }
                catch (Exception)
                {
                    _logger.Error("[Connection {Id}] Could not find valid IPv4 address fitting hostname {Hostname}", state.ConnectionId, gssettings.Guacd.Hostname);
                    GSListener.Close(state);
                    return;
                }

                state.Guacd.Socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                state.Guacd.Socket.BeginConnect(gssettings.Guacd.Hostname, gssettings.Guacd.Port, new AsyncCallback(ConnectCallback), state);
            }
        }

        internal static void Send(ConnectionState state, string message)
        {
            state.Guacd.SendDone.Reset();

            _logger.Debug("[Connection {Id}] <<<W2G< {Message}", state.ConnectionId, message);

            byte[] data = Encoding.UTF8.GetBytes(message);
            state.Guacd.Socket.BeginSend(data, 0, data.Length, 0, new AsyncCallback(SendCallback), state);

            state.Guacd.SendDone.WaitOne();
        }

        #endregion Internal Methods

        #region Private Methods

        private static void ConnectCallback(IAsyncResult ar)
        {
            var state = (ConnectionState)ar.AsyncState!;
            state.Guacd.Socket.EndConnect(ar);

            _logger.Information("[Connection {Id}] Socket connected to {Endpoint}", state.ConnectionId, state.Guacd.Socket.RemoteEndPoint?.ToString());
            _logger.Information("[Connection {Id}] Selecting connection type: {type}", state.ConnectionId, state.Connection.Type);

            Send(state, GuacamoleProtocolHelpers.BuildProtocol("select", state.Connection.Type.ToLowerInvariant()));

            state.Guacd.Socket.BeginReceive(state.Guacd.Buffer, 0, state.Guacd.Buffer.Length, SocketFlags.None, new AsyncCallback(HandshakeCallback), state);
        }

        private static void HandshakeCallback(IAsyncResult ar)
        {
            var state = (ConnectionState)ar.AsyncState!;
            var receivedLength = state.Guacd.Socket.EndReceive(ar);

            if (DateTime.Now > state.LastActivity.AddMinutes(_gssettings.WebSocket.MaxInactivityAllowedInMin))
            {
                _logger.Warning("[Connection {Id}] Timeout", state.LastActivity);
                Close(state);
                return;
            }

            if (receivedLength <= 0)
            {
                state.Guacd.Socket.BeginReceive(state.Guacd.Buffer, 0, state.Guacd.Buffer.Length, SocketFlags.None, new AsyncCallback(HandshakeCallback), state);
                return;
            }

            state.Guacd.OverflowBuffer.Append(Encoding.UTF8.GetString(state.Guacd.Buffer[0..receivedLength]));
            string response = state.Guacd.OverflowBuffer.ToString();
            (string? handshake, int delimiterIndex) = GuacamoleProtocolHelpers.ReadProtocolUntilLastDelimiter(response);

            if (handshake == null)
            {
                state.Guacd.Socket.BeginReceive(state.Guacd.Buffer, 0, state.Guacd.Buffer.Length, SocketFlags.None, new AsyncCallback(HandshakeCallback), state);
                return;
            }

            state.Guacd.OverflowBuffer.Remove(0, delimiterIndex);
            var handshakeReply = GuacamoleProtocolHelpers.BuildHandshakeReply(state.Connection.Settings, handshake);

            Send(state, GuacamoleProtocolHelpers.BuildProtocol("size", state.Connection.Settings["width"], state.Connection.Settings["height"], state.Connection.Settings["dpi"]));
            Send(state, GuacamoleProtocolHelpers.BuildProtocol("audio", "audio/L16", state.Connection.Settings["audio"]));
            Send(state, GuacamoleProtocolHelpers.BuildProtocol("video", state.Connection.Settings["video"]));
            Send(state, GuacamoleProtocolHelpers.BuildProtocol("image", "image/png", "image/jpeg", "image/webp", state.Connection.Settings["image"]));

            _logger.Debug("[Connection {Id}] Server sent handshake: {handshake}", state.ConnectionId, handshake);

            Send(state, GuacamoleProtocolHelpers.BuildProtocol(handshakeReply));

            state.Guacd.HandshakeDone.Set();

            BackgroundWorker guacdThread = new();
            guacdThread.DoWork += new DoWorkEventHandler(Receive_DoWork);
            guacdThread.RunWorkerAsync(state);
        }

        private static void Receive_DoWork(object? sender, DoWorkEventArgs e)
        {
            ConnectionState state = (ConnectionState)e.Argument!;

            state.Client.HandshakeDone.WaitOne();

            try
            {
                while (!state.Guacd.Closed)
                {
                    state.Guacd.ReceiveDone.Reset();

                    state.Guacd.Socket.BeginReceive(state.Guacd.Buffer, 0, state.Guacd.Buffer.Length, SocketFlags.None, new AsyncCallback(ReceiveCallback), state);

                    state.Guacd.ReceiveDone.WaitOne();
                }
            }
            catch (Exception ex)
            {
                _logger.Error("[Connection {Id}] Error while running guacd socket client thread: {ex}", state.ConnectionId, ex);
                Close(state);
            }
        }

        private static void ReceiveCallback(IAsyncResult ar)
        {
            var state = (ConnectionState)ar.AsyncState!;

            if (state.Guacd.Closed)
            {
                state.Guacd.ReceiveDone.Set();
                return;
            }

            int receivedLength;

            try
            {
                receivedLength = state.Guacd.Socket.EndReceive(ar);
            }
            catch (ObjectDisposedException)
            {
                _logger.Warning("[Connection {Id}] Guacd socket tried to receive data from disposed connection", state.ConnectionId);

                Close(state);
                state.Guacd.ReceiveDone.Set();
                return;
            }

            if (DateTime.Now > state.LastActivity.AddMinutes(_gssettings.WebSocket.MaxInactivityAllowedInMin))
            {
                _logger.Warning("[Connection {Id}] Timeout", state.LastActivity);

                Close(state);
                state.Guacd.ReceiveDone.Set();
                return;
            }

            if (receivedLength <= 0)
            {
                state.Guacd.ReceiveDone.Set();
                return;
            }

            state.Guacd.OverflowBuffer.Append(Encoding.UTF8.GetString(state.Guacd.Buffer[0..receivedLength]));
            string reponse = state.Guacd.OverflowBuffer.ToString();
            (string? message, int delimiterIndex) = GuacamoleProtocolHelpers.ReadProtocolUntilLastDelimiter(reponse);

            if (message == null)
            {
                state.Guacd.Socket.BeginReceive(state.Guacd.Buffer, 0, state.Guacd.Buffer.Length, SocketFlags.None, new AsyncCallback(ReceiveCallback), state);
                return;
            }

            state.Guacd.OverflowBuffer.Remove(0, delimiterIndex);

            if (message.Contains("10.disconnect;"))
            {
                Close(state);
                state.Guacd.ReceiveDone.Set();
                return;
            }

            GSListener.Send(state, message);

            state.Guacd.ReceiveDone.Set();
        }

        private static void SendCallback(IAsyncResult ar)
        {
            var state = (ConnectionState)ar.AsyncState!;

            if (state.Guacd.Closed)
            {
                state.Guacd.SendDone.Set();
                return;
            }

            try
            {
                state.Guacd.Socket.EndSend(ar);
            }
            catch (ObjectDisposedException)
            {
                _logger.Warning("[Connection {Id}] Guacd socket tried to send data to disposed connection", state.ConnectionId);

                Close(state);
                state.Guacd.SendDone.Set();
                return;
            }

            state.LastActivity = DateTime.Now;

            state.Guacd.SendDone.Set();
        }

        #endregion Private Methods
    }
}
