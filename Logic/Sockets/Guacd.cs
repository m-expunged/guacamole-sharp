using GuacamoleSharp.Helpers;
using GuacamoleSharp.Logic.States;
using Serilog;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace GuacamoleSharp.Logic.Sockets
{
    public static class Guacd
    {
        public static void Close(ConnectionState state)
        {
            try
            {
                lock (state.DisposeLock)
                {
                    if (state.Guacd.Socket != null && !state.Guacd.Closed)
                    {
                        Log.Information("[Connection {Id}] Closing guacd connection.", state.ConnectionId);

                        state.Guacd.Socket.Shutdown(SocketShutdown.Both);
                        state.Guacd.Socket.Close();
                    }

                    state.Guacd.Closed = true;
                }
            }
            catch (Exception ex)
            {
                Log.Error("[Connection {id}] Error while closing guacd connection: {ex}", state.ConnectionId, ex);
            }
            finally
            {
                Client.Close(state);
            }
        }

        public static void Send(ConnectionState state, string message)
        {
            state.Guacd.SendDone.Reset();

            Log.Debug("[Connection {Id}] <C2G<<< {Message}", state.ConnectionId, message);

            byte[] data = Encoding.UTF8.GetBytes(message);
            state.Guacd.Socket.BeginSend(data, 0, data.Length, 0, new AsyncCallback(SendCallback), state);

            state.Guacd.SendDone.WaitOne();
        }

        public static void Start(ConnectionState state)
        {
            Log.Information("[Connection {Id}] Attempting connection to guacd proxy at: {Hostname}:{Port}", state.ConnectionId, OptionsHelper.Guacd.Hostname, OptionsHelper.Guacd.Port);
            Log.Debug("[Connection {Id}] Connection settings: {@State}", state.ConnectionId, state);

            if (IPAddress.TryParse(OptionsHelper.Guacd.Hostname, out IPAddress? address))
            {
                IPEndPoint endpoint = new(address, OptionsHelper.Guacd.Port);
                state.Guacd.Socket = new Socket(endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                state.Guacd.Socket.BeginConnect(endpoint, new AsyncCallback(ConnectCallback), state);
            }
            else
            {
                try
                {
                    address = Dns.GetHostAddresses(OptionsHelper.Guacd.Hostname).FirstOrDefault(x => x.AddressFamily == AddressFamily.InterNetwork)
                        ?? throw new ArgumentException(nameof(address));
                }
                catch (Exception)
                {
                    Log.Error("[Connection {Id}] Could not find valid IPv4 address fitting hostname: {Hostname}", state.ConnectionId, OptionsHelper.Guacd.Hostname);
                    Close(state);
                    return;
                }

                state.Guacd.Socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                state.Guacd.Socket.BeginConnect(OptionsHelper.Guacd.Hostname, OptionsHelper.Guacd.Port, new AsyncCallback(ConnectCallback), state);
            }
        }

        private static void ConnectCallback(IAsyncResult ar)
        {
            var state = (ConnectionState)ar.AsyncState!;

            try
            {
                state.Guacd.Socket.EndConnect(ar);

                Log.Information("[Connection {Id}] Socket connected to {Endpoint}", state.ConnectionId, state.Guacd.Socket.RemoteEndPoint?.ToString());
                Log.Information("[Connection {Id}] Selecting connection type: {Type}", state.ConnectionId, state.Connection.Type);

                Send(state, ProtocolHelper.BuildProtocol("select", state.Connection.Type));

                state.Guacd.Socket.BeginReceive(state.Guacd.Buffer, 0, state.Guacd.Buffer.Length, 0, new AsyncCallback(HandshakeCallback), state);
            }
            catch (Exception ex)
            {
                Log.Error("[Connection {Id}] {Message}", state.ConnectionId, ex.Message);
                Client.Close(state);
            }
        }

        private static void HandshakeCallback(IAsyncResult ar)
        {
            var state = (ConnectionState)ar.AsyncState!;

            try
            {
                var receivedLength = state.Guacd.Socket.EndReceive(ar);

                if (state.Timeout) throw new Exception($"Timeout.");

                if (receivedLength <= 0)
                {
                    state.Guacd.Socket.BeginReceive(state.Guacd.Buffer, 0, state.Guacd.Buffer.Length, SocketFlags.None, new AsyncCallback(HandshakeCallback), state);
                    return;
                }

                state.Guacd.OverflowBuffer.Append(Encoding.UTF8.GetString(state.Guacd.Buffer[0..receivedLength]));
                string response = state.Guacd.OverflowBuffer.ToString();
                (string? handshake, int delimiterIndex) = ProtocolHelper.ReadProtocolUntilLastDelimiter(response);

                if (handshake == null)
                {
                    state.Guacd.Socket.BeginReceive(state.Guacd.Buffer, 0, state.Guacd.Buffer.Length, SocketFlags.None, new AsyncCallback(HandshakeCallback), state);
                    return;
                }

                Log.Debug("[Connection {Id}] Attempting to resolve handshake: {Handshake}", state.ConnectionId, handshake);

                state.Guacd.OverflowBuffer.Remove(0, delimiterIndex);
                var handshakeReply = ProtocolHelper.BuildHandshakeReply(state.Connection, handshake);

                Send(state, ProtocolHelper.BuildProtocol("size", state.Connection.Arguments["width"], state.Connection.Arguments["height"], state.Connection.Arguments["dpi"]));
                Send(state, ProtocolHelper.BuildProtocol("audio", "audio/L16", state.Connection.Arguments["audio"]));
                Send(state, ProtocolHelper.BuildProtocol("video", state.Connection.Arguments["video"]));
                Send(state, ProtocolHelper.BuildProtocol("image", "image/png", "image/jpeg", "image/webp", state.Connection.Arguments["image"]));
                Send(state, ProtocolHelper.BuildProtocol(handshakeReply));

                state.Guacd.HandshakeDone.Set();

                Receive(state);
            }
            catch (Exception ex)
            {
                Log.Error("[Connection {Id}] {Message}", state.ConnectionId, ex.Message);
                Close(state);
            }
        }

        private static void Receive(ConnectionState state)
        {
            Task.Run(() =>
            {
                state.Client.HandshakeDone.WaitOne();

                try
                {
                    while (!state.Guacd.Closed)
                    {
                        state.Guacd.ReceiveDone.Reset();

                        state.Guacd.Socket.BeginReceive(state.Guacd.Buffer, 0, state.Guacd.Buffer.Length, 0, new AsyncCallback(ReceiveCallback), state);

                        state.Guacd.ReceiveDone.WaitOne();
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("[Connection {Id}] Error while running guacd socket client thread: {Message}", state.ConnectionId, ex.Message);
                    Close(state);
                }
            });
        }

        private static void ReceiveCallback(IAsyncResult ar)
        {
            var state = (ConnectionState)ar.AsyncState!;

            if (state.Guacd.Closed)
            {
                state.Guacd.ReceiveDone.Set();
                return;
            }

            try
            {
                int receivedLength = state.Guacd.Socket.EndReceive(ar);

                if (state.Timeout) throw new Exception($"Timeout.");

                if (receivedLength <= 0)
                {
                    state.Guacd.ReceiveDone.Set();
                    return;
                }

                state.Guacd.OverflowBuffer.Append(Encoding.UTF8.GetString(state.Guacd.Buffer[0..receivedLength]));
                string reponse = state.Guacd.OverflowBuffer.ToString();
                (string? message, int delimiterIndex) = ProtocolHelper.ReadProtocolUntilLastDelimiter(reponse);

                if (message == null)
                {
                    state.Guacd.Socket.BeginReceive(state.Guacd.Buffer, 0, state.Guacd.Buffer.Length, 0, new AsyncCallback(ReceiveCallback), state);
                    return;
                }

                state.Guacd.OverflowBuffer.Remove(0, delimiterIndex);

                if (message.Contains("10.disconnect;"))
                {
                    Close(state);
                    state.Guacd.ReceiveDone.Set();
                    return;
                }

                Client.Send(state, message);

                state.Guacd.ReceiveDone.Set();
            }
            catch (ObjectDisposedException)
            {
                Log.Warning("[Connection {Id}] Guacd socket tried to receive data from disposed connection.", state.ConnectionId);

                Close(state);
                state.Guacd.ReceiveDone.Set();
            }
            catch (Exception ex)
            {
                Log.Error("[Connection {Id}] {Message}", state.ConnectionId, ex.Message);

                Close(state);
                state.Guacd.ReceiveDone.Set();
            }
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
                Log.Warning("[Connection {Id}] Guacd socket tried to send data to disposed connection.", state.ConnectionId);

                Close(state);
                state.Guacd.SendDone.Set();
                return;
            }

            state.LastActivity = DateTimeOffset.Now;
            state.Guacd.SendDone.Set();
        }
    }
}